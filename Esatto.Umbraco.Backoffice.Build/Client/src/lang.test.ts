import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import en from './lang/en';

// Every text of the dashboard is looked up as esattoBuild_<key>. The localization controller answers a key it cannot find with the
// key itself, so a typo would show "esattoBuild_chnagesTitle" in the backoffice. This reads the sources and checks that each key
// they ask for has an English text.
const SRC = fileURLToPath(new URL('.', import.meta.url));

const sourceFiles = () =>
  readdirSync(SRC, { recursive: true, encoding: 'utf8' })
    .filter((file) => file.endsWith('.ts') && !file.endsWith('.test.ts'))
    .map((file) => join(SRC, file));

const referencedKeys = () =>
  new Set(sourceFiles().flatMap((file) => [...readFileSync(file, 'utf8').matchAll(/esattoBuild_([A-Za-z0-9]+)/g)].map((match) => match[1])));

describe('the English texts', () => {
  it('finds the keys the sources refer to', () => {
    expect(referencedKeys().size).toBeGreaterThan(0);
  });

  it('has a text for every key the sources refer to', () => {
    const texts = Object.keys(en.esattoBuild);

    for (const key of referencedKeys()) expect(texts, `esattoBuild_${key}`).toContain(key);
  });

  it('only holds strings and functions that return strings', () => {
    for (const [key, text] of Object.entries(en.esattoBuild)) {
      expect(['string', 'function'], key).toContain(typeof text);
      if (typeof text === 'string') expect(text.trim(), key).not.toBe('');
    }
  });
});

// A {0} or {1} marks where the dashboard puts a link or a <code> element (fill-text.ts). Texts with numbers are functions.
describe('the wording', () => {
  const text = en.esattoBuild;

  it('counts the commits since the previous deployment', () => {
    expect(text.changesSome(1, false)).toBe('1 commit {0}.');
    expect(text.changesSome(3, false)).toBe('3 commits {0}.');
  });

  it('says a full list is only the latest commits', () => {
    expect(text.changesSome(100, true)).toBe('100+ commits {0} (only the latest 100 shown).');
  });

  it('says what it can when the previous deployment is not known', () => {
    expect(text.changesNoPrevious(20)).toBe('The previous deployment could not be found, so these are the latest 20 commits.');
    expect(text.changesNoPrevious(1)).toBe('The previous deployment could not be found, so this is the latest commit.');
    expect(text.changesNoPrevious(0)).toBe('The previous deployment could not be found, and no commits were listed.');
  });

  it('says a rebuild of the same code has no new commits', () => {
    expect(text.changesNone).toBe('No new commits {0}: this deployment rebuilt the same code.');
  });

  it('names what the changes are counted from', () => {
    expect(text.sinceBuild).toBe('since build {0}');
    expect(text.sinceCommit).toBe('since commit {0}');
  });

  it('sums up the working copy, with the branch where there is one', () => {
    expect(text.localSummary(20, true, 2, 3)).toBe('The latest 20 commits on {0}: 2 not pushed, 3 files changed but not committed.');
    expect(text.localSummary(5, true, 0, 1)).toBe('The latest 5 commits on {0}: 1 file changed but not committed.');
    expect(text.localSummary(5, false, 1, 0)).toBe('The latest 5 commits: 1 not pushed.');
    expect(text.localSummary(1, false, 0, 0)).toBe('The latest commit.');
    expect(text.localSummary(0, true, 0, 0)).toBe('No commits on {0}.');
  });

  it('does not tell a deployed environment apart by name', () => {
    expect(text.localNotDeployed).toBe(
      'Not deployed by a pipeline: no {0} on this server. Deployed environments show the build number and the commits since the previous deployment.',
    );
  });

  it('points the reader of the empty state to the README', () => {
    expect(text.unknownNotFound).toBe('No {0} was found on this server. See the {1} for how your pipeline can write it.');
  });
});
