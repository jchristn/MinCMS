import { useState } from 'react';
import Modal from './Modal.jsx';
import './DeleteConfirmModal.css';

const DeleteConfirmModal = ({
  isOpen,
  onClose,
  onConfirm,
  entityName,
  entityType = 'item',
  title = 'Confirm Delete',
  actionLabel = 'Delete',
  message,
  warningMessage = 'This action cannot be undone.'
}) => {
  const [deleting, setDeleting] = useState(false);

  const handleConfirm = async () => {
    setDeleting(true);
    try {
      await onConfirm();
    } finally {
      setDeleting(false);
    }
  };

  const handleClose = () => {
    if (!deleting) onClose();
  };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="medium">
      <div className="delete-confirm-modal">
        <div className="delete-warning-icon">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#dc2626" strokeWidth="2">
            <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path>
            <line x1="12" y1="9" x2="12" y2="13"></line>
            <line x1="12" y1="17" x2="12.01" y2="17"></line>
          </svg>
        </div>
        <p className="delete-message">
          {message || `Are you sure you want to delete this ${entityType}?`}
        </p>
        {entityName && <p className="entity-name">{entityName}</p>}
        {warningMessage && <p className="delete-warning">{warningMessage}</p>}
        <div className="delete-actions">
          <button className="btn btn-secondary" onClick={handleClose} disabled={deleting}>Cancel</button>
          <button className="btn btn-danger" onClick={handleConfirm} disabled={deleting}>
            {deleting ? <span className="btn-spinner" /> : actionLabel}
          </button>
        </div>
      </div>
    </Modal>
  );
};

export default DeleteConfirmModal;
