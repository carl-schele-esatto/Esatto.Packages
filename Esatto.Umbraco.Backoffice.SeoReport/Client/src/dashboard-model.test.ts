import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './api-error';
import { deferred, finished, response, running, setup } from './testing/dashboard-harness';
import { fetchStatus, status } from './testing/fixtures';
import type { FetchStatus, ReportResponse, StartFetchResponse, StatusResponse } from './types';
import { progressOf, viewStateOf } from './view-state';

// The shell's behaviour without a browser: what it reads, when it follows a backfill, and what it does when the element is gone.

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('loading', () => {
  it('reads the status and the newest report, and nothing that goes to Google', async () => {
    const { api, model } = setup();

    await model.connect();

    expect(api.getStatus).toHaveBeenCalledTimes(1);
    expect(api.getReport).toHaveBeenCalledWith(undefined);
    expect(api.refreshReport).not.toHaveBeenCalled();
    expect(api.startFetch).not.toHaveBeenCalled();
    expect(model.state.loading).toBe(false);
    expect(model.state.report?.report_period).toBe('2026-10');
    expect(model.state.period).toBe('2026-10');
    expect(model.state.fetchedUtc).toBe('2026-11-02T04:00:00Z');
  });

  it('reads no report while the package is not set up, or when nothing is archived', async () => {
    for (const next of [status({ setup: 'notConfigured' }), status({ periods: [], upToDate: false })]) {
      const { api, model } = setup();
      api.getStatus.mockResolvedValue(next);

      await model.connect();

      expect(api.getReport).not.toHaveBeenCalled();
      expect(model.state.report).toBeNull();
      expect(model.state.readFailed).toBe(false);
    }
  });

  it('follows a backfill that is already running when the dashboard opens', async () => {
    const { api, model } = setup();
    api.getStatus.mockResolvedValue(status({ fetch: running() }));

    await model.connect();
    await vi.advanceTimersByTimeAsync(2000);

    expect(api.getFetchProgress).toHaveBeenCalledTimes(1);
  });

  it('shows a status that could not be read as a problem, with Try again reading it again', async () => {
    const { api, model } = setup();
    api.getStatus.mockRejectedValueOnce(new ApiError(0, 'unknown'));

    await model.connect();

    expect(model.state.problem?.code).toBe('unknown');
    expect(model.state.status).toBeNull();
    expect(model.state.loading).toBe(false);

    model.retry();
    await vi.advanceTimersByTimeAsync(0);

    expect(model.state.problem).toBeNull();
    expect(model.state.status).not.toBeNull();
  });

  it('drops the answer of a load that a newer load has replaced', async () => {
    const { api, model } = setup();
    const slow = deferred<StatusResponse>();
    api.getStatus.mockReturnValueOnce(slow.promise);

    const first = model.connect();
    api.getStatus.mockResolvedValueOnce(status({ displayName: 'the newer one' }));
    await model.load();
    slow.resolve(status({ displayName: 'the older one' }));
    await first;

    expect(model.state.status?.displayName).toBe('the newer one');
  });
});

