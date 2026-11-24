// What the shell model's tests share: a model over stand-ins for the API, with a status, the newest report and a run's progress to read.

import { vi } from 'vitest';
import { DashboardModel, type DashboardApi } from '../dashboard-model';
import type { FetchStatus, ReportResponse, StartFetchResponse, StatusResponse } from '../types';
import { fetchStatus, report, status } from './fixtures';

export { deferred } from './deferred';

export const response = (period = '2026-10'): ReportResponse => ({
  report: report({ report_period: period }),
  note: null,
  fetchedUtc: '2026-11-02T04:00:00Z',
  period,
});

export const running = (done = 1, total = 12): FetchStatus => fetchStatus({ running: true, done, total });
export const finished = (done = 12, total = 12): FetchStatus => fetchStatus({ running: false, done, total });

export function setup(poll = {}) {
  const api = {
    getStatus: vi.fn<() => Promise<StatusResponse>>(),
    getReport: vi.fn<(period?: string) => Promise<ReportResponse>>(),
    refreshReport: vi.fn<(period: string) => Promise<ReportResponse>>(),
    startFetch: vi.fn<() => Promise<StartFetchResponse>>(),
    getFetchProgress: vi.fn<() => Promise<FetchStatus>>(),
  } satisfies DashboardApi;
  const changed = vi.fn();
  const model = new DashboardModel(api, changed, { intervalMs: 2000, maxIntervalMs: 16000, ...poll });

  api.getStatus.mockResolvedValue(status());
  api.getReport.mockImplementation(async (period) => response(period ?? '2026-10'));
  api.getFetchProgress.mockResolvedValue(running(2));

  return { api, model, changed };
}
