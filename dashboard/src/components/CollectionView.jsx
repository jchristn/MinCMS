import { useState, useEffect, useCallback, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext.jsx';
import Topbar from './Topbar.jsx';
import DataTable from './DataTable.jsx';
import CopyableUrl, { SitemapCopyButton, copyToClipboard } from './CopyableUrl.jsx';
import UploadFileModal, { traverseEntry } from './modals/UploadFileModal.jsx';
import DeleteConfirmModal from './modals/DeleteConfirmModal.jsx';
import ViewMetadataModal from './modals/ViewMetadataModal.jsx';
import AlertModal from './modals/AlertModal.jsx';
import EditTextContentModal, { isEditableTextFile } from './modals/EditTextContentModal.jsx';
import './CollectionView.css';

const CollectionView = () => {
  const { slug } = useParams();
  const { apiClient } = useAuth();
  const navigate = useNavigate();

  const [collection, setCollection] = useState(null);
  const [files, setFiles] = useState([]);
  const [loading, setLoading] = useState(false);
  const [uploadModalOpen, setUploadModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [metadataModalOpen, setMetadataModalOpen] = useState(false);
  const [selectedFile, setSelectedFile] = useState(null);
  const [fileMetadata, setFileMetadata] = useState(null);
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });
  const [baseUrlCopied, setBaseUrlCopied] = useState(false);
  const [initialFiles, setInitialFiles] = useState(null);
  const [pageDragOver, setPageDragOver] = useState(false);
  const [selectedFiles, setSelectedFiles] = useState([]);
  const [batchDeleteModalOpen, setBatchDeleteModalOpen] = useState(false);
  const [currentPrefix, setCurrentPrefix] = useState('');
  const [textEditorOpen, setTextEditorOpen] = useState(false);
  const [editorFile, setEditorFile] = useState(null);
  const [editorInitialPath, setEditorInitialPath] = useState('');

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const [collectionData, filesData] = await Promise.all([
        apiClient.getCollection(slug),
        apiClient.listFiles(slug)
      ]);
      setCollection(collectionData);
      setFiles(filesData || []);
    } catch (error) {
      setAlertModal({ isOpen: true, message: 'Failed to load collection data: ' + error.message, type: 'error' });
    } finally {
      setLoading(false);
    }
  }, [apiClient, slug]);

  useEffect(() => {
    fetchData();
    setCurrentPrefix('');
  }, [fetchData]);

  const { folders, currentFiles } = useMemo(() => {
    const folderSet = new Set();
    const current = [];
    for (const file of files) {
      if (!file.FileName.startsWith(currentPrefix)) continue;
      const remainder = file.FileName.slice(currentPrefix.length);
      const slashIdx = remainder.indexOf('/');
      if (slashIdx >= 0) {
        folderSet.add(remainder.slice(0, slashIdx));
      } else {
        current.push(file);
      }
    }
    return { folders: [...folderSet].sort(), currentFiles: current };
  }, [files, currentPrefix]);

  const tableData = useMemo(() => {
    const rows = [];
    if (currentPrefix) {
      rows.push({
        _isFolder: true,
        _isParent: true,
        _folderName: '..',
        _rowKey: 'folder:..',
        FileName: '..',
        Size: null,
        LastModifiedUtc: null,
      });
    }
    const folderRows = folders.map(name => ({
      _isFolder: true,
      _folderName: name,
      _rowKey: 'folder:' + name,
      FileName: name,
      Size: null,
      LastModifiedUtc: null,
    }));
    const fileRows = currentFiles.map(f => ({
      ...f,
      FileName: f.FileName.slice(currentPrefix.length),
      _fullPath: f.FileName,
      _rowKey: f.FileName,
    }));
    return [...rows, ...folderRows, ...fileRows];
  }, [folders, currentFiles, currentPrefix]);

  const handleUpload = async (file, relativePath) => {
    // Only pass custom fileName when relativePath differs from the file's own name
    const fileName = relativePath && relativePath !== file.name ? relativePath : undefined;
    await apiClient.uploadFile(slug, file, fileName);
  };

  const handlePageDrop = async (e) => {
    e.preventDefault();
    setPageDragOver(false);

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

    const collected = await Promise.all(fileReadPromises);

    for (const dirEntry of directoryEntries) {
      const result = await traverseEntry(dirEntry);
      collected.push(...result);
    }

    if (collected.length > 0) {
      setInitialFiles(collected);
      setUploadModalOpen(true);
    }
  };

  const handlePageDragOver = (e) => {
    e.preventDefault();
    setPageDragOver(true);
  };

  const handlePageDragLeave = (e) => {
    // Only clear if leaving the content area itself, not a child
    if (e.currentTarget === e.target || !e.currentTarget.contains(e.relatedTarget)) {
      setPageDragOver(false);
    }
  };

  const handleDeleteFile = async () => {
    if (!selectedFile) return;
    try {
      const filesToDelete = [];
      if (selectedFile._isFolder) {
        const prefix = currentPrefix + selectedFile._folderName + '/';
        filesToDelete.push(...getFilesUnderPrefix(prefix).map(f => f.FileName));
      } else {
        filesToDelete.push(selectedFile._fullPath || selectedFile.FileName);
      }
      await apiClient.deleteFiles(slug, filesToDelete);
      setDeleteModalOpen(false);
      setSelectedFile(null);
      await fetchData();
    } catch (error) {
      setDeleteModalOpen(false);
      setAlertModal({ isOpen: true, message: 'Failed to delete: ' + error.message, type: 'error' });
    }
  };

  const handleBatchDelete = async () => {
    try {
      const filesToDelete = [];
      for (const item of selectedFiles) {
        if (item._isFolder) {
          const prefix = currentPrefix + item._folderName + '/';
          filesToDelete.push(...getFilesUnderPrefix(prefix).map(f => f.FileName));
        } else {
          filesToDelete.push(item._fullPath || item.FileName);
        }
      }
      // Deduplicate in case a folder and its contents are both selected
      const unique = [...new Set(filesToDelete)];
      await apiClient.deleteFiles(slug, unique);
      setBatchDeleteModalOpen(false);
      setSelectedFiles([]);
      await fetchData();
    } catch (error) {
      setBatchDeleteModalOpen(false);
      setAlertModal({ isOpen: true, message: 'Failed to delete files: ' + error.message, type: 'error' });
    }
  };

  const handleViewMetadata = async (file) => {
    try {
      const metadata = await apiClient.getFileMetadata(slug, file._fullPath || file.FileName);
      setFileMetadata(metadata);
      setMetadataModalOpen(true);
    } catch (error) {
      setAlertModal({ isOpen: true, message: 'Failed to load metadata: ' + error.message, type: 'error' });
    }
  };

  const handleOpenTextEditor = (file) => {
    setEditorFile(file);
    setEditorInitialPath(file?._fullPath || file?.FileName || currentPrefix);
    setTextEditorOpen(true);
  };

  const handleOpenNewTextFile = () => {
    setEditorFile(null);
    setEditorInitialPath(currentPrefix);
    setTextEditorOpen(true);
  };

  const handleCloseTextEditor = () => {
    setTextEditorOpen(false);
    setEditorFile(null);
    setEditorInitialPath('');
  };

  const handleTextEditorSaved = async () => {
    handleCloseTextEditor();
    await fetchData();
  };

  const handleAction = (action, file) => {
    setSelectedFile(file);
    if (action === 'edit') {
      handleOpenTextEditor(file);
    } else if (action === 'delete') {
      setDeleteModalOpen(true);
    } else if (action === 'metadata') {
      handleViewMetadata(file);
    } else if (action === 'download') {
      const url = apiClient.getDownloadUrl(slug, file._fullPath || file.FileName);
      window.open(url, '_blank');
    } else if (action === 'copy') {
      const url = apiClient.getDownloadUrl(slug, file._fullPath || file.FileName);
      navigator.clipboard.writeText(url).catch(() => {});
    }
  };

  const formatSize = (bytes) => {
    if (bytes == null) return '-';
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(1) + ' GB';
  };

  const baseUrl = `${apiClient.baseUrl}/download/${slug}`;

  const columns = [
    {
      key: 'FileName',
      label: 'File',
      sortable: true,
      render: (val, row) => row._isFolder ? (
        <span className="folder-link" onClick={(e) => {
          e.stopPropagation();
          if (row._isParent) {
            const parts = currentPrefix.replace(/\/$/, '').split('/');
            parts.pop();
            setCurrentPrefix(parts.length > 0 ? parts.join('/') + '/' : '');
          } else {
            setCurrentPrefix(currentPrefix + row._folderName + '/');
          }
        }}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" stroke="none"><path d="M10 4H4a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-8l-2-2z"/></svg>
          {val}
        </span>
      ) : val
    },
    {
      key: 'Size',
      label: 'Size',
      sortable: true,
      render: (val) => formatSize(val)
    },
    {
      key: 'LastModifiedUtc',
      label: 'Last Modified',
      sortable: true,
      render: (val) => val ? new Date(val).toLocaleString() : '-'
    },
    {
      key: '_publicUrl',
      label: 'Public URL',
      sortable: false,
      render: (_, row) => row._isFolder ? '-' : (
        <CopyableUrl value={apiClient.getDownloadUrl(slug, row._fullPath || row.FileName)} />
      )
    }
  ];

  const deleteAction = {
    name: 'delete',
    label: 'Delete',
    className: 'btn-danger',
    icon: <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
  };

  const editAction = {
    name: 'edit',
    label: 'Edit',
    className: 'btn-secondary',
    icon: <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 20h9"></path><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z"></path></svg>
  };

  const actions = [
    editAction,
    {
      name: 'download',
      label: 'Download',
      className: 'btn-primary',
      icon: <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
    },
    {
      name: 'metadata',
      label: 'Metadata',
      className: 'btn-secondary',
      icon: <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>
    },
    deleteAction
  ];

  const folderActions = [deleteAction];

  const getFilesUnderPrefix = (prefix) => files.filter(f => f.FileName.startsWith(prefix));

  return (
    <div className="collection-view">
      <Topbar />
      <div
        className={`collection-view-content${pageDragOver ? ' drag-over' : ''}`}
        onDragOver={handlePageDragOver}
        onDragLeave={handlePageDragLeave}
        onDrop={handlePageDrop}
      >
        <div className="collection-view-breadcrumb">
          <button className="btn-link" onClick={() => navigate('/dashboard')}>Collections</button>
          <span className="breadcrumb-sep">/</span>
          {currentPrefix ? (
            <>
              <button className="btn-link" onClick={() => setCurrentPrefix('')}>{collection?.Name || slug}</button>
              {currentPrefix.split('/').filter(Boolean).map((segment, i, arr) => {
                const path = arr.slice(0, i + 1).join('/') + '/';
                const isLast = i === arr.length - 1;
                return (
                  <span key={path} style={{ display: 'contents' }}>
                    <span className="breadcrumb-sep">/</span>
                    {isLast ? (
                      <span className="breadcrumb-current">{segment}</span>
                    ) : (
                      <button className="btn-link" onClick={() => setCurrentPrefix(path)}>{segment}</button>
                    )}
                  </span>
                );
              })}
            </>
          ) : (
            <span className="breadcrumb-current">{collection?.Name || slug}</span>
          )}
        </div>

        <div className="collection-view-header">
          <div>
            <h2>{collection?.Name || slug} <SitemapCopyButton url={`${apiClient.baseUrl}/download/${slug}/sitemap.xml`} /></h2>
            <p className="collection-view-subtitle">Upload, manage, and share files for this collection.</p>
            <span className="collection-base-url"><span className="collection-base-url-label">Base URL:</span> <code className="collection-base-url-value">{baseUrl}</code><button type="button" className="collection-base-url-copy" onClick={async () => { await copyToClipboard(baseUrl); setBaseUrlCopied(true); setTimeout(() => setBaseUrlCopied(false), 1500); }} title="Copy base URL">{baseUrlCopied ? (<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#22c55e" strokeWidth="2"><polyline points="20 6 9 17 4 12"></polyline></svg>) : (<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>)}</button></span>
          </div>
          <div className="collection-view-actions">
            {selectedFiles.length > 0 && (
              <button className="btn btn-danger" onClick={() => setBatchDeleteModalOpen(true)}>
                Delete Selected ({selectedFiles.length})
              </button>
            )}
            <button className="btn btn-secondary" onClick={handleOpenNewTextFile}>
              + New Text File
            </button>
            <button className="btn btn-primary" onClick={() => setUploadModalOpen(true)}>
              + Upload File(s)
            </button>
          </div>
        </div>

        <DataTable
          columns={columns}
          data={tableData}
          loading={loading}
          onAction={handleAction}
          onRefresh={fetchData}
          actions={actions}
          selectable
          rowKey="_rowKey"
          rowActions={(row) => {
            if (row._isParent) return [];
            if (row._isFolder) return folderActions;
            return isEditableTextFile(row) ? actions : actions.filter((action) => action.name !== 'edit');
          }}
          isRowSelectable={(row) => !row._isParent}
          onSelectionChange={setSelectedFiles}
        />
      </div>

      <UploadFileModal
        isOpen={uploadModalOpen}
        onClose={() => { setUploadModalOpen(false); setInitialFiles(null); fetchData(); }}
        onSubmit={handleUpload}
        initialFiles={initialFiles}
      />

      <DeleteConfirmModal
        isOpen={deleteModalOpen}
        onClose={() => { setDeleteModalOpen(false); setSelectedFile(null); }}
        onConfirm={handleDeleteFile}
        entityName={selectedFile?._isFolder ? `${selectedFile._folderName}/` : (selectedFile?._fullPath || selectedFile?.FileName)}
        entityType={selectedFile?._isFolder ? 'folder' : 'file'}
        message={selectedFile?._isFolder ? `Are you sure you want to delete the folder "${selectedFile._folderName}" and all ${getFilesUnderPrefix(currentPrefix + (selectedFile?._folderName || '') + '/').length} file(s) inside it?` : undefined}
      />

      <DeleteConfirmModal
        isOpen={batchDeleteModalOpen}
        onClose={() => setBatchDeleteModalOpen(false)}
        onConfirm={handleBatchDelete}
        entityName={`${selectedFiles.length} file(s)`}
        entityType="file"
        message={`Are you sure you want to delete these ${selectedFiles.length} item(s)? Folders will have all contents deleted.`}
      />

      <ViewMetadataModal
        isOpen={metadataModalOpen}
        onClose={() => { setMetadataModalOpen(false); setFileMetadata(null); }}
        data={fileMetadata}
      />

      <EditTextContentModal
        isOpen={textEditorOpen}
        apiClient={apiClient}
        slug={slug}
        file={editorFile}
        existingFiles={files}
        initialPath={editorInitialPath}
        onClose={handleCloseTextEditor}
        onSaved={handleTextEditorSaved}
      />

      <AlertModal
        isOpen={alertModal.isOpen}
        onClose={() => setAlertModal({ ...alertModal, isOpen: false })}
        message={alertModal.message}
        type={alertModal.type}
      />
    </div>
  );
};

export default CollectionView;
