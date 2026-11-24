// What the Analysis tab shows, decided from what the server said. Pure functions, so every "is this shown?" rule is tested without a browser.
//
// `hasAnalysis` IS READ FIRST. GET analysis answers `{ "hasAnalysis": false }` when the archive holds nothing to analyze, and nothing else is in
// that body; only a `true` is an analysis (a body without the member is not read as one).
//
// THE TEXTS ARE KEYS. The server sends no prose: a statement is a reference in area `narrative`, an opportunity one reference in area `opportunity`
// whose key is its kind (both its title and its text are looked up from it, with the same arguments), and a target query's advice one in area
// `target` (see resolveTextRef and the doc comments of Narrative, Opportunities and TargetAnalysis).
//
// WHAT IS HIDDEN, AND WHEN. The quick wins are `null` for a user outside the findings groups and `[]` when none were found: either way their card is
// not drawn (no empty card, no "hidden" message). An empty list of target queries hides the table and the history of the terms.

import type { ApiError } from './api-error';
import { refText, text, type Localizer } from './localize';
import { moversView, type MovementView } from './movement-view';
import { historyView, targetsView, type HistoryView, type TargetsView } from './target-view';
import type { AnalysisResponse, NoAnalysisResponse, Opportunity, Statement } from './types';

/** Whether the answer is an analysis. Read first: only `hasAnalysis: true` is. */
export function isAnalysis(response: AnalysisResponse | NoAnalysisResponse | null | undefined): response is AnalysisResponse {
  return response?.hasAnalysis === true;
}

/**
 * - idle      nothing asked for yet (no month)
 * - loading   the analysis of the month is being read and nothing is held to show meanwhile
 * - failed    the read failed and nothing is held: the problem, with Try again
 * - none      the archive has nothing to analyze: a short text, no cards
 * - ready     an analysis. Also while it is read again (the target queries changed) or when that re-read failed: the cards, and the editors in
 *             them, stay where they are, so that the keyboard's place and the "Saved" announcement are not lost
 */
export type AnalysisState = 'idle' | 'loading' | 'failed' | 'none' | 'ready';

export function analysisState(held: {
  analysis: AnalysisResponse | NoAnalysisResponse | null;
  loading: boolean;
  problem: ApiError | null;
}): AnalysisState {
  if (held.analysis) return isAnalysis(held.analysis) ? 'ready' : 'none';
  if (held.problem) return 'failed';
  return held.loading ? 'loading' : 'idle';
}

// ---- The month in words -----------------------------------------------------------------------------------------------------------

export interface StatementView {
  /** The statement's identity (sources, visibility, nonBrand, conversion, movement, season). */
  key: string;
  text: string;
}

/** The statements as sentences, in the order the server sent them. One whose key is malformed is left out: a bad value must not stop the rest. */
export function statementsView(host: Localizer, statements: readonly Statement[] | null | undefined): StatementView[] {
  return (statements ?? [])
    .map((statement) => ({ key: statement.text?.key ?? '', text: refText(host, 'narrative', statement.text) }))
    .filter((item) => item.text !== '');
}

// ---- The quick wins -----------------------------------------------------------------------------------------------------------------

export interface OpportunityView {
  kind: string;
  title: string;
  text: string;
  /** How big the fix is, in words (null when the server sent no number). */
  effort: { level: 'small' | 'medium' | 'large'; label: string } | null;
}

// Opportunity.Effort is an ordinal: lower is cheaper, and it sets the order of the list (near: 2, cannibal: 3). The words are bands of it.
const EFFORT: readonly { upTo: number; level: 'small' | 'medium' | 'large'; key: string }[] = [
  { upTo: 2, level: 'small', key: 'uiEffortSmallLabel' },
  { upTo: 3, level: 'medium', key: 'uiEffortMediumLabel' },
  { upTo: Number.POSITIVE_INFINITY, level: 'large', key: 'uiEffortLargeLabel' },
];

function effortOf(host: Localizer, effort: number | null | undefined): OpportunityView['effort'] {
  if (typeof effort !== 'number' || !Number.isFinite(effort)) return null;

  const band = EFFORT.find((item) => effort <= item.upTo)!;
  return { level: band.level, label: text(host, band.key) };
}

/** The quick wins in the server's order (cheapest first). Empty for none, and for `null`, which is how the server withholds them. */
export function opportunitiesView(host: Localizer, opportunities: readonly Opportunity[] | null | undefined): OpportunityView[] {
  return (opportunities ?? [])
    .map((item) => ({
      kind: item.kind,
      title: refText(host, 'opportunity', item.text, 'Title'),
      text: refText(host, 'opportunity', item.text, 'Text'),
      effort: effortOf(host, item.effort),
    }))
    .filter((item) => item.title !== '' || item.text !== '');
}

// ---- The whole tab ------------------------------------------------------------------------------------------------------------------

export interface AnalysisView {
  statements: StatementView[];
  targets: TargetsView;
  /** The positions of the target queries month by month; null without terms or months. */
  history: HistoryView | null;
  movement: MovementView;
  /** Empty hides the card. */
  opportunities: OpportunityView[];
}

export function analysisView(host: Localizer, response: AnalysisResponse): AnalysisView {
  return {
    statements: statementsView(host, response.summary),
    targets: targetsView(host, response.targets),
    history: historyView(host, response.targetHistory),
    movement: moversView(host, response.movement),
    opportunities: opportunitiesView(host, response.opportunities),
  };
}
