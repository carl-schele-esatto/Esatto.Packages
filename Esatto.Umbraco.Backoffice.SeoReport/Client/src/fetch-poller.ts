// Follows a running backfill by asking GET fetch/progress until it says the run is over.
//
// Right after POST fetch answers 202 the run is "starting" (running, with no month counted yet); the months are counted a moment later, so
// the dashboard cannot read the answer of the POST as the end state and has to ask again. This does the asking:
//
//   - one read per interval while the run is going, the next one only AFTER the last was answered, so reads never pile up on a slow server;
//   - a failed read is not the end of the run: it is retried after a longer wait (double each time, up to a longest wait), and a read that
//     succeeds goes back to the normal interval. Only a run of failures in a row gives up, and says so;
//   - stop() cancels the timer AND disowns a read that is already on its way, so an element that was removed from the page is never
//     called back. dispose() is the same and FINAL: after it start() does nothing, so a request that answers after the element has gone
//     (a status read, the answer to Fetch now) cannot bring the poller back. A shell that is connected again makes a new poller.
//
// It does not know about the screen: it reports through the callbacks it is given.

import type { FetchStatus } from './types';

export interface FetchPollerOptions {
  /** Reads the progress (GET fetch/progress). */
  load: () => Promise<FetchStatus>;
  /** A read that says the run is still going. */
  onProgress: (status: FetchStatus) => void;
  /** Called once, with the read that says the run is over (`running` is false). The poller has stopped by then. */
  onFinished: (status: FetchStatus) => void;
  /** A read that failed; <c>failures</c> is how many in a row. The poller keeps trying, more slowly. */
  onError?: (error: unknown, failures: number) => void;
  /** Called once when <c>maxFailures</c> reads failed in a row. The poller has stopped by then. */
  onGiveUp?: (error: unknown) => void;
  /** The wait between reads. Default 2 seconds. */
  intervalMs?: number;
  /** The longest wait after failures. Default 30 seconds. */
  maxIntervalMs?: number;
  /** How many failed reads in a row end it. Default 8. */
  maxFailures?: number;
}

export class FetchPoller {
  readonly #options: FetchPollerOptions;
  readonly #interval: number;
  readonly #longest: number;
  readonly #maxFailures: number;

  #timer: ReturnType<typeof setTimeout> | undefined;
  // Bumped by every stop and every end, so a read that was started under an earlier one is ignored when it answers.
  #generation = 0;
  #active = false;
  #disposed = false;
  #failures = 0;

  constructor(options: FetchPollerOptions) {
    this.#options = options;
    this.#interval = options.intervalMs ?? 2000;
    this.#longest = options.maxIntervalMs ?? 30_000;
    this.#maxFailures = options.maxFailures ?? 8;
  }

  /** Whether it is waiting for, or reading, the next progress. */
  get active(): boolean {
    return this.#active;
  }

  /** Whether it has been disposed: it will not start again. */
  get disposed(): boolean {
    return this.#disposed;
  }

  /** Starts following the run: the first read is one interval from now. Does nothing while it is already going, or once it is disposed. */
  start(): void {
    if (this.#active || this.#disposed) return;

    this.#active = true;
    this.#failures = 0;
    this.#schedule(this.#generation, this.#interval);
  }

  /** Stops following, cancels the timer and ignores a read that is on its way. It can be started again. */
  stop(): void {
    this.#end();
  }

  /** Stops for good, as stop() does, and makes every later start() do nothing. For an element that has left the page. */
  dispose(): void {
    this.#disposed = true;
    this.#end();
  }

  #end(): void {
    this.#active = false;
    this.#generation++;
    clearTimeout(this.#timer);
    this.#timer = undefined;
  }

  #schedule(generation: number, delay: number): void {
    this.#timer = setTimeout(() => void this.#read(generation), delay);
  }

  async #read(generation: number): Promise<void> {
    this.#timer = undefined;

    let status: FetchStatus;
    try {
      status = await this.#options.load();
    } catch (error) {
      if (generation !== this.#generation) return;

      this.#failures++;
      this.#options.onError?.(error, this.#failures);

      if (this.#failures >= this.#maxFailures) {
        this.#end();
        this.#options.onGiveUp?.(error);
        return;
      }

      this.#schedule(generation, Math.min(this.#interval * 2 ** this.#failures, this.#longest));
      return;
    }

    if (generation !== this.#generation) return;
    this.#failures = 0;

    if (!status.running) {
      this.#end();
      this.#options.onFinished(status);
      return;
    }

    this.#options.onProgress(status);
    this.#schedule(generation, this.#interval);
  }
}
