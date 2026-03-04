import { createContext, useContext, useState, useEffect } from 'react';
import ApiClient from '../utils/api.js';

const AuthContext = createContext(null);

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

export const AuthProvider = ({ children }) => {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [apiClient, setApiClient] = useState(null);
  const [serverUrl, setServerUrl] = useState(window.__MINCMS_CONFIG__?.serverUrl || 'http://localhost:8200');
  const [accessKey, setAccessKey] = useState('');
  const [theme, setTheme] = useState('light');

  useEffect(() => {
    const savedUrl = localStorage.getItem('mincms_server_url');
    const savedKey = localStorage.getItem('mincms_access_key');
    const savedTheme = localStorage.getItem('mincms_theme');

    if (savedUrl && savedKey) {
      setServerUrl(savedUrl);
      setAccessKey(savedKey);
      const client = new ApiClient(savedUrl, savedKey);
      setApiClient(client);
      setIsAuthenticated(true);
    }

    if (savedTheme) {
      setTheme(savedTheme);
      document.body.setAttribute('data-theme', savedTheme);
    }
  }, []);

  const login = async (url, key) => {
    const client = new ApiClient(url, key);
    const isConnected = await client.testConnection();
    if (!isConnected) throw new Error('Failed to connect to server. Check your URL and access key.');

    localStorage.setItem('mincms_server_url', url);
    localStorage.setItem('mincms_access_key', key);
    setServerUrl(url);
    setAccessKey(key);
    setApiClient(client);
    setIsAuthenticated(true);
  };

  const logout = () => {
    localStorage.removeItem('mincms_server_url');
    localStorage.removeItem('mincms_access_key');
    setServerUrl('');
    setAccessKey('');
    setApiClient(null);
    setIsAuthenticated(false);
  };

  const toggleTheme = () => {
    const newTheme = theme === 'light' ? 'dark' : 'light';
    setTheme(newTheme);
    localStorage.setItem('mincms_theme', newTheme);
    document.body.setAttribute('data-theme', newTheme);
  };

  const value = {
    isAuthenticated,
    apiClient,
    serverUrl,
    accessKey,
    theme,
    login,
    logout,
    toggleTheme,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};
