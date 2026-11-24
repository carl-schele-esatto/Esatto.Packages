import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './api-error';
import { deferred, finished, running, setup } from './testing/dashboard-harness';
import { status } from './testing/fixtures';
import type { StatusResponse } from './types';
import { viewStateOf } from './view-state';

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

// The Settings tab saves settings and starts runs of its own: the shell reads the status again and follows the run with its one poller, without the
// loading screen, which would take the tab (and what is being typed in it) off the page.
describe('reloadStatus', () => {
  it('reads the status again without the loading screen, and starts the poller when a run is going', async () => {
    const { api, model } = setup();
    api.getStatus.mockResolvedValueOnce(status({ setup: 'notConfigured', periods: [] }));
    await model.connect();
    api.getStatus.mockResolvedValueOnce(status({ periods: [], upToDate: false, fetch: running(0, 0), displayName: 'Example' }));
    const loading: boolean[] = [];

    const reading = model.reloadStatus();
    loading.push(model.state.loading);
    await reading;
    await vi.advanceTimersByTimeAsync(2000);

    expect(loading).toEqual([false]);
    expect(model.state.status?.displayName).toBe('Example');
    expect(viewStateOf(model.state.status!, model.state.report)).toBe('progress');
    expect(api.getFetchProgress).toHaveBeenCalledTimes(1);
  });

  it('reads the newest report when there is none yet and the archive has months', async () => {
    const { api, model } = setup();
    api.getStatus.mockResolvedValueOnce(status({ setup: 'notConfigured' }));
    await model.connect();
    expect(api.getReport).not.toHaveBeenCalled();

    await model.reloadStatus();

    expect(api.getReport).toHaveBeenCalledWith(undefined);
    expect(model.state.report?.report_period).toBe('2026-10');
  });

  it('leaves the report that is shown as it is', async () => {
    const { api, model } = setup();
    await model.connect();
    api.getReport.mockClear();

    await model.reloadStatus();

    expect(api.getReport).not.toHaveBeenCalled();
    expect(model.state.report?.report_period).toBe('2026-10');
  });

  it('shows a status that could not be read as a problem, and Try again reads it again', async () => {
    const { api, model } = setup();
    await model.connect();
    api.getStatus.mockRejectedValueOnce(new ApiError(502, 'unknown'));

    await model.reloadStatus();
    expect(model.state.problem?.status).toBe(502);
    expect(model.state.loading).toBe(false);

    model.retry();
    await vi.advanceTimersByTimeAsync(0);
    expect(model.state.problem).toBeNull();
  });

  it('touches nothing when the element has left', async () => {
    const { api, model } = setup();
    await model.connect();
    const later = deferred<StatusResponse>();
    api.getStatus.mockReturnValueOnce(later.promise);

    const reading = model.reloadStatus();
    model.disconnect();
    later.resolve(status({ fetch: running(), displayName: 'too late' }));
    await reading;
    await vi.advanceTimersByTimeAsync(60_000);

    expect(model.state.status?.displayName).toBeNull();
    expect(api.getFetchProgress).not.toHaveBeenCalled();
  });
});

describe('followRun', () => {
  it('shows a run another part of the dashboard started and follows it to its end', async () => {
    const { api, model } = setup();
    await model.connect();
    api.getFetchProgress.mockResolvedValueOnce(finished());

    await model.followRun(running(0, 0));
    expect(model.state.status?.fetch).toEqual(running(0, 0));
    await vi.advanceTimersByTimeAsync(2000);

    expect(model.state.outcome).toEqual({ kind: 'fetched', done: 12, errorCode: null });
  });

  it('follows whatever run is going when it was asked to follow one that was running already', async () => {
    const { api, model } = setup();
    await model.connect();

    await model.followRun(null);
    await vi.advanceTimersByTimeAsync(2000);

    expect(model.state.status?.fetch.running).toBe(true);
    expect(api.getFetchProgress).toHaveBeenCalledTimes(1);
  });

  it('starts nothing when the element has left', async () => {
    const { api, model } = setup();
    await model.connect();
    model.disconnect();

    await model.followRun(running(0, 0));
    await vi.advanceTimersByTimeAsync(60_000);

    expect(api.getFetchProgress).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
  });
});

// The end of a run reads the status and the month again. When that read fails, Try again must not bring back the loading screen, which would take
// the open tab off the page, and with it what is being typed on the Settings tab.
describe('a read that fails at the end of a run', () => {
  it('is tried again without the loading screen, and reads the archive the run changed', async () => {
    const { api, model, changed } = setup();
    await model.connect();
    api.getFetchProgress.mockResolvedValueOnce(finished());
    api.getStatus.mockRejectedValueOnce(new ApiError(502, 'unknown'));
    await model.followRun(running(0, 0));
    await vi.advanceTimersByTimeAsync(2000);
    expect(model.state.problem?.status).toBe(502);

    const loading: boolean[] = [];
    changed.mockImplementation(() => loading.push(model.state.loading));
    api.getReport.mockClear();
    model.retry();
    await vi.advanceTimersByTimeAsync(0);

    expect(loading).not.toContain(true);
    expect(model.state.problem).toBeNull();
    expect(api.getReport).toHaveBeenCalledWith('2026-10');
    expect(model.state.archiveRevision).toBe(1);
  });
});
