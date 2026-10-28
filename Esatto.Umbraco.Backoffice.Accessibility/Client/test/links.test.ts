import { expect, test } from 'vitest';
import { editContentUrl, editMediaUrl, documentLinksIn, safeHref } from '../src/links';

test('content edit url includes culture when variant', () =>
  expect(editContentUrl('abc', 'sv')).toBe('/umbraco/section/content/workspace/document/edit/abc/sv'));

test('content edit url without culture for invariant content', () =>
  expect(editContentUrl('abc', '')).toBe('/umbraco/section/content/workspace/document/edit/abc'));

test('media edit url', () => expect(editMediaUrl('m1')).toBe('/umbraco/section/media/workspace/media/edit/m1'));

test('documentLinksIn finds PDF and Office links, absolute and distinct', () => {
  const hrefs = ['https://a.se/media/1/r.pdf', 'https://a.se/media/1/r.pdf', 'https://a.se/x.DOCX?v=1', 'https://a.se/page', 'https://a.se/m.xlsx#s'];
  expect(documentLinksIn(hrefs)).toEqual(['https://a.se/media/1/r.pdf', 'https://a.se/x.DOCX?v=1', 'https://a.se/m.xlsx#s']);
});

test('safeHref only lets http(s) through', () => {
  expect(safeHref('https://a.se/x')).toBe('https://a.se/x');
  expect(safeHref('http://a.se/x')).toBe('http://a.se/x');
  expect(safeHref('javascript:alert(1)')).toBeUndefined();
  expect(safeHref('data:text/html,x')).toBeUndefined();
  expect(safeHref('')).toBeUndefined();
});
