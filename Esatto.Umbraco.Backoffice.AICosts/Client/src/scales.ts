// Heatmap levels and chart axes.

/** Upper bounds of the first three quartiles of the non-zero values. */
export function quartiles(values: number[]): number[] {
  if (!values.length) return [];
  const sorted = [...values].sort((a, b) => a - b);
  return [0.25, 0.5, 0.75].map((q) => sorted[Math.min(sorted.length - 1, Math.floor(q * sorted.length))]);
}

/** Heatmap level 0–3 for a non-zero value. */
export function binOf(value: number, thresholds: number[]): number {
  for (let i = 0; i < thresholds.length; i++) if (value <= thresholds[i]) return i;
  return 3;
}

/** 0 plus 3–5 round ticks covering max (in display currency). */
export function niceTicks(max: number): number[] {
  if (max <= 0) return [0, 1];
  const rough = max / 4;
  const pow = 10 ** Math.floor(Math.log10(rough));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * pow).find((s) => s >= rough) ?? 10 * pow;
  const ticks: number[] = [];
  for (let t = 0; t <= max + step * 0.999; t += step) ticks.push(Number(t.toFixed(10)));
  return ticks;
}

/** Column with a 4px rounded top, square at the baseline. */
export function columnPath(x: number, baseline: number, w: number, h: number): string {
  const r = Math.min(4, w / 2, h);
  const top = baseline - h;
  return `M${x},${baseline} V${top + r} Q${x},${top} ${x + r},${top} H${x + w - r} Q${x + w},${top} ${x + w},${top + r} V${baseline} Z`;
}
