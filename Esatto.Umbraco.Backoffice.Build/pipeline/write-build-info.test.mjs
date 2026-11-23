// Tests for write-build-info.mjs: `node --test pipeline/write-build-info.test.mjs` from the package folder.
// No network and no real CI: git is a fake function and fetch is a fake that records its calls. A few
// tests at the end run the real git against throwaway repositories under the temp folder.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, it } from 'node:test';
import { fileURLToPath, pathToFileURL } from 'node:url';

import {
    buildStamp,
    collectFields,
    createGit,
    detectCi,
    findPreviousDeployment,
    main,
    parseGitLog,
    stripTrailers,
} from './write-build-info.mjs';

// ---------------------------------------------------------------------------------------------
// Fixtures and fakes
// ---------------------------------------------------------------------------------------------

// The fixtures are shared with the C# tests, so both sides read the same files.
const fixture = (name) => JSON.parse(readFileSync(
    fileURLToPath(new URL(`../../Esatto.Umbraco.Backoffice.Build.Tests/Fixtures/${name}`, import.meta.url)), 'utf8'));

const HEAD = '0123456789abcdef0123456789abcdef01234567';
const PREVIOUS = '89abcdef0123456789abcdef0123456789abcdef';
const FIXED_NOW = () => new Date('2026-11-20T09:12:00Z');
const FAKE_AZURE_TOKEN = 'fake-azure-token-0000-for-tests';
const FAKE_GITHUB_TOKEN = 'fake-github-token-1111-for-tests';

const AZURE_ENV = {
    TF_BUILD: 'True',
    BUILD_BUILDNUMBER: '0.0.108',
    BUILD_BUILDID: '4242',
    BUILD_SOURCEBRANCH: 'refs/heads/main',
    BUILD_SOURCEBRANCHNAME: 'main',
    BUILD_REPOSITORY_URI: 'https://example-org@ci.example.com/example-org/Example%20Site/_git/site',
    SYSTEM_COLLECTIONURI: 'https://ci.example.com/example-org/',
    SYSTEM_TEAMPROJECT: 'Example Site',
    SYSTEM_DEFINITIONID: '7',
    SYSTEM_ACCESSTOKEN: FAKE_AZURE_TOKEN,
};

const GITHUB_ENV = {
    GITHUB_ACTIONS: 'true',
    GITHUB_RUN_NUMBER: '42',
    GITHUB_RUN_ID: '9042',
    GITHUB_SERVER_URL: 'https://git.example.com',
    GITHUB_REPOSITORY: 'acme/site',
    GITHUB_API_URL: 'https://api.git.example.com',
    GITHUB_WORKFLOW_REF: 'acme/site/.github/workflows/deploy.yml@refs/heads/main',
    GITHUB_REF_NAME: 'main',
    GITHUB_TOKEN: FAKE_GITHUB_TOKEN,
};

const sha = (i) => i.toString(16).padStart(40, '0');

/** `git log` output as git writes it: the record separator is followed by a newline, and so is a non-empty body. */
const gitLogText = (records) => records
    .map((r) => `${r.sha}\x1f${r.author}\x1f${r.date}\x1f${r.subject}\x1f${r.body ? `${r.body}\n` : ''}\x1e\n`)
    .join('');

const syntheticCommits = (count) => Array.from({ length: count }, (_, i) => ({
    sha: sha(i + 1),
    author: 'Test Author',
    date: '2026-11-20T10:00:00+01:00',
    subject: `Change ${i + 1}`,
    body: '',
}));

/**
 * A fake git. `log` answers like git: newest first, cut off at --max-count. A call it does not know
 * throws, like a failing git would. Every call is recorded in `calls`.
 */
function createFakeGit({
    head = HEAD,
    branch = 'main',
    subject = 'Add the contact form',
    shallow = false,
    commits = syntheticCommits(150),
    isAncestor = () => true,
} = {}) {
    const calls = [];
    const git = (...args) => {
        calls.push(args);
        if (args[0] === 'rev-parse' && args[1] === 'HEAD') return head;
        if (args[0] === 'rev-parse' && args[1] === '--is-shallow-repository') return shallow ? 'true' : 'false';
        if (args[0] === 'branch' && args[1] === '--show-current') return branch;
        if (args[0] === 'merge-base' && args[1] === '--is-ancestor') {
            if (isAncestor(args[2])) return '';
            throw new Error('git merge-base failed: exit code 1');
        }
        if (args[0] === 'log' && args[1] === '-1') return subject;
        if (args[0] === 'log') {
            const limit = Number(args.find((a) => a.startsWith('--max-count=')).split('=')[1]);
            return gitLogText(commits.slice(0, limit)).trim();
        }
        throw new Error(`unexpected git call: ${args.join(' ')}`);
    };
    git.calls = calls;
    git.logCalls = () => calls.filter((c) => c[0] === 'log' && c[1] !== '-1');
    return git;
}

/** A fake fetch: `handler(url, init)` returns a JSON body, a Response, or throws. */
function createFakeFetch(handler) {
    const calls = [];
    const fetchImpl = async (url, init = {}) => {
        calls.push({ url: String(url), headers: init.headers ?? {} });
        const result = await handler(new URL(url), init);
        return result instanceof Response
            ? result
            : new Response(JSON.stringify(result), { status: 200, headers: { 'content-type': 'application/json' } });
    };
    fetchImpl.calls = calls;
    return fetchImpl;
}

const neverFetch = () => createFakeFetch(() => { throw new Error('fetch must not be called'); });

const capture = () => {
    const lines = [];
    return { lines, log: (m) => lines.push(String(m)), error: (m) => lines.push(String(m)) };
};

function withTempDir(run) {
    const directory = mkdtempSync(join(tmpdir(), 'build-info-test-'));
    return Promise.resolve(run(directory)).finally(() => rmSync(directory, { recursive: true, force: true }));
}

// ---------------------------------------------------------------------------------------------
// CI detection and field mapping
// ---------------------------------------------------------------------------------------------

describe('detectCi', () => {
    it('detects Azure DevOps from TF_BUILD=True', () => {
        assert.equal(detectCi({ TF_BUILD: 'True' }), 'azure-devops');
    });

    it('detects GitHub Actions from GITHUB_ACTIONS=true', () => {
        assert.equal(detectCi({ GITHUB_ACTIONS: 'true' }), 'github-actions');
    });

    it('falls back to generic', () => {
        assert.equal(detectCi({}), 'generic');
        assert.equal(detectCi({ TF_BUILD: 'False', GITHUB_ACTIONS: 'false' }), 'generic');
    });

    it('prefers Azure DevOps when both are set', () => {
        assert.equal(detectCi({ TF_BUILD: 'True', GITHUB_ACTIONS: 'true' }), 'azure-devops');
    });
});

