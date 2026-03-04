import { useState } from 'react';
import './CopyableUrl.css';

export const copyToClipboard = async (text) => {
  if (navigator.clipboard && window.isSecureContext) {
    await navigator.clipboard.writeText(text);
  } else {
    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.select();
    document.execCommand('copy');
    document.body.removeChild(textarea);
  }
};

const CopyableUrl = ({ value, className = '' }) => {
  const [copied, setCopied] = useState(false);

  const handleCopy = async (e) => {
    e.stopPropagation();
    try {
      await copyToClipboard(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch (err) {
      console.error('Failed to copy:', err);
    }
  };

  if (!value) return <span className="copyable-url-empty">-</span>;

  return (
    <span className={`copyable-url ${className}`}>
      <span className="copyable-url-value" title={value}>{value}</span>
      <button
        type="button"
        className={`copyable-url-btn ${copied ? 'copied' : ''}`}
        onClick={handleCopy}
        title="Copy to clipboard"
      >
        {copied ? (
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <polyline points="20 6 9 17 4 12"></polyline>
          </svg>
        ) : (
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
            <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
          </svg>
        )}
      </button>
    </span>
  );
};

const globeIcon = <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="10"></circle><line x1="2" y1="12" x2="22" y2="12"></line><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path></svg>;
const checkIcon = <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="#22c55e" strokeWidth="3"><polyline points="20 6 9 17 4 12"></polyline></svg>;

export const SitemapCopyButton = ({ url, className = '' }) => {
  const [copied, setCopied] = useState(false);

  const handleClick = async (e) => {
    e.stopPropagation();
    try {
      await copyToClipboard(url);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch (err) {
      console.error('Failed to copy:', err);
    }
  };

  return (
    <button
      type="button"
      className={`btn btn-sm btn-secondary ${className}`}
      onClick={handleClick}
      title={copied ? 'Copied!' : 'Copy sitemap URL'}
    >
      {copied ? checkIcon : globeIcon}
    </button>
  );
};

export const SitemapCopyIcon = ({ url }) => {
  const [copied, setCopied] = useState(false);

  const handleClick = async (e) => {
    e.stopPropagation();
    try {
      await copyToClipboard(url);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch (err) {
      console.error('Failed to copy:', err);
    }
  };

  return (
    <span onClick={handleClick}>
      {copied ? checkIcon : globeIcon}
    </span>
  );
};

export default CopyableUrl;
