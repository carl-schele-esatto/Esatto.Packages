import { describe, expect, it } from 'vitest';
import { NOTE_MAX_LENGTH, noteLimit } from './note-text';

describe('the note limit', () => {
  it('is 1000 characters, as the server keeps', () => {
    expect(NOTE_MAX_LENGTH).toBe(1000);
  });
});

describe('noteLimit', () => {
  it('measures and trims the text: the server counts the trimmed text, so white space around it is free', () => {
    expect(noteLimit('  a launch in March \n')).toEqual({ length: 'a launch in March'.length, excess: 0, over: false, trimmed: 'a launch in March' });
    expect(noteLimit('x'.repeat(NOTE_MAX_LENGTH).padStart(NOTE_MAX_LENGTH + 20, ' ')).over).toBe(false);
  });

  it('allows exactly the limit and refuses one more', () => {
    expect(noteLimit('x'.repeat(NOTE_MAX_LENGTH))).toMatchObject({ length: 1000, over: false, excess: 0 });
    expect(noteLimit('x'.repeat(NOTE_MAX_LENGTH + 1))).toMatchObject({ length: 1001, over: true, excess: 1 });
  });

  it('counts UTF-16 code units, as the server does: an emoji is two', () => {
    const emoji = '\u{1F600}';

    expect(emoji).toHaveLength(2);
    expect(noteLimit(emoji.repeat(500)).over).toBe(false);
    expect(noteLimit(emoji.repeat(501))).toMatchObject({ length: 1002, over: true, excess: 2 });
  });

  it('is empty for nothing and for white space only: the note is then deleted', () => {
    expect(noteLimit('')).toEqual({ length: 0, excess: 0, over: false, trimmed: '' });
    expect(noteLimit(' \n\t ')).toEqual({ length: 0, excess: 0, over: false, trimmed: '' });
  });

  it('tolerates a missing text', () => {
    expect(noteLimit(null)).toEqual({ length: 0, excess: 0, over: false, trimmed: '' });
    expect(noteLimit(undefined).trimmed).toBe('');
  });
});
