import { documentLinksIn } from '../links';
import type { Job } from '../types';
import { classifyPreflight, preflight } from './preflight';
import type { JobOutcome } from './queue';
import { emptyResult, reduceAxe, type AxeResultsLike } from './reduce';

export interface FrameOptions { axeUrl: string; tags: string[]; settleMs: number; timeoutMs: number }

const FRAME_HEIGHT = 900;

/** Adds `esatto-a11y-scan=1`, so sites can keep scans out of their analytics. */
export function scanUrl(url: string): string {
  const u = new URL(url);
  u.searchParams.set('esatto-a11y-scan', '1');
  return u.toString();
}

/**
 * Loads one page in a hidden, same-origin iframe at the job's width, injects axe-core and runs it.
 * The frame is always removed afterwards.
 */
export async function runInFrame(job: Job, host: HTMLElement, opts: FrameOptions): Promise<JobOutcome> {
  const frame = document.createElement('iframe');
  frame.setAttribute('aria-hidden', 'true');
  frame.tabIndex = -1;
  frame.title = 'Accessibility scan';
  frame.width = String(job.viewport);
  frame.height = String(FRAME_HEIGHT);
  // Off-screen but rendered at full size: layout-dependent rules (contrast, target size) need a real layout.
  frame.style.cssText = `position:absolute;left:-10000px;top:0;width:${job.viewport}px;height:${FRAME_HEIGHT}px;border:0;`;

  let timer: ReturnType<typeof setTimeout> | undefined;
  try {
    const timeout = new Promise<JobOutcome>((resolve) => {
      timer = setTimeout(() => resolve({ kind: 'failed', reason: 'timeout', result: emptyResult(job, 'failed', 'timeout') }), opts.timeoutMs);
    });
    return await Promise.race([scan(job, frame, host, opts), timeout]);
  } finally {
    clearTimeout(timer);
    frame.remove();
  }
}

async function scan(job: Job, frame: HTMLIFrameElement, host: HTMLElement, opts: FrameOptions): Promise<JobOutcome> {
  // Ask first: error pages, redirects to other sites and downloads fail this page only, and only real
  // framing headers stop the scan.
  let verdict: JobOutcome | null;
  try {
    verdict = classifyPreflight(await preflight(scanUrl(job.url)), location.origin);
  } catch (error) {
    verdict = { kind: 'failed', reason: error instanceof Error ? error.message : 'request failed' };
  }
  if (verdict) return verdict.kind === 'failed' ? { ...verdict, result: emptyResult(job, 'failed', verdict.reason ?? 'failed') } : verdict;

  const loaded = new Promise<void>((resolve) => frame.addEventListener('load', () => resolve(), { once: true }));
  frame.src = scanUrl(job.url);
  host.appendChild(frame);
  await loaded;
  await delay(opts.settleMs);

  let doc: Document | null = null;
  try {
    doc = frame.contentDocument;
  } catch {
    doc = null; // cross-origin or blocked
  }
  if (!doc || doc.location.href === 'about:blank' || !doc.documentElement) {
    // The pre-flight said the page can be framed, so this page navigated away (script redirect, download).
    return { kind: 'failed', reason: 'the page left the frame', result: emptyResult(job, 'failed', 'the page left the frame') };
  }

  const win = frame.contentWindow as (Window & { axe?: { run: (ctx: Document, options: unknown) => Promise<AxeResultsLike> } }) | null;
  if (!win) return { kind: 'failed', reason: 'the frame has no window' };

  if (!win.axe) {
    const script = doc.createElement('script');
    script.src = opts.axeUrl;
    const ready = new Promise<void>((resolve, reject) => {
      script.onload = () => resolve();
      script.onerror = () => reject(new Error('axe-core could not be loaded into the page (Content-Security-Policy script-src?)'));
    });
    doc.head.appendChild(script);
    try {
      await ready;
    } catch (error) {
      // Every page will refuse it the same way, so stop the scan and say why.
      return { kind: 'blocked', reason: error instanceof Error ? error.message : 'axe-core could not be loaded into the page.' };
    }
  }

  const results = await win.axe!.run(doc, {
    runOnly: { type: 'tag', values: opts.tags },
    // Node lists only for what we store; `passes` still lists every passing rule id and its tags.
    resultTypes: ['violations', 'incomplete'],
  });
  const links = documentLinksIn([...doc.querySelectorAll('a[href]')].map((a) => (a as HTMLAnchorElement).href));
  return { kind: 'ok', result: reduceAxe(results, job, links) };
}

const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));
