// The notes under a chart (and under the search block): the annotations, which are facts the processing layer established about the data (a gap, a
// tracking outage, a finding), and the insights, which interpret what the curve does. Pure, so what a note says and which icon and label it has are
// tested without a browser.
//
// A kind is never told by colour alone: each has an icon AND a word ("Warning", "Note", "Insight") that is shown, not only read out.

import { refText, text, type Localizer } from './localize';
import type { Annotation, TextRef } from './types';

export type NoteSeverity = 'warning' | 'info' | 'insight';

export interface NoteView {
  kind: 'annotation' | 'insight';
  severity: NoteSeverity;
  icon: string;
  /** The kind in words, shown beside the icon. */
  label: string;
  /** An annotation's title; an insight has none. */
  title: string;
  text: string;
}

/** Report.cs knows two severities, warning and info; anything else a future report brings is read as info. */
export function severityOf(raw: string | null | undefined): 'warning' | 'info' {
  return raw === 'warning' ? 'warning' : 'info';
}

const ICONS: Record<NoteSeverity, string> = {
  warning: 'icon-alert',
  info: 'icon-info',
  insight: 'icon-lightbulb',
};

const LABEL_KEYS: Record<NoteSeverity, string> = {
  warning: 'uiNoteWarningLabel',
  info: 'uiNoteInfoLabel',
  insight: 'uiNoteInsightLabel',
};

/**
 * Whether the warnings are shown: `include` all, `exclude` them or `only` them. The warnings of the search block are the findings of the "To fix"
 * list, so its tab leaves them out (a finding that points at a page belongs where the page is). A chart's own warnings (no key events, an anomalous
 * baseline) stay with the chart. The server strips the findings for a user outside the groups that may see them: whatever arrives is shown.
 */
export type WarningFilter = 'include' | 'exclude' | 'only';

/** The notes of a chart or of the search block: annotations first (what was established), then insights (what it means). */
export function noteViews(
  host: Localizer,
  annotations: readonly Annotation[] | null | undefined,
  insights: readonly TextRef[] | null | undefined,
  warnings: WarningFilter = 'include',
): NoteView[] {
  const views: NoteView[] = [];

  for (const annotation of annotations ?? []) {
    const severity = severityOf(annotation.severity);
    if ((warnings === 'exclude' && severity === 'warning') || (warnings === 'only' && severity !== 'warning')) continue;

    const title = refText(host, 'annotation', annotation.text, 'Title');
    const body = refText(host, 'annotation', annotation.text, 'Text');
    if (!title && !body) continue;
    views.push({ kind: 'annotation', severity, icon: ICONS[severity], label: text(host, LABEL_KEYS[severity]), title, text: body });
  }

  // An insight is not a warning, so a list of "only warnings" has none.
  if (warnings !== 'only') {
    for (const insight of insights ?? []) {
      const body = refText(host, 'insight', insight);
      if (!body) continue;
      views.push({ kind: 'insight', severity: 'insight', icon: ICONS.insight, label: text(host, LABEL_KEYS.insight), title: '', text: body });
    }
  }

  return views;
}
