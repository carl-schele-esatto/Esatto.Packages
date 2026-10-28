import type { Job, PageResult, RuleResult } from '../types';

// The parts of axe-core's result shape we use. axe targets are strings, or arrays of strings for elements
// inside shadow DOM / iframes (outer → inner).
type AxeTarget = string | string[];
interface AxeNodeLike { target: AxeTarget[]; html: string }
interface AxeRuleLike { id: string; impact?: string | null; help: string; helpUrl: string; tags: string[]; nodes: AxeNodeLike[] }
export interface AxeResultsLike { violations: AxeRuleLike[]; incomplete: AxeRuleLike[]; passes: AxeRuleLike[] }

const selectorOf = (target: AxeTarget[]): string =>
  target.map((t) => (Array.isArray(t) ? t.join(' >>> ') : t)).join(' ');

const toRule = (rule: AxeRuleLike, kind: RuleResult['kind']): RuleResult => ({
  ruleId: rule.id,
  kind,
  impact: rule.impact ?? null,
  help: rule.help,
  helpUrl: rule.helpUrl,
  tags: rule.tags,
  nodes: rule.nodes.map((n) => ({ selector: selectorOf(n.target), html: n.html })),
});

/** axe's results for one job, reduced to what the server stores. */
export function reduceAxe(results: AxeResultsLike, job: Job, documentLinks: string[]): PageResult {
  return {
    contentKey: job.contentKey,
    name: job.name,
    culture: job.culture,
    url: job.url,
    viewport: job.viewport,
    status: 'ok',
    reason: null,
    passes: results.passes.map((p) => ({ ruleId: p.id, tags: p.tags })),
    rules: [
      ...results.violations.map((r) => toRule(r, 'violation')),
      ...results.incomplete.map((r) => toRule(r, 'incomplete')),
    ],
    documentLinks,
  };
}

/** A page result for a job that was skipped or failed. */
export function emptyResult(job: Pick<Job, 'contentKey' | 'name' | 'culture' | 'url' | 'viewport'>, status: 'skipped' | 'failed', reason: string): PageResult {
  return { ...job, status, reason, passes: [], rules: [], documentLinks: [] };
}
