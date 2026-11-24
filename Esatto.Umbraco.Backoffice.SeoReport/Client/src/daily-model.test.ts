import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from './api-error';
import { DailyModel, type DailyApi } from './daily-model';
import { chart } from './testing/fixtures';
import type { DailyResponse, DailySourcesResponse } from './types';

afterEach(() => vi.useRealTimers());

const MONTHS = ['', '2026-11', '2026-10', '2026-09'];

function response(month: string | null, windowDays = 30): DailyResponse {
  return {
    chart: chart({ id: 'daily_traffic', labels: ['2026-11-01'], series: [{ key: 'daily', values: [10], emphasis: true, style: 'solid' }] }),
    textArgs: { period: month, windowDays, provisionalDays: 3 },
    months: MONTHS,
    window: { start: '2026-10-05', end: '2026-11-03', month },
    total: 10,
    changePercent: 1.5,
    peak: { date: '2026-11-01', sessions: 10 },
    days: [{ date: '2026-11-01', sessions: 10 }],
    windowDays,
  };
}

const sources = (date: string): DailySourcesResponse => ({ date, sources: [{ channel: 'Organic Search', source: 'google', sessions: 7 }] });

// A promise that is settled by the test, so the order of the answers is the test's.
function later<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((ok, fail) => {
    resolve = ok;
    reject = fail;
  });
  return { promise, resolve, reject };
}

function setup(overrides: Partial<DailyApi> = {}) {
  const api: DailyApi = {
    getDaily: vi.fn((month?: string) => Promise.resolve(response(month ? month : null, month ? 31 : 30))),
    getDailySources: vi.fn((date: string) => Promise.resolve(sources(date))),
    ...overrides,
  };
  const changed = vi.fn();
  const model = new DailyModel(api, changed);
  return { api, changed, model };
}

const failure = (code = 'quota') => new ApiError(502, code as never);

describe('DailyModel: opening', () => {
  it('reads the rolling window once, and nothing else: no sources, no month', async () => {
    const { api, model } = setup();

    await model.connect();

    expect(api.getDaily).toHaveBeenCalledTimes(1);
    expect(api.getDaily).toHaveBeenCalledWith(undefined);
    expect(api.getDailySources).not.toHaveBeenCalled();
    expect(model.state.daily?.window.month).toBeNull();
    expect(model.state.loading).toBe(false);
    expect(model.state.months).toEqual(MONTHS);
  });

  it('is loading until the curve has come', async () => {
    const pending = later<DailyResponse>();
    const { model } = setup({ getDaily: vi.fn(() => pending.promise) });

    const done = model.connect();
    expect(model.state.loading).toBe(true);
    expect(model.state.daily).toBeNull();

    pending.resolve(response(null));
    await done;
    expect(model.state.loading).toBe(false);
  });

  it('remembers how long the rolling window is, to name it in the picker after another month is chosen', async () => {
    const { model } = setup();

    await model.connect();
    await model.selectMonth('2026-10');

    expect(model.state.rollingDays).toBe(30);
  });

  it('shows a failure as a problem, with no curve, and the picker has nothing to offer yet', async () => {
    const { model } = setup({ getDaily: vi.fn(() => Promise.reject(failure('noAccess'))) });

    await model.connect();

    expect(model.state.problem?.code).toBe('noAccess');
    expect(model.state.daily).toBeNull();
    expect(model.state.loading).toBe(false);
    expect(model.state.months).toEqual([]);
  });

  it('does not read again when the element is connected again with a curve in hand', async () => {
    const { api, model } = setup();

    await model.connect();
    model.disconnect();
    await model.connect();

    expect(api.getDaily).toHaveBeenCalledTimes(1);
  });

  it('reads again when it connects again after a failure', async () => {
    const getDaily = vi.fn().mockRejectedValueOnce(failure()).mockResolvedValue(response(null));
    const { model } = setup({ getDaily });

    await model.connect();
    model.disconnect();
    await model.connect();

    expect(getDaily).toHaveBeenCalledTimes(2);
    expect(model.state.problem).toBeNull();
  });

  it('never polls and never refreshes by itself: after the answer there is no timer and no further call', async () => {
    vi.useFakeTimers();
    const { api, model } = setup();

    await model.connect();
    await model.selectDay('2026-11-01');
    vi.advanceTimersByTime(60 * 60 * 1000);

    expect(vi.getTimerCount()).toBe(0);
    expect(api.getDaily).toHaveBeenCalledTimes(1);
    expect(api.getDailySources).toHaveBeenCalledTimes(1);
  });
});