// I1: a request that answers after the element has left must not bring a poller back to life or touch the state.
describe('when the element is gone', () => {
  it('does not start polling when the status answers after it left, even if the status says a run is going', async () => {
    const { api, model } = setup();
    const status$ = deferred<StatusResponse>();
    api.getStatus.mockReturnValueOnce(status$.promise);

    const loading = model.connect();
    model.disconnect();
    status$.resolve(status({ fetch: running() }));
    await loading;
    await vi.advanceTimersByTimeAsync(60_000);

    expect(api.getFetchProgress).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
    expect(model.state.status).toBeNull();
  });

  it('does not start polling when the answer to Fetch now arrives after it left', async () => {
    const { api, model } = setup();
    await model.connect();
    const started = deferred<StartFetchResponse>();
    api.startFetch.mockReturnValueOnce(started.promise);

    const starting = model.fetchNow();
    model.disconnect();
    started.resolve({ result: 'started', progress: running(0, 0) });
    await starting;
    await vi.advanceTimersByTimeAsync(60_000);

    expect(api.getFetchProgress).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
  });

  it('stops a poller that is going: nothing is read or scheduled after it left', async () => {
    const { api, model } = setup();
    api.getStatus.mockResolvedValue(status({ fetch: running() }));
    await model.connect();
    await vi.advanceTimersByTimeAsync(2000);
    expect(api.getFetchProgress).toHaveBeenCalledTimes(1);

    model.disconnect();
    await vi.advanceTimersByTimeAsync(60_000);

    expect(api.getFetchProgress).toHaveBeenCalledTimes(1);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('ignores the answer of a read of the progress that was on its way when it left', async () => {
    const { api, model, changed } = setup();
    api.getStatus.mockResolvedValue(status({ fetch: running() }));
    await model.connect();
    const progress = deferred<FetchStatus>();
    api.getFetchProgress.mockReturnValueOnce(progress.promise);
    await vi.advanceTimersByTimeAsync(2000);
    model.disconnect();
    const before = changed.mock.calls.length;

    progress.resolve(finished());
    await vi.advanceTimersByTimeAsync(60_000);

    expect(changed.mock.calls.length).toBe(before);
    expect(api.getStatus).toHaveBeenCalledTimes(1); // the end of the run was not acted on
  });

  it('does not touch the state with a refresh that answers after it left', async () => {
    const { api, model } = setup();
    await model.connect();
    const refreshing = deferred<ReportResponse>();
    api.refreshReport.mockReturnValueOnce(refreshing.promise);

    const refresh = model.refresh();
    model.disconnect();
    refreshing.resolve(response('2026-10'));
    await refresh;

    expect(api.getStatus).toHaveBeenCalledTimes(1); // no read of the status after the refresh
  });

  it('works again when the same element is connected again: one poller, one read per interval', async () => {
    const { api, model } = setup();
    api.getStatus.mockResolvedValue(status({ fetch: running() }));
    await model.connect();
    model.disconnect();

    await model.connect();
    await vi.advanceTimersByTimeAsync(2000);
    expect(api.getFetchProgress).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(2000);
    expect(api.getFetchProgress).toHaveBeenCalledTimes(2);
    expect(vi.getTimerCount()).toBe(1);
  });

  it('can be disconnected twice and before it was ever connected', () => {
    const { model } = setup();

    expect(() => {
      model.disconnect();
      model.disconnect();
    }).not.toThrow();
  });
});

// Minor 2: every fresh status goes through one place, which starts the poller when it says a run is going.
describe('a fresh status that says a run is going', () => {
  it('starts the poller when a refresh reads it', async () => {
    const { api, model } = setup();
    await model.connect();
    api.refreshReport.mockResolvedValue(response('2026-10'));
    api.getStatus.mockResolvedValue(status({ fetch: running() }));

    await model.refresh();
    await vi.advanceTimersByTimeAsync(2000);

    expect(api.getFetchProgress).toHaveBeenCalledTimes(1);
  });

  it('keeps polling when the end of one run is followed by a status that says another has started', async () => {
    const { api, model } = setup();
    api.getStatus.mockResolvedValueOnce(status({ fetch: running() }));
    await model.connect();

    api.getFetchProgress.mockResolvedValueOnce(finished());
    api.getStatus.mockResolvedValueOnce(status({ fetch: running(0, 3) }));
    await vi.advanceTimersByTimeAsync(2000);
    api.getFetchProgress.mockClear();
    api.getFetchProgress.mockResolvedValue(running(1, 3));
    await vi.advanceTimersByTimeAsync(2000);

    expect(api.getFetchProgress).toHaveBeenCalledTimes(1);
  });
});

// I2: a failed read of the report is an error, not "no report".
describe('when the report cannot be read', () => {
  it('is a problem and the error state, with the report left empty and the month the picker stands on', async () => {
    const { api, model } = setup();
    api.getReport.mockRejectedValueOnce(new ApiError(500, 'unknown', undefined, 'trace-9'));

    await model.connect();

    expect(model.state.problem?.traceId).toBe('trace-9');
    expect(model.state.report).toBeNull();
    expect(model.state.readFailed).toBe(true);
    expect(model.state.loading).toBe(false);
    expect(viewStateOf(model.state.status!, model.state.report, model.state.readFailed)).toBe('error');
  });

  it('is the error state whether or not the archive is up to date, never "nothing fetched yet"', async () => {
    for (const upToDate of [true, false]) {
      const { api, model } = setup();
      api.getStatus.mockResolvedValue(status({ upToDate }));
      api.getReport.mockRejectedValueOnce(new Error('boom'));

      await model.connect();

      expect(viewStateOf(model.state.status!, model.state.report, model.state.readFailed), `upToDate ${upToDate}`).toBe('error');
    }
  });

  it('is mended by Try again, which reads everything again', async () => {
    const { api, model } = setup();
    api.getReport.mockRejectedValueOnce(new Error('boom'));
    await model.connect();

    model.retry();
    await vi.advanceTimersByTimeAsync(0);

    expect(model.state.problem).toBeNull();
    expect(model.state.readFailed).toBe(false);
    expect(model.state.report?.report_period).toBe('2026-10');
  });

  it('is shown while a run is going as the progress card with the problem beside it', async () => {
    const { api, model } = setup();
    api.getStatus.mockResolvedValue(status({ fetch: running() }));
    api.getReport.mockRejectedValueOnce(new Error('boom'));

    await model.connect();

    expect(model.state.problem).not.toBeNull();
    expect(viewStateOf(model.state.status!, model.state.report, model.state.readFailed)).toBe('progress');
  });

  it('does not hide a report that is already shown: a failed switch keeps the old month and the picker on it', async () => {
    const { api, model } = setup();
    await model.connect();
    api.getReport.mockRejectedValueOnce(new ApiError(502, 'unknown'));

    await model.selectPeriod('2026-09');

    expect(model.state.report?.report_period).toBe('2026-10');
    expect(model.state.period).toBe('2026-10');
    expect(model.state.pendingPeriod).toBeNull();
    expect(model.state.readFailed).toBe(false);
    expect(model.state.problem?.status).toBe(502);
    expect(model.state.switching).toBe(false);
  });
});

// I3: the month picker is not disabled while it works (it would lose the keyboard's place); it ignores input instead.
describe('choosing a month', () => {
  it('reads that month, stands on it while it reads, and then makes it the selected one', async () => {
    const { api, model } = setup();
    await model.connect();
    const read = deferred<ReportResponse>();
    api.getReport.mockReturnValueOnce(read.promise);

    const switching = model.selectPeriod('2026-09');
    expect(model.state.switching).toBe(true);
    expect(model.state.pendingPeriod).toBe('2026-09');

    read.resolve(response('2026-09'));
    await switching;

    expect(model.state.period).toBe('2026-09');
    expect(model.state.report?.report_period).toBe('2026-09');
    expect(model.state.pendingPeriod).toBeNull();
    expect(model.state.switching).toBe(false);
  });

  it('ignores a second choice while the first is being read, and the month that is already selected', async () => {
    const { api, model } = setup();
    await model.connect();
    api.getReport.mockClear();
    const read = deferred<ReportResponse>();
    api.getReport.mockReturnValueOnce(read.promise);

    const first = model.selectPeriod('2026-09');
    await model.selectPeriod('2026-08');
    await model.selectPeriod('2026-09');
    read.resolve(response('2026-09'));
    await first;
    await model.selectPeriod('2026-09');

    expect(api.getReport).toHaveBeenCalledTimes(1);
    expect(api.getReport).toHaveBeenCalledWith('2026-09');
  });

  it('ignores a month that is not archived', async () => {
    const { api, model } = setup();
    await model.connect();
    api.getReport.mockClear();

    await model.selectPeriod('2020-01');

    expect(api.getReport).not.toHaveBeenCalled();
  });

  it('is ignored while a month is being refreshed', async () => {
    const { api, model } = setup();
    await model.connect();
    api.getReport.mockClear();
    api.refreshReport.mockReturnValueOnce(deferred<ReportResponse>().promise);

    void model.refresh();
    await model.selectPeriod('2026-09');

    expect(api.getReport).not.toHaveBeenCalled();
  });
});

describe('Refresh', () => {
  it('fetches the selected month, shows it, and reads the status again', async () => {
    const { api, model } = setup();
    await model.connect();
    api.refreshReport.mockResolvedValue({ ...response('2026-10'), note: 'a note' });

    await model.refresh();

    expect(api.refreshReport).toHaveBeenCalledWith('2026-10');
    expect(model.state.note).toBe('a note');
    expect(api.getStatus).toHaveBeenCalledTimes(2);
    expect(model.state.refreshing).toBe(false);
  });

  it('is ignored while it is already fetching', async () => {
    const { api, model } = setup();
    await model.connect();
    api.refreshReport.mockReturnValue(deferred<ReportResponse>().promise);

    void model.refresh();
    await model.refresh();

    expect(api.refreshReport).toHaveBeenCalledTimes(1);
    expect(model.state.refreshing).toBe(true);
  });

  it('shows what went wrong, and Try again fetches the same month again', async () => {
    const { api, model } = setup();
    await model.connect();
    api.refreshReport.mockRejectedValueOnce(new ApiError(502, 'quota'));

    await model.refresh();
    expect(model.state.problem?.code).toBe('quota');
    expect(model.state.refreshing).toBe(false);

    api.refreshReport.mockResolvedValue(response('2026-10'));
    model.retry();
    await vi.advanceTimersByTimeAsync(0);

    expect(api.refreshReport).toHaveBeenCalledTimes(2);
    expect(model.state.problem).toBeNull();
  });

  it('can fetch the newest month when no report could be read, so the control that mends it works', async () => {
    const { api, model } = setup();
    api.getReport.mockRejectedValueOnce(new Error('boom'));
    await model.connect();
    api.refreshReport.mockResolvedValue(response('2026-10'));

    await model.refresh();

    expect(api.refreshReport).toHaveBeenCalledWith('2026-10');
    expect(model.state.report?.report_period).toBe('2026-10');
    expect(viewStateOf(model.state.status!, model.state.report, model.state.readFailed)).toBe('ready');
  });
});

describe('Fetch now', () => {
  it('shows the start, follows the run by polling, and reads the archive again when it ends', async () => {
    const { api, model } = setup();
    api.getStatus.mockResolvedValueOnce(status({ upToDate: false, periods: [] }));
    await model.connect();
    api.startFetch.mockResolvedValue({ result: 'started', progress: running(0, 0) });

    await model.fetchNow();
    expect(model.state.status?.fetch).toEqual(running(0, 0));
    expect(model.state.starting).toBe(false);

    api.getFetchProgress.mockResolvedValueOnce(running(3, 12)).mockResolvedValueOnce(finished());
    api.getStatus.mockResolvedValue(status());
    await vi.advanceTimersByTimeAsync(2000);
    expect(model.state.status?.fetch).toEqual(running(3, 12));

    await vi.advanceTimersByTimeAsync(2000);
    expect(model.state.outcome).toEqual({ kind: 'fetched', done: 12, errorCode: null });
    expect(model.state.report?.report_period).toBe('2026-10');
    expect(api.getReport).toHaveBeenCalled();
  });

  it('does not read the start as "nothing to fetch": a run that is starting counts no months yet', async () => {
    const { api, model } = setup();
    await model.connect();
    api.startFetch.mockResolvedValue({ result: 'started', progress: running(0, 0) });

    await model.fetchNow();

    expect(model.state.outcome).toBeNull();
    expect(viewStateOf(model.state.status!, null)).toBe('progress');
  });

  it('says there was nothing to fetch when the run is over at the first read and counted no months', async () => {
    const { api, model } = setup();
    await model.connect();
    api.startFetch.mockResolvedValue({ result: 'started', progress: running(0, 0) });
    api.getFetchProgress.mockResolvedValue(finished(0, 0));

    await model.fetchNow();
    await vi.advanceTimersByTimeAsync(2000);

    expect(model.state.outcome).toEqual({ kind: 'nothingToFetch', done: 0, errorCode: null });
  });

  it('handles a run that has already ended in the answer to the start, without polling', async () => {
    const { api, model } = setup();
    await model.connect();
    api.startFetch.mockResolvedValue({ result: 'started', progress: finished(0, 0) });

    await model.fetchNow();
    await vi.advanceTimersByTimeAsync(60_000);

    expect(model.state.outcome?.kind).toBe('nothingToFetch');
    expect(api.getFetchProgress).not.toHaveBeenCalled();
  });

  it('says how a run that failed ended', async () => {
    const { api, model } = setup();
    await model.connect();
    api.startFetch.mockResolvedValue({ result: 'started', progress: running(0, 0) });
    api.getFetchProgress.mockResolvedValue(fetchStatus({ running: false, done: 4, total: 12, errorCode: 'quota' }));

    await model.fetchNow();
    await vi.advanceTimersByTimeAsync(2000);

    expect(model.state.outcome).toEqual({ kind: 'failed', done: 4, errorCode: 'quota' });
  });

  it('follows a run somebody else started instead of reporting a failure', async () => {
    const { api, model } = setup();
    await model.connect();
    api.startFetch.mockRejectedValue(new ApiError(409, 'alreadyRunning'));

    await model.fetchNow();
    await vi.advanceTimersByTimeAsync(2000);

    expect(model.state.problem).toBeNull();
    expect(model.state.status?.fetch.running).toBe(true);
    expect(api.getFetchProgress).toHaveBeenCalledTimes(1);
  });

  it('shows any other refusal as a problem, and Try again asks again', async () => {
    const { api, model } = setup();
    await model.connect();
    api.startFetch.mockRejectedValueOnce(new ApiError(409, 'notConfigured'));

    await model.fetchNow();
    expect(model.state.problem?.code).toBe('notConfigured');

    api.startFetch.mockResolvedValue({ result: 'started', progress: running(0, 0) });
    model.retry();
    await vi.advanceTimersByTimeAsync(0);

    expect(api.startFetch).toHaveBeenCalledTimes(2);
    expect(model.state.problem).toBeNull();
  });

  it('is ignored while a start is on its way', async () => {
    const { api, model } = setup();
    await model.connect();
    api.startFetch.mockReturnValue(deferred<StartFetchResponse>().promise);

    void model.fetchNow();
    await model.fetchNow();

    expect(api.startFetch).toHaveBeenCalledTimes(1);
  });

  it('says contact was lost when the progress cannot be read many times in a row, and Try again follows it again', async () => {
    const { api, model } = setup({ maxFailures: 2 });
    await model.connect();
    api.startFetch.mockResolvedValue({ result: 'started', progress: running(0, 0) });
    api.getFetchProgress.mockRejectedValue(new Error('offline'));
    await model.fetchNow();

    await vi.advanceTimersByTimeAsync(2000 + 4000);
    expect(model.state.lost).toBe(true);

    api.getFetchProgress.mockClear();
    api.getFetchProgress.mockResolvedValue(running(1, 12));
    model.followAgain();
    expect(model.state.lost).toBe(false);
    await vi.advanceTimersByTimeAsync(2000);

    expect(api.getFetchProgress).toHaveBeenCalledTimes(1);
  });

  it('lets the outcome be dismissed', async () => {
    const { api, model } = setup();
    await model.connect();
    api.startFetch.mockResolvedValue({ result: 'started', progress: finished(0, 0) });
    await model.fetchNow();

    model.dismissOutcome();

    expect(model.state.outcome).toBeNull();
  });
});

// The first connect: the archive is empty and a backfill of thirteen months starts, newest first. The newest month is shown as soon as it is
// archived, under the progress card, while the older months load.
describe('the newest month during a first backfill', () => {
  async function firstBackfill() {
    const harness = setup();
    const { api, model } = harness;
    api.getStatus.mockResolvedValueOnce(status({ upToDate: false, periods: [] }));
    await model.connect();
    api.startFetch.mockResolvedValue({ result: 'started', progress: running(0, 0) });
    await model.fetchNow();
    api.getReport.mockClear();
    return harness;
  }

  it('is not read while no month is done yet', async () => {
    const { api, model } = await firstBackfill();
    api.getFetchProgress.mockResolvedValue(running(0, 13));

    await vi.advanceTimersByTimeAsync(2000 * 3);

    expect(api.getReport).not.toHaveBeenCalled();
    expect(model.state.report).toBeNull();
  });

  it('is read once, as soon as the first month is done, and shown under the progress card with its month on the picker', async () => {
    const { api, model } = await firstBackfill();
    api.getFetchProgress.mockResolvedValueOnce(running(0, 13)).mockResolvedValueOnce(running(1, 13)).mockResolvedValue(running(4, 13));

    await vi.advanceTimersByTimeAsync(2000 * 2);
    expect(api.getReport).toHaveBeenCalledTimes(1);
    expect(api.getReport).toHaveBeenCalledWith(undefined);

    await vi.advanceTimersByTimeAsync(2000 * 3);
    expect(api.getReport).toHaveBeenCalledTimes(1);
    expect(model.state.report?.report_period).toBe('2026-10');
    expect(model.state.status?.periods).toEqual(['2026-10']);
    expect(model.state.status?.fetch.running).toBe(true);
    expect(viewStateOf(model.state.status!, model.state.report, model.state.readFailed)).toBe('progress');
  });

  it('is read once even when the run moves on while the read is on its way', async () => {
    const { api, model } = await firstBackfill();
    const pending = deferred<ReportResponse>();
    api.getReport.mockReturnValue(pending.promise);
    api.getFetchProgress.mockResolvedValueOnce(running(1, 13)).mockResolvedValueOnce(running(2, 13)).mockResolvedValue(running(3, 13));

    await vi.advanceTimersByTimeAsync(2000 * 3);
    pending.resolve(response('2026-10'));
    await vi.advanceTimersByTimeAsync(0);

    expect(api.getReport).toHaveBeenCalledTimes(1);
    expect(model.state.report?.report_period).toBe('2026-10');
  });

  it('is not read when a report is already shown', async () => {
    const { api, model } = setup();
    await model.connect();
    api.startFetch.mockResolvedValue({ result: 'started', progress: running(0, 0) });
    await model.fetchNow();
    api.getReport.mockClear();
    api.getFetchProgress.mockResolvedValue(running(1, 13));

    await vi.advanceTimersByTimeAsync(2000 * 2);

    expect(api.getReport).not.toHaveBeenCalled();
  });

  it('drops an answer that arrives after the element left', async () => {
    const { api, model } = await firstBackfill();
    const pending = deferred<ReportResponse>();
    api.getReport.mockReturnValue(pending.promise);
    api.getFetchProgress.mockResolvedValue(running(1, 13));
    await vi.advanceTimersByTimeAsync(2000);
    expect(api.getReport).toHaveBeenCalledTimes(1);

    model.disconnect();
    pending.resolve(response('2026-10'));
    await vi.advanceTimersByTimeAsync(0);

    expect(model.state.report).toBeNull();
  });

  it('that cannot be read leaves the run going: the progress card stays with the problem beside it, never the error state', async () => {
    const { api, model } = await firstBackfill();
    api.getReport.mockRejectedValueOnce(new ApiError(500, 'unknown'));
    api.getFetchProgress.mockResolvedValueOnce(running(1, 13)).mockResolvedValueOnce(running(2, 13)).mockResolvedValueOnce(finished(13, 13));
    api.getStatus.mockResolvedValue(status());

    await vi.advanceTimersByTimeAsync(2000);
    expect(model.state.problem?.code).toBe('unknown');
    expect(model.state.readFailed).toBe(false);
    expect(viewStateOf(model.state.status!, model.state.report, model.state.readFailed)).toBe('progress');

    // Not read again on every poll after it failed: the run's end reads the archive again anyway.
    await vi.advanceTimersByTimeAsync(2000);
    expect(api.getReport).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(2000);
    expect(api.getFetchProgress).toHaveBeenCalledTimes(3);
    expect(model.state.outcome?.kind).toBe('fetched');
    expect(model.state.report?.report_period).toBe('2026-10');
    // The end of the run read everything again and it worked: the early failure is not shown above a report that was read.
    expect(model.state.problem).toBeNull();
  });

  it('that cannot be read and whose re-read at the end fails too keeps the problem of that failure', async () => {
    const { api, model } = await firstBackfill();
    api.getReport.mockRejectedValue(new ApiError(500, 'unknown'));
    api.getFetchProgress.mockResolvedValueOnce(running(1, 13)).mockResolvedValueOnce(finished(13, 13));
    api.getStatus.mockResolvedValue(status());

    await vi.advanceTimersByTimeAsync(2000 * 2);

    expect(model.state.report).toBeNull();
    expect(model.state.problem?.code).toBe('unknown');
    expect(model.state.readFailed).toBe(true);
  });
});

// A first backfill archived nothing and the status read at its end failed: the error state, with the problem. Try again then finds an empty archive.
describe('a read that failed and then succeeded', () => {
  it('after Try again is the card with Fetch now, never an error state with nothing in it', async () => {
    const { api, model } = setup();
    api.getStatus.mockResolvedValueOnce(status({ upToDate: false, periods: [] }));
    await model.connect();
    api.startFetch.mockResolvedValue({ result: 'started', progress: running(0, 0) });
    await model.fetchNow();
    api.getFetchProgress.mockResolvedValueOnce(fetchStatus({ running: false, done: 0, total: 13, errorCode: 'noAccess' }));
    api.getStatus.mockRejectedValueOnce(new ApiError(0, 'unknown'));
    await vi.advanceTimersByTimeAsync(2000);
    expect(model.state.readFailed).toBe(true);
    expect(viewStateOf(model.state.status!, model.state.report, model.state.readFailed)).toBe('error');

    api.getStatus.mockResolvedValue(status({ upToDate: false, periods: [], fetch: fetchStatus({ errorCode: 'noAccess' }) }));
    model.retry();
    await vi.advanceTimersByTimeAsync(0);

    expect(model.state.problem).toBeNull();
    expect(model.state.readFailed).toBe(false);
    expect(viewStateOf(model.state.status!, model.state.report, model.state.readFailed)).toBe('progress');
    expect(progressOf(model.state.status!.fetch).phase).toBe('idle');
  });

  it('when the Settings tab reads the status again is no longer the error state', async () => {
    const { api, model } = setup();
    api.getStatus.mockResolvedValueOnce(status({ upToDate: false, periods: ['2026-10'] }));
    api.getReport.mockRejectedValueOnce(new ApiError(500, 'unknown'));
    await model.connect();
    expect(viewStateOf(model.state.status!, model.state.report, model.state.readFailed)).toBe('error');

    api.getStatus.mockResolvedValue(status({ upToDate: false, periods: [] }));
    await model.reloadStatus();

    expect(model.state.readFailed).toBe(false);
    expect(viewStateOf(model.state.status!, model.state.report, model.state.readFailed)).toBe('progress');
  });

  it('is the error state again when the read after it fails too', async () => {
    const { api, model } = setup();
    api.getReport.mockRejectedValueOnce(new ApiError(500, 'unknown')).mockRejectedValueOnce(new ApiError(500, 'unknown'));
    await model.connect();

    await model.reloadStatus();

    expect(model.state.readFailed).toBe(true);
    expect(model.state.problem?.code).toBe('unknown');
  });
});

describe('telling the element', () => {
  it('says so every time the state changes, so it can draw again', async () => {
    const { model, changed } = setup();

    await model.connect();

    expect(changed).toHaveBeenCalled();
  });
});

// The note of the shown month comes with the report and is saved by the Analysis tab: the shell keeps the note it holds in step, so that a tab
// that is opened again shows what was saved and not what the report was read with.
describe('setNote', () => {
  const withNote = (note: string | null) => async (period?: string): Promise<ReportResponse> => ({ ...response(period ?? '2026-10'), note });

  it('keeps the held note in step with a note that was saved for the shown month, and clears it when it was removed', async () => {
    const { api, model, changed } = setup();
    api.getReport.mockImplementation(withNote('the first note'));
    await model.connect();
    const before = changed.mock.calls.length;

    model.setNote('2026-10', 'a launch in October');
    expect(model.state.note).toBe('a launch in October');
    expect(changed.mock.calls.length).toBeGreaterThan(before);

    model.setNote('2026-10', null);
    expect(model.state.note).toBeNull();
  });

  it('ignores the note of another month: the server has it, and the next read of that month brings it', async () => {
    const { api, model, changed } = setup();
    api.getReport.mockImplementation(withNote('the note of the shown month'));
    await model.connect();
    const before = changed.mock.calls.length;

    model.setNote('2026-09', 'the note of September');

    expect(model.state.note).toBe('the note of the shown month');
    expect(changed.mock.calls.length).toBe(before);
  });

  it('ignores a note before any month has been read', () => {
    const { model } = setup();

    model.setNote('2026-10', 'too early');

    expect(model.state.note).toBeNull();
  });

  it('ignores a note that arrives after the element left', async () => {
    const { api, model } = setup();
    api.getReport.mockImplementation(withNote('before'));
    await model.connect();
    model.disconnect();

    model.setNote('2026-10', 'after');

    expect(model.state.note).toBe('before');
  });

  it('is replaced by the note a month comes with when another month is read, and by the note of a read of the same month', async () => {
    const { api, model } = setup();
    api.getReport.mockImplementation(withNote('first'));
    await model.connect();
    model.setNote('2026-10', 'saved');

    api.getReport.mockImplementation(withNote('another month'));
    await model.selectPeriod('2026-09');
    expect(model.state.note).toBe('another month');

    api.getReport.mockImplementation(withNote('saved'));
    await model.selectPeriod('2026-10');
    expect(model.state.note).toBe('saved');
  });
});

// A tab that is worked out from many months (the analysis) has to read again when the archive has changed under it: the month it shows can be the
// very same month, so the month alone does not tell it.
describe('archiveRevision', () => {
  it('starts at zero, and the first load and the choice of another month do not change the archive', async () => {
    const { model } = setup();

    await model.connect();
    expect(model.state.archiveRevision).toBe(0);

    await model.selectPeriod('2026-09');
    expect(model.state.archiveRevision).toBe(0);
  });

  it('counts a refresh of the month: the report of the same month has been replaced', async () => {
    const { api, model } = setup();
    await model.connect();
    api.refreshReport.mockResolvedValue(response('2026-10'));

    await model.refresh();

    expect(model.state.archiveRevision).toBe(1);
    expect(model.state.period).toBe('2026-10');
  });

  it('does not count a refresh that failed', async () => {
    const { api, model } = setup();
    await model.connect();
    api.refreshReport.mockRejectedValueOnce(new ApiError(502, 'unknown'));

    await model.refresh();

    expect(model.state.archiveRevision).toBe(0);
  });

  it('counts the end of a backfill: months have been added, and the month shown is read again', async () => {
    const { api, model } = setup();
    await model.connect();
    api.startFetch.mockResolvedValue({ result: 'started', progress: running(0, 0) });
    api.getFetchProgress.mockResolvedValueOnce(finished());

    await model.fetchNow();
    await vi.advanceTimersByTimeAsync(2000);

    expect(model.state.archiveRevision).toBe(1);
    expect(model.state.outcome?.kind).toBe('fetched');
  });
});
