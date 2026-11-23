#!/usr/bin/env node
// Writes build-info.json: which pipeline build and commit an artifact is, and which commits it adds since the
// previous deployment. The Esatto.Umbraco.Backoffice.Build package reads this file to show Content > Build in
// the backoffice. Run it in your pipeline after the checkout and before the deploy artifact is zipped.
//
//   node write-build-info.mjs [--out <path>]      (default ./build-info.json; missing folders are created)
//
// Needs Node 20+ (global fetch), no npm packages, and a full git history in the checkout:
// `fetch-depth: 0` on actions/checkout (GitHub Actions), `fetchDepth: 0` on the checkout step (Azure DevOps).
// Exit code 0 when the file is written, 1 with a message when required information is missing (not in a git
// repository, or neither Azure DevOps nor GitHub Actions detected and BUILD_INFO_BUILD_NUMBER not set).
//
// Environment variables it reads:
//   Any CI (these win over what the CI provides)
//     BUILD_INFO_BUILD_NUMBER          buildNumber (required when no supported CI is detected)
//     BUILD_INFO_BUILD_URL             buildUrl
//     BUILD_INFO_COMMIT_URL_TEMPLATE   commit links, with {sha} where the commit id goes
//     BUILD_INFO_BRANCH                branch (default: the CI's branch, else `git branch --show-current`)
//     BUILD_INFO_TARGET                targetEnvironment, for example "live" (default: none)
//     BUILD_INFO_SINCE_COMMIT          list the commits since this commit instead of looking up the previous deployment
//   Azure DevOps (TF_BUILD=True)
//     BUILD_BUILDNUMBER, BUILD_BUILDID, BUILD_SOURCEBRANCH, BUILD_SOURCEBRANCHNAME, BUILD_REPOSITORY_URI,
//     SYSTEM_COLLECTIONURI, SYSTEM_TEAMPROJECT, SYSTEM_DEFINITIONID: predefined variables, exposed as env vars
//     SYSTEM_ACCESSTOKEN               must be mapped into the step explicitly: reads the earlier runs
//     BUILD_INFO_DEPLOY_TASK           display name of the task that means "deployed"; a run counts once that task
//                                      succeeded. Unset: a run counts when its result is succeeded.
//   GitHub Actions (GITHUB_ACTIONS=true)
//     GITHUB_RUN_NUMBER, GITHUB_RUN_ID, GITHUB_SERVER_URL, GITHUB_REPOSITORY, GITHUB_API_URL, GITHUB_WORKFLOW_REF,
//     GITHUB_REF_NAME: set by Actions
//     GITHUB_TOKEN                     must be mapped into the step explicitly (permission `actions: read`)
//
// The tokens are only ever sent to the CI's own API: they are never written to the file or to the log.
import { execFileSync } from 'node:child_process';
import { mkdirSync, realpathSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

/** The version of the build-info.json contract this script writes. */
export const SCHEMA_VERSION = 1;

// Without a known previous deployment the tab shows the latest commits instead.
const FALLBACK_COMMITS = 20;
const MAX_COMMITS = 100;
const BUILDS_TO_CHECK = 20;
const REQUEST_TIMEOUT_MS = 15_000;

// The same log format and trailer rule as LocalGitLog in the package, so a local and a deployed list look alike.
const FIELD = '\x1f';
const RECORD = '\x1e';
const LOG_FORMAT = '--format=%H%x1f%an%x1f%aI%x1f%s%x1f%b%x1e';
const TRAILER = /^[\w-]+: .+@.+$/;

const SECRET_VARIABLES = ['SYSTEM_ACCESSTOKEN', 'GITHUB_TOKEN'];
const COMMIT_ID = /^[0-9a-f]{7,64}$/i;
const USAGE = 'Usage: node write-build-info.mjs [--out <path>]';

// ---------------------------------------------------------------------------------------------
// Small helpers
// ---------------------------------------------------------------------------------------------

/** The variable's value, or undefined when it is missing or blank. */
const value = (env, name) => env[name]?.trim() || undefined;

const withoutTrailingSlashes = (text) => text.replace(/\/+$/, '');

/** Wraps a logger so the secrets in the environment can never reach it, whatever ends up in a message. */
function withoutSecrets(write, env) {
    const secrets = SECRET_VARIABLES.map((name) => value(env, name)).filter(Boolean);
    return (message) => write(secrets.reduce((text, secret) => text.replaceAll(secret, '***'), String(message)));
}

/** A git runner for `cwd`: returns trimmed stdout and throws an Error carrying git's own message. */
export function createGit(cwd = process.cwd(), extraEnv = {}) {
    return (...args) => {
        try {
            return execFileSync('git', args, {
                cwd,
                env: { ...process.env, ...extraEnv },
                encoding: 'utf8',
                stdio: ['ignore', 'pipe', 'pipe'],
                maxBuffer: 64 * 1024 * 1024,
            }).trim();
        } catch (error) {
            const reason = error.code === 'ENOENT'
                ? 'git is not installed or not on the PATH'
                : String(error.stderr ?? '').trim() || error.message;
            throw new Error(`git ${args[0]} failed: ${reason}`);
        }
    };
}

async function getJson(fetchImpl, url, token, headers = {}) {
    const response = await fetchImpl(url, {
        headers: { ...headers, Authorization: `Bearer ${token}` },
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
    });
    // The token travels in the header only, so neither the URL nor this message can carry it.
    if (!response.ok) {
        throw new Error(`HTTP ${[response.status, response.statusText].filter(Boolean).join(' ')} from ${url}`);
    }
    return response.json();
}

const missingVariables = (env, names) => names.filter((name) => !value(env, name));

// ---------------------------------------------------------------------------------------------
// Azure DevOps
// ---------------------------------------------------------------------------------------------

function azureProjectUrl(env) {
    const collection = value(env, 'SYSTEM_COLLECTIONURI');
    const project = value(env, 'SYSTEM_TEAMPROJECT');
    return collection && project ? `${withoutTrailingSlashes(collection)}/${encodeURIComponent(project)}` : null;
}

function azureBuildUrl(env, buildId) {
    const project = azureProjectUrl(env);
    return project && buildId ? `${project}/_build/results?buildId=${buildId}` : null;
}

// Build.Repository.Uri can carry the organisation as user info (https://org@dev.azure.com/...).
function azureRepositoryUrl(env) {
    const uri = value(env, 'BUILD_REPOSITORY_URI');
    if (!uri) return null;
    try {
        const url = new URL(uri);
        url.username = '';
        url.password = '';
        return withoutTrailingSlashes(url.href);
    } catch {
        return null;
    }
}

async function azurePreviousDeployment(env, fetchImpl, log) {
    const required = ['SYSTEM_ACCESSTOKEN', 'SYSTEM_COLLECTIONURI', 'SYSTEM_TEAMPROJECT', 'SYSTEM_DEFINITIONID', 'BUILD_SOURCEBRANCH'];
    const missing = missingVariables(env, required);
    if (missing.length > 0) {
        log(`Cannot look up the previous deployment on Azure DevOps: ${missing.join(', ')} not set. `
            + 'SYSTEM_ACCESSTOKEN must be mapped into the step explicitly: env: SYSTEM_ACCESSTOKEN: $(System.AccessToken).');
        return null;
    }

    const token = value(env, 'SYSTEM_ACCESSTOKEN');
    const deployTask = value(env, 'BUILD_INFO_DEPLOY_TASK');
    const api = (path) => getJson(fetchImpl, `${azureProjectUrl(env)}/_apis/build/${path}`, token);

    const builds = await api(`builds?definitions=${encodeURIComponent(value(env, 'SYSTEM_DEFINITIONID'))}`
        + `&branchName=${encodeURIComponent(value(env, 'BUILD_SOURCEBRANCH'))}`
        + `&statusFilter=completed&queryOrder=finishTimeDescending&$top=${BUILDS_TO_CHECK}&api-version=7.1`);

    // With a deploy task, a run counts once that task succeeded, even if the run failed in a later step.
    // Without one, only a succeeded run counts and no timeline needs reading.
    const deployed = async (build) => {
        if (!deployTask) return build.result === 'succeeded';
        const timeline = await api(`builds/${build.id}/timeline?api-version=7.1`);
        return timeline.records?.some((record) =>
            record.type === 'Task' && record.name === deployTask && record.result === 'succeeded') ?? false;
    };

    const completed = builds.value ?? [];
    for (const build of completed) {
        if (build.sourceVersion && await deployed(build)) {
            return {
                sha: build.sourceVersion,
                buildNumber: build.buildNumber ?? null,
                url: azureBuildUrl(env, build.id),
            };
        }
    }

    // A deploy task that no run has is the likeliest mistake (a name that is not the task's display name), and it
    // would otherwise only show as "latest 20 commits" with no hint why.
    if (deployTask) {
        const branch = value(env, 'BUILD_SOURCEBRANCH');
        log(completed.length === 0
            ? `There are no earlier completed runs on ${branch} to look for a task named '${deployTask}' in.`
            : `None of the last ${completed.length} completed run${completed.length === 1 ? '' : 's'} on ${branch} `
                + `had a succeeded task named '${deployTask}'. `
                + 'BUILD_INFO_DEPLOY_TASK must be the display name of your deploy task, spelled exactly as in the pipeline.');
    }
    return null;
}

// ---------------------------------------------------------------------------------------------
// GitHub Actions
//
// Not verified against the real GitHub API: only against fixtures and a fake fetch (write-build-info.test.mjs).
// The first real run on GitHub Actions is the check; the request and the response fields it reads follow
// the "List workflow runs for a workflow" endpoint of the REST API.
// ---------------------------------------------------------------------------------------------

function githubRepositoryUrl(env) {
    const server = value(env, 'GITHUB_SERVER_URL');
    const repository = value(env, 'GITHUB_REPOSITORY');
    return server && repository ? `${withoutTrailingSlashes(server)}/${repository}` : null;
}

function githubBuildUrl(env) {
    const repositoryUrl = githubRepositoryUrl(env);
    const runId = value(env, 'GITHUB_RUN_ID');
    return repositoryUrl && runId ? `${repositoryUrl}/actions/runs/${runId}` : null;
}

async function githubPreviousDeployment(env, fetchImpl, log) {
    const required = ['GITHUB_TOKEN', 'GITHUB_API_URL', 'GITHUB_REPOSITORY', 'GITHUB_WORKFLOW_REF', 'GITHUB_REF_NAME'];
    const missing = missingVariables(env, required);
    if (missing.length > 0) {
        log(`Cannot look up the previous deployment on GitHub: ${missing.join(', ')} not set. `
            + 'GITHUB_TOKEN must be mapped into the step explicitly (env: GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}) and needs permission actions: read.');
        return null;
    }

    // GITHUB_WORKFLOW_REF is owner/repo/.github/workflows/<file>@<ref>: the runs are listed by the file name.
    const workflowFile = /\/\.github\/workflows\/([^@/]+)@/.exec(value(env, 'GITHUB_WORKFLOW_REF'))?.[1];
    if (!workflowFile) {
        log(`Cannot look up the previous deployment on GitHub: the workflow file name is not in GITHUB_WORKFLOW_REF (${value(env, 'GITHUB_WORKFLOW_REF')}).`);
        return null;
    }

    const runs = await getJson(
        fetchImpl,
        `${withoutTrailingSlashes(value(env, 'GITHUB_API_URL'))}/repos/${value(env, 'GITHUB_REPOSITORY')}`
            + `/actions/workflows/${encodeURIComponent(workflowFile)}/runs`
            + `?branch=${encodeURIComponent(value(env, 'GITHUB_REF_NAME'))}&status=success&per_page=${BUILDS_TO_CHECK}`,
        value(env, 'GITHUB_TOKEN'),
        { Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28' });

    const current = value(env, 'GITHUB_RUN_ID');
    const previous = (runs.workflow_runs ?? []).find((run) =>
        run.conclusion === 'success' && run.head_sha && String(run.id) !== current);
    return previous
        ? {
            sha: previous.head_sha,
            buildNumber: previous.run_number == null ? null : String(previous.run_number),
            url: previous.html_url ?? null,
        }
        : null;
}

// ---------------------------------------------------------------------------------------------
// CI detection and field mapping
// ---------------------------------------------------------------------------------------------

const CI_SYSTEMS = {
    'azure-devops': {
        buildNumberVariable: 'BUILD_BUILDNUMBER',
        buildNumber: (env) => value(env, 'BUILD_BUILDNUMBER'),
        buildUrl: (env) => azureBuildUrl(env, value(env, 'BUILD_BUILDID')),
        repositoryUrl: azureRepositoryUrl,
        branch: (env) => value(env, 'BUILD_SOURCEBRANCHNAME'),
        previousDeployment: azurePreviousDeployment,
    },
    'github-actions': {
        buildNumberVariable: 'GITHUB_RUN_NUMBER',
        buildNumber: (env) => value(env, 'GITHUB_RUN_NUMBER'),
        buildUrl: githubBuildUrl,
        repositoryUrl: githubRepositoryUrl,
        branch: (env) => value(env, 'GITHUB_REF_NAME'),
        previousDeployment: githubPreviousDeployment,
    },
    generic: {
        buildNumberVariable: null,
        buildNumber: () => undefined,
        buildUrl: () => null,
        repositoryUrl: () => null,
        branch: () => undefined,
        previousDeployment: null,
    },
};

/** @returns {'azure-devops' | 'github-actions' | 'generic'} */
export function detectCi(env) {
    if (env.TF_BUILD === 'True') return 'azure-devops';
    if (env.GITHUB_ACTIONS === 'true') return 'github-actions';
    return 'generic';
}

function currentBranch(git) {
    try {
        // Empty on a detached HEAD, which is what most CI checkouts are.
        return git('branch', '--show-current') || null;
    } catch {
        return null;
    }
}

/**
 * The CI-derived fields of the stamp. The BUILD_INFO_* variables win over what the CI provides.
 * @returns {{ buildNumber: string, buildUrl: string | null, commitUrl: (sha: string) => string | null,
 *             branch: string | null, targetEnvironment: string | null }}
 */
export function collectFields(ci, env, git) {
    const system = CI_SYSTEMS[ci];
    const buildNumber = value(env, 'BUILD_INFO_BUILD_NUMBER') ?? system.buildNumber(env);
    if (!buildNumber) {
        throw new Error(system.buildNumberVariable
            ? `${system.buildNumberVariable} is not set. Set BUILD_INFO_BUILD_NUMBER to the build number of this run.`
            : 'BUILD_INFO_BUILD_NUMBER is not set. Neither Azure DevOps nor GitHub Actions was detected, '
                + 'so set BUILD_INFO_BUILD_NUMBER to the build number of this run.');
    }

    const template = value(env, 'BUILD_INFO_COMMIT_URL_TEMPLATE');
    const repositoryUrl = system.repositoryUrl(env);

    return {
        buildNumber,
        buildUrl: value(env, 'BUILD_INFO_BUILD_URL') ?? system.buildUrl(env),
        commitUrl: (sha) => {
            if (template) return template.replaceAll('{sha}', sha);
            return repositoryUrl ? `${repositoryUrl}/commit/${sha}` : null;
        },
        branch: value(env, 'BUILD_INFO_BRANCH') ?? system.branch(env) ?? currentBranch(git),
        targetEnvironment: value(env, 'BUILD_INFO_TARGET') ?? null,
    };
}

// ---------------------------------------------------------------------------------------------
// Git log
// ---------------------------------------------------------------------------------------------

/** Trailers (Co-Authored-By and the like) say nothing about what changed. */
export function stripTrailers(body) {
    const text = body.split(/\r\n|\r|\n/).filter((line) => !TRAILER.test(line.trim())).join('\n').trim();
    return text || null;
}

/** Parses the output of `git log` with LOG_FORMAT. A commit's `url` is filled in by the caller. */
export function parseGitLog(output) {
    return output.split(RECORD)
        .map((record) => record.trim())
        .filter(Boolean)
        .map((record) => record.split(FIELD))
        .filter((fields) => fields.length >= 5)
        .map(([sha, author, date, subject, body]) => {
            const time = new Date(date);
            return {
                sha,
                subject,
                body: stripTrailers(body),
                author,
                dateUtc: Number.isNaN(time.getTime()) ? null : time.toISOString(),
                url: null,
            };
        });
}

function listCommits(git, commitUrl, range, limit) {
    const output = git('log', '--no-merges', `--max-count=${limit}`, LOG_FORMAT, ...range);
    return parseGitLog(output).map((commit) => ({ ...commit, url: commitUrl(commit.sha) }));
}

function isAncestor(git, sha) {
    try {
        git('merge-base', '--is-ancestor', sha, 'HEAD');
        return true;
    } catch {
        return false;
    }
}

function warnIfShallow(git, log) {
    let shallow = false;
    try {
        shallow = git('rev-parse', '--is-shallow-repository') === 'true';
    } catch {
        // A git too old to know the option: assume a full checkout.
    }
    if (shallow) {
        log('This checkout is shallow, so the list of commits will be incomplete or empty. Check out the full history: '
            + '`fetch-depth: 0` on actions/checkout (GitHub Actions) or `fetchDepth: 0` on the checkout step (Azure DevOps). '
            + 'Writing the stamp anyway.');
    }
}

// ---------------------------------------------------------------------------------------------
// The previous deployment and the stamp
// ---------------------------------------------------------------------------------------------

/**
 * The latest earlier deployment of this pipeline on this branch, or null when there is none or no lookup is possible.
 * Rejects when the CI's API fails: the caller decides that this is not worth failing the build over.
 * @returns {Promise<{ sha: string, buildNumber: string | null, url: string | null } | null>}
 */
export async function findPreviousDeployment(ci, env, fetchImpl, log = () => {}) {
    const lookup = CI_SYSTEMS[ci].previousDeployment;
    return lookup ? lookup(env, fetchImpl, log) : null;
}

/**
 * A network failure reaches us as `TypeError: fetch failed`; the reason (DNS, refused, reset ...) is in `error.cause`.
 * An AggregateError (several addresses tried) has no message of its own, only a code.
 */
function reasonOf(error) {
    const reason = error.cause?.message || error.cause?.code;
    return reason ? ` (${reason})` : '';
}

async function resolvePrevious(ci, env, fetchImpl, log) {
    const explicit = value(env, 'BUILD_INFO_SINCE_COMMIT');
    if (explicit) {
        if (!COMMIT_ID.test(explicit)) {
            // Also keeps a value that looks like a git option away from git.
            log(`BUILD_INFO_SINCE_COMMIT must be a commit id (7 to 64 hexadecimal digits), not "${explicit}".`);
            return null;
        }
        return { sha: explicit, buildNumber: null, url: null };
    }

    try {
        return await findPreviousDeployment(ci, env, fetchImpl, log);
    } catch (error) {
        // The list is a help, not a gate: never fail the deployment over it.
        log(`Could not look up the previous deployment: ${error.message}${reasonOf(error)}`);
        return null;
    }
}

async function changesSince({ ci, env, git, fetchImpl, log, commitUrl }) {
    const previous = await resolvePrevious(ci, env, fetchImpl, log);

    if (previous && isAncestor(git, previous.sha)) {
        const commits = listCommits(git, commitUrl, [`${previous.sha}..HEAD`], MAX_COMMITS + 1);
        return {
            sinceCommit: previous.sha,
            sinceBuildNumber: previous.buildNumber,
            sinceBuildUrl: previous.url,
            truncated: commits.length > MAX_COMMITS,
            commits: commits.slice(0, MAX_COMMITS),
        };
    }

    if (previous) {
        const label = previous.buildNumber ? `${previous.buildNumber} (${previous.sha})` : previous.sha;
        log(`The previous deployment ${label} is not in this checkout's history.`);
    }
    log(`Listing the latest ${FALLBACK_COMMITS} commits instead.`);
    return {
        sinceCommit: null,
        sinceBuildNumber: null,
        sinceBuildUrl: null,
        truncated: true,
        commits: listCommits(git, commitUrl, [], FALLBACK_COMMITS),
    };
}

function readHead(git) {
    try {
        return git('rev-parse', 'HEAD');
    } catch (error) {
        throw new Error(`Cannot read the current commit: run this inside the git checkout of the project (${error.message}).`);
    }
}

/**
 * Builds the contents of build-info.json. Throws only when required information is missing.
 * @param {{ env: Record<string, string | undefined>, git?: (...args: string[]) => string, fetchImpl?: typeof fetch,
 *           now?: () => Date, log?: (message: string) => void }} options
 */
export async function buildStamp({ env, git = createGit(), fetchImpl = globalThis.fetch, now = () => new Date(), log = console.log }) {
    const safeLog = withoutSecrets(log, env);
    const ci = detectCi(env);
    const fields = collectFields(ci, env, git);
    const commit = readHead(git);
    warnIfShallow(git, safeLog);

    return {
        schemaVersion: SCHEMA_VERSION,
        buildNumber: fields.buildNumber,
        commit,
        buildUrl: fields.buildUrl,
        commitMessage: git('log', '-1', '--format=%s') || null,
        commitUrl: fields.commitUrl(commit),
        branch: fields.branch,
        targetEnvironment: fields.targetEnvironment,
        builtAtUtc: now().toISOString(),
        changes: await changesSince({ ci, env, git, fetchImpl, log: safeLog, commitUrl: fields.commitUrl }),
    };
}

// ---------------------------------------------------------------------------------------------
// Command line
// ---------------------------------------------------------------------------------------------

function parseArguments(argv) {
    let out = 'build-info.json';
    for (let i = 0; i < argv.length; i++) {
        if (argv[i] === '--out' && i + 1 < argv.length) {
            out = argv[++i];
        } else {
            throw new Error(`Unexpected argument ${argv[i]}${argv[i] === '--out' ? ' (it needs a path)' : ''}. ${USAGE}`);
        }
    }
    return { out };
}

/**
 * Runs the script. Returns the exit code instead of exiting, so a test can call it.
 * @param {string[]} argv the arguments after the script name
 * @param {Record<string, string | undefined>} env
 */
export async function main(argv, env, { git, fetchImpl, now, log = console.log, error = console.error } = {}) {
    const safeLog = withoutSecrets(log, env);
    const safeError = withoutSecrets(error, env);

    try {
        const { out } = parseArguments(argv);
        const stamp = await buildStamp({ env, git, fetchImpl, now, log: safeLog });

        const target = resolve(out);
        mkdirSync(dirname(target), { recursive: true });
        writeFileSync(target, `${JSON.stringify(stamp, null, 2)}\n`);

        const { changes } = stamp;
        const since = changes.sinceBuildNumber ? `since build ${changes.sinceBuildNumber}`
            : changes.sinceCommit ? `since commit ${changes.sinceCommit.slice(0, 7)}`
                : 'the latest, no previous deployment known';
        safeLog(`Wrote ${target}: build ${stamp.buildNumber}, commit ${stamp.commit.slice(0, 7)}, `
            + `${changes.commits.length} commit(s) listed (${since}).`);
        return 0;
    } catch (failure) {
        safeError(failure.message);
        return 1;
    }
}

/** True when this file is the one `node` was started with, so importing it (the tests do) runs nothing. */
function isEntryPoint() {
    try {
        return Boolean(process.argv[1]) && pathToFileURL(realpathSync(process.argv[1])).href === import.meta.url;
    } catch {
        return false;
    }
}

if (isEntryPoint()) {
    process.exitCode = await main(process.argv.slice(2), process.env);
}
