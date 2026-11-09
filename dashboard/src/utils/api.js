class ApiClient {
  constructor(baseUrl, accessKey) {
    this.baseUrl = baseUrl.replace(/\/+$/, '');
    this.accessKey = accessKey;
  }

  async request(endpoint, options = {}) {
    const url = `${this.baseUrl}${endpoint}`;
    const headers = {
      ...options.headers,
    };

    if (this.accessKey) {
      headers['x-api-key'] = this.accessKey;
    }

    if (options.body !== undefined && !(options.body instanceof FormData)) {
      headers['Content-Type'] = 'application/json';
    }

    const response = await fetch(url, { ...options, headers });

    if (!response.ok) {
      let errorMessage = `HTTP ${response.status}`;
      try {
        const contentType = response.headers.get('content-type');
        if (contentType && contentType.includes('application/json')) {
          const errorData = await response.json();
          errorMessage = errorData.Description || errorData.Message || errorMessage;
        }
      } catch (_) { /* ignore parse errors */ }

      const error = new Error(errorMessage);
      error.status = response.status;
      throw error;
    }

    if (response.status === 204 || response.headers.get('content-length') === '0') {
      return null;
    }

    const contentType = response.headers.get('content-type');
    if (contentType && contentType.includes('application/json')) {
      const text = await response.text();
      return text ? JSON.parse(text) : null;
    }

    return await response.text();
  }

  async get(endpoint) {
    return this.request(endpoint, { method: 'GET' });
  }

  async post(endpoint, data) {
    return this.request(endpoint, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async del(endpoint) {
    return this.request(endpoint, { method: 'DELETE' });
  }

  // Uses XMLHttpRequest because fetch cannot report request-body progress.
  // onProgress receives { loaded, total } in bytes, including multipart overhead.
  // Aborting signal closes the connection, which makes the server discard the upload.
  async upload(endpoint, file, fileName, onProgress, signal) {
    const formData = new FormData();
    if (fileName) {
      formData.append('file', file, fileName);
    } else {
      formData.append('file', file);
    }

    return new Promise((resolve, reject) => {
      if (signal?.aborted) {
        reject(new Error('Upload aborted'));
        return;
      }

      const xhr = new XMLHttpRequest();
      const abort = () => xhr.abort();
      signal?.addEventListener('abort', abort);
      xhr.onloadend = () => signal?.removeEventListener('abort', abort);

      xhr.open('POST', `${this.baseUrl}${endpoint}`);

      if (this.accessKey) {
        xhr.setRequestHeader('x-api-key', this.accessKey);
      }

      if (onProgress) {
        xhr.upload.onprogress = (e) => {
          if (e.lengthComputable) {
            onProgress({ loaded: e.loaded, total: e.total });
          }
        };
      }

      xhr.onload = () => {
        const contentType = xhr.getResponseHeader('content-type');
        const isJson = contentType && contentType.includes('application/json');

        if (xhr.status < 200 || xhr.status >= 300) {
          let errorMessage = `HTTP ${xhr.status}`;
          try {
            if (isJson) {
              const errorData = JSON.parse(xhr.responseText);
              errorMessage = errorData.Description || errorData.Message || errorMessage;
            }
          } catch (_) { /* ignore parse errors */ }

          const error = new Error(errorMessage);
          error.status = xhr.status;
          reject(error);
          return;
        }

        if (xhr.status === 204 || xhr.getResponseHeader('content-length') === '0') {
          resolve(null);
          return;
        }

        try {
          if (isJson) {
            resolve(xhr.responseText ? JSON.parse(xhr.responseText) : null);
          } else {
            resolve(xhr.responseText);
          }
        } catch (e) {
          reject(e);
        }
      };

      xhr.onerror = () => reject(new Error('Network error during upload'));
      xhr.onabort = () => reject(new Error('Upload aborted'));

      xhr.send(formData);
    });
  }

  async testConnection() {
    try {
      await this.get('/v1.0/collections');
      return true;
    } catch (e) {
      return false;
    }
  }

  async listCollections() {
    return this.get('/v1.0/collections');
  }

  async createCollection(name, slug) {
    return this.post('/v1.0/collections', { Name: name, Slug: slug });
  }

  async getCollection(slug) {
    return this.get(`/v1.0/collections/${encodeURIComponent(slug)}`);
  }

  async deleteCollection(slug) {
    return this.del(`/v1.0/collections/${encodeURIComponent(slug)}`);
  }

  async listFiles(slug) {
    return this.get(`/v1.0/collections/${encodeURIComponent(slug)}/files`);
  }

  async uploadFile(slug, file, fileName, onProgress, signal) {
    return this.upload(`/v1.0/collections/${encodeURIComponent(slug)}/files`, file, fileName, onProgress, signal);
  }

  async getFileText(slug, fileName) {
    return this.request(`/download/${encodeURIComponent(slug)}/${encodeURIComponent(fileName)}`, {
      method: 'GET',
      headers: {
        Accept: 'text/plain, text/*;q=0.9, */*;q=0.1',
      },
    });
  }

  async saveTextFile(slug, fileName, content, contentType = 'text/plain') {
    const fileParts = fileName.split('/').filter(Boolean);
    const leafName = fileParts[fileParts.length - 1] || 'untitled.txt';
    const file = new File([content], leafName, { type: contentType });
    return this.uploadFile(slug, file, fileName);
  }

  async getFileMetadata(slug, fileName) {
    return this.get(`/v1.0/collections/${encodeURIComponent(slug)}/files/${encodeURIComponent(fileName)}`);
  }

  async deleteFile(slug, fileName) {
    return this.del(`/v1.0/collections/${encodeURIComponent(slug)}/files/${encodeURIComponent(fileName)}`);
  }

  async deleteFiles(slug, fileNames) {
    return this.request(`/v1.0/collections/${encodeURIComponent(slug)}/files`, {
      method: 'DELETE',
      body: JSON.stringify({ FileNames: fileNames }),
    });
  }

  getDownloadUrl(slug, fileName) {
    return `${this.baseUrl}/download/${encodeURIComponent(slug)}/${encodeURIComponent(fileName)}`;
  }
}

export default ApiClient;