describe('DailyModel: choosing a month', () => {
  it('reads that month and shows it, and the picker stands on it while it is read', async () => {
    const pending = later<DailyResponse>();
    const getDaily = vi.fn().mockResolvedValueOnce(response(null)).mockReturnValueOnce(pending.promise);
    const { model } = setup({ getDaily });
    await model.connect();

    const done = model.selectMonth('2026-10');
    expect(model.state.pendingMonth).toBe('2026-10');
    expect(model.state.loading).toBe(true);
    expect(model.state.daily).toBeNull();
    expect(model.state.months).toEqual(MONTHS);

    pending.resolve(response('2026-10', 31));
    await done;
    expect(getDaily).toHaveBeenLastCalledWith('2026-10');
    expect(model.state.daily?.window.month).toBe('2026-10');
    expect(model.state.pendingMonth).toBeNull();
  });

  it('asks for the rolling window with no month, not with an empty one', async () => {
    const { api, model } = setup();
    await model.connect();
    await model.selectMonth('2026-10');

    await model.selectMonth('');

    expect(api.getDaily).toHaveBeenLastCalledWith(undefined);
    expect(model.state.daily?.window.month).toBeNull();
  });

  it('drops the day that was picked, with its sources: they belong to the other window', async () => {
    const { model } = setup();
    await model.connect();
    await model.selectDay('2026-11-01');
    expect(model.state.sources?.date).toBe('2026-11-01');

    await model.selectMonth('2026-10');

    expect(model.state.day).toBeNull();
    expect(model.state.sources).toBeNull();
  });

  it('ignores the month that is already shown, and a month being read already', async () => {
    const { api, model } = setup();
    await model.connect();

    await model.selectMonth('');
    expect(api.getDaily).toHaveBeenCalledTimes(1);

    const pending = later<DailyResponse>();
    (api.getDaily as ReturnType<typeof vi.fn>).mockReturnValueOnce(pending.promise);
    const first = model.selectMonth('2026-10');
    await model.selectMonth('2026-10');
    expect(api.getDaily).toHaveBeenCalledTimes(2);

    pending.resolve(response('2026-10', 31));
    await first;
  });

  it('drops a slower older answer: the month chosen last wins', async () => {
    const slow = later<DailyResponse>();
    const fast = later<DailyResponse>();
    const getDaily = vi.fn().mockResolvedValueOnce(response(null)).mockReturnValueOnce(slow.promise).mockReturnValueOnce(fast.promise);
    const { model } = setup({ getDaily });
    await model.connect();

    const older = model.selectMonth('2026-10');
    const newer = model.selectMonth('2026-09');
    fast.resolve(response('2026-09', 30));
    await newer;
    slow.resolve(response('2026-10', 31));
    await older;

    expect(model.state.daily?.window.month).toBe('2026-09');
    expect(model.state.loading).toBe(false);
  });

  it('shows a failed read as a problem and keeps the picker on the month that failed, so Try again repeats it', async () => {
    const getDaily = vi.fn().mockResolvedValueOnce(response(null)).mockRejectedValueOnce(failure('quota')).mockResolvedValueOnce(response('2026-10', 31));
    const { model } = setup({ getDaily });
    await model.connect();

    await model.selectMonth('2026-10');
    expect(model.state.problem?.code).toBe('quota');
    expect(model.state.daily).toBeNull();
    expect(model.state.pendingMonth).toBe('2026-10');
    expect(model.state.months).toEqual(MONTHS);

    await model.retry();
    expect(getDaily).toHaveBeenLastCalledWith('2026-10');
    expect(model.state.problem).toBeNull();
    expect(model.state.daily?.window.month).toBe('2026-10');
    expect(model.state.pendingMonth).toBeNull();
  });

  it('turns anything that is not an ApiError into one, so the card always has a code to write', async () => {
    const getDaily = vi.fn().mockResolvedValueOnce(response(null)).mockRejectedValueOnce(new TypeError('Failed to fetch'));
    const { model } = setup({ getDaily });
    await model.connect();

    await model.selectMonth('2026-10');

    expect(model.state.problem?.code).toBe('unknown');
  });
});

