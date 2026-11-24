const t = (e, n, i) => e === 1 ? n : i, a = {
  esattoBuild: {
    title: "Build",
    loadFailed: "Could not read the build information.",
    // The first box.
    deployedBuild: "Deployed build",
    localBuild: "Local build",
    unknownBuild: "Unknown build",
    branchTitle: "Git branch",
    branch: (e) => `branch: ${e}`,
    commit: "Commit",
    built: "Built",
    pipelineTarget: "Pipeline target",
    builtFrom: "Built from",
    localNotDeployed: "Not deployed by a pipeline: no {0} on this server. Deployed environments show the build number and the commits since the previous deployment.",
    unknownNotFound: "No {0} was found on this server. See the {1} for how your pipeline can write it.",
    readmeLink: "package README",
    // What is in this build.
    changesTitle: "What's in this build",
    changesNoPrevious: (e) => e === 0 ? "The previous deployment could not be found, and no commits were listed." : `The previous deployment could not be found, so ${t(e, "this is the latest commit", `these are the latest ${e} commits`)}.`,
    changesNone: "No new commits {0}: this deployment rebuilt the same code.",
    changesSome: (e, n) => `${e}${n ? "+" : ""} ${t(e, "commit", "commits")} {0}${n ? ` (only the latest ${e} shown)` : ""}.`,
    sinceBuild: "since build {0}",
    sinceCommit: "since commit {0}",
    // The latest commits in the working copy (a local build).
    localChangesTitle: "Latest commits in this working copy",
    localSummary: (e, n, i, o) => {
      const s = e === 0 ? "No commits" : t(e, "The latest commit", `The latest ${e} commits`), l = [
        i ? `${i} not pushed` : "",
        o ? `${o} ${t(o, "file", "files")} changed but not committed` : ""
      ].filter(Boolean);
      return `${s}${n ? " on {0}" : ""}${l.length ? `: ${l.join(", ")}` : ""}.`;
    },
    tagRunning: "running",
    tagNotPushed: "not pushed",
    // This environment and its packages.
    environmentTitle: "This environment",
    environment: "Environment",
    instance: "Instance",
    runningSince: "Running since",
    umbraco: "Umbraco",
    packagesTitle: "Packages"
  }
};
export {
  a as default
};
//# sourceMappingURL=en.js.map
