import { useState } from 'react';
import { useAuth } from '../context/AuthContext.jsx';
import AlertModal from './modals/AlertModal.jsx';
import './Login.css';

const Login = () => {
  const { login } = useAuth();
  const [serverUrl, setServerUrl] = useState(window.__MINCMS_CONFIG__?.serverUrl || 'http://localhost:8200');
  const [accessKey, setAccessKey] = useState('');
  const [loading, setLoading] = useState(false);
  const [alertModal, setAlertModal] = useState({ isOpen: false, message: '', type: 'error' });

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!serverUrl || !accessKey) {
      setAlertModal({ isOpen: true, message: 'Please enter both Server URL and Access Key.', type: 'warning' });
      return;
    }
    setLoading(true);
    try {
      await login(serverUrl, accessKey);
    } catch (error) {
      setAlertModal({ isOpen: true, message: error.message || 'Failed to connect to server.', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-container">
      <div className="login-box">
        <div className="login-logo">
          <img src={window.__MINCMS_CONFIG__?.logoFile || '/assets/logo.png'} alt="MinCMS" />
        </div>
        <h1 className="login-title">MinCMS</h1>
        <p className="login-subtitle">Content Management System</p>

        <form onSubmit={handleSubmit} className="login-form">
          <div className="form-group">
            <label htmlFor="serverUrl">Server URL</label>
            <input
              id="serverUrl"
              type="text"
              value={serverUrl}
              onChange={(e) => setServerUrl(e.target.value)}
              placeholder="http://localhost:8200"
              autoFocus
            />
          </div>
          <div className="form-group">
            <label htmlFor="accessKey">Access Key</label>
            <input
              id="accessKey"
              type="password"
              value={accessKey}
              onChange={(e) => setAccessKey(e.target.value)}
              placeholder="Enter your access key"
            />
          </div>
          <button type="submit" className="btn btn-primary login-btn" disabled={loading}>
            {loading ? 'Connecting...' : 'Connect'}
          </button>
        </form>
      </div>

      <AlertModal
        isOpen={alertModal.isOpen}
        onClose={() => setAlertModal({ ...alertModal, isOpen: false })}
        message={alertModal.message}
        type={alertModal.type}
      />
    </div>
  );
};

export default Login;
