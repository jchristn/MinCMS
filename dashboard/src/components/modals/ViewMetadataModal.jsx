import Modal from './Modal.jsx';
import './ViewMetadataModal.css';

const ViewMetadataModal = ({ isOpen, onClose, title = 'File Metadata', data }) => {
  if (!data) return null;

  const formatSize = (bytes) => {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(1) + ' GB';
  };

  const fields = [
    { label: 'File Name', value: data.FileName },
    { label: 'S3 Key', value: data.Key },
    { label: 'Content Type', value: data.ContentType },
    { label: 'Size', value: data.Size != null ? formatSize(data.Size) + ' (' + data.Size.toLocaleString() + ' bytes)' : '-' },
    { label: 'Last Modified', value: data.LastModifiedUtc ? new Date(data.LastModifiedUtc).toLocaleString() + ' UTC' : '-' },
    { label: 'ETag', value: data.ETag },
  ];

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title} size="large">
      <div className="metadata-modal">
        <table className="metadata-table">
          <tbody>
            {fields.map((field, idx) => (
              <tr key={idx}>
                <td className="metadata-label">{field.label}</td>
                <td className="metadata-value">{field.value || '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Modal>
  );
};

export default ViewMetadataModal;
