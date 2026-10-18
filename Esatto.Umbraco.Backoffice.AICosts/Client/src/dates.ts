/** "2026-10-12" → Date at UTC midnight (DateOnly from the API). */
export function parseDate(value: string): Date {
  const [y, m, d] = value.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d));
}
