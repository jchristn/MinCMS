import { useEffect, useState } from 'react';
import Modal from './Modal.jsx';
import DeleteConfirmModal from './DeleteConfirmModal.jsx';
import { copyToClipboard } from '../CopyableUrl.jsx';
import './EditTextContentModal.css';

export const MAX_EDITABLE_TEXT_BYTES = 1024 * 1024;

const TEXT_FILE_EXTENSIONS = new Set([
  '.txt',
  '.md',
  '.markdown',
  '.js',
  '.mjs',
  '.cjs',
  '.json',
  '.xml',
  '.html',
  '.htm',
  '.css',
  '.svg',
  '.yml',
  '.yaml',
  '.csv',
  '.tsv',
  '.ts',
  '.tsx',
  '.jsx',
  '.sql',
  '.log',
  '.ini',
  '.cfg',
  '.conf',
  '.toml',
  '.properties',
  '.sh',
  '.bash',
  '.zsh',
  '.ps1',
  '.cmd',
  '.bat',
  '.py',
  '.rb',
  '.php',
  '.java',
  '.cs',
  '.xsd',
  '.xsl',
  '.xslt',
  '.rss',
  '.atom',
]);

const SPECIAL_TEXT_FILENAMES = new Set([
  '.env',
  '.gitignore',
  '.gitattributes',
  '.editorconfig',
  'cname',
  'license',
  'readme',
]);

const CONTENT_TYPE_BY_EXTENSION = {
  '.txt': 'text/plain',
  '.md': 'text/markdown',
  '.markdown': 'text/markdown',
  '.js': 'text/javascript',
  '.mjs': 'text/javascript',
  '.cjs': 'text/javascript',
  '.json': 'application/json',
  '.xml': 'application/xml',
  '.html': 'text/html',
  '.htm': 'text/html',
  '.css': 'text/css',
  '.svg': 'image/svg+xml',
  '.yml': 'text/yaml',
  '.yaml': 'text/yaml',
  '.csv': 'text/csv',
  '.tsv': 'text/tab-separated-values',
  '.ts': 'text/plain',
  '.tsx': 'text/plain',
  '.jsx': 'text/plain',
  '.sql': 'text/plain',
  '.log': 'text/plain',
  '.ini': 'text/plain',
  '.cfg': 'text/plain',
  '.conf': 'text/plain',
  '.toml': 'text/plain',
  '.properties': 'text/plain',
  '.sh': 'text/plain',
  '.bash': 'text/plain',
  '.zsh': 'text/plain',
  '.ps1': 'text/plain',
  '.cmd': 'text/plain',
  '.bat': 'text/plain',
  '.py': 'text/plain',
  '.rb': 'text/plain',
  '.php': 'text/plain',
  '.java': 'text/plain',
  '.cs': 'text/plain',
  '.xsd': 'application/xml',
  '.xsl': 'application/xml',
  '.xslt': 'application/xml',
  '.rss': 'application/xml',
  '.atom': 'application/xml',
};

const TEXTUAL_CONTENT_TYPES = [
  'text/',
  'application/json',
  'application/xml',
  'image/svg+xml',
  'application/javascript',
  'text/javascript',
];

const getFilePath = (file) => file?._fullPath || file?.FileName || '';

const getLeafName = (filePath) => {
  const parts = filePath.split('/').filter(Boolean);
  return parts[parts.length - 1] || '';
};

const getExtension = (filePath) => {
  const leafName = getLeafName(filePath);
  const idx = leafName.lastIndexOf('.');
  if (idx <= 0) return '';
  return leafName.slice(idx).toLowerCase();
};

export const normalizeTextFilePath = (value) => {
  const normalized = (value || '').trim().replace(/\\/g, '/').replace(/^\/+/, '');
  return normalized.replace(/\/{2,}/g, '/');
};

const isTextualContentType = (contentType) => {
  if (!contentType) return false;
  const lower = contentType.toLowerCase();
  return TEXTUAL_CONTENT_TYPES.some((entry) => lower.startsWith(entry));
};

export const isEditableTextFile = (file) => {
  if (!file || file._isFolder) return false;

  const filePath = normalizeTextFilePath(getFilePath(file));
  if (!filePath) return false;
  if ((file.Size ?? 0) > MAX_EDITABLE_TEXT_BYTES) return false;

  const leafName = getLeafName(filePath).toLowerCase();
  const extension = getExtension(filePath);

  if (TEXT_FILE_EXTENSIONS.has(extension)) return true;
  if (SPECIAL_TEXT_FILENAMES.has(leafName)) return true;

  return !extension;
};

