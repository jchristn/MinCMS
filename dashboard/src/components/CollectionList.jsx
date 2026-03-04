import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext.jsx';
import DataTable from './DataTable.jsx';
import CreateCollectionModal from './modals/CreateCollectionModal.jsx';
import DeleteConfirmModal from './modals/DeleteConfirmModal.jsx';
import AlertModal from './modals/AlertModal.jsx';
import { SitemapCopyIcon } from './CopyableUrl.jsx';
import './CollectionList.css';

const CollectionList = () => {
  const { apiClient } = useAuth();
  const navigate = useNavigate();

  const [collections, setCollections] = useState([]);
  const [loading, setLoading] = useState(false);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [selectedCollection, setSelectedCollection] = useState(null);
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });

  const fetchCollections = useCallback(async () => {
    setLoading(true);
    try {
      const data = await apiClient.listCollections();
      setCollections(data || []);
    } catch (error) {
      setAlertModal({ isOpen: true, message: 'Failed to load collections: ' + error.message, type: 'error' });
      setCollections([]);
    } finally {
      setLoading(false);
    }
  }, [apiClient]);

  useEffect(() => {
    fetchCollections();
  }, [fetchCollections]);

  const handleCreateCollection = async (name, slug) => {
    await apiClient.createCollection(name, slug);
    await fetchCollections();
  };

  const handleDeleteCollection = async () => {
    if (!selectedCollection) return;
    try {
      // Delete all files within the collection first
      const files = await apiClient.listFiles(selectedCollection.Slug);
      if (files && files.length > 0) {
        for (const file of files) {
          await apiClient.deleteFile(selectedCollection.Slug, file.FileName);
        }
      }
      await apiClient.deleteCollection(selectedCollection.Slug);
      setDeleteModalOpen(false);
      setSelectedCollection(null);
      await fetchCollections();
    } catch (error) {
      setDeleteModalOpen(false);
      setAlertModal({ isOpen: true, message: 'Failed to delete collection: ' + error.message, type: 'error' });
    }
  };

  const handleAction = (action, collection) => {
    setSelectedCollection(collection);
    if (action === 'enter') {
      navigate('/dashboard/collection/' + collection.Slug);
    } else if (action === 'delete') {
      setDeleteModalOpen(true);
    }
  };

  const columns = [
    { key: 'Name', label: 'Collection', sortable: true },
    { key: 'Slug', label: 'Slug', sortable: true },
    {
      key: 'CreatedUtc',
      label: 'Created',
      sortable: true,
      render: (val) => val ? new Date(val).toLocaleDateString() : '-'
    },
    {
      key: 'IsActive',
      label: 'Active',
      sortable: true,
      render: (val) => val ? 'Yes' : 'No'
    }
  ];

  const actions = [
    {
      name: 'enter',
      label: 'Enter',
      className: 'btn-primary',
      icon: <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="9 18 15 12 9 6"></polyline></svg>
    },
    {
      name: 'sitemap',
      label: 'Sitemap',
      className: 'btn-secondary',
      icon: (row) => <SitemapCopyIcon url={`${apiClient.baseUrl}/download/${row.Slug}/sitemap.xml`} />
    },
    {
      name: 'delete',
      label: 'Delete',
      className: 'btn-danger',
      icon: <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
    }
  ];

  return (
    <div className="collection-list">
      <div className="collection-list-header">
        <div>
          <h2>Collections</h2>
          <p className="collection-list-subtitle">Manage your collections and their associated files.</p>
        </div>
        <button className="btn btn-primary" onClick={() => setCreateModalOpen(true)}>
          + Create
        </button>
      </div>

      <DataTable
        columns={columns}
        data={collections}
        loading={loading}
        onAction={handleAction}
        onRefresh={fetchCollections}
        actions={actions}
        onRowClick={(collection) => navigate('/dashboard/collection/' + collection.Slug)}
      />

      <CreateCollectionModal
        isOpen={createModalOpen}
        onClose={() => setCreateModalOpen(false)}
        onSubmit={handleCreateCollection}
      />

      <DeleteConfirmModal
        isOpen={deleteModalOpen}
        onClose={() => { setDeleteModalOpen(false); setSelectedCollection(null); }}
        onConfirm={handleDeleteCollection}
        entityName={selectedCollection?.Name}
        entityType="collection"
        warningMessage="This will permanently delete the collection and ALL its files. This action cannot be undone."
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

export default CollectionList;
