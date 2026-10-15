export type Change = { kind: 'none' } | { kind: 'new' } | { kind: 'change'; percent: number };

/** Change vs the previous period; "new" when there was nothing before. Rounded to whole percent. */
export function change(current: number, previous: number | undefined | null): Change {
  if (previous === null || previous === undefined) return { kind: 'none' };
  if (previous === 0) return current === 0 ? { kind: 'change', percent: 0 } : { kind: 'new' };
  return { kind: 'change', percent: Math.round(((current - previous) / previous) * 100) };
}
