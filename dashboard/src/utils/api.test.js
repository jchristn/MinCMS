import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import ApiClient from './api.js';

class FakeXhr {
  static instances = [];

  constructor() {
    this.headers = {};
    this.responseHeaders = {};
    this.upload = {};
    this.status = 0;
    this.responseText = '';
    FakeXhr.instances.push(this);
  }

  open(method, url) {
    this.method = method;
    this.url = url;
  }

  setRequestHeader(name, value) {
    this.headers[name] = value;
  }

  getResponseHeader(name) {
    return this.responseHeaders[name.toLowerCase()] ?? null;
  }

  send(body) {
    this.body = body;
  }

  respond(status, responseText = '', responseHeaders = {}) {
    this.status = status;
    this.responseText = responseText;
    this.responseHeaders = responseHeaders;
    this.onload();
  }
}

const JSON_HEADERS = { 'content-type': 'application/json' };
const file = () => new File(['hello'], 'hello.txt', { type: 'text/plain' });
const lastXhr = () => FakeXhr.instances[FakeXhr.instances.length - 1];

describe('ApiClient uploads', () => {
  let client;

  beforeEach(() => {
    FakeXhr.instances = [];
    vi.stubGlobal('XMLHttpRequest', FakeXhr);
    client = new ApiClient('http://server:8200/', 'secret');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('posts the file as form data to the encoded collection route with the API key', async () => {
    const pending = client.uploadFile('my slug', file());
    const xhr = lastXhr();

    expect(xhr.method).toBe('POST');
    expect(xhr.url).toBe('http://server:8200/v1.0/collections/my%20slug/files');
    expect(xhr.headers['x-api-key']).toBe('secret');
    // The browser must set the multipart boundary itself.
    expect(xhr.headers['Content-Type']).toBeUndefined();
    expect(xhr.body).toBeInstanceOf(FormData);
    expect(xhr.body.get('file').name).toBe('hello.txt');

    xhr.respond(201, '{"FileName":"hello.txt"}', JSON_HEADERS);
    await expect(pending).resolves.toEqual({ FileName: 'hello.txt' });
  });

  it('keeps a custom relative path as the uploaded file name', async () => {
    const pending = client.uploadFile('alpha', file(), 'folder/renamed.txt');
    const xhr = lastXhr();
    expect(xhr.body.get('file').name).toBe('folder/renamed.txt');
    xhr.respond(201, '{}', JSON_HEADERS);
    await pending;
  });

  it('omits the API key header when no key is configured', async () => {
    const pending = new ApiClient('http://server:8200', '').uploadFile('alpha', file());
    const xhr = lastXhr();
    expect(xhr.headers['x-api-key']).toBeUndefined();
    xhr.respond(201, '{}', JSON_HEADERS);
    await pending;
  });

  it('reports byte progress when the total is known and ignores it otherwise', async () => {
    const onProgress = vi.fn();
    const pending = client.uploadFile('alpha', file(), undefined, onProgress);
    const xhr = lastXhr();

    xhr.upload.onprogress({ lengthComputable: false, loaded: 1, total: 0 });
    expect(onProgress).not.toHaveBeenCalled();

    xhr.upload.onprogress({ lengthComputable: true, loaded: 5, total: 10 });
    expect(onProgress).toHaveBeenCalledWith({ loaded: 5, total: 10 });

    xhr.respond(201, '{}', JSON_HEADERS);
    await pending;
  });

  it.each([
    ['Description', '{"Description":"Bad things"}', 'Bad things'],
    ['Message', '{"Message":"Other things"}', 'Other things'],
    ['empty JSON', '{}', 'HTTP 400'],
    ['malformed JSON', '{not json', 'HTTP 400'],
  ])('rejects with the server error message from %s', async (_label, body, expected) => {
    const pending = client.uploadFile('alpha', file());
    lastXhr().respond(400, body, JSON_HEADERS);
    await expect(pending).rejects.toMatchObject({ message: expected, status: 400 });
  });

  it('rejects with the HTTP status for non-JSON errors', async () => {
    const pending = client.uploadFile('alpha', file());
    lastXhr().respond(502, '<html>Bad Gateway</html>', { 'content-type': 'text/html' });
    await expect(pending).rejects.toMatchObject({ message: 'HTTP 502', status: 502 });
  });

  it('resolves null for empty responses and text for non-JSON responses', async () => {
    let pending = client.uploadFile('alpha', file());
    lastXhr().respond(204);
    await expect(pending).resolves.toBeNull();

    pending = client.uploadFile('alpha', file());
    lastXhr().respond(201, '', { 'content-length': '0' });
    await expect(pending).resolves.toBeNull();

    pending = client.uploadFile('alpha', file());
    lastXhr().respond(200, 'ok', { 'content-type': 'text/plain' });
    await expect(pending).resolves.toBe('ok');
  });

  it('rejects on a network failure or abort', async () => {
    let pending = client.uploadFile('alpha', file());
    lastXhr().onerror();
    await expect(pending).rejects.toThrow('Network error during upload');

    pending = client.uploadFile('alpha', file());
    lastXhr().onabort();
    await expect(pending).rejects.toThrow('Upload aborted');
  });

  it('aborts the request when the signal fires and stops listening afterward', async () => {
    const controller = new AbortController();
    const pending = client.uploadFile('alpha', file(), undefined, undefined, controller.signal);
    const xhr = lastXhr();
    xhr.abort = vi.fn(() => xhr.onabort());

    controller.abort();
    expect(xhr.abort).toHaveBeenCalledTimes(1);
    await expect(pending).rejects.toThrow('Upload aborted');

    xhr.onloadend();
    controller.abort();
    expect(xhr.abort).toHaveBeenCalledTimes(1);
  });

  it('never sends a request when the signal has already fired', async () => {
    const controller = new AbortController();
    controller.abort();
    await expect(client.uploadFile('alpha', file(), undefined, undefined, controller.signal)).rejects.toThrow('Upload aborted');
    expect(FakeXhr.instances.length).toBe(0);
  });

  it('saves edited text through the same upload route', async () => {
    const pending = client.saveTextFile('alpha', 'docs/notes.txt', 'content');
    const xhr = lastXhr();
    expect(xhr.url).toBe('http://server:8200/v1.0/collections/alpha/files');
    const sent = xhr.body.get('file');
    expect(sent.name).toBe('docs/notes.txt');
    expect(await sent.text()).toBe('content');
    xhr.respond(201, '{}', JSON_HEADERS);
    await pending;
  });
});

describe('ApiClient non-upload requests', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('still fetches text content and builds encoded download URLs', async () => {
    const fetchMock = vi.fn(async () => new Response('body text', { headers: { 'content-type': 'text/plain' } }));
    vi.stubGlobal('fetch', fetchMock);
    const client = new ApiClient('http://server:8200', 'secret');

    await expect(client.getFileText('alpha', 'a b.txt')).resolves.toBe('body text');
    expect(fetchMock.mock.calls[0][0]).toBe('http://server:8200/download/alpha/a%20b.txt');
    expect(client.getDownloadUrl('alpha', 'a/b c.zip')).toBe('http://server:8200/download/alpha/a%2Fb%20c.zip');
  });
});
