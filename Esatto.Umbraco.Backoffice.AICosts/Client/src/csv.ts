/** Excel splits CSV on the locale's list separator: ';' where the decimal separator is ','. */
export function csvSeparators(locale?: string): { field: ';' | ','; decimal: ',' | '.' } {
  const decimal = new Intl.NumberFormat(locale).formatToParts(1.5).find((p) => p.type === 'decimal')?.value;
  return decimal === ',' ? { field: ';', decimal: ',' } : { field: ',', decimal: '.' };
}

/** RFC 4180 CSV with a UTF-8 BOM (so Excel reads å/ä/ö), numbers in the locale's decimal style, no grouping. */
export function toCsv(header: string[], rows: (string | number)[][], locale?: string): string {
  const { field, decimal } = csvSeparators(locale);
  const cell = (v: string | number) => {
    const text = typeof v === 'number' ? String(v).replace('.', decimal) : v;
    return /["\r\n]/.test(text) || text.includes(field) ? `"${text.replace(/"/g, '""')}"` : text;
  };
  return '﻿' + [header, ...rows].map((r) => r.map(cell).join(field)).join('\r\n');
}

export function downloadCsv(fileName: string, text: string): void {
  const url = URL.createObjectURL(new Blob([text], { type: 'text/csv;charset=utf-8' }));
  const a = Object.assign(document.createElement('a'), { href: url, download: fileName });
  a.click();
  URL.revokeObjectURL(url);
}
