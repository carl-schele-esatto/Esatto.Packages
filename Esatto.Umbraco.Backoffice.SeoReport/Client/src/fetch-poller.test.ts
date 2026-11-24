import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FetchPoller } from './fetch-poller';
import { fetchStatus } from './testing/fixtures';
import type { FetchStatus } from './types';

const running = (done: number, total = 12) => fetchStatus({ running: true, done, total });
const finished = (done = 12, total = 12) => fetchStatus({ running: false, done, total });

// A poller whose reads are answered one by one by the test.
function setup(options: { maxFailures?: number } = {}) {
  const load = vi.fn<() => Promise<FetchStatus>>();
  const onProgress = vi.fn();
  const onFinished = vi.fn();
  const onError = vi.fn();
  const onGiveUp = vi.fn();
  const poller = new FetchPoller({ load, onProgress, onFinished, onError, onGiveUp, intervalMs: 2000, maxIntervalMs: 16000, ...options });
  return { load, onProgress, onFinished, onError, onGiveUp, poller };
}

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('FetchPoller', () => {
  it('reads nothing at once and then once every interval while the run is going', async () => {
    const { load, onProgress, poller } = setup();
    load.mockResolvedValueOnce(running(1)).mockResolvedValueOnce(running(2)).mockResolvedValue(running(3));

    poller.start();
    expect(load).not.toHaveBeenCalled();
    expect(poller.active).toBe(true);

    await vi.advanceTimersByTimeAsync(1999);
    expect(load).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(1);
    expect(load).toHaveBeenCalledTimes(1);
    expect(onProgress).toHaveBeenLastCalledWith(running(1));

    await vi.advanceTimersByTimeAsync(2000);
    expect(load).toHaveBeenCalledTimes(2);
    expect(onProgress).toHaveBeenLastCalledWith(running(2));
  });

  it('stops by itself, and says so once, when the read says the run is over', async () => {
    const { load, onProgress, onFinished, poller } = setup();
    load.mockResolvedValueOnce(running(5)).mockResolvedValueOnce(finished());

    poller.start();
    await vi.advanceTimersByTimeAsync(2000);
    await vi.advanceTimersByTimeAsync(2000);

    expect(onProgress).toHaveBeenCalledTimes(1);
    expect(onFinished).toHaveBeenCalledTimes(1);
    expect(onFinished).toHaveBeenCalledWith(finished());
    expect(poller.active).toBe(false);

    await vi.advanceTimersByTimeAsync(60_000);
    expect(load).toHaveBeenCalledTimes(2);
  });

  it('treats a run that was over before the first read as finished, with nothing to report as progress', async () => {
    const { load, onProgress, onFinished, poller } = setup();
    load.mockResolvedValueOnce(finished(0, 0));

    poller.start();
    await vi.advanceTimersByTimeAsync(2000);

    expect(onProgress).not.toHaveBeenCalled();
    expect(onFinished).toHaveBeenCalledWith(finished(0, 0));
  });

  it('never has two reads in flight: the next one waits for the answer to the last', async () => {
    const { load, poller } = setup();
    let answer!: (status: FetchStatus) => void;
    load.mockImplementationOnce(() => new Promise<FetchStatus>((resolve) => (answer = resolve)));
    load.mockResolvedValue(running(2));

    poller.start();
    await vi.advanceTimersByTimeAsync(2000);
    await vi.advanceTimersByTimeAsync(20_000);
    expect(load).toHaveBeenCalledTimes(1);

    answer(running(1));
    await vi.advanceTimersByTimeAsync(2000);
    expect(load).toHaveBeenCalledTimes(2);
  });

  it('backs off after a failed read, doubling up to the longest wait, and keeps trying', async () => {
    const { load, onError, poller } = setup();
    load.mockRejectedValue(new Error('offline'));

    poller.start();
    await vi.advanceTimersByTimeAsync(2000); // read 1 fails; next in 4 s
    expect(load).toHaveBeenCalledTimes(1);
    expect(onError).toHaveBeenLastCalledWith(expect.any(Error), 1);

    await vi.advanceTimersByTimeAsync(3999);
    expect(load).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(1); // read 2 fails; next in 8 s
    expect(load).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(8000); // read 3 fails; next in 16 s (the longest)
    expect(load).toHaveBeenCalledTimes(3);
    await vi.advanceTimersByTimeAsync(16_000); // read 4 fails; next in 16 s again, not 32
    expect(load).toHaveBeenCalledTimes(4);
    await vi.advanceTimersByTimeAsync(16_000);
    expect(load).toHaveBeenCalledTimes(5);
    expect(onError).toHaveBeenLastCalledWith(expect.any(Error), 5);
  });

  it('goes back to the normal interval after a read succeeds', async () => {
    const { load, poller } = setup();
    load.mockRejectedValueOnce(new Error('offline')).mockResolvedValue(running(1));

    poller.start();
    await vi.advanceTimersByTimeAsync(2000); // fails
    await vi.advanceTimersByTimeAsync(4000); // succeeds
    expect(load).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(2000);
    expect(load).toHaveBeenCalledTimes(3);
  });

  it('gives up, and says so, after too many failed reads in a row', async () => {
    const { load, onGiveUp, poller } = setup({ maxFailures: 3 });
    load.mockRejectedValue(new Error('gone'));

    poller.start();
    await vi.advanceTimersByTimeAsync(2000);
    await vi.advanceTimersByTimeAsync(4000);
    expect(onGiveUp).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(8000);

    expect(onGiveUp).toHaveBeenCalledTimes(1);
    expect(poller.active).toBe(false);
    await vi.advanceTimersByTimeAsync(60_000);
    expect(load).toHaveBeenCalledTimes(3);
  });

  it('forgets earlier failures when a read succeeds, so only a run of failures gives up', async () => {
    const { load, onGiveUp, poller } = setup({ maxFailures: 2 });
    load
      .mockRejectedValueOnce(new Error('a'))
      .mockResolvedValueOnce(running(1))
      .mockRejectedValueOnce(new Error('b'))
      .mockResolvedValue(running(2));

    poller.start();
    await vi.advanceTimersByTimeAsync(2000 + 4000 + 2000 + 4000 + 2000);

    expect(onGiveUp).not.toHaveBeenCalled();
    expect(poller.active).toBe(true);
  });

  it('stop() cancels the next read and ignores the answer of a read that is already on its way', async () => {
    const { load, onProgress, onFinished, poller } = setup();
    let answer!: (status: FetchStatus) => void;
    load.mockImplementationOnce(() => new Promise<FetchStatus>((resolve) => (answer = resolve)));

    poller.start();
    await vi.advanceTimersByTimeAsync(2000);
    poller.stop();
    answer(finished());
    await vi.advanceTimersByTimeAsync(60_000);

    expect(onProgress).not.toHaveBeenCalled();
    expect(onFinished).not.toHaveBeenCalled();
    expect(load).toHaveBeenCalledTimes(1);
    expect(poller.active).toBe(false);
  });

  it('stop() before the first read leaves no timer behind', async () => {
    const { load, poller } = setup();

    poller.start();
    poller.stop();
    await vi.advanceTimersByTimeAsync(60_000);

    expect(load).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
  });

  it('start() while it is already going does not start a second chain of reads', async () => {
    const { load, poller } = setup();
    load.mockResolvedValue(running(1));

    poller.start();
    poller.start();
    await vi.advanceTimersByTimeAsync(2000);

    expect(load).toHaveBeenCalledTimes(1);
    expect(vi.getTimerCount()).toBe(1);
  });

  it('can be started again after it has finished', async () => {
    const { load, onFinished, poller } = setup();
    load.mockResolvedValueOnce(finished()).mockResolvedValueOnce(finished());

    poller.start();
    await vi.advanceTimersByTimeAsync(2000);
    poller.start();
    await vi.advanceTimersByTimeAsync(2000);

    expect(onFinished).toHaveBeenCalledTimes(2);
  });

  // A poller that belongs to an element that has left the page must never run again: dispose is final (a new element has its own).
  describe('dispose', () => {
    it('stops a poller that is going, cancels its timer and ignores the read that is on its way', async () => {
      const { load, onProgress, onFinished, poller } = setup();
      let answer!: (status: FetchStatus) => void;
      load.mockImplementationOnce(() => new Promise<FetchStatus>((resolve) => (answer = resolve)));

      poller.start();
      await vi.advanceTimersByTimeAsync(2000);
      poller.dispose();
      answer(finished());
      await vi.advanceTimersByTimeAsync(60_000);

      expect(onProgress).not.toHaveBeenCalled();
      expect(onFinished).not.toHaveBeenCalled();
      expect(load).toHaveBeenCalledTimes(1);
      expect(poller.active).toBe(false);
      expect(vi.getTimerCount()).toBe(0);
    });

    it('makes start() do nothing for good, so a request that answers after the element has gone cannot bring the poller back', async () => {
      const { load, poller } = setup();
      load.mockResolvedValue(running(1));

      poller.dispose();
      poller.start();
      await vi.advanceTimersByTimeAsync(60_000);

      expect(load).not.toHaveBeenCalled();
      expect(poller.active).toBe(false);
      expect(vi.getTimerCount()).toBe(0);
      expect(poller.disposed).toBe(true);
    });

    it('can be called twice, and before anything started', () => {
      const { poller } = setup();

      expect(() => {
        poller.dispose();
        poller.dispose();
      }).not.toThrow();
    });

    it('does not report a failed read that was on its way, nor give up on it', async () => {
      const { load, onError, onGiveUp, poller } = setup({ maxFailures: 1 });
      let fail!: (error: unknown) => void;
      load.mockImplementationOnce(() => new Promise<FetchStatus>((_, reject) => (fail = reject)));

      poller.start();
      await vi.advanceTimersByTimeAsync(2000);
      poller.dispose();
      fail(new Error('late'));
      await vi.advanceTimersByTimeAsync(60_000);

      expect(onError).not.toHaveBeenCalled();
      expect(onGiveUp).not.toHaveBeenCalled();
    });

    it('is not undone by a new poller: stopping the old one leaves the poller of the next element alone', async () => {
      const first = setup();
      const second = setup();
      first.load.mockResolvedValue(running(1));
      second.load.mockResolvedValue(running(1));

      first.poller.start();
      first.poller.dispose();
      second.poller.start();
      await vi.advanceTimersByTimeAsync(2000);

      expect(first.load).not.toHaveBeenCalled();
      expect(second.load).toHaveBeenCalledTimes(1);
    });
  });
});
