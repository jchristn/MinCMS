import Modal from './Modal.jsx';
import './AlertModal.css';

const AlertModal = ({ isOpen, onClose, title, message, type = 'error', buttonLabel = 'OK' }) => {
  const getIcon = () => {
    switch (type) {
      case 'success':
        return <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="#16a34a" strokeWidth="2"><circle cx="12" cy="12" r="10"></circle><polyline points="16 8 10 16 7 13"></polyline></svg>;
      case 'warning':
        return <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="#d97706" strokeWidth="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>;
      case 'info':
        return <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="#2563eb" strokeWidth="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>;
      default:
        return <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="#dc2626" strokeWidth="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>;
    }
  };

  const getTitle = () => {
    if (title) return title;
    switch (type) {
      case 'success': return 'Success';
      case 'warning': return 'Warning';
      case 'info': return 'Information';
      default: return 'Error';
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={getTitle()} size="medium">
      <div className={`alert-modal alert-modal-${type}`}>
        <div className="alert-icon">{getIcon()}</div>
        <p className="alert-message">{message}</p>
        <div className="alert-actions">
          <button className={`btn btn-alert btn-${type}`} onClick={onClose}>
            {buttonLabel}
          </button>
        </div>
      </div>
    </Modal>
  );
};

export default AlertModal;