describe('collectFields', () => {
    const git = createFakeGit();

    it('maps the Azure DevOps variables', () => {
        const fields = collectFields('azure-devops', AZURE_ENV, git);

        assert.equal(fields.buildNumber, '0.0.108');
        assert.equal(fields.buildUrl, 'https://ci.example.com/example-org/Example%20Site/_build/results?buildId=4242');
        assert.equal(fields.commitUrl(HEAD), `https://ci.example.com/example-org/Example%20Site/_git/site/commit/${HEAD}`);
        assert.equal(fields.branch, 'main');
        assert.equal(fields.targetEnvironment, null);
    });

    it('strips the credentials from the repository URL', () => {
        const env = { ...AZURE_ENV, BUILD_REPOSITORY_URI: 'https://org:fake-password@dev.azure.com/org/proj/_git/repo' };

        const url = collectFields('azure-devops', env, git).commitUrl(HEAD);

        assert.equal(url, `https://dev.azure.com/org/proj/_git/repo/commit/${HEAD}`);
        const userOnly = { ...AZURE_ENV, BUILD_REPOSITORY_URI: 'https://org@dev.azure.com/org/proj/_git/repo' };
        assert.equal(collectFields('azure-devops', userOnly, git).commitUrl(HEAD), `https://dev.azure.com/org/proj/_git/repo/commit/${HEAD}`);
    });

    it('leaves the URLs out when Azure DevOps does not provide what they need', () => {
        const env = { TF_BUILD: 'True', BUILD_BUILDNUMBER: '7', BUILD_REPOSITORY_URI: 'not a url' };

        const fields = collectFields('azure-devops', env, git);

        assert.equal(fields.buildUrl, null);
        assert.equal(fields.commitUrl(HEAD), null);
    });

    it('maps the GitHub Actions variables', () => {
        const fields = collectFields('github-actions', GITHUB_ENV, git);

        assert.equal(fields.buildNumber, '42');
        assert.equal(fields.buildUrl, 'https://git.example.com/acme/site/actions/runs/9042');
        assert.equal(fields.commitUrl(HEAD), `https://git.example.com/acme/site/commit/${HEAD}`);
        assert.equal(fields.branch, 'main');
    });

    it('needs only a build number in generic mode', () => {
        const fields = collectFields('generic', { BUILD_INFO_BUILD_NUMBER: 'local-1' }, git);

        assert.equal(fields.buildNumber, 'local-1');
        assert.equal(fields.buildUrl, null);
        assert.equal(fields.commitUrl(HEAD), null);
        assert.equal(fields.targetEnvironment, null);
        assert.equal(fields.branch, 'main', 'the branch comes from git when no variable names it');
    });

    for (const [name, ci, env] of [['Azure DevOps', 'azure-devops', AZURE_ENV], ['GitHub Actions', 'github-actions', GITHUB_ENV]]) {
        it(`lets the BUILD_INFO_* overrides win over ${name}`, () => {
            const fields = collectFields(ci, {
                ...env,
                BUILD_INFO_BUILD_NUMBER: 'override-7',
                BUILD_INFO_BUILD_URL: 'https://ci.example.com/override/7',
                BUILD_INFO_COMMIT_URL_TEMPLATE: 'https://git.example.com/view/{sha}?again={sha}',
                BUILD_INFO_BRANCH: 'release',
                BUILD_INFO_TARGET: 'test',
            }, git);

            assert.equal(fields.buildNumber, 'override-7');
            assert.equal(fields.buildUrl, 'https://ci.example.com/override/7');
            assert.equal(fields.commitUrl(HEAD), `https://git.example.com/view/${HEAD}?again=${HEAD}`);
            assert.equal(fields.branch, 'release');
            assert.equal(fields.targetEnvironment, 'test');
        });
    }

    it('treats blank overrides as unset', () => {
        const fields = collectFields('github-actions', { ...GITHUB_ENV, BUILD_INFO_BUILD_NUMBER: '  ', BUILD_INFO_BRANCH: '' }, git);

        assert.equal(fields.buildNumber, '42');
        assert.equal(fields.branch, 'main');
    });

    it('has no branch when git is on a detached HEAD or cannot say', () => {
        assert.equal(collectFields('generic', { BUILD_INFO_BUILD_NUMBER: '1' }, createFakeGit({ branch: '' })).branch, null);
        const failing = () => { throw new Error('git failed'); };
        assert.equal(collectFields('generic', { BUILD_INFO_BUILD_NUMBER: '1' }, failing).branch, null);
    });

    it('fails with a clear message in generic mode without a build number', () => {
        assert.throws(() => collectFields('generic', {}, git), /BUILD_INFO_BUILD_NUMBER is not set/);
    });

    it('fails with a clear message when a detected CI has no build number', () => {
        assert.throws(() => collectFields('azure-devops', { TF_BUILD: 'True' }, git), /BUILD_BUILDNUMBER is not set.*BUILD_INFO_BUILD_NUMBER/);
        assert.throws(() => collectFields('github-actions', { GITHUB_ACTIONS: 'true' }, git), /GITHUB_RUN_NUMBER is not set.*BUILD_INFO_BUILD_NUMBER/);
    });
});

// ---------------------------------------------------------------------------------------------
// Git log parsing (same fixture as the C# LocalGitLogTests)
// ---------------------------------------------------------------------------------------------

describe('parseGitLog', () => {
    it('matches the shared fixture, comparing dates as instants', () => {
        const sample = fixture('git-log-sample.json');

        const commits = parseGitLog(sample.output);

        assert.equal(sample.commits.length, 3);
        assert.equal(commits.length, sample.commits.length);
        sample.commits.forEach((expected, i) => {
            assert.equal(commits[i].sha, expected.sha);
            assert.equal(commits[i].subject, expected.subject);
            assert.equal(commits[i].body, expected.body);
            assert.equal(commits[i].author, expected.author);
            assert.equal(Date.parse(commits[i].dateUtc), Date.parse(expected.dateUtc));
            assert.equal(commits[i].url, null);
        });
    });

    it('writes the dates as UTC', () => {
        const [commit] = parseGitLog(fixture('git-log-sample.json').output);

        assert.equal(commit.dateUtc, '2026-11-20T09:01:00.000Z');
    });

    it('skips blank and short records', () => {
        assert.deepEqual(parseGitLog(''), []);
        assert.deepEqual(parseGitLog('abc'), []);
        assert.deepEqual(parseGitLog('\n\x1e\n'), []);
    });

    it('keeps a commit without a body and ignores a missing final newline', () => {
        const [commit] = parseGitLog(`${HEAD}\x1fAda Example\x1f2026-11-20T10:01:00+01:00\x1fFix it\x1f\x1e`);

        assert.equal(commit.body, null);
        assert.equal(commit.subject, 'Fix it');
    });

    it('gives a commit with an unreadable date a null date rather than failing', () => {
        const [commit] = parseGitLog(`${HEAD}\x1fAda Example\x1fnot a date\x1fFix it\x1f\x1e`);

        assert.equal(commit.dateUtc, null);
    });
});

