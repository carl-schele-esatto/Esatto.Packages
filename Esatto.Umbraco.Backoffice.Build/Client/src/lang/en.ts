// English texts (the only language, and the fallback for every other one). Looked up as esattoBuild_<key>.
//
// A text with numbers is a function of them. A {0} or {1} in a text marks where the dashboard puts a link or a <code> element
// (fill-text.ts), so the sentence stays whole.

const plural = (count: number, one: string, many: string) => (count === 1 ? one : many);

export default {
  esattoBuild: {
    title: 'Build',
    loadFailed: 'Could not read the build information.',

    // The first box.
    deployedBuild: 'Deployed build',
    localBuild: 'Local build',
    unknownBuild: 'Unknown build',
    branchTitle: 'Git branch',
    branch: (name: string) => `branch: ${name}`,
    commit: 'Commit',
    built: 'Built',
    pipelineTarget: 'Pipeline target',
    builtFrom: 'Built from',
    localNotDeployed:
      'Not deployed by a pipeline: no {0} on this server. Deployed environments show the build number and the commits since the previous deployment.',
    unknownNotFound: 'No {0} was found on this server. See the {1} for how your pipeline can write it.',
    readmeLink: 'package README',

    // What is in this build.
    changesTitle: "What's in this build",
    changesNoPrevious: (count: number) =>
      count === 0
        ? 'The previous deployment could not be found, and no commits were listed.'
        : `The previous deployment could not be found, so ${plural(count, 'this is the latest commit', `these are the latest ${count} commits`)}.`,
    changesNone: 'No new commits {0}: this deployment rebuilt the same code.',
    changesSome: (count: number, truncated: boolean) =>
      `${count}${truncated ? '+' : ''} ${plural(count, 'commit', 'commits')} {0}${truncated ? ` (only the latest ${count} shown)` : ''}.`,
    sinceBuild: 'since build {0}',
    sinceCommit: 'since commit {0}',

    // The latest commits in the working copy (a local build).
    localChangesTitle: 'Latest commits in this working copy',
    localSummary: (count: number, hasBranch: boolean, notPushed: number, uncommitted: number) => {
      const lead = count === 0 ? 'No commits' : plural(count, 'The latest commit', `The latest ${count} commits`);
      const details = [
        notPushed ? `${notPushed} not pushed` : '',
        uncommitted ? `${uncommitted} ${plural(uncommitted, 'file', 'files')} changed but not committed` : '',
      ].filter(Boolean);
      return `${lead}${hasBranch ? ' on {0}' : ''}${details.length ? `: ${details.join(', ')}` : ''}.`;
    },
    tagRunning: 'running',
    tagNotPushed: 'not pushed',

    // This environment and its packages.
    environmentTitle: 'This environment',
    environment: 'Environment',
    instance: 'Instance',
    runningSince: 'Running since',
    umbraco: 'Umbraco',
    packagesTitle: 'Packages',
  },
};
