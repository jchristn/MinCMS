#!/bin/sh
# Generate runtime config from environment variables
cat > /usr/share/nginx/html/runtime-config.js <<EOF
window.__MINCMS_CONFIG__ = {
  serverUrl: "${MINCMS_SERVER_URL:-http://localhost:8100}",
  logoFile: "${MINCMS_LOGO_FILE:-/assets/logo.png}",
  logoNoTextFile: "${MINCMS_LOGO_NOTEXT_FILE:-/assets/logo-no-text.png}",
  faviconFile: "${MINCMS_FAVICON_FILE:-/assets/logo-no-text.ico}"
};
EOF

# Override default nginx config to prevent caching of runtime-config.js
cat > /etc/nginx/conf.d/default.conf <<'NGINX'
server {
    listen 8300;
    root /usr/share/nginx/html;
    index index.html;

    location = /runtime-config.js {
        add_header Cache-Control "no-store, no-cache, must-revalidate";
        add_header Pragma "no-cache";
        add_header Expires "0";
    }

    location / {
        try_files $uri $uri/ /index.html;
    }
}
NGINX

exec nginx -g 'daemon off;'