describe('stripTrailers', () => {
    it('removes Co-Authored-By style trailers and keeps the text', () => {
        const body = 'Explain the change.\n\nSecond paragraph.\n\nCo-Authored-By: Grace Example <grace@example.com>\nSigned-off-by: Ben Placeholder <ben@example.com>';

        assert.equal(stripTrailers(body), 'Explain the change.\n\nSecond paragraph.');
    });

    it('returns null when only trailers or whitespace are left', () => {
        assert.equal(stripTrailers('Co-Authored-By: Grace Example <grace@example.com>'), null);
        assert.equal(stripTrailers(' \n\n'), null);
        assert.equal(stripTrailers(''), null);
    });

    it('keeps lines that only look a little like trailers', () => {
        assert.equal(stripTrailers('Note: this is not a trailer'), 'Note: this is not a trailer');
        assert.equal(stripTrailers('see ada@example.com'), 'see ada@example.com');
    });

    it('copes with Windows line endings', () => {
        assert.equal(stripTrailers('Text.\r\n\r\nCo-Authored-By: Grace Example <grace@example.com>\r\n'), 'Text.');
    });
});

// ---------------------------------------------------------------------------------------------
// Finding the previous deployment
// ---------------------------------------------------------------------------------------------

describe('findPreviousDeployment on Azure DevOps', () => {
    const DEPLOY_TASK = 'Hand over to hosting';
    const builds = [
        { id: 105, buildNumber: '0.0.105', sourceVersion: sha(105), result: 'failed' },
        { id: 104, buildNumber: '0.0.104', sourceVersion: sha(104), result: 'failed' },
        { id: 103, buildNumber: '0.0.103', sourceVersion: sha(103), result: 'succeeded' },
    ];
    const timelines = {
        // Failed overall (a later wait step timed out) although the deployment was accepted.
        105: [{ type: 'Task', name: DEPLOY_TASK, result: 'succeeded' }],
        104: [{ type: 'Task', name: DEPLOY_TASK, result: 'failed' }],
        103: [{ type: 'Task', name: DEPLOY_TASK, result: 'succeeded' }],
    };
    const route = (url) => {
        if (url.pathname.endsWith('/_apis/build/builds')) return { value: builds };
        const id = /builds\/(\d+)\/timeline/.exec(url.pathname)?.[1];
        if (id) return { records: timelines[id] };
        throw new Error(`unexpected request ${url}`);
    };

    it('asks the builds API for completed runs of this pipeline on this branch, newest first', async () => {
        const fetchImpl = createFakeFetch(route);

        await findPreviousDeployment('azure-devops', AZURE_ENV, fetchImpl);

        const url = new URL(fetchImpl.calls[0].url);
        assert.equal(url.origin + url.pathname, 'https://ci.example.com/example-org/Example%20Site/_apis/build/builds');
        assert.equal(url.searchParams.get('definitions'), '7');
        assert.equal(url.searchParams.get('branchName'), 'refs/heads/main');
        assert.equal(url.searchParams.get('statusFilter'), 'completed');
        assert.equal(url.searchParams.get('queryOrder'), 'finishTimeDescending');
        assert.equal(url.searchParams.get('$top'), '20');
        assert.equal(fetchImpl.calls[0].headers.Authorization, `Bearer ${FAKE_AZURE_TOKEN}`);
    });

    it('counts a run whose deploy task succeeded even if the run failed later, when BUILD_INFO_DEPLOY_TASK is set', async () => {
        const fetchImpl = createFakeFetch(route);

        const previous = await findPreviousDeployment('azure-devops', { ...AZURE_ENV, BUILD_INFO_DEPLOY_TASK: DEPLOY_TASK }, fetchImpl);

        assert.deepEqual(previous, {
            sha: sha(105),
            buildNumber: '0.0.105',
            url: 'https://ci.example.com/example-org/Example%20Site/_build/results?buildId=105',
        });
        assert.equal(fetchImpl.calls.length, 2, 'the builds list plus one timeline: it stops at the first match');
    });

    it('skips runs where the deploy task did not succeed', async () => {
        const fetchImpl = createFakeFetch((url) => {
            const id = /builds\/(\d+)\/timeline/.exec(url.pathname)?.[1];
            return id === '105' ? { records: [] } : route(url);
        });

        const previous = await findPreviousDeployment('azure-devops', { ...AZURE_ENV, BUILD_INFO_DEPLOY_TASK: DEPLOY_TASK }, fetchImpl);

        assert.equal(previous.buildNumber, '0.0.103', 'run 104 failed the task and run 105 had no such task');
    });

    it('does not mistake a task of the same name that is not a task record', async () => {
        const fetchImpl = createFakeFetch((url) => (url.pathname.endsWith('/builds')
            ? { value: [builds[0]] }
            : { records: [{ type: 'Stage', name: DEPLOY_TASK, result: 'succeeded' }] }));

        const previous = await findPreviousDeployment('azure-devops', { ...AZURE_ENV, BUILD_INFO_DEPLOY_TASK: DEPLOY_TASK }, fetchImpl);

        assert.equal(previous, null);
    });

    it('counts only runs with the result succeeded when BUILD_INFO_DEPLOY_TASK is unset, without reading any timeline', async () => {
        const fetchImpl = createFakeFetch(route);

        const previous = await findPreviousDeployment('azure-devops', AZURE_ENV, fetchImpl);

        assert.equal(previous.buildNumber, '0.0.103');
        assert.equal(fetchImpl.calls.length, 1, 'only the builds list was requested');
    });

    it('finds nothing when no run qualifies', async () => {
        const fetchImpl = createFakeFetch(() => ({ value: [builds[0], builds[1]] }));

        assert.equal(await findPreviousDeployment('azure-devops', AZURE_ENV, fetchImpl), null);
    });

    it('says how many runs it inspected and which task it looked for when a named deploy task matches none', async () => {
        const fetchImpl = createFakeFetch(route);
        const out = capture();

        const previous = await findPreviousDeployment(
            'azure-devops', { ...AZURE_ENV, BUILD_INFO_DEPLOY_TASK: 'Deploy to hosting' }, fetchImpl, out.log);

        assert.equal(previous, null);
        assert.deepEqual(out.lines, [
            "None of the last 3 completed runs on refs/heads/main had a succeeded task named 'Deploy to hosting'. "
            + 'BUILD_INFO_DEPLOY_TASK must be the display name of your deploy task, spelled exactly as in the pipeline.',
        ]);
    });

    it('says there are no earlier runs when the branch has none, instead of blaming the task name', async () => {
        const out = capture();

        const previous = await findPreviousDeployment(
            'azure-devops', { ...AZURE_ENV, BUILD_INFO_DEPLOY_TASK: DEPLOY_TASK }, createFakeFetch(() => ({ value: [] })), out.log);

        assert.equal(previous, null);
        assert.equal(out.lines.length, 1);
        assert.match(out.lines[0], /no earlier completed runs on refs\/heads\/main/);
        assert.ok(!/None of the last/.test(out.lines[0]));
    });

    it('logs nothing about the deploy task when a run qualifies, or when no deploy task is set', async () => {
        const matched = capture();
        await findPreviousDeployment('azure-devops', { ...AZURE_ENV, BUILD_INFO_DEPLOY_TASK: DEPLOY_TASK }, createFakeFetch(route), matched.log);
        const unnamed = capture();
        await findPreviousDeployment('azure-devops', AZURE_ENV, createFakeFetch(() => ({ value: [builds[0], builds[1]] })), unnamed.log);

        assert.deepEqual(matched.lines, []);
        assert.deepEqual(unnamed.lines, [], 'without a deploy task nothing is said about one');
    });

    it('reports a missing token or variable, and does not call the API', async () => {
        for (const missing of ['SYSTEM_ACCESSTOKEN', 'SYSTEM_DEFINITIONID', 'BUILD_SOURCEBRANCH', 'SYSTEM_COLLECTIONURI']) {
            const env = { ...AZURE_ENV, [missing]: undefined };
            const fetchImpl = neverFetch();
            const out = capture();

            assert.equal(await findPreviousDeployment('azure-devops', env, fetchImpl, out.log), null);

            assert.equal(fetchImpl.calls.length, 0);
            assert.match(out.lines.join('\n'), new RegExp(missing));
        }
    });

    it('rejects when the API answers with an error, naming the status and not the token', async () => {
        const fetchImpl = createFakeFetch(() => new Response('denied', { status: 401, statusText: 'Unauthorized' }));

        await assert.rejects(
            findPreviousDeployment('azure-devops', AZURE_ENV, fetchImpl),
            (error) => /401 Unauthorized/.test(error.message) && !error.message.includes(FAKE_AZURE_TOKEN));
    });
});

