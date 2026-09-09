import './ObservabilityPanel.css';

const ExternalLinkIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path>
    <polyline points="15 3 21 3 21 9"></polyline>
    <line x1="10" y1="14" x2="21" y2="3"></line>
  </svg>
);

const ObservabilityPanel = () => {
  const services = (window.__MINCMS_CONFIG__?.observability || []).filter((s) => s && s.url);

  if (services.length === 0) return null;

  return (
    <div className="observability-panel">
      <div className="observability-header">
        <h2 className="observability-title">Observability</h2>
        <span className="observability-subtitle">Metrics, traces, and logs for this deployment</span>
      </div>
      <div className="observability-grid">
        {services.map((service) => (
          <a
            key={service.name}
            className="observability-card"
            href={service.url}
            target="_blank"
            rel="noopener noreferrer"
            title={`Open ${service.name} in a new tab`}
          >
            <div className="observability-card-top">
              <span className="observability-card-name">{service.name}</span>
              <ExternalLinkIcon />
            </div>
            {service.role && <span className="observability-card-role">{service.role}</span>}
            <span className="observability-card-url">{service.url}</span>
            {service.credentials && (
              <span className="observability-card-creds">
                <span className="observability-card-creds-label">Credentials:</span> {service.credentials}
              </span>
            )}
          </a>
        ))}
      </div>
    </div>
  );
};

export default ObservabilityPanel;
