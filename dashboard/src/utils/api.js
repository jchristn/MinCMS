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

    if (!(options.body instanceof FormData)) {
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

  async upload(endpoint, file, fileName) {
    const formData = new FormData();
    if (fileName) {
      formData.append('file', file, fileName);
    } else {
      formData.append('file', file);
    }

    return this.request(endpoint, {
      method: 'POST',
      body: formData,
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

  async uploadFile(slug, file, fileName) {
    return this.upload(`/v1.0/collections/${encodeURIComponent(slug)}/files`, file, fileName);
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