describe('findPreviousDeployment on GitHub Actions', () => {
    const runs = [
        { id: 9042, conclusion: 'success', run_number: 42, head_sha: sha(42), html_url: 'https://git.example.com/acme/site/actions/runs/9042' },
        { id: 9041, conclusion: 'failure', run_number: 41, head_sha: sha(41), html_url: 'https://git.example.com/acme/site/actions/runs/9041' },
        { id: 9040, conclusion: 'success', run_number: 40, head_sha: sha(40), html_url: 'https://git.example.com/acme/site/actions/runs/9040' },
        { id: 9039, conclusion: 'success', run_number: 39, head_sha: sha(39), html_url: 'https://git.example.com/acme/site/actions/runs/9039' },
    ];

    it('asks for successful runs of this workflow file on this branch', async () => {
        const fetchImpl = createFakeFetch(() => ({ workflow_runs: runs }));

        await findPreviousDeployment('github-actions', GITHUB_ENV, fetchImpl);

        const url = new URL(fetchImpl.calls[0].url);
        assert.equal(url.origin + url.pathname, 'https://api.git.example.com/repos/acme/site/actions/workflows/deploy.yml/runs');
        assert.equal(url.searchParams.get('branch'), 'main');
        assert.equal(url.searchParams.get('status'), 'success');
        assert.equal(url.searchParams.get('per_page'), '20');
        assert.equal(fetchImpl.calls[0].headers.Authorization, `Bearer ${FAKE_GITHUB_TOKEN}`);
        assert.equal(fetchImpl.calls[0].headers.Accept, 'application/vnd.github+json');
    });

    it('skips the current run and runs that did not succeed', async () => {
        const fetchImpl = createFakeFetch(() => ({ workflow_runs: runs }));

        const previous = await findPreviousDeployment('github-actions', GITHUB_ENV, fetchImpl);

        assert.deepEqual(previous, {
            sha: sha(40),
            buildNumber: '40',
            url: 'https://git.example.com/acme/site/actions/runs/9040',
        });
    });

    it('finds nothing when there is no earlier successful run', async () => {
        const fetchImpl = createFakeFetch(() => ({ workflow_runs: [runs[0], runs[1]] }));

        assert.equal(await findPreviousDeployment('github-actions', GITHUB_ENV, fetchImpl), null);
        assert.equal(await findPreviousDeployment('github-actions', GITHUB_ENV, createFakeFetch(() => ({ workflow_runs: [] }))), null);
    });

    it('reports a missing token or variable, and does not call the API', async () => {
        for (const missing of ['GITHUB_TOKEN', 'GITHUB_API_URL', 'GITHUB_REPOSITORY', 'GITHUB_WORKFLOW_REF', 'GITHUB_REF_NAME']) {
            const fetchImpl = neverFetch();
            const out = capture();

            assert.equal(await findPreviousDeployment('github-actions', { ...GITHUB_ENV, [missing]: undefined }, fetchImpl, out.log), null);

            assert.equal(fetchImpl.calls.length, 0);
            assert.match(out.lines.join('\n'), new RegExp(missing));
        }
    });

    it('reports a workflow reference it cannot read', async () => {
        const out = capture();

        const previous = await findPreviousDeployment('github-actions', { ...GITHUB_ENV, GITHUB_WORKFLOW_REF: 'garbage' }, neverFetch(), out.log);

        assert.equal(previous, null);
        assert.match(out.lines.join('\n'), /GITHUB_WORKFLOW_REF/);
    });
});

describe('findPreviousDeployment on other CI systems', () => {
    it('looks nothing up in generic mode', async () => {
        const fetchImpl = neverFetch();

        assert.equal(await findPreviousDeployment('generic', { SYSTEM_ACCESSTOKEN: 'x', GITHUB_TOKEN: 'y' }, fetchImpl), null);
        assert.equal(fetchImpl.calls.length, 0);
    });
});

// ---------------------------------------------------------------------------------------------
// The stamp: range choice, limits, shallow checkouts
// ---------------------------------------------------------------------------------------------

