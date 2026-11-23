// What to show, decided from the response. Pure functions with no DOM and no Lit, so the rules are easy to test;
// the dashboard element only draws what these return.

import type { BuildInfo, LocalChanges, PipelineChanges, PipelineCommit } from './types';

/** Which first box the dashboard shows: a pipeline deployed this build, a local build with git history, or a build nobody can identify. */
export type BuildKind = 'pipeline' | 'local' | 'unknown';

export function buildKind(info: Pick<BuildInfo, 'pipeline' | 'local'>): BuildKind {
  if (info.pipeline) return 'pipeline';
  return info.local ? 'local' : 'unknown';
}

/** What the commits are counted from: the previous build (with a link when the stamp has its url), or just its commit. */
export type Since = { type: 'build'; number: string; url: string | null } | { type: 'commit'; sha: string } | null;

export type ChangesSummary =
  // No known previous deployment: the commits are only the latest ones.
  | { kind: 'noPrevious'; count: number }
  // The previous deployment is known and nothing is new: the same code was rebuilt.
  | { kind: 'none'; since: Since }
  | { kind: 'some'; count: number; truncated: boolean; since: Since };

const present = (value: string | null | undefined) => (value?.trim() ? value : null);

function sinceOf(changes: PipelineChanges): Since {
  const number = present(changes.sinceBuildNumber);
  if (number) return { type: 'build', number, url: present(changes.sinceBuildUrl) };

  const sha = present(changes.sinceCommit);
  return sha ? { type: 'commit', sha } : null;
}

export function summarizeChanges(changes: PipelineChanges): ChangesSummary {
  const count = changes.commits.length;
  if (!present(changes.sinceCommit)) return { kind: 'noPrevious', count };
  if (count === 0) return { kind: 'none', since: sinceOf(changes) };
  return { kind: 'some', count, truncated: changes.truncated, since: sinceOf(changes) };
}

/**
 * The tags of a commit in the working copy's list: `running` for the commit the running app was built from (`builtFromSha` is the
 * start of its sha, from the "+" in the app version), `notPushed` for a commit that is not on the remote yet.
 */
export function localTags(
  commit: PipelineCommit,
  local: LocalChanges,
  builtFromSha: string | null,
): { running: boolean; notPushed: boolean } {
  return {
    running: !!builtFromSha && commit.sha.startsWith(builtFromSha),
    notPushed: local.unpushedCommits.includes(commit.sha),
  };
}