const inferTextContentType = (filePath) => {
  const extension = getExtension(filePath);
  return CONTENT_TYPE_BY_EXTENSION[extension] || 'text/plain';
};

const resolveTextContentType = (filePath, contentType) => {
  if (isTextualContentType(contentType)) return contentType;
  return inferTextContentType(filePath);
};

const formatSize = (bytes) => {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

const EditTextContentModal = ({
  isOpen,
  apiClient,
  slug,
  file,
  existingFiles = [],
  initialPath = '',
  onClose,
  onSaved,
}) => {
  const isExistingFile = Boolean(file);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [filePath, setFilePath] = useState('');
  const [originalPath, setOriginalPath] = useState('');
  const [content, setContent] = useState('');
  const [originalContent, setOriginalContent] = useState('');
  const [metadata, setMetadata] = useState(null);
  const [contentType, setContentType] = useState('text/plain');
  const [error, setError] = useState('');
  const [saveConfirmOpen, setSaveConfirmOpen] = useState(false);
  const [bodyCopied, setBodyCopied] = useState(false);

  useEffect(() => {
    if (!isOpen) return undefined;

    let cancelled = false;
    const seedPath = isExistingFile ? getFilePath(file) : normalizeTextFilePath(initialPath);

    setLoading(isExistingFile);
    setSaving(false);
    setError('');
    setSaveConfirmOpen(false);
    setBodyCopied(false);
    setMetadata(null);
    setFilePath(seedPath);
    setOriginalPath(seedPath);
    setContent('');
    setOriginalContent('');
    setContentType(inferTextContentType(seedPath));

    if (!isExistingFile) return undefined;

    const loadFile = async () => {
      try {
        const fullPath = getFilePath(file);
        const [metadataData, text] = await Promise.all([
          apiClient.getFileMetadata(slug, fullPath),
          apiClient.getFileText(slug, fullPath),
        ]);

        if (cancelled) return;

        setMetadata(metadataData);
        setContent(text);
        setOriginalContent(text);
        setContentType(resolveTextContentType(fullPath, metadataData?.ContentType));
      } catch (loadError) {
        if (cancelled) return;
        setError(`Failed to load file content: ${loadError.message}`);
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    loadFile();

    return () => {
      cancelled = true;
    };
  }, [apiClient, file, initialPath, isExistingFile, isOpen, slug]);

  useEffect(() => {
    if (!isOpen || isExistingFile) return;
    setContentType(inferTextContentType(filePath));
  }, [filePath, isExistingFile, isOpen]);

  const normalizedPath = normalizeTextFilePath(filePath);
  const normalizedOriginalPath = normalizeTextFilePath(originalPath);
  const currentContentType = resolveTextContentType(normalizedPath, contentType);
  const contentBytes = new Blob([content]).size;
  const isDirty = normalizedPath !== normalizedOriginalPath || content !== originalContent;

  const saveTargetExists = existingFiles.some((entry) => {
    const existingPath = normalizeTextFilePath(entry.FileName);
    if (!existingPath || existingPath !== normalizedPath) return false;
    if (!isExistingFile) return true;
    return existingPath !== normalizeTextFilePath(getFilePath(file));
  });

  const validate = () => {
    if (!normalizedPath) {
      setError('File path is required.');
      return false;
    }

    if (normalizedPath.endsWith('/')) {
      setError('File path must include a file name.');
      return false;
    }

    if (!isExistingFile && !isEditableTextFile({ FileName: normalizedPath, Size: contentBytes })) {
      setError('New files must use a text-compatible name or extension.');
      return false;
    }

    if (contentBytes > MAX_EDITABLE_TEXT_BYTES) {
      setError(`Text editor is limited to ${formatSize(MAX_EDITABLE_TEXT_BYTES)} per file.`);
      return false;
    }

    setError('');
    return true;
  };

  const closeEditor = () => {
    if (loading || saving) return;

    if (isDirty && !window.confirm('Discard unsaved changes?')) {
      return;
    }

    onClose();
  };

  const persistFile = async () => {
    if (!validate()) return;

    setSaving(true);

    try {
      const savedMetadata = await apiClient.saveTextFile(slug, normalizedPath, content, currentContentType);
      setMetadata(savedMetadata);
      setOriginalPath(normalizedPath);
      setOriginalContent(content);
      setContentType(resolveTextContentType(normalizedPath, savedMetadata?.ContentType || currentContentType));
      setSaveConfirmOpen(false);

      if (onSaved) {
        await onSaved(savedMetadata, {
          fileName: normalizedPath,
          isNew: !isExistingFile,
          replacedExisting: isExistingFile || saveTargetExists,
        });
      }
    } catch (saveError) {
      setError(`Failed to save file: ${saveError.message}`);
    } finally {
      setSaving(false);
    }
  };

  const requestSave = () => {
    if (!validate()) return;

    if (isExistingFile || saveTargetExists) {
      setSaveConfirmOpen(true);
      return;
    }

    persistFile();
  };

  const handleCopyBody = async () => {
    try {
      await copyToClipboard(content);
      setBodyCopied(true);
      window.setTimeout(() => setBodyCopied(false), 1500);
    } catch (copyError) {
      setError(`Failed to copy content: ${copyError.message}`);
    }
  };

  const modalTitle = isExistingFile ? 'Edit Text File' : 'New Text File';
  const saveActionLabel = isExistingFile || saveTargetExists ? 'Save Anyway' : (isExistingFile ? 'Save' : 'Create File');
  const helperText = isExistingFile
    ? 'Saving replaces the current object contents using the existing upload API.'
    : 'Create a new text file in the current collection. Use forward slashes for folders.';

  return (
    <>
      <Modal isOpen={isOpen} onClose={closeEditor} title={modalTitle} size="xlarge">
        <div className="edit-text-modal">
          <div className="edit-text-modal-header">
            <p>{helperText}</p>
            <div className="edit-text-modal-header-actions">
              <button
                type="button"
                className={`btn btn-secondary edit-text-copy-btn${bodyCopied ? ' copied' : ''}`}
                onClick={handleCopyBody}
                disabled={loading || saving}
                title="Copy body to clipboard"
              >
                {bodyCopied ? 'Copied' : 'Copy Body'}
              </button>
              <span className={`edit-text-dirty${isDirty ? ' is-dirty' : ''}`}>
                {isDirty ? 'Unsaved changes' : 'Saved'}
              </span>
            </div>
          </div>

          <div className="edit-text-modal-grid">
            <div className="form-group">
              <label htmlFor="edit-text-file-path">{isExistingFile ? 'Filename' : 'Filename'}</label>
              {isExistingFile ? (
                <div className="edit-text-meta-value edit-text-file-display" title={normalizedPath}>
                  {normalizedPath}
                </div>
              ) : (
                <input
                  id="edit-text-file-path"
                  type="text"
                  value={filePath}
                  onChange={(e) => setFilePath(e.target.value)}
                  disabled={loading || saving}
                  placeholder="docs/readme.md"
                  spellCheck={false}
                />
              )}
            </div>

            <div className="form-group">
              <label>Content Type</label>
              <div className="edit-text-meta-value">{currentContentType}</div>
            </div>
          </div>

          <div className="edit-text-modal-meta">
            <span>Size: {loading ? 'Loading...' : formatSize(contentBytes)}</span>
            <span>Limit: {formatSize(MAX_EDITABLE_TEXT_BYTES)}</span>
            {metadata?.LastModifiedUtc && (
              <span>Last Modified: {new Date(metadata.LastModifiedUtc).toLocaleString()}</span>
            )}
            {metadata?.ETag && <span>ETag: {metadata.ETag}</span>}
          </div>

          {error && (
            <div className="edit-text-error" role="alert">
              {error}
            </div>
          )}

          <div className="form-group edit-text-editor-group">
            <div className="edit-text-editor-label-row">
              <label htmlFor="edit-text-content">Content</label>
              <span className="edit-text-editor-bytes">{formatSize(contentBytes)}</span>
            </div>
            <textarea
              id="edit-text-content"
              className="edit-text-editor"
              value={content}
              onChange={(e) => setContent(e.target.value)}
              disabled={loading || saving}
              spellCheck={false}
              wrap="off"
              placeholder={loading ? 'Loading file contents...' : 'Enter text content...'}
            />
          </div>

          <div className="form-actions">
            <button type="button" className="btn btn-secondary" onClick={closeEditor} disabled={loading || saving}>
              Cancel
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={requestSave}
              disabled={loading || saving || !isDirty}
            >
              {saving ? 'Saving...' : saveActionLabel}
            </button>
          </div>
        </div>
      </Modal>

      <DeleteConfirmModal
        isOpen={saveConfirmOpen}
        onClose={() => setSaveConfirmOpen(false)}
        onConfirm={persistFile}
        title="Confirm Save"
        actionLabel={saveTargetExists && !isExistingFile ? 'Overwrite' : 'Save'}
        entityName={normalizedPath}
        entityType="file"
        message={
          saveTargetExists && !isExistingFile
            ? 'A file already exists at this path. Saving will overwrite the current object contents.'
            : 'Saving will replace the current object contents in S3. External changes made after you opened this file will be lost.'
        }
        warningMessage="Review the path and content before continuing."
      />
    </>
  );
};

export default EditTextContentModal;
