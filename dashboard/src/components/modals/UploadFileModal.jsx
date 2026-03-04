import { useState, useRef, useCallback, useEffect } from 'react';
import Modal from './Modal.jsx';
import './UploadFileModal.css';

const formatSize = (bytes) => {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
  if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  return (bytes / (1024 * 1024 * 1024)).toFixed(1) + ' GB';
};

async function readEntriesPromise(reader) {
  return new Promise((resolve, reject) => {
    reader.readEntries(resolve, reject);
  });
}

async function fileFromEntry(fileEntry) {
  return new Promise((resolve, reject) => {
    fileEntry.file(resolve, reject);
  });
}

export async function traverseEntry(entry, basePath = '') {
  if (entry.isFile) {
    const file = await fileFromEntry(entry);
    return [{ file, relativePath: basePath + entry.name, status: 'pending', error: null }];
  }
  if (entry.isDirectory) {
    const dirReader = entry.createReader();
    const results = [];
    // readEntries may not return all entries at once — loop until empty
    let batch;
    do {
      batch = await readEntriesPromise(dirReader);
      for (const child of batch) {
        const childResults = await traverseEntry(child, basePath + entry.name + '/');
        results.push(...childResults);
      }
    } while (batch.length > 0);
    return results;
  }
  return [];
}

const StatusIcon = ({ status }) => {
  if (status === 'pending') {
    return <span className="upload-status-icon status-pending" title="Pending" />;
  }
  if (status === 'uploading') {
    return <span className="upload-status-icon status-uploading" title="Uploading" />;
  }
  if (status === 'done') {
    return (
      <svg className="upload-status-icon status-done" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3">
        <polyline points="20 6 9 17 4 12" />
      </svg>
    );
  }
  if (status === 'failed') {
    return (
      <svg className="upload-status-icon status-failed" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3">
        <line x1="18" y1="6" x2="6" y2="18" />
        <line x1="6" y1="6" x2="18" y2="18" />
      </svg>
    );
  }
  return null;
};

