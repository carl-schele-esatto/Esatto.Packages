import type { Job, PageResult } from '../types';

export interface JobOutcome {
  kind: 'ok' | 'failed' | 'blocked';
  result?: PageResult;
  reason?: string;
}

export interface Progress { done: number; total: number; current: string[] }

/** A page refused to be framed (X-Frame-Options / CSP): scanning can't work until the site allows it. */
export class BlockedFrameError extends Error {
  constructor(public readonly url: string, public readonly reason?: string) {
    super(`The page ${url} can't be loaded in a frame${reason ? ` (${reason})` : ''}.`);
    this.name = 'BlockedFrameError';
  }
}

/**
 * Runs scan jobs a few at a time. `run` scans one job and is retried on a `failed` outcome; `settle`
 * receives each job's final outcome (it posts it to the server) and any error it throws stops the whole
 * queue. A `blocked` outcome stops the queue too, because every other page will be blocked as well.
 */
export class ScanQueue {
  #pending: Job[];
  #inFlight = new Set<Job>();
  #done = 0;
  #total: number;
  #stop: Error | null = null;
  #cancelled = false;
  #listeners: ((p: Progress) => void)[] = [];

  constructor(
    jobs: Job[],
    private readonly opts: { concurrency: number; retries: number },
    private readonly run: (job: Job) => Promise<JobOutcome>,
    private readonly settle: (job: Job, outcome: JobOutcome) => Promise<void>,
  ) {
    this.#pending = [...jobs];
    this.#total = jobs.length;
  }

  get cancelled(): boolean {
    return this.#cancelled;
  }

  onProgress(cb: (p: Progress) => void): void {
    this.#listeners.push(cb);
  }

  /**
   * No new jobs start. Jobs already running finish; a successful one is settled, a failed one is dropped
   * (not retried, not recorded): after a cancel its frame usually can't load, and a resume will scan it.
   */
  cancel(): void {
    this.#cancelled = true;
  }

  async start(): Promise<void> {
    const workers = Array.from({ length: Math.max(1, this.opts.concurrency) }, () => this.#worker());
    await Promise.all(workers);
    this.#emit();
    if (this.#stop) throw this.#stop;
  }

  async #worker(): Promise<void> {
    while (!this.#cancelled && !this.#stop) {
      const job = this.#pending.shift();
      if (!job) return;
      this.#inFlight.add(job);
      this.#emit();
      try {
        const outcome = await this.#attempt(job);
        if (outcome.kind === 'blocked') {
          this.#stop ??= new BlockedFrameError(job.url, outcome.reason);
          return;
        }
        if (this.#cancelled && outcome.kind === 'failed') return;
        await this.settle(job, outcome);
        this.#done++;
      } catch (error) {
        this.#stop ??= error instanceof Error ? error : new Error(String(error));
        return;
      } finally {
        this.#inFlight.delete(job);
      }
    }
  }

  async #attempt(job: Job): Promise<JobOutcome> {
    let outcome: JobOutcome = { kind: 'failed', reason: 'not run' };
    for (let attempt = 0; attempt <= this.opts.retries; attempt++) {
      try {
        outcome = await this.run(job);
      } catch (error) {
        outcome = { kind: 'failed', reason: error instanceof Error ? error.message : String(error) };
      }
      if (outcome.kind !== 'failed' || this.#cancelled) return outcome;
    }
    return outcome;
  }

  #emit(): void {
    const progress = { done: this.#done, total: this.#total, current: [...this.#inFlight].map((j) => j.url) };
    for (const cb of this.#listeners) cb(progress);
  }
}
