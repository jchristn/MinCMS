const DEFAULT_WINDOW_MS = 5000;
const MIN_ELAPSED_MS = 200;

// Tracks transfer speed over a sliding window, so the figure follows the current rate
// and falls to zero during a stall rather than holding its last value.
export function createSpeedTracker(windowMs = DEFAULT_WINDOW_MS) {
  let samples = [];
  let firstSample = null;
  let completedAt = null;

  return {
    record(loaded, total, now = Date.now()) {
      const sample = { loaded, at: now };
      samples.push(sample);
      if (!firstSample) firstSample = sample;
      if (completedAt === null && total > 0 && loaded >= total) completedAt = now;

      const recent = samples.filter((s) => s.at >= now - windowMs);
      samples = recent.length > 0 ? recent : [sample];
    },

    // Bytes per second over the window, or null until there is enough data.
    bytesPerSecond(now = Date.now()) {
      if (samples.length === 0) return null;
      const last = samples[samples.length - 1];
      const inWindow = samples.find((s) => s.at >= now - windowMs) ?? last;
      const elapsedMs = now - inWindow.at;
      if (elapsedMs < MIN_ELAPSED_MS) return null;
      return ((last.loaded - inWindow.loaded) / elapsedMs) * 1000;
    },

    // Average speed from the first sample until all bytes were sent, or null if unfinished.
    averageBytesPerSecond() {
      if (completedAt === null || !firstSample) return null;
      const elapsedMs = completedAt - firstSample.at;
      if (elapsedMs <= 0) return null;
      const last = samples[samples.length - 1];
      return ((last.loaded - firstSample.loaded) / elapsedMs) * 1000;
    },
  };
}

export function formatBytes(bytes) {
  if (bytes < 1024) return Math.round(bytes) + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
  if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  return (bytes / (1024 * 1024 * 1024)).toFixed(1) + ' GB';
}

export function formatSpeed(bytesPerSecond) {
  if (bytesPerSecond === null || bytesPerSecond === undefined) return '—';
  return `${formatBytes(bytesPerSecond)}/s`;
}
