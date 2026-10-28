import { expect, test } from 'vitest';
import { unobscure } from '../public-site/obscure';

// WCAG 2.4.11: the floating preferences button must not hide the element that has keyboard focus.
const button = { top: 720, bottom: 768, left: 950, right: 1100 };

test('a focused element clear of the button needs nothing', () =>
  expect(unobscure({ top: 100, bottom: 140, left: 950, right: 1000 }, button, 500)).toBeNull());

test('an element beside the button needs nothing', () =>
  expect(unobscure({ top: 730, bottom: 760, left: 100, right: 300 }, button, 500)).toBeNull());

test('an element under the button is scrolled up above it when the page can scroll', () =>
  expect(unobscure({ top: 700, bottom: 740, left: 900, right: 1000 }, button, 500)).toEqual({ scrollBy: 28 }));

test('at the end of the page the button steps aside instead', () =>
  expect(unobscure({ top: 700, bottom: 740, left: 900, right: 1000 }, button, 10)).toBe('yield'));
