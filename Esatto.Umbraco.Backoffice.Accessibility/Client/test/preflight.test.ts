import { expect, test } from 'vitest';
import { classifyPreflight } from '../src/scan/preflight';

const ORIGIN = 'https://a.se';
const ok = { url: 'https://a.se/x', status: 200, contentType: 'text/html; charset=utf-8', xFrameOptions: null, csp: null };

test('a normal HTML page can be framed', () => expect(classifyPreflight(ok, ORIGIN)).toBeNull());

test('HTTP errors fail that page only', () =>
  expect(classifyPreflight({ ...ok, status: 404 }, ORIGIN)).toEqual({ kind: 'failed', reason: 'HTTP 404' }));

test('a redirect to another site fails that page only', () =>
  expect(classifyPreflight({ ...ok, url: 'https://other.example/' }, ORIGIN)).toEqual({ kind: 'failed', reason: 'redirects to https://other.example' }));

test('a same-origin redirect is fine', () => expect(classifyPreflight({ ...ok, url: 'https://a.se/x/' }, ORIGIN)).toBeNull());

test('a non-HTML response (download) fails that page only', () =>
  expect(classifyPreflight({ ...ok, contentType: 'application/pdf' }, ORIGIN)).toEqual({ kind: 'failed', reason: 'not an HTML page (application/pdf)' }));

test('X-Frame-Options DENY blocks the scan', () =>
  expect(classifyPreflight({ ...ok, xFrameOptions: 'DENY' }, ORIGIN)?.kind).toBe('blocked'));

test("frame-ancestors 'none' blocks the scan", () =>
  expect(classifyPreflight({ ...ok, csp: "default-src 'self'; frame-ancestors 'none'" }, ORIGIN)?.kind).toBe('blocked'));

test('SAMEORIGIN and frame-ancestors self are fine', () =>
  expect(classifyPreflight({ ...ok, xFrameOptions: 'SAMEORIGIN', csp: "frame-ancestors 'self'" }, ORIGIN)).toBeNull());
