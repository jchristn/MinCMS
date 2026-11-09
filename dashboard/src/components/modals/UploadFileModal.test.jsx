import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import UploadFileModal from './UploadFileModal.jsx';

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function makeFile(name) {
  return new File(['data'], name, { type: 'text/plain' });
}

function pickFiles(container, files) {
  fireEvent.change(container.querySelector('input[type="file"][multiple]'), { target: { files } });
}

function renderModal(onSubmit, onClose = vi.fn()) {
  return { onClose, ...render(<UploadFileModal isOpen onClose={onClose} onSubmit={onSubmit} initialFiles={null} />) };
}

const startUpload = (count = 1) =>
  fireEvent.click(screen.getByRole('button', { name: `Upload ${count} file${count !== 1 ? 's' : ''}` }));

describe('UploadFileModal', () => {
  afterEach(cleanup);

  it('shows percent progress, then Finalizing, then done', async () => {
    const upload = deferred();
    const onSubmit = vi.fn(() => upload.promise);
    const { container } = renderModal(onSubmit);

    pickFiles(container, [makeFile('big.zip')]);
    startUpload();
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));

    const [sentFile, relativePath, onProgress] = onSubmit.mock.calls[0];
    expect(sentFile.name).toBe('big.zip');
    expect(relativePath).toBe('big.zip');

    act(() => onProgress({ loaded: 425, total: 1000 }));
    expect(screen.getByText('42%')).toBeTruthy();
    expect(screen.getByRole('progressbar').getAttribute('aria-valuenow')).toBe('42');

    // All bytes sent: the server is still storing the file, which is not "done".
    act(() => onProgress({ loaded: 1000, total: 1000 }));
    expect(screen.getByText('Finalizing…')).toBeTruthy();
    expect(screen.queryByRole('progressbar')).toBeNull();
    expect(container.querySelector('.status-done')).toBeNull();

    await act(async () => upload.resolve());
    await waitFor(() => expect(screen.getByText('1 of 1 uploaded successfully')).toBeTruthy());
    expect(screen.queryByText('Finalizing…')).toBeNull();
    expect(container.querySelector('svg.status-done')).toBeTruthy();
  });

  it('reports failures and clears the progress display', async () => {
    const upload = deferred();
    const onSubmit = vi.fn(() => upload.promise);
    const { container } = renderModal(onSubmit);

    pickFiles(container, [makeFile('bad.zip')]);
    startUpload();
    await waitFor(() => expect(onSubmit).toHaveBeenCalled());
    act(() => onSubmit.mock.calls[0][2]({ loaded: 50, total: 100 }));

    await act(async () => upload.reject(new Error('Storage is full')));
    await waitFor(() => expect(screen.getByText(/0 of 1 uploaded successfully/)).toBeTruthy());
    expect(screen.getByText(/1 failed/)).toBeTruthy();
    expect(screen.getByTitle('Storage is full')).toBeTruthy();
    expect(screen.queryByText('50%')).toBeNull();
  });

  it('runs at most four uploads at a time and tracks progress per file', async () => {
    const uploads = Array.from({ length: 5 }, deferred);
    let next = 0;
    const onSubmit = vi.fn(() => uploads[next++].promise);
    const { container } = renderModal(onSubmit);

    pickFiles(container, ['a', 'b', 'c', 'd', 'e'].map((n) => makeFile(`${n}.bin`)));
    startUpload(5);
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(4));

    act(() => onSubmit.mock.calls[1][2]({ loaded: 10, total: 100 }));
    const rows = container.querySelectorAll('.upload-file-row');
    expect(rows[1].textContent).toContain('10%');
    expect(rows[0].textContent).not.toContain('%');

    await act(async () => uploads[0].resolve());
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(5));

    await act(async () => uploads.slice(1).forEach((u) => u.resolve()));
    await waitFor(() => expect(screen.getByText('5 of 5 uploaded successfully')).toBeTruthy());
  });

  it('ignores progress from an upload after the modal is closed', async () => {
    const upload = deferred();
    const onSubmit = vi.fn(() => upload.promise);
    const { container, onClose } = renderModal(onSubmit);

    pickFiles(container, [makeFile('first.bin')]);
    startUpload();
    await waitFor(() => expect(onSubmit).toHaveBeenCalled());
    const staleProgress = onSubmit.mock.calls[0][2];

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(onClose).toHaveBeenCalled();

    pickFiles(container, [makeFile('second.bin')]);
    act(() => staleProgress({ loaded: 50, total: 100 }));
    expect(container.querySelector('.upload-file-row').textContent).toContain('second.bin');
    expect(screen.queryByText('50%')).toBeNull();

    await act(async () => upload.resolve());
  });

  it('keeps file selection behavior: removing files and de-duplicating paths', () => {
    const { container } = renderModal(vi.fn());

    pickFiles(container, [makeFile('a.txt'), makeFile('b.txt')]);
    pickFiles(container, [makeFile('a.txt')]);
    expect(container.querySelectorAll('.upload-file-row').length).toBe(2);

    fireEvent.click(screen.getAllByTitle('Remove')[0]);
    expect(container.querySelectorAll('.upload-file-row').length).toBe(1);
    expect(container.querySelector('.upload-file-row').textContent).toContain('b.txt');

    fireEvent.click(screen.getByRole('button', { name: 'Clear All' }));
    expect(container.querySelectorAll('.upload-file-row').length).toBe(0);
  });

  it('passes relative folder paths through to the upload', async () => {
    const onSubmit = vi.fn(async () => {});
    const { container } = renderModal(onSubmit);
    const nested = makeFile('c.txt');
    Object.defineProperty(nested, 'webkitRelativePath', { value: 'folder/c.txt' });

    pickFiles(container, [nested]);
    startUpload();
    await waitFor(() => expect(onSubmit).toHaveBeenCalled());
    expect(onSubmit.mock.calls[0][1]).toBe('folder/c.txt');
    await waitFor(() => expect(screen.getByText('1 of 1 uploaded successfully')).toBeTruthy());
  });
});
