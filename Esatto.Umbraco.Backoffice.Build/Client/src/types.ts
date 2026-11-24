// What GET /umbraco/management/api/v1/esatto/build-info returns: BuildInfoResponse and the records inside it (BuildInfoService.cs,
// PipelineBuild.cs, LocalGitLog.cs, PackageCatalog.cs). camelCase JSON.
//
// A property the server may send as null (`?: T | null` here) may also be missing: whether null properties are written depends on the
// host's JSON options, so the client reads "null or missing" as the same thing.

/** One commit, from the stamp (pipeline) or from the working copy's git log (local). */
export interface PipelineCommit {
  sha: string;
  subject: string;
  body?: string | null;
  author?: string | null;
  dateUtc?: string | null;
  url?: string | null;
}

/** The commits that went live in a deployment, relative to the previous one. `sinceCommit` null: no known previous deployment. */
export interface PipelineChanges {
  sinceCommit?: string | null;
  sinceBuildNumber?: string | null;
  sinceBuildUrl?: string | null;
  truncated: boolean;
  commits: PipelineCommit[];
}

/** What the CI pipeline recorded about this deployment (the contents of build-info.json). */
export interface PipelineBuild {
  buildNumber: string;
  commit: string;
  buildUrl?: string | null;
  commitMessage?: string | null;
  commitUrl?: string | null;
  branch?: string | null;
  targetEnvironment?: string | null;
  builtAtUtc?: string | null;
  changes?: PipelineChanges | null;
  schemaVersion?: number | null;
}

/** The working copy's own answer to "what's new", read only in Development when there is no stamp. */
export interface LocalChanges {
  branch?: string | null;
  uncommittedFiles: number;
  /** The shas of the commits that are not pushed yet. */
  unpushedCommits: string[];
  commits: PipelineCommit[];
}

export interface PackageVersion {
  name: string;
  version?: string | null;
}

export interface BuildInfo {
  /** The stamp. Null or missing: no pipeline deployed this build. */
  pipeline?: PipelineBuild | null;
  /** The host app's informational version; after a "+" it can carry the commit it was built from. */
  appVersion?: string | null;
  umbracoVersion: string;
  environment: string;
  instance: string;
  runningSinceUtc: string;
  packages: PackageVersion[];
  local?: LocalChanges | null;
}
