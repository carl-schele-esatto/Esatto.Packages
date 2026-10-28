import { describe, expect, test, vi } from 'vitest';
import { recheckJobs, scanOptions, ScanController, type ScanDeps } from '../src/scan/scan-controller';
import type { JobOutcome } from '../src/scan/queue';
import type { DonePage, Job, PageResult, ScanPage, ScanSummary } from '../src/types';

const ORIGIN = 'https://a.se';
const page = (n: number, skipReason: string | null = null, host = 'a.se'): ScanPage => ({
  contentKey: `k${n}`, name: `P${n}`, culture: '', url: `https://${host}/${n}`, skipReason,
});
const summary = (complete: boolean): ScanSummary => ({
  id: 7, startedUtc: '', finishedUtc: '', status: complete ? 'complete' : 'incomplete', target: '2.2 · AA', score: null,
  pagesTotal: 0, pagesDone: 0, pagesSkipped: 0, pagesFailed: 0, userName: 'A', mobile: false,
});

function deps(pages: ScanPage[], overrides: Partial<ScanDeps> = {}) {
  const posted: PageResult[] = [];
  const ran: Job[] = [];
  const d: ScanDeps = {
    origin: ORIGIN,
    getPages: async () => pages,
    startScan: async () => 7,
    getDone: async () => [] as DonePage[],
    postPage: async (_id, r) => { posted.push(r); },
    finishScan: vi.fn(async (_id: number, complete: boolean) => summary(complete)),
    runJob: async (job): Promise<JobOutcome> => {
      ran.push(job);
      return { kind: 'ok', result: { ...job, status: 'ok', reason: null, passes: [], rules: [], documentLinks: [] } };
    },
    sleep: async () => {},
    ...overrides,
  };
  return { d, posted, ran };
}

describe('ScanController', () => {
  test('scans every page and finishes complete', async () => {
    const { d, posted } = deps([page(1), page(2)]);
    const result = await new ScanController(d).start('site', { mobile: false });
    expect(posted.map((p) => p.url)).toEqual(['https://a.se/1', 'https://a.se/2']);
    expect(d.finishScan).toHaveBeenCalledWith(7, true);
    expect(result.summary?.status).toBe('complete');
  });

  test('mobile doubles jobs with 320 viewport', async () => {
    const { d, ran } = deps([page(1)]);
    await new ScanController(d).start('site', { mobile: true });
    expect(ran.map((j) => j.viewport).sort()).toEqual([1280, 320]);
  });

  test('skipped pages are posted as skipped and other hosts are reported', async () => {
    const { d, posted, ran } = deps([page(1, 'protected'), page(2, null, 'b.se'), page(3)]);
    const result = await new ScanController(d).start('site', { mobile: false });
    expect(posted.filter((p) => p.status === 'skipped').map((p) => p.reason)).toEqual(['protected', 'other-origin:https://b.se']);
    expect(ran.map((j) => j.url)).toEqual(['https://a.se/3']);
    expect(result.otherOrigins).toEqual(['https://b.se']);
  });

  test('resume skips done jobs', async () => {
    const startScan = vi.fn(async () => 99);
    const { d, ran } = deps([page(1), page(2)], {
      startScan,
      getDone: async () => [{ contentKey: 'k1', culture: '', viewport: 1280 }],
    });
    await new ScanController(d).start('site', { mobile: false, resumeScanId: 7 });
    expect(startScan).not.toHaveBeenCalled();
    expect(ran.map((j) => j.url)).toEqual(['https://a.se/2']);
  });

  test('a resumed scan counts progress against the whole scan, not just what is left', async () => {
    const seen: { done: number; total: number }[] = [];
    const { d } = deps([page(1), page(2), page(3)], {
      getDone: async () => [{ contentKey: 'k1', culture: '', viewport: 1280 }],
    });
    await new ScanController(d).start('site', { mobile: false, resumeScanId: 7 }, (p) => seen.push({ done: p.done, total: p.total }));
    expect(seen[0]).toEqual({ done: 1, total: 3 });
    expect(seen.at(-1)).toEqual({ done: 3, total: 3 });
  });

  test('cancel finishes as incomplete', async () => {
    let controller!: ScanController;
    const { d } = deps([page(1), page(2), page(3), page(4)], {
      runJob: async (job) => {
        controller.cancel();
        return { kind: 'ok', result: { ...job, status: 'ok', reason: null, passes: [], rules: [], documentLinks: [] } };
      },
    });
    controller = new ScanController(d);
    await controller.start('site', { mobile: false });
    expect(d.finishScan).toHaveBeenCalledWith(7, false);
  });

  test('blocked frame finishes incomplete and reports it', async () => {
    const { d } = deps([page(1)], { runJob: async () => ({ kind: 'blocked', reason: 'x-frame-options' }) });
    const result = await new ScanController(d).start('site', { mobile: false });
    expect(result.blocked?.url).toBe('https://a.se/1');
    expect(d.finishScan).toHaveBeenCalledWith(7, false);
  });

  test('post failure pauses after three retries', async () => {
    const postPage = vi.fn(async () => { throw new Error('503'); });
    const { d } = deps([page(1)], { postPage });
    const result = await new ScanController(d).start('site', { mobile: false });
    expect(postPage).toHaveBeenCalledTimes(4);
    expect(result.paused?.message).toBe('503');
    expect(result.scanId).toBe(7);
    expect(d.finishScan).not.toHaveBeenCalled();
  });

  test('progress counts every job including skipped ones', async () => {
    const { d } = deps([page(1, 'protected'), page(2)]);
    const seen: string[] = [];
    await new ScanController(d).start('site', { mobile: false }, (p) => seen.push(`${p.done}/${p.total}`));
    expect(seen.at(-1)).toBe('2/2');
  });
});

describe('final review: a scan of failed pages is not complete', () => {
  test('all jobs failing finishes incomplete', async () => {
    const { d } = deps([page(1), page(2)], { runJob: async () => ({ kind: 'failed', reason: 'timeout' }) });
    const result = await new ScanController(d).start('site', { mobile: false });
    expect(d.finishScan).toHaveBeenCalledWith(7, false);
    expect(result.mostlyFailed).toBe(true);
  });

  test('a few failures still finish complete', async () => {
    let n = 0;
    const { d } = deps([page(1), page(2), page(3), page(4)], {
      runJob: async (job) => (++n === 1 ? { kind: 'failed', reason: 'timeout' } : { kind: 'ok', result: { ...job, status: 'ok', reason: null, passes: [], rules: [], documentLinks: [] } }),
    });
    await new ScanController(d).start('site', { mobile: false });
    expect(d.finishScan).toHaveBeenCalledWith(7, true);
  });
});

describe('options', () => {
  test('a resumed scan keeps the widths it started with, whatever the setting is now', () => {
    expect(scanOptions(false, { ...summary(false), id: 9, mobile: true })).toEqual({ mobile: true, resumeScanId: 9 });
    expect(scanOptions(true)).toEqual({ mobile: true });
  });

  test('re-check scans every width the page was scanned at', () => {
    const p = { contentKey: 'k', name: 'P', culture: '', url: 'https://a.se/1', nodes: [] };
    expect(recheckJobs({ ...p, viewports: [1280, 320] }).map((j) => j.viewport)).toEqual([1280, 320]);
    expect(recheckJobs({ ...p, viewports: [] }).map((j) => j.viewport)).toEqual([1280]);
  });
});