describe('buildStamp', () => {
    const azureFetch = (timeline = true) => createFakeFetch((url) => (url.pathname.endsWith('/builds')
        ? { value: [{ id: 41, buildNumber: '0.0.107', sourceVersion: PREVIOUS, result: timeline ? 'succeeded' : 'failed' }] }
        : { records: [] }));

    const run = (env, { git = createFakeGit(), fetchImpl = azureFetch() } = {}) => {
        const out = capture();
        return buildStamp({ env, git, fetchImpl, now: FIXED_NOW, log: out.log }).then((stamp) => ({ stamp, out, git, fetchImpl }));
    };

    it('fills the fields of the contract', async () => {
        const { stamp } = await run({ ...AZURE_ENV, BUILD_INFO_TARGET: 'live' });

        assert.equal(stamp.schemaVersion, 1);
        assert.equal(stamp.buildNumber, '0.0.108');
        assert.equal(stamp.commit, HEAD);
        assert.equal(stamp.commitMessage, 'Add the contact form');
        assert.equal(stamp.branch, 'main');
        assert.equal(stamp.targetEnvironment, 'live');
        assert.equal(stamp.builtAtUtc, '2026-11-20T09:12:00.000Z');
        assert.equal(stamp.commitUrl, `https://ci.example.com/example-org/Example%20Site/_git/site/commit/${HEAD}`);
    });

    it('lists the commits since the previous deployment when it is an ancestor', async () => {
        const git = createFakeGit({ commits: syntheticCommits(3) });

        const { stamp } = await run(AZURE_ENV, { git });

        assert.equal(stamp.changes.sinceCommit, PREVIOUS);
        assert.equal(stamp.changes.sinceBuildNumber, '0.0.107');
        assert.equal(stamp.changes.sinceBuildUrl, 'https://ci.example.com/example-org/Example%20Site/_build/results?buildId=41');
        assert.equal(stamp.changes.commits.length, 3);
        assert.equal(stamp.changes.truncated, false);
        const [log] = git.logCalls();
        assert.ok(log.includes(`${PREVIOUS}..HEAD`), 'the range starts at the previous deployment');
        assert.ok(log.includes('--no-merges'));
        assert.ok(log.includes('--max-count=101'), 'one more than the limit is requested to detect truncation');
        assert.deepEqual(git.calls.find((c) => c[0] === 'merge-base'), ['merge-base', '--is-ancestor', PREVIOUS, 'HEAD']);
    });

    it('gives every listed commit its URL', async () => {
        const { stamp } = await run(AZURE_ENV);

        assert.equal(stamp.changes.commits[0].url, `https://ci.example.com/example-org/Example%20Site/_git/site/commit/${sha(1)}`);
    });

    it('lists the latest 20 commits when the previous deployment is not an ancestor, and says so', async () => {
        const git = createFakeGit({ isAncestor: () => false });

        const { stamp, out } = await run(AZURE_ENV, { git });

        assert.equal(stamp.changes.sinceCommit, null);
        assert.equal(stamp.changes.sinceBuildNumber, null);
        assert.equal(stamp.changes.sinceBuildUrl, null);
        assert.equal(stamp.changes.truncated, true);
        assert.equal(stamp.changes.commits.length, 20);
        assert.match(out.lines.join('\n'), /0\.0\.107.*not in this checkout's history/s);
        assert.ok(git.logCalls().at(-1).includes('--max-count=20'));
        assert.ok(!git.logCalls().at(-1).some((a) => a.includes('..')), 'no range in the fallback');
    });

    it('logs a failed lookup and still writes a stamp with the latest 20 commits', async () => {
        const fetchImpl = createFakeFetch(() => { throw new Error('connect ETIMEDOUT'); });

        const { stamp, out } = await run(AZURE_ENV, { fetchImpl });

        assert.match(out.lines.join('\n'), /Could not look up the previous deployment: connect ETIMEDOUT/);
        assert.equal(stamp.changes.sinceCommit, null);
        assert.equal(stamp.changes.truncated, true);
        assert.equal(stamp.changes.commits.length, 20);
    });

    it('adds the real reason behind "fetch failed" (the error cause) to the log, and still falls back to the latest 20 commits', async () => {
        // Node's fetch rejects with a TypeError("fetch failed") and keeps the reason (DNS, refused, reset ...) in error.cause.
        const fetchImpl = createFakeFetch(() => {
            throw new TypeError('fetch failed', { cause: new Error('getaddrinfo ENOTFOUND api.example.com') });
        });

        const { stamp, out } = await run(AZURE_ENV, { fetchImpl });

        const log = out.lines.join('\n');
        assert.match(log, /Could not look up the previous deployment: fetch failed.*ENOTFOUND api\.example\.com/);
        assert.ok(!log.includes(FAKE_AZURE_TOKEN));
        assert.match(log, /Listing the latest 20 commits instead\./);
        assert.equal(stamp.changes.sinceCommit, null);
        assert.equal(stamp.changes.commits.length, 20);
    });

    it('keeps a token out of the log when the error cause echoes it', async () => {
        const fetchImpl = createFakeFetch(() => {
            throw new TypeError('fetch failed', { cause: new Error(`tunnel failed (Authorization: Bearer ${FAKE_AZURE_TOKEN})`) });
        });

        const { out } = await run(AZURE_ENV, { fetchImpl });

        const log = out.lines.join('\n');
        assert.match(log, /fetch failed.*tunnel failed \(Authorization: Bearer \*\*\*\)/);
        assert.ok(!log.includes(FAKE_AZURE_TOKEN));
    });

    it('names the error code when the cause has no message of its own (an AggregateError)', async () => {
        const cause = Object.assign(new AggregateError([]), { code: 'ECONNREFUSED' });
        const fetchImpl = createFakeFetch(() => { throw new TypeError('fetch failed', { cause }); });

        const { out } = await run(AZURE_ENV, { fetchImpl });

        assert.match(out.lines.join('\n'), /Could not look up the previous deployment: fetch failed.*ECONNREFUSED/);
    });

    it('says why when BUILD_INFO_DEPLOY_TASK is set and no inspected run had that task succeeded, then falls back', async () => {
        // The fake builds API lists one completed run, and its timeline has no records at all: nothing qualifies.
        const { stamp, out } = await run({ ...AZURE_ENV, BUILD_INFO_DEPLOY_TASK: 'Hand over to hosting' });

        const log = out.lines.join('\n');
        assert.match(log, /None of the last 1 completed run on refs\/heads\/main had a succeeded task named 'Hand over to hosting'/);
        assert.match(log, /Listing the latest 20 commits instead\./);
        assert.ok(!log.includes(FAKE_AZURE_TOKEN));
        assert.equal(out.lines.filter((line) => line.includes('Hand over to hosting')).length, 1, 'said once');
        assert.equal(stamp.changes.sinceCommit, null);
        assert.equal(stamp.changes.truncated, true);
        assert.equal(stamp.changes.commits.length, 20);
    });

    it('falls back without calling the API when the token is missing, and says how to fix it', async () => {
        const fetchImpl = neverFetch();

        const { stamp, out } = await run({ ...AZURE_ENV, SYSTEM_ACCESSTOKEN: undefined }, { fetchImpl });

        assert.equal(fetchImpl.calls.length, 0);
        assert.match(out.lines.join('\n'), /SYSTEM_ACCESSTOKEN/);
        assert.equal(stamp.changes.commits.length, 20);
        assert.equal(stamp.changes.sinceCommit, null);
    });

    it('falls back in generic mode, which has no lookup', async () => {
        const fetchImpl = neverFetch();

        const { stamp } = await run({ BUILD_INFO_BUILD_NUMBER: 'local-1' }, { fetchImpl });

        assert.equal(fetchImpl.calls.length, 0);
        assert.equal(stamp.buildNumber, 'local-1');
        assert.equal(stamp.changes.commits.length, 20);
        assert.equal(stamp.changes.truncated, true);
        assert.equal(stamp.buildUrl, null);
        assert.equal(stamp.commitUrl, null);
    });

    it('lets BUILD_INFO_SINCE_COMMIT win on every CI without calling the API', async () => {
        for (const env of [AZURE_ENV, GITHUB_ENV, { BUILD_INFO_BUILD_NUMBER: 'local-1' }]) {
            const fetchImpl = neverFetch();
            const git = createFakeGit();

            const { stamp } = await run({ ...env, BUILD_INFO_SINCE_COMMIT: PREVIOUS }, { git, fetchImpl });

            assert.equal(fetchImpl.calls.length, 0);
            assert.equal(stamp.changes.sinceCommit, PREVIOUS);
            assert.equal(stamp.changes.sinceBuildNumber, null);
            assert.equal(stamp.changes.sinceBuildUrl, null);
            assert.ok(git.logCalls()[0].includes(`${PREVIOUS}..HEAD`));
        }
    });

    it('falls back when BUILD_INFO_SINCE_COMMIT is not in the history', async () => {
        const git = createFakeGit({ isAncestor: () => false });
        const out = capture();

        const stamp = await buildStamp({
            env: { BUILD_INFO_BUILD_NUMBER: 'local-1', BUILD_INFO_SINCE_COMMIT: PREVIOUS },
            git, fetchImpl: neverFetch(), now: FIXED_NOW, log: out.log,
        });

        assert.equal(stamp.changes.sinceCommit, null);
        assert.equal(stamp.changes.commits.length, 20);
        assert.match(out.lines.join('\n'), /not in this checkout's history/);
    });

    it('falls back when BUILD_INFO_SINCE_COMMIT is not a commit id, and never hands it to git', async () => {
        for (const since of ['--output=oops', 'main', 'abc']) {
            const git = createFakeGit();
            const fetchImpl = neverFetch();
            const out = capture();

            const stamp = await buildStamp({
                env: { ...AZURE_ENV, BUILD_INFO_SINCE_COMMIT: since },
                git, fetchImpl, now: FIXED_NOW, log: out.log,
            });

            assert.equal(stamp.changes.sinceCommit, null);
            assert.equal(stamp.changes.commits.length, 20);
            assert.match(out.lines.join('\n'), /BUILD_INFO_SINCE_COMMIT must be a commit id/);
            assert.ok(!git.calls.some((c) => c.includes(since)), `"${since}" never reaches git`);
            assert.equal(fetchImpl.calls.length, 0, 'an explicit value still replaces the lookup');
        }
    });

    it('cuts the list at 100 commits and marks it truncated', async () => {
        const git = createFakeGit({ commits: syntheticCommits(150) });

        const { stamp } = await run(AZURE_ENV, { git });

        assert.equal(stamp.changes.commits.length, 100);
        assert.equal(stamp.changes.truncated, true);
        assert.equal(stamp.changes.commits[0].sha, sha(1));
        assert.equal(stamp.changes.commits[99].sha, sha(100));
    });

    it('does not mark exactly 100 commits as truncated', async () => {
        const { stamp } = await run(AZURE_ENV, { git: createFakeGit({ commits: syntheticCommits(100) }) });

        assert.equal(stamp.changes.commits.length, 100);
        assert.equal(stamp.changes.truncated, false);
    });

    it('reports no new commits as an empty, not truncated list', async () => {
        const { stamp } = await run(AZURE_ENV, { git: createFakeGit({ commits: [] }) });

        assert.deepEqual(stamp.changes.commits, []);
        assert.equal(stamp.changes.truncated, false);
        assert.equal(stamp.changes.sinceCommit, PREVIOUS);
    });

    describe('in a shallow checkout (Review Focus 1)', () => {
        it('says to fetch the full history on GitHub Actions and on Azure DevOps, and still writes a valid stamp', async () => {
            const out = capture();

            const stamp = await buildStamp({
                env: { BUILD_INFO_BUILD_NUMBER: '7' },
                git: createFakeGit({ shallow: true, commits: syntheticCommits(1) }),
                fetchImpl: neverFetch(),
                now: FIXED_NOW,
                log: out.log,
            });

            const text = out.lines.join('\n');
            assert.match(text, /shallow/);
            assert.ok(text.includes('fetch-depth: 0'), 'names the GitHub Actions setting');
            assert.ok(text.includes('fetchDepth: 0'), 'names the Azure DevOps setting');
            assert.equal(stamp.buildNumber, '7');
            assert.equal(stamp.commit, HEAD);
            assert.equal(stamp.schemaVersion, 1);
            assert.equal(stamp.changes.truncated, true);
            assert.equal(stamp.changes.sinceCommit, null);
            assert.equal(stamp.changes.commits.length, 1);
        });

        it('does not nag about a full checkout', async () => {
            const { out } = await run(AZURE_ENV);

            assert.ok(!out.lines.join('\n').includes('fetch-depth'));
        });

        it('treats a git that cannot tell as a full checkout', async () => {
            const git = createFakeGit();
            const answering = (...args) => {
                if (args[1] === '--is-shallow-repository') throw new Error('unknown option');
                return git(...args);
            };

            const { out } = await run(AZURE_ENV, { git: answering });

            assert.ok(!out.lines.join('\n').includes('fetch-depth'));
        });
    });

    it('fails with a clear message outside a git repository', async () => {
        const git = (...args) => {
            if (args[0] === 'branch') throw new Error('git branch failed: fatal: not a git repository');
            throw new Error('git rev-parse failed: fatal: not a git repository (or any of the parent directories): .git');
        };

        await assert.rejects(
            buildStamp({ env: { BUILD_INFO_BUILD_NUMBER: '1' }, git, fetchImpl: neverFetch(), now: FIXED_NOW, log: () => {} }),
            /not a git repository/);
    });
});

// ---------------------------------------------------------------------------------------------
// Secrets
// ---------------------------------------------------------------------------------------------

describe('secrets', () => {
    const everything = (out, written = '') => `${out.lines.join('\n')}\n${written}`;

    it('keeps the Azure DevOps token out of the file and the log, although it is sent to the API', async () => {
        await withTempDir(async (directory) => {
            const file = join(directory, 'build-info.json');
            const out = capture();
            const fetchImpl = createFakeFetch((url) => (url.pathname.endsWith('/builds')
                ? { value: [{ id: 41, buildNumber: '0.0.107', sourceVersion: PREVIOUS, result: 'succeeded' }] }
                : { records: [] }));

            const code = await main(['--out', file], AZURE_ENV, { git: createFakeGit(), fetchImpl, now: FIXED_NOW, ...out });

            assert.equal(code, 0);
            assert.equal(fetchImpl.calls[0].headers.Authorization, `Bearer ${FAKE_AZURE_TOKEN}`, 'the token was really used');
            assert.ok(!everything(out, readFileSync(file, 'utf8')).includes(FAKE_AZURE_TOKEN));
        });
    });

    it('keeps the GitHub token out of the file and the log, although it is sent to the API', async () => {
        await withTempDir(async (directory) => {
            const file = join(directory, 'build-info.json');
            const out = capture();
            const fetchImpl = createFakeFetch(() => ({
                workflow_runs: [{ id: 9040, conclusion: 'success', run_number: 40, head_sha: PREVIOUS, html_url: 'https://git.example.com/acme/site/actions/runs/9040' }],
            }));

            const code = await main(['--out', file], GITHUB_ENV, { git: createFakeGit(), fetchImpl, now: FIXED_NOW, ...out });

            assert.equal(code, 0);
            assert.equal(fetchImpl.calls[0].headers.Authorization, `Bearer ${FAKE_GITHUB_TOKEN}`, 'the token was really used');
            assert.ok(!everything(out, readFileSync(file, 'utf8')).includes(FAKE_GITHUB_TOKEN));
        });
    });

    it('keeps a token out of the log even when a failing HTTP call echoes it back', async () => {
        for (const [env, token] of [[AZURE_ENV, FAKE_AZURE_TOKEN], [GITHUB_ENV, FAKE_GITHUB_TOKEN]]) {
            const out = capture();
            const echoing = createFakeFetch(() => { throw new Error(`request failed (Authorization: Bearer ${token})`); });

            await buildStamp({ env, git: createFakeGit(), fetchImpl: echoing, now: FIXED_NOW, log: out.log });

            assert.match(out.lines.join('\n'), /Could not look up the previous deployment: request failed \(Authorization: Bearer \*\*\*\)/);
            assert.ok(!out.lines.join('\n').includes(token));
        }
    });

    it('keeps a token out of the error output of main', async () => {
        const out = capture();
        const git = () => { throw new Error(`git failed: ${FAKE_GITHUB_TOKEN}`); };

        const code = await main([], { ...GITHUB_ENV }, { git, fetchImpl: neverFetch(), now: FIXED_NOW, ...out });

        assert.equal(code, 1);
        assert.ok(!out.lines.join('\n').includes(FAKE_GITHUB_TOKEN));
    });

    it('keeps the credentials of the repository URL out of the stamp', async () => {
        const env = { ...AZURE_ENV, SYSTEM_ACCESSTOKEN: undefined, BUILD_REPOSITORY_URI: 'https://x-access-token:fake-repo-secret@ci.example.com/example-org/site' };
        const out = capture();

        const stamp = await buildStamp({ env, git: createFakeGit(), fetchImpl: neverFetch(), now: FIXED_NOW, log: out.log });

        assert.ok(!JSON.stringify(stamp).includes('fake-repo-secret'));
        assert.ok(!JSON.stringify(stamp).includes('x-access-token'));
    });
});

// ---------------------------------------------------------------------------------------------
// main: arguments, files, exit codes
// ---------------------------------------------------------------------------------------------

describe('main', () => {
    const generic = { BUILD_INFO_BUILD_NUMBER: 'local-1' };
    const deps = (out, overrides = {}) => ({ git: createFakeGit(), fetchImpl: neverFetch(), now: FIXED_NOW, ...out, ...overrides });

    it('creates the folders of --out that do not exist yet (Review Focus 2)', async () => {
        await withTempDir(async (directory) => {
            const file = join(directory, 'a', 'b', 'build-info.json');
            const out = capture();

            const code = await main(['--out', file], generic, deps(out));

            assert.equal(code, 0);
            const bytes = readFileSync(file);
            assert.equal(bytes[0], 0x7b, 'plain UTF-8 starting with {, no byte order mark');
            const stamp = JSON.parse(bytes.toString('utf8'));
            assert.equal(stamp.buildNumber, 'local-1');
            assert.equal(stamp.commit, HEAD);
            assert.ok(bytes.toString('utf8').endsWith('}\n'));
        });
    });

    it('resolves --out against the working directory and writes ./build-info.json by default (Review Focus 2)', async () => {
        await withTempDir(async (directory) => {
            const originalDirectory = process.cwd();
            process.chdir(directory);
            try {
                assert.equal(await main([], generic, deps(capture())), 0);
                assert.ok(existsSync(join(directory, 'build-info.json')));

                assert.equal(await main(['--out', 'a/b/build-info.json'], generic, deps(capture())), 0);
                assert.equal(JSON.parse(readFileSync(join(directory, 'a', 'b', 'build-info.json'), 'utf8')).buildNumber, 'local-1');
            } finally {
                process.chdir(originalDirectory);
            }
        });
    });

    it('overwrites an existing stamp', async () => {
        await withTempDir(async (directory) => {
            const file = join(directory, 'build-info.json');

            await main(['--out', file], { BUILD_INFO_BUILD_NUMBER: 'first' }, deps(capture()));
            await main(['--out', file], { BUILD_INFO_BUILD_NUMBER: 'second' }, deps(capture()));

            assert.equal(JSON.parse(readFileSync(file, 'utf8')).buildNumber, 'second');
        });
    });

    it('exits with 1 and a clear message in generic mode without a build number, writing nothing', async () => {
        await withTempDir(async (directory) => {
            const file = join(directory, 'build-info.json');
            const out = capture();

            const code = await main(['--out', file], {}, deps(out));

            assert.equal(code, 1);
            assert.match(out.lines.join('\n'), /BUILD_INFO_BUILD_NUMBER is not set/);
            assert.ok(!existsSync(file));
        });
    });

    it('exits with 1 and a clear message outside a git repository, writing nothing', async () => {
        await withTempDir(async (directory) => {
            const file = join(directory, 'out', 'build-info.json');
            const out = capture();
            // GIT_DIR pointing nowhere makes git behave as it does outside a repository, wherever the temp folder is.
            const git = createGit(directory, { GIT_DIR: join(directory, 'no-such-git-dir') });

            const code = await main(['--out', file], generic, deps(out, { git }));

            assert.equal(code, 1);
            // The script's own wording, plus the fact that git's reason is carried along: git's own text is localized.
            assert.match(out.lines.join('\n'), /Cannot read the current commit: run this inside the git checkout of the project \(git rev-parse failed: .+\)/s);
            assert.ok(!existsSync(file));
        });
    });

    it('exits with 1 and the usage for an unknown argument or an --out without a path', async () => {
        for (const argv of [['--nope'], ['--out'], ['stray']]) {
            const out = capture();

            assert.equal(await main(argv, generic, deps(out)), 1);

            assert.match(out.lines.join('\n'), /Usage: node write-build-info\.mjs \[--out <path>\]/);
        }
    });

    it('summarises what it wrote without printing the whole file', async () => {
        await withTempDir(async (directory) => {
            const out = capture();

            await main(['--out', join(directory, 'build-info.json')], generic, deps(out));

            assert.ok(out.lines.length <= 5, `a short summary, not the stamp: ${out.lines.length} lines`);
            assert.match(out.lines.join('\n'), /local-1/);
        });
    });
});

// ---------------------------------------------------------------------------------------------
// Round trip: the stamp the script writes is the stamp the C# reader is tested with
// ---------------------------------------------------------------------------------------------

describe('round trip', () => {
    it('writes exactly Fixtures/script-stamp.json, which PipelineBuildReader accepts (ScriptStampTests)', async () => {
        await withTempDir(async (directory) => {
            const file = join(directory, 'build-info.json');
            const commits = [
                { sha: HEAD, author: 'A. Developer', date: '2026-11-20T10:01:00+01:00', subject: 'Add the contact form', body: '' },
                {
                    sha: 'fedcba9876543210fedcba9876543210fedcba98',
                    author: 'Dana Sample',
                    date: '2026-11-19T17:30:00+01:00',
                    subject: 'Fix the footer spacing',
                    body: 'The spacing now follows the design tokens.\n\nCo-Authored-By: Grace Example <grace@example.com>',
                },
            ];
            const env = { ...GITHUB_ENV, BUILD_INFO_TARGET: 'live' };
            const fetchImpl = createFakeFetch(() => ({
                workflow_runs: [
                    { id: 9042, conclusion: 'success', run_number: 42, head_sha: HEAD, html_url: 'https://git.example.com/acme/site/actions/runs/9042' },
                    { id: 9041, conclusion: 'success', run_number: 41, head_sha: PREVIOUS, html_url: 'https://git.example.com/acme/site/actions/runs/9041' },
                ],
            }));

            const code = await main(['--out', file], env, { git: createFakeGit({ commits }), fetchImpl, now: FIXED_NOW, ...capture() });

            assert.equal(code, 0);
            const written = JSON.parse(readFileSync(file, 'utf8'));
            const expected = fixture('script-stamp.json');
            assert.deepEqual(written, expected);
            assert.equal(JSON.stringify(written), JSON.stringify(expected), 'same field order as the documented contract');
        });
    });
});

// ---------------------------------------------------------------------------------------------
// The real git, against throwaway repositories
// ---------------------------------------------------------------------------------------------

describe('with a real git repository', () => {
    const identity = ['-c', 'user.name=Ada Example', '-c', 'user.email=ada@example.com', '-c', 'commit.gpgsign=false'];

    /** Runs git in `cwd`, with fixed commit dates so the log order is deterministic. Its chatter is swallowed. */
    const git = (cwd, date, ...args) => execFileSync('git', [...identity, ...args], {
        cwd,
        encoding: 'utf8',
        stdio: ['ignore', 'pipe', 'pipe'],
        env: { ...process.env, GIT_AUTHOR_DATE: date, GIT_COMMITTER_DATE: date },
    }).trim();

    const commit = (cwd, date, ...messages) => {
        git(cwd, date, 'commit', '--allow-empty', ...messages.flatMap((m) => ['-m', m]));
        return git(cwd, date, 'rev-parse', 'HEAD');
    };

    function createRepository(directory) {
        git(directory, '2026-11-18T07:00:00+01:00', 'init', '-b', 'main');
        const first = commit(directory, '2026-11-18T08:00:00+01:00', 'First change');
        commit(directory, '2026-11-19T09:00:00+01:00', 'Second change', 'Skip links come first.', 'Co-Authored-By: Grace Example <grace@example.com>');
        git(directory, '2026-11-19T09:30:00+01:00', 'checkout', '-b', 'side');
        commit(directory, '2026-11-19T10:00:00+01:00', 'Side change');
        git(directory, '2026-11-19T10:30:00+01:00', 'checkout', 'main');
        commit(directory, '2026-11-20T09:00:00+01:00', 'Third change');
        git(directory, '2026-11-20T10:00:00+01:00', 'merge', '--no-ff', 'side', '-m', 'Merge the side branch');
        return first;
    }

    it('lists the commits since a commit, without merges and without trailers', async () => {
        await withTempDir(async (directory) => {
            const first = createRepository(directory);

            const stamp = await buildStamp({
                env: { BUILD_INFO_BUILD_NUMBER: 'local-1', BUILD_INFO_SINCE_COMMIT: first },
                git: createGit(directory),
                fetchImpl: neverFetch(),
                now: FIXED_NOW,
                log: () => {},
            });

            assert.equal(stamp.commitMessage, 'Merge the side branch');
            assert.equal(stamp.branch, 'main');
            assert.equal(stamp.changes.sinceCommit, first);
            assert.equal(stamp.changes.truncated, false);
            assert.deepEqual(stamp.changes.commits.map((c) => c.subject), ['Third change', 'Side change', 'Second change']);
            const second = stamp.changes.commits[2];
            assert.equal(second.body, 'Skip links come first.');
            assert.equal(second.author, 'Ada Example');
            assert.equal(second.dateUtc, '2026-11-19T08:00:00.000Z');
            assert.match(second.sha, /^[0-9a-f]{40}$/);
        });
    });

    it('writes a valid stamp from a shallow clone and says to fetch the full history (Review Focus 1)', async () => {
        await withTempDir(async (directory) => {
            const origin = join(directory, 'origin');
            const clone = join(directory, 'clone');
            git(directory, '2026-11-18T07:00:00+01:00', 'init', '-b', 'main', origin);
            const first = commit(origin, '2026-11-18T08:00:00+01:00', 'First change');
            commit(origin, '2026-11-19T09:00:00+01:00', 'Second change');
            const head = commit(origin, '2026-11-20T09:00:00+01:00', 'Third change');
            // A file:// URL: a plain path would be cloned in full and ignore --depth.
            git(directory, '2026-11-20T09:30:00+01:00', 'clone', '--depth', '1', pathToFileURL(origin).href, clone);
            const out = capture();

            const stamp = await buildStamp({
                // The commit the previous deployment built is not in the shallow history: it must not crash the script.
                env: { BUILD_INFO_BUILD_NUMBER: 'local-1', BUILD_INFO_SINCE_COMMIT: first },
                git: createGit(clone),
                fetchImpl: neverFetch(),
                now: FIXED_NOW,
                log: out.log,
            });

            assert.ok(out.lines.join('\n').includes('fetch-depth: 0'));
            assert.ok(out.lines.join('\n').includes('fetchDepth: 0'));
            assert.equal(stamp.commit, head);
            assert.equal(stamp.changes.sinceCommit, null);
            assert.equal(stamp.changes.truncated, true);
            assert.deepEqual(stamp.changes.commits.map((c) => c.subject), ['Third change']);
        });
    });

    it('does not warn about a full clone', async () => {
        await withTempDir(async (directory) => {
            createRepository(directory);
            const out = capture();

            await buildStamp({ env: { BUILD_INFO_BUILD_NUMBER: '1' }, git: createGit(directory), fetchImpl: neverFetch(), now: FIXED_NOW, log: out.log });

            assert.ok(!out.lines.join('\n').includes('fetch-depth'));
        });
    });

    it('reports a clear error outside a repository through createGit', async () => {
        await withTempDir(async (directory) => {
            // LC_ALL=C: git translates its own message ("not a git repository") on a machine set to another language.
            const outside = createGit(directory, { GIT_DIR: join(directory, 'no-such-git-dir'), LC_ALL: 'C' });

            assert.throws(() => outside('rev-parse', 'HEAD'), /git rev-parse failed:.*not a git repository/is);
        });
    });
});