const UploadFileModal = ({ isOpen, onClose, onSubmit, initialFiles }) => {
  const [files, setFiles] = useState([]);
  const [uploading, setUploading] = useState(false);
  const [done, setDone] = useState(false);
  const [dragOver, setDragOver] = useState(false);
  const fileInputRef = useRef(null);
  const folderInputRef = useRef(null);
  const cancelledRef = useRef(false);

  // Seed with files passed in from an external drop (e.g. on the table)
  useEffect(() => {
    if (initialFiles && initialFiles.length > 0) {
      setFiles(initialFiles);
    }
  }, [initialFiles]);

  // Prevent browser from opening files when they miss the dropzone or
  // when the drag event propagates past the modal overlay.
  useEffect(() => {
    if (!isOpen) return;
    const prevent = (e) => {
      e.preventDefault();
    };
    document.addEventListener('dragover', prevent);
    document.addEventListener('drop', prevent);
    return () => {
      document.removeEventListener('dragover', prevent);
      document.removeEventListener('drop', prevent);
    };
  }, [isOpen]);

  const addFiles = useCallback((newEntries) => {
    setFiles((prev) => {
      const existingPaths = new Set(prev.map((f) => f.relativePath));
      const unique = newEntries.filter((e) => !existingPaths.has(e.relativePath));
      return [...prev, ...unique];
    });
  }, []);

  const handleDrop = async (e) => {
    e.preventDefault();
    setDragOver(false);

    const items = e.dataTransfer.items;
    const dtFiles = e.dataTransfer.files;
    const directoryEntries = [];
    const fileReadPromises = [];

    if (items) {
      for (let i = 0; i < items.length; i++) {
        const entry = items[i].webkitGetAsEntry?.();
        if (entry && entry.isDirectory) {
          directoryEntries.push(entry);
        } else if (dtFiles[i]) {
          const f = dtFiles[i];
          // Start reading file content NOW, before the DataTransfer is cleaned up
          fileReadPromises.push(
            f.arrayBuffer().then((buf) => ({
              file: new File([buf], f.name, { type: f.type, lastModified: f.lastModified }),
              relativePath: f.name,
              status: 'pending',
              error: null,
            }))
          );
        }
      }
    } else {
      for (let i = 0; i < dtFiles.length; i++) {
        const f = dtFiles[i];
        fileReadPromises.push(
          f.arrayBuffer().then((buf) => ({
            file: new File([buf], f.name, { type: f.type, lastModified: f.lastModified }),
            relativePath: f.name,
            status: 'pending',
            error: null,
          }))
        );
      }
    }

    // All reads were started synchronously above — now await them
    const collected = await Promise.all(fileReadPromises);

    // Traverse directories (FileSystem API entries persist beyond the event)
    for (const dirEntry of directoryEntries) {
      const result = await traverseEntry(dirEntry);
      collected.push(...result);
    }

    if (collected.length > 0) {
      addFiles(collected);
    }
  };

  const handleFileInput = (e) => {
    const selected = Array.from(e.target.files);
    if (selected.length === 0) return;
    const newFiles = selected.map((file) => ({
      file,
      relativePath: file.webkitRelativePath || file.name,
      status: 'pending',
      error: null,
    }));
    addFiles(newFiles);
    e.target.value = '';
  };

  const removeFile = (index) => {
    setFiles((prev) => prev.filter((_, i) => i !== index));
  };

  const clearAll = () => {
    setFiles([]);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (files.length === 0 || uploading) return;

    setUploading(true);
    setDone(false);
    cancelledRef.current = false;

    const maxConcurrent = 4;
    let nextIndex = 0;

    const uploadNext = async () => {
      while (nextIndex < files.length) {
        if (cancelledRef.current) break;
        const i = nextIndex++;
        setFiles((prev) =>
          prev.map((f, idx) => (idx === i ? { ...f, status: 'uploading' } : f))
        );
        try {
          await onSubmit(files[i].file, files[i].relativePath);
          setFiles((prev) =>
            prev.map((f, idx) => (idx === i ? { ...f, status: cancelledRef.current ? 'pending' : 'done' } : f))
          );
        } catch (err) {
          if (cancelledRef.current) break;
          setFiles((prev) =>
            prev.map((f, idx) =>
              idx === i ? { ...f, status: 'failed', error: err.message || 'Upload failed' } : f
            )
          );
        }
      }
    };

    const workers = Array.from({ length: Math.min(maxConcurrent, files.length) }, () => uploadNext());
    await Promise.all(workers);

    setUploading(false);
    if (!cancelledRef.current) {
      setDone(true);
    }
  };

  const handleClose = () => {
    if (uploading) {
      cancelledRef.current = true;
    }
    setFiles([]);
    setUploading(false);
    setDone(false);
    setDragOver(false);
    onClose();
  };

  const pendingCount = files.filter((f) => f.status === 'pending').length;
  const doneCount = files.filter((f) => f.status === 'done').length;
  const failedCount = files.filter((f) => f.status === 'failed').length;

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="Upload Files" size="large">
      <form onSubmit={handleSubmit}>
        {/* Drop zone */}
        <div
          className={`upload-dropzone ${dragOver ? 'dragover' : ''} ${files.length > 0 ? 'has-file' : ''}`}
          onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
          onDragLeave={() => setDragOver(false)}
          onDrop={handleDrop}
          onClick={() => !uploading && fileInputRef.current?.click()}
        >
          <input
            ref={fileInputRef}
            type="file"
            multiple
            style={{ display: 'none' }}
            onChange={handleFileInput}
          />
          <input
            ref={folderInputRef}
            type="file"
            webkitdirectory=""
            style={{ display: 'none' }}
            onChange={handleFileInput}
          />
          <div className="upload-placeholder">
            <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <polyline points="16 16 12 12 8 16" />
              <line x1="12" y1="12" x2="12" y2="21" />
              <path d="M20.39 18.39A5 5 0 0 0 18 9h-1.26A8 8 0 1 0 3 16.3" />
              <polyline points="16 16 12 12 8 16" />
            </svg>
            <p>Drop files or folders here, or click to browse</p>
          </div>
        </div>

        {/* Folder picker button */}
        <div className="upload-picker-row">
          <button
            type="button"
            className="btn btn-secondary btn-sm"
            onClick={() => folderInputRef.current?.click()}
            disabled={uploading}
          >
            Select Folder
          </button>
          {files.length > 0 && !uploading && !done && (
            <button type="button" className="btn btn-secondary btn-sm" onClick={clearAll}>
              Clear All
            </button>
          )}
        </div>

        {/* File list */}
        {files.length > 0 && (
          <div className="upload-file-list">
            {files.map((entry, index) => (
              <div key={entry.relativePath + index} className={`upload-file-row status-${entry.status}`}>
                <StatusIcon status={entry.status} />
                <span className="upload-file-path" title={entry.relativePath}>{entry.relativePath}</span>
                <span className="upload-file-size">{formatSize(entry.file.size)}</span>
                {entry.status === 'pending' && !uploading && (
                  <button
                    type="button"
                    className="upload-file-remove"
                    onClick={() => removeFile(index)}
                    title="Remove"
                  >
                    &times;
                  </button>
                )}
                {entry.status === 'failed' && entry.error && (
                  <span className="upload-file-error" title={entry.error}>!</span>
                )}
              </div>
            ))}
          </div>
        )}

        {/* Summary */}
        {done && (
          <div className="upload-summary">
            {doneCount} of {files.length} uploaded successfully
            {failedCount > 0 && <span className="upload-summary-failed"> ({failedCount} failed)</span>}
          </div>
        )}

        {/* Actions */}
        <div className="form-actions">
          <button type="button" className="btn btn-secondary" onClick={handleClose}>
            {done ? 'Done' : 'Cancel'}
          </button>
          {!done && (
            <button type="submit" className="btn btn-primary" disabled={uploading || files.length === 0}>
              {uploading
                ? `Uploading (${doneCount + failedCount + 1}/${files.length})...`
                : `Upload ${files.length} file${files.length !== 1 ? 's' : ''}`}
            </button>
          )}
        </div>
      </form>
    </Modal>
  );
};

export default UploadFileModal;
