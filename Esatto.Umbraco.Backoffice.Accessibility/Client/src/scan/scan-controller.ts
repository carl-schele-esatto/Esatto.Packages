import type { DonePage, IssuePage, Job, PageResult, ScanPage, ScanSummary } from '../types';
import { jobsFor, OTHER_ORIGIN, partitionPages } from './partition';
import { BlockedFrameError, ScanQueue, type JobOutcome, type Progress } from './queue';
import { emptyResult } from './reduce';

export const DESKTOP = 1280;
export const MOBILE = 320;
const CONCURRENCY = 3;
const RETRIES = 1;
const POST_BACKOFF_MS = [1000, 2000, 4000];

/** What a scan needs from the outside world; the dashboard wires these to api.ts and runInFrame. */
export interface ScanDeps {
  origin: string;
  getPages(siteKey: string): Promise<ScanPage[]>;
  startScan(siteKey: string, mobile: boolean, pagesTotal: number): Promise<number>;
  getDone(scanId: number): Promise<DonePage[]>;
  postPage(scanId: number, result: PageResult): Promise<void>;
  finishScan(scanId: number, complete: boolean): Promise<ScanSummary>;
  runJob(job: Job): Promise<JobOutcome>;
  sleep?(ms: number): Promise<void>;
}

export interface ScanRunResult {
  scanId: number;
  /** Set when the scan was finished (complete, cancelled or blocked). */
  summary?: ScanSummary;
  /** Set when a page refused to be framed; the scan was finished as incomplete. */
  blocked?: BlockedFrameError;
  /** Set when results couldn't be saved; the scan is left running so it can be resumed. */
  paused?: Error;
  /** Origins whose pages were skipped because they're not the backoffice's origin. */
  otherOrigins: string[];
  /** More than half the scanned pages failed; the scan was finished as incomplete so it can't replace a good report. */
  mostlyFailed?: boolean;
}

/** Options for a new scan (widths from the setting) or a resumed one (widths it started with). */
export function scanOptions(mobileSetting: boolean, resume?: ScanSummary): { mobile: boolean; resumeScanId?: number } {
  return resume ? { mobile: resume.mobile, resumeScanId: resume.id } : { mobile: mobileSetting };
}

/** "Re-check this page": one job per width the scan loaded the page at, so no width keeps stale findings. */
export function recheckJobs(page: IssuePage): Job[] {
  return jobsFor([page], page.viewports.length ? page.viewports : [DESKTOP]);
}

/** Runs one scan of a site: list pages, skip what can't be scanned, scan the rest, save as it goes. */
export class ScanController {
  #queue: ScanQueue | null = null;
  #cancelled = false;

  constructor(private readonly deps: ScanDeps) {}

  cancel(): void {
    this.#cancelled = true;
    this.#queue?.cancel();
  }

  async start(siteKey: string, opts: { mobile: boolean; resumeScanId?: number }, onProgress?: (p: Progress) => void): Promise<ScanRunResult> {
    const pages = await this.deps.getPages(siteKey);
    const { scannable, skipped } = partitionPages(pages, this.deps.origin);
    const otherOrigins = [...new Set(skipped.filter((p) => p.skipReason.startsWith(OTHER_ORIGIN)).map((p) => p.skipReason.slice(OTHER_ORIGIN.length)))];
    const viewports = opts.mobile ? [DESKTOP, MOBILE] : [DESKTOP];

    let jobs = jobsFor(scannable, viewports);
    let skippedJobs = skipped.map((p) => ({ job: jobsFor([p], [DESKTOP])[0], reason: p.skipReason }));

    // A resume shows progress against the whole scan ("page 25 of 130"), not just what's left ("1 of 106").
    const total = jobs.length + skippedJobs.length;
    const scanId = opts.resumeScanId ?? await this.deps.startScan(siteKey, opts.mobile, total);
    if (opts.resumeScanId !== undefined) {
      const done = new Set((await this.deps.getDone(scanId)).map(key));
      jobs = jobs.filter((j) => !done.has(key(j)));
      skippedJobs = skippedJobs.filter((s) => !done.has(key(s.job)));
    }

    const alreadyDone = total - jobs.length - skippedJobs.length;
    let skippedDone = 0;
    let failed = 0;
    const report = (p: Progress) => onProgress?.({ ...p, done: p.done + skippedDone + alreadyDone, total });

    try {
      for (const s of skippedJobs) {
        await this.#post(scanId, emptyResult(s.job, 'skipped', s.reason));
        skippedDone++;
      }
      report({ done: 0, total, current: [] });

      this.#queue = new ScanQueue(jobs, { concurrency: CONCURRENCY, retries: RETRIES }, (job) => this.deps.runJob(job),
        (job, outcome) => {
          if (outcome.kind !== 'ok') failed++;
          return this.#post(scanId, outcome.kind === 'ok' && outcome.result ? outcome.result : emptyResult(job, 'failed', outcome.reason ?? 'failed'));
        });
      this.#queue.onProgress(report);
      if (this.#cancelled) this.#queue.cancel();
      await this.#queue.start();
    } catch (error) {
      if (error instanceof BlockedFrameError) {
        return { scanId, blocked: error, summary: await this.deps.finishScan(scanId, false), otherOrigins };
      }
      return { scanId, paused: error instanceof Error ? error : new Error(String(error)), otherOrigins };
    }

    // A scan where most pages failed (the tab lost its frames, the site was down, axe couldn't load…)
    // must not become the "latest complete scan": that would replace a real report with an empty one.
    const mostlyFailed = jobs.length > 0 && failed * 2 > jobs.length;
    const complete = !this.#cancelled && !this.#queue.cancelled && !mostlyFailed;
    return { scanId, summary: await this.deps.finishScan(scanId, complete), otherOrigins, mostlyFailed };
  }

  /** Saves one page result, retrying with back-off; throws after the last retry. */
  async #post(scanId: number, result: PageResult): Promise<void> {
    const sleep = this.deps.sleep ?? ((ms: number) => new Promise((r) => setTimeout(r, ms)));
    for (let attempt = 0; ; attempt++) {
      try {
        await this.deps.postPage(scanId, result);
        return;
      } catch (error) {
        if (attempt >= POST_BACKOFF_MS.length) throw error;
        await sleep(POST_BACKOFF_MS[attempt]);
      }
    }
  }
}

const key = (p: { contentKey: string; culture: string; viewport: number }) => `${p.contentKey}|${p.culture}|${p.viewport}`;
