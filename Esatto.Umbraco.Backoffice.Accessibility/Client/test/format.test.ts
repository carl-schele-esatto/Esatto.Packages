import { expect, test } from 'vitest';
import { band, notVerifiedParts, parseUtc, scanStatusLabel, targetLabel } from '../src/format';

test('score bands', () => {
  expect([band(null), band(0), band(49), band(50), band(89), band(90), band(100)])
    .toEqual([null, 'critical', 'critical', 'warning', 'warning', 'good', 'good']);
});

test('server dates without offset are read as UTC', () => {
  expect(parseUtc('2026-10-21T10:00:00')!.toISOString()).toBe('2026-10-21T10:00:00.000Z');
  expect(parseUtc('2026-10-21T10:00:00Z')!.toISOString()).toBe('2026-10-21T10:00:00.000Z');
});

test('target label', () => expect(targetLabel({ version: 'V21', level: 'AA' })).toBe('WCAG 2.1 AA'));

test('a scan still marked running is shown as not finished (it may have been interrupted)', () => {
  expect(scanStatusLabel('running')).toBe('not finished');
  expect(scanStatusLabel('incomplete')).toBe('incomplete');
  expect(scanStatusLabel('complete')).toBe('complete');
});

test('not verified splits into what the scanner checked (confirm it) and what only a person can check', () => {
  expect(notVerifiedParts({ notVerified: 51, scannerChecked: 18 })).toEqual({ toConfirm: 18, personOnly: 33 });
  expect(notVerifiedParts({ notVerified: 0, scannerChecked: 0 })).toEqual({ toConfirm: 0, personOnly: 0 });
});
