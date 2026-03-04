import { useState } from 'react';
import Modal from './Modal.jsx';
import './CreateCollectionModal.css';

const CreateCollectionModal = ({ isOpen, onClose, onSubmit }) => {
  const [name, setName] = useState('');
  const [slug, setSlug] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const slugify = (text) => {
    return text
      .toLowerCase()
      .trim()
      .replace(/[^a-z0-9\s-]/g, '')
      .replace(/[\s_]+/g, '-')
      .replace(/-+/g, '-')
      .replace(/^-|-$/g, '');
  };

  const handleNameChange = (e) => {
    const val = e.target.value;
    setName(val);
    setSlug(slugify(val));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');

    if (!name.trim()) { setError('Name is required.'); return; }
    if (!slug.trim()) { setError('Slug is required.'); return; }
    if (!/^[a-z0-9-]+$/.test(slug)) { setError('Slug must be lowercase, alphanumeric, and hyphens only.'); return; }
    if (slug.trim().toLowerCase() === 'config') { setError("The slug 'config' is reserved and cannot be used."); return; }

    setLoading(true);
    try {
      await onSubmit(name.trim(), slug.trim());
      setName('');
      setSlug('');
      onClose();
    } catch (err) {
      setError(err.message || 'Failed to create collection.');
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    setName('');
    setSlug('');
    setError('');
    onClose();
  };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="Create Collection" size="medium">
      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label htmlFor="collectionName">Collection Name</label>
          <input
            id="collectionName"
            type="text"
            value={name}
            onChange={handleNameChange}
            placeholder="e.g. Nintendo"
            autoFocus
          />
        </div>
        <div className="form-group">
          <label htmlFor="collectionSlug">Slug</label>
          <input
            id="collectionSlug"
            type="text"
            value={slug}
            onChange={(e) => setSlug(e.target.value)}
            placeholder="e.g. nintendo"
          />
          <span className="slug-hint">Used in download URLs: /download/{slug || '...'}/</span>
        </div>
        {error && <p className="form-error">{error}</p>}
        <div className="form-actions">
          <button type="button" className="btn btn-secondary" onClick={handleClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={loading}>
            {loading ? 'Creating...' : 'Create'}
          </button>
        </div>
      </form>
    </Modal>
  );
};

export default CreateCollectionModal;