describe('DailyModel: choosing a day', () => {
  it('reads where that day came from, and only that day', async () => {
    const { api, model } = setup();
    await model.connect();

    await model.selectDay('2026-11-01');

    expect(api.getDailySources).toHaveBeenCalledTimes(1);
    expect(api.getDailySources).toHaveBeenCalledWith('2026-11-01');
    expect(model.state.day).toBe('2026-11-01');
    expect(model.state.sources?.sources[0].source).toBe('google');
    expect(model.state.sourcesLoading).toBe(false);
  });

  it('is loading, with the day set and no sources, until the answer comes', async () => {
    const pending = later<DailySourcesResponse>();
    const { model } = setup({ getDailySources: vi.fn(() => pending.promise) });
    await model.connect();

    const done = model.selectDay('2026-11-01');
    expect(model.state.day).toBe('2026-11-01');
    expect(model.state.sourcesLoading).toBe(true);
    expect(model.state.sources).toBeNull();

    pending.resolve(sources('2026-11-01'));
    await done;
    expect(model.state.sourcesLoading).toBe(false);
  });

  it('drops a slower answer for a day that is no longer the one picked', async () => {
    const slow = later<DailySourcesResponse>();
    const fast = later<DailySourcesResponse>();
    const getDailySources = vi.fn().mockReturnValueOnce(slow.promise).mockReturnValueOnce(fast.promise);
    const { model } = setup({ getDailySources });
    await model.connect();

    const older = model.selectDay('2026-11-01');
    const newer = model.selectDay('2026-11-02');
    fast.resolve(sources('2026-11-02'));
    await newer;
    slow.resolve(sources('2026-11-01'));
    await older;

    expect(model.state.day).toBe('2026-11-02');
    expect(model.state.sources?.date).toBe('2026-11-02');
  });

  it('clears the day when the empty choice is made, without asking Google', async () => {
    const { api, model } = setup();
    await model.connect();
    await model.selectDay('2026-11-01');

    await model.selectDay(null);

    expect(model.state.day).toBeNull();
    expect(model.state.sources).toBeNull();
    expect(api.getDailySources).toHaveBeenCalledTimes(1);
  });

  it('shows a failed read inline, beside the curve that is still there, and Try again repeats only that read', async () => {
    const getDailySources = vi.fn().mockRejectedValueOnce(failure('noAccess')).mockResolvedValueOnce(sources('2026-11-01'));
    const { api, model } = setup({ getDailySources });
    await model.connect();

    await model.selectDay('2026-11-01');
    expect(model.state.sourcesProblem?.code).toBe('noAccess');
    expect(model.state.problem).toBeNull();
    expect(model.state.daily).not.toBeNull();

    await model.retry();
    expect(getDailySources).toHaveBeenCalledTimes(2);
    expect(api.getDaily).toHaveBeenCalledTimes(1);
    expect(model.state.sourcesProblem).toBeNull();
    expect(model.state.sources?.date).toBe('2026-11-01');
  });

  it('forgets an earlier failure when another day is picked', async () => {
    const getDailySources = vi.fn().mockRejectedValueOnce(failure()).mockResolvedValueOnce(sources('2026-11-02'));
    const { model } = setup({ getDailySources });
    await model.connect();
    await model.selectDay('2026-11-01');

    await model.selectDay('2026-11-02');

    expect(model.state.sourcesProblem).toBeNull();
  });
});

describe('DailyModel: the element has gone', () => {
  it('touches no state and tells nobody when a curve arrives after the element left', async () => {
    const pending = later<DailyResponse>();
    const { model, changed } = setup({ getDaily: vi.fn(() => pending.promise) });

    const done = model.connect();
    model.disconnect();
    const calls = changed.mock.calls.length;
    pending.resolve(response(null));
    await done;

    expect(model.state.daily).toBeNull();
    expect(changed.mock.calls.length).toBe(calls);
  });

  it('does the same for a failure and for sources', async () => {
    const failing = later<DailyResponse>();
    const { model, changed } = setup({ getDaily: vi.fn(() => failing.promise) });
    const done = model.connect();
    model.disconnect();
    const calls = changed.mock.calls.length;
    failing.reject(failure());
    await done;
    expect(model.state.problem).toBeNull();
    expect(changed.mock.calls.length).toBe(calls);

    const pending = later<DailySourcesResponse>();
    const other = setup({ getDailySources: vi.fn(() => pending.promise) });
    await other.model.connect();
    const picked = other.model.selectDay('2026-11-01');
    other.model.disconnect();
    const after = other.changed.mock.calls.length;
    pending.resolve(sources('2026-11-01'));
    await picked;
    expect(other.model.state.sources).toBeNull();
    expect(other.changed.mock.calls.length).toBe(after);
  });

  it('does nothing when asked to disconnect twice or before it connected', () => {
    const { model } = setup();

    model.disconnect();
    model.disconnect();

    expect(model.state.loading).toBe(false);
  });
});

describe('DailyModel: telling the element', () => {
  it('says so each time its state changes', async () => {
    const { model, changed } = setup();

    await model.connect();

    expect(changed).toHaveBeenCalled();
  });
});
