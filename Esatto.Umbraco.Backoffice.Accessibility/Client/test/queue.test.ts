import { expect, test } from 'vitest';
import { BlockedFrameError, ScanQueue, type JobOutcome } from '../src/scan/queue';
import type { Job } from '../src/types';

const job = (i: number): Job => ({ contentKey: `k${i}`, name: `P${i}`, culture: '', url: `https://a.se/${i}`, viewport: 1280 });
const tick = () => new Promise((r) => setTimeout(r, 1));

test('queue runs at most `concurrency` jobs at once and finishes all', async () => {
  let inFlight = 0;
  let max = 0;
  const settled: string[] = [];
  const queue = new ScanQueue(
    [1, 2, 3, 4, 5, 6, 7].map(job),
    { concurrency: 3, retries: 1 },
    async () => {
      inFlight++;
      max = Math.max(max, inFlight);
      await tick();
      inFlight--;
      return { kind: 'ok' } as JobOutcome;
    },
    async (j) => { settled.push(j.url); },
  );
  await queue.start();
  expect(max).toBe(3);
  expect(settled).toHaveLength(7);
});

test('failed job is retried once then reported failed', async () => {
  let calls = 0;
  const outcomes: JobOutcome[] = [];
  const queue = new ScanQueue([job(1)], { concurrency: 1, retries: 1 }, async () => {
    calls++;
    return { kind: 'failed', reason: 'timeout' };
  }, async (_j, o) => { outcomes.push(o); });
  await queue.start();
  expect(calls).toBe(2);
  expect(outcomes).toEqual([{ kind: 'failed', reason: 'timeout' }]);
});

test('a retry that succeeds is reported ok', async () => {
  let calls = 0;
  const outcomes: JobOutcome[] = [];
  const queue = new ScanQueue([job(1)], { concurrency: 1, retries: 1 },
    async () => (++calls === 1 ? { kind: 'failed', reason: 'x' } : { kind: 'ok' }),
    async (_j, o) => { outcomes.push(o); });
  await queue.start();
  expect(outcomes.map((o) => o.kind)).toEqual(['ok']);
});

test('blocked frame stops queue', async () => {
  const started: string[] = [];
  const queue = new ScanQueue([1, 2, 3, 4].map(job), { concurrency: 1, retries: 1 }, async (j) => {
    started.push(j.url);
    return j.url.endsWith('/2') ? { kind: 'blocked', reason: 'x-frame-options' } : { kind: 'ok' };
  }, async () => {});
  await expect(queue.start()).rejects.toBeInstanceOf(BlockedFrameError);
  expect(started).toEqual(['https://a.se/1', 'https://a.se/2']);
});

test('cancel stops starting new jobs', async () => {
  const started: string[] = [];
  const queue = new ScanQueue([1, 2, 3, 4].map(job), { concurrency: 1, retries: 0 }, async (j) => {
    started.push(j.url);
    if (j.url.endsWith('/2')) queue.cancel();
    await tick();
    return { kind: 'ok' };
  }, async () => {});
  await queue.start();
  expect(started).toEqual(['https://a.se/1', 'https://a.se/2']);
  expect(queue.cancelled).toBe(true);
});

test('an error from settle is fatal and rejects start', async () => {
  const queue = new ScanQueue([1, 2, 3].map(job), { concurrency: 1, retries: 0 }, async () => ({ kind: 'ok' }), async () => {
    throw new Error('server down');
  });
  await expect(queue.start()).rejects.toThrow('server down');
});

test('progress reports done, total and current urls', async () => {
  const seen: string[] = [];
  const queue = new ScanQueue([1, 2].map(job), { concurrency: 1, retries: 0 }, async () => ({ kind: 'ok' }), async () => {});
  queue.onProgress((p) => seen.push(`${p.done}/${p.total}:${p.current.join(',')}`));
  await queue.start();
  expect(seen).toContain('0/2:https://a.se/1');
  expect(seen.at(-1)).toBe('2/2:');
});

test('after cancel, a running job that fails is neither retried nor recorded (resume does it again)', async () => {
  const runs: string[] = [];
  const settled: string[] = [];
  const queue = new ScanQueue([1, 2].map(job), { concurrency: 1, retries: 1 }, async (j) => {
    runs.push(j.url);
    queue.cancel(); // the editor left the dashboard while this page was loading
    await tick();
    return { kind: 'failed', reason: 'timeout' };
  }, async (j) => { settled.push(j.url); });
  await queue.start();
  expect(runs).toEqual(['https://a.se/1']);
  expect(settled).toEqual([]);
});
