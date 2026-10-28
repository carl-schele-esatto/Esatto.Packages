import { expect, test } from 'vitest';
import { otherOriginsMessage, partitionPages, jobsFor } from '../src/scan/partition';
import type { ScanPage } from '../src/types';

const page = (url: string, skipReason: string | null = null): ScanPage => ({
  contentKey: crypto.randomUUID(), name: 'P', culture: 'sv', url, skipReason,
});

test('same-origin pages are scannable', () => {
  const { scannable, skipped } = partitionPages([page('https://a.se/x')], 'https://a.se');
  expect(scannable).toHaveLength(1);
  expect(skipped).toHaveLength(0);
});

test('other-origin pages are skipped with their origin', () => {
  const { skipped } = partitionPages([page('https://b.se/x')], 'https://a.se');
  expect(skipped[0].skipReason).toBe('other-origin:https://b.se');
});

test('http vs https on the same host is another origin, and the reason shows the scheme', () => {
  const { skipped } = partitionPages([page('http://a.se/x')], 'https://a.se');
  expect(skipped[0].skipReason).toBe('other-origin:http://a.se');
});

test('the skipped-pages message names the scheme when only the scheme differs', () => {
  const message = otherOriginsMessage(['http://a.se'], 'https://a.se');
  expect(message).toContain('http://a.se');
  expect(message).toContain('https://a.se');
  expect(message).toMatch(/http instead of https/);
});

test('the skipped-pages message for another host says to open the backoffice there', () =>
  expect(otherOriginsMessage(['https://b.se'], 'https://a.se')).toContain('Open the backoffice on https://b.se'));

test('existing skip reason is kept', () => {
  const { skipped } = partitionPages([page('https://a.se/x', 'protected')], 'https://a.se');
  expect(skipped[0].skipReason).toBe('protected');
});

test('jobsFor makes one job per page per viewport', () => {
  const jobs = jobsFor([page('https://a.se/1'), page('https://a.se/2')], [1280, 320]);
  expect(jobs.map((j) => `${j.url}@${j.viewport}`)).toEqual([
    'https://a.se/1@1280', 'https://a.se/1@320', 'https://a.se/2@1280', 'https://a.se/2@320',
  ]);
});
