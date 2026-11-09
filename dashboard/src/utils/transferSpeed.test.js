import { describe, expect, it } from 'vitest';
import { createSpeedTracker, formatBytes, formatSpeed } from './transferSpeed.js';

const MB = 1000 * 1000;

describe('createSpeedTracker', () => {
  it('has no speed until enough time has passed', () => {
    const tracker = createSpeedTracker();
    expect(tracker.bytesPerSecond(0)).toBeNull();
    tracker.record(0, 100 * MB, 0);
    expect(tracker.bytesPerSecond(100)).toBeNull();
  });

  it('measures a steady rate', () => {
    const tracker = createSpeedTracker();
    for (let s = 0; s <= 4; s++) tracker.record(s * 5 * MB, 100 * MB, s * 1000);
    expect(tracker.bytesPerSecond(4000)).toBe(5 * MB);
  });

  it('follows the current rate rather than the lifetime average', () => {
    const tracker = createSpeedTracker(2000);
    for (let s = 0; s <= 10; s++) tracker.record(s * 50 * MB, 2000 * MB, s * 1000);
    let loaded = 500 * MB;
    for (let s = 11; s <= 20; s++) {
      loaded += 5 * MB;
      tracker.record(loaded, 2000 * MB, s * 1000);
    }
    expect(tracker.bytesPerSecond(20000)).toBe(5 * MB);
  });

  it('decays during a stall and reaches zero once the window has passed', () => {
    const tracker = createSpeedTracker(2000);
    tracker.record(0, 100 * MB, 0);
    tracker.record(10 * MB, 100 * MB, 1000);
    tracker.record(20 * MB, 100 * MB, 2000);

    const during = tracker.bytesPerSecond(2500);
    expect(during).toBeGreaterThan(0);
    expect(during).toBeLessThan(10 * MB);
    expect(tracker.bytesPerSecond(10000)).toBe(0);
  });

  it('reports an average only once every byte is sent', () => {
    const tracker = createSpeedTracker();
    tracker.record(0, 20 * MB, 0);
    tracker.record(10 * MB, 20 * MB, 1000);
    expect(tracker.averageBytesPerSecond()).toBeNull();

    tracker.record(20 * MB, 20 * MB, 4000);
    expect(tracker.averageBytesPerSecond()).toBe(5 * MB);
  });
});

describe('formatting', () => {
  it.each([
    [0, '0 B'],
    [1023.4, '1023 B'],
    [1024, '1.0 KB'],
    [5 * 1024 * 1024, '5.0 MB'],
    [1.5 * 1024 * 1024 * 1024, '1.5 GB'],
  ])('formats %d bytes as %s', (bytes, expected) => {
    expect(formatBytes(bytes)).toBe(expected);
  });

  it('formats speeds and shows a dash when unknown', () => {
    expect(formatSpeed(12.3 * 1024 * 1024)).toBe('12.3 MB/s');
    expect(formatSpeed(0)).toBe('0 B/s');
    expect(formatSpeed(null)).toBe('—');
  });
});
