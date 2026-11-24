import { describe, expect, it } from 'vitest';
import { noteViews, severityOf } from './notes-view';
import { enHost } from './testing/host';
import type { Annotation } from './types';

const host = enHost();

// The report has no insight texts yet (nothing in the processing layer writes insights), so an insight is read through a localizer that is the
// English one, except that a key it has no text for is answered with the key itself in angle brackets.
const echo = {
  localize: {
    term: (key: string, ...args: unknown[]) => {
      const written = host.localize.term(key, ...args);
      return written === key ? `<${key.replace('esattoSeoReport_', '')}>` : written;
    },
    lang: () => 'en-gb',
  },
};

const annotation = (key: string, severity: string, args?: Record<string, string | number | boolean | null>): Annotation => ({
  period: null,
  severity: severity as Annotation['severity'],
  text: { key, args },
});

describe('severityOf', () => {
  it('knows warning and info, and reads anything else as info', () => {
    expect(severityOf('warning')).toBe('warning');
    expect(severityOf('info')).toBe('info');
    expect(severityOf('notice')).toBe('info');
    expect(severityOf('')).toBe('info');
  });
});

describe('noteViews', () => {
  it('writes an annotation with its title and text, and says what kind it is in words and with an icon', () => {
    const [note] = noteViews(host, [annotation('noKeyEvents', 'warning')], []);

    expect(note).toMatchObject({
      kind: 'annotation',
      severity: 'warning',
      icon: 'icon-alert',
      label: 'Warning',
      title: 'No key events in GA4',
    });
    expect(note.text).toMatch(/^GA4 reports zero key events/);
  });

  it('gives an info annotation and an insight their own icon and label, so no kind is told by colour alone', () => {
    const notes = noteViews(echo, [annotation('anonymizedQueries', 'info')], [{ key: 'trafficDip' }]);

    expect(notes.map((note) => [note.kind, note.severity, note.icon, note.label])).toEqual([
      ['annotation', 'info', 'icon-info', 'Note'],
      ['insight', 'insight', 'icon-lightbulb', 'Insight'],
    ]);
    expect(notes[1].text).toBe('<insightTrafficDipText>');
    expect(notes[1].title).toBe('');
  });

  it('passes the annotation arguments to its text', () => {
    const [note] = noteViews(host, [annotation('anonymizedQueries', 'info', { percent: 91.5, period: '2026-10' })], []);

    expect(note.text).toContain('91.5%');
    expect(note.text).toContain('October 2026');
  });

  it('puts what was established before what is interpreted: annotations first, then insights', () => {
    const notes = noteViews(echo, [annotation('noKeyEvents', 'warning')], [{ key: 'trafficDip' }]);

    expect(notes.map((note) => note.kind)).toEqual(['annotation', 'insight']);
  });

  it('can leave out the warnings, which belong to the To fix list, or show only them', () => {
    const list = [annotation('noKeyEvents', 'warning'), annotation('anonymizedQueries', 'info')];

    expect(noteViews(host, list, [], 'exclude').map((note) => note.severity)).toEqual(['info']);
    expect(noteViews(host, list, [], 'only').map((note) => note.severity)).toEqual(['warning']);
    expect(noteViews(host, list, [], 'include').map((note) => note.severity)).toEqual(['warning', 'info']);
  });

  it('shows whatever arrives: a user who may not see findings gets no warnings from the server, and none are invented here', () => {
    expect(noteViews(host, [annotation('anonymizedQueries', 'info')], [], 'include')).toHaveLength(1);
  });

  it('skips what it cannot write (a malformed key), and tolerates missing lists', () => {
    expect(noteViews(host, [annotation('not a key', 'info')], [])).toEqual([]);
    expect(noteViews(host, undefined, undefined)).toEqual([]);
    expect(noteViews(host, null as never, null as never)).toEqual([]);
  });
});
