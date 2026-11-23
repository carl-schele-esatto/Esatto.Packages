import { describe, expect, it } from 'vitest';
import type { BuildInfo, LocalChanges, PipelineBuild, PipelineChanges, PipelineCommit } from './types';
import { buildKind, localTags, summarizeChanges } from './view-state';

const commit = (sha: string): PipelineCommit => ({ sha, subject: `Subject of ${sha}` });
const commits = (count: number) => Array.from({ length: count }, (_, i) => commit(`c${i}`));

const changes = (extra: Partial<PipelineChanges> = {}): PipelineChanges => ({ truncated: false, commits: [], ...extra });
const local = (extra: Partial<LocalChanges> = {}): LocalChanges => ({ uncommittedFiles: 0, unpushedCommits: [], commits: [], ...extra });
const pipeline: PipelineBuild = { buildNumber: '42', commit: '0123456789abcdef' };

const info = (extra: Partial<BuildInfo> = {}): BuildInfo => ({
  umbracoVersion: '18.3.1',
  environment: 'Production',
  instance: 'web-1',
  runningSinceUtc: '2026-11-20T08:00:00+00:00',
  packages: [],
  ...extra,
});

describe('buildKind', () => {
  it('is pipeline when the stamp was found', () => {
    expect(buildKind(info({ pipeline }))).toBe('pipeline');
  });

  it('is pipeline even when local changes came along', () => {
    expect(buildKind(info({ pipeline, local: local() }))).toBe('pipeline');
  });

  it('is local when there is no stamp but there are local changes', () => {
    expect(buildKind(info({ pipeline: null, local: local() }))).toBe('local');
    expect(buildKind(info({ local: local() }))).toBe('local');
  });

  it('is unknown when there is neither, whether the fields are null or missing', () => {
    expect(buildKind(info({ pipeline: null, local: null }))).toBe('unknown');
    expect(buildKind(info())).toBe('unknown');
  });
});

describe('summarizeChanges', () => {
  it('says there is no previous deployment when sinceCommit is null, and counts the commits it lists', () => {
    expect(summarizeChanges(changes({ sinceCommit: null, truncated: true, commits: commits(3) }))).toEqual({
      kind: 'noPrevious',
      count: 3,
    });
  });

  it('treats a missing or blank sinceCommit the same as null', () => {
    expect(summarizeChanges(changes({ commits: commits(2) }))).toEqual({ kind: 'noPrevious', count: 2 });
    expect(summarizeChanges(changes({ sinceCommit: '  ', commits: commits(2) }))).toEqual({ kind: 'noPrevious', count: 2 });
  });

  it('has nothing to compare with when only the build number is known', () => {
    expect(summarizeChanges(changes({ sinceBuildNumber: '41', commits: commits(1) }))).toEqual({ kind: 'noPrevious', count: 1 });
  });

  it('reports no new commits when sinceCommit is known and the list is empty', () => {
    expect(summarizeChanges(changes({ sinceCommit: 'abc1234def', sinceBuildNumber: '41', sinceBuildUrl: 'https://ci.example.com/builds/41' }))).toEqual({
      kind: 'none',
      since: { type: 'build', number: '41', url: 'https://ci.example.com/builds/41' },
    });
  });

  it('names the previous build when its number is known, with no url when there is none', () => {
    const summary = summarizeChanges(changes({ sinceCommit: 'abc1234def', sinceBuildNumber: '41' }));

    expect(summary).toEqual({ kind: 'none', since: { type: 'build', number: '41', url: null } });
  });

  it('names the previous commit when only that is known', () => {
    expect(summarizeChanges(changes({ sinceCommit: 'abc1234def', sinceBuildNumber: null }))).toEqual({
      kind: 'none',
      since: { type: 'commit', sha: 'abc1234def' },
    });
    expect(summarizeChanges(changes({ sinceCommit: 'abc1234def' }))).toEqual({ kind: 'none', since: { type: 'commit', sha: 'abc1234def' } });
  });

  it('counts the commits since the previous deployment', () => {
    const summary = summarizeChanges(
      changes({ sinceCommit: 'abc1234def', sinceBuildNumber: '41', sinceBuildUrl: 'https://ci.example.com/builds/41', commits: commits(2) }),
    );

    expect(summary).toEqual({
      kind: 'some',
      count: 2,
      truncated: false,
      since: { type: 'build', number: '41', url: 'https://ci.example.com/builds/41' },
    });
  });

  it('carries truncated and the count of the commits it has', () => {
    expect(summarizeChanges(changes({ sinceCommit: 'abc1234def', truncated: true, commits: commits(100) }))).toEqual({
      kind: 'some',
      count: 100,
      truncated: true,
      since: { type: 'commit', sha: 'abc1234def' },
    });
  });
});

describe('localTags', () => {
  const running = commit('f1ff7ec9a0b1c2d3e4f5061728394a5b6c7d8e9f');

  it('marks the commit the running app was built from, matching on the start of the sha', () => {
    expect(localTags(running, local(), 'f1ff7ec').running).toBe(true);
    expect(localTags(running, local(), running.sha).running).toBe(true);
  });

  it('does not mark any other commit', () => {
    expect(localTags(commit('0000000aaaa'), local(), 'f1ff7ec').running).toBe(false);
  });

  it('marks nothing as running when the build commit is unknown', () => {
    expect(localTags(running, local(), null).running).toBe(false);
    expect(localTags(running, local(), '').running).toBe(false);
  });

  it('does not match a shorter commit sha against a longer build sha', () => {
    expect(localTags(commit('f1ff7ec'), local(), 'f1ff7ec9a0').running).toBe(false);
  });

  it('marks only the commits that are in the unpushed list', () => {
    const unpushed = local({ unpushedCommits: [running.sha] });

    expect(localTags(running, unpushed, null).notPushed).toBe(true);
    expect(localTags(commit('0000000aaaa'), unpushed, null).notPushed).toBe(false);
    expect(localTags(running, local(), null).notPushed).toBe(false);
  });

  it('can mark a commit as both', () => {
    expect(localTags(running, local({ unpushedCommits: [running.sha] }), 'f1ff7ec')).toEqual({ running: true, notPushed: true });
  });
});
