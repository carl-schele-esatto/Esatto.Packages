// The dashboard's endpoints (SeoReportController), one function each. Shapes are in types.ts.
//
// Every call returns the parsed body, or REJECTS WITH AN ApiError (api-error.ts): the status, a code the client has words for, the
// code's arguments and the trace id. A caller catches it and shows errorTexts(host, error.code, error.args); it never looks at HTTP.
//
// The calls say `throwOnError: false` themselves. The shared backoffice client is configured to throw the parsed body, which loses
// the HTTP status of an answer that has no JSON body (a 403, a gateway page); asked not to throw it hands back the response beside
// the error, so the status is always known. Only a request that got no answer at all (a network failure) rejects.

import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { toApiError } from './api-error';
import type {
  AnalysisResponse,
  ConnectionTestRequest,
  ConnectionTestResult,
  DailyResponse,
  DailySourcesResponse,
  FetchStatus,
  NoAnalysisResponse,
  NoteResponse,
  PutSettingsRequest,
  ReportResponse,
  SettingsResponse,
  StartFetchResponse,
  StatusResponse,
  TargetsResponse,
  UserGroupItem,
} from './types';

export { ApiError, isApiError } from './api-error';

export const BASE = '/umbraco/management/api/v1/esatto/seo-report';

// `security: [bearer]` makes the shared backoffice client authenticate the request (the same as SEO Health).
const security = () => [{ scheme: 'bearer' as const, type: 'http' as const }];
const json = { 'Content-Type': 'application/json' };

// The part of the client's answer this file reads. With throwOnError: false a success has `data`, an HTTP error has `error`, and both
// have the response (which a request that failed before it was sent does not).
interface Answer {
  data?: unknown;
  error?: unknown;
  response?: { status: number };
}

async function send<T>(request: () => Promise<unknown>): Promise<T> {
  let answer: Answer;
  try {
    answer = (await request()) as Answer;
  } catch (failure) {
    // No answer at all (fetch rejected), or a client that threw the body after all.
    throw toApiError(failure);
  }

  if (answer.error !== undefined) throw toApiError(answer.error, answer.response?.status);
  return answer.data as T;
}

// A query string with the values encoded; a missing or empty value leaves its parameter out.
function query(params: Record<string, string | undefined>): string {
  const pairs = Object.entries(params)
    .filter(([, value]) => value)
    .map(([name, value]) => `${name}=${encodeURIComponent(value!)}`);
  return pairs.length > 0 ? `?${pairs.join('&')}` : '';
}

const get = <T>(path: string) => send<T>(() => umbHttpClient.get({ url: `${BASE}/${path}`, security: security(), throwOnError: false }));
const post = <T>(path: string, body?: unknown) =>
  send<T>(() =>
    umbHttpClient.post({ url: `${BASE}/${path}`, security: security(), throwOnError: false, ...(body === undefined ? {} : { body, headers: json }) }),
  );
const put = <T>(path: string, body: unknown) =>
  send<T>(() => umbHttpClient.put({ url: `${BASE}/${path}`, body, headers: json, security: security(), throwOnError: false }));

// ---- Reading (Content access) ----------------------------------------------------------------------------------------------

export const getStatus = () => get<StatusResponse>('status');

/** The archived report of a month with its note and when it was fetched; no month is the newest. Never fetches. */
export const getReport = (period?: string) => get<ReportResponse>(`report${query({ period })}`);

/** The analysis of a month against the archive; no month is the newest. Read `hasAnalysis` first. */
export const getAnalysis = (period?: string) => get<AnalysisResponse | NoAnalysisResponse>(`analysis${query({ period })}`);

/** The live daily curve: the rolling window, or a calendar month. Asks GA4. */
export const getDaily = (month?: string) => get<DailyResponse>(`daily${query({ month })}`);

/** Where one day's visits came from. Asks GA4. */
export const getDailySources = (date: string) => get<DailySourcesResponse>(`daily/sources${query({ date })}`);

export const getFetchProgress = () => get<FetchStatus>('fetch/progress');

export const getTargets = () => get<TargetsResponse>('targets');

// ---- Acting (Content access): a person's click ------------------------------------------------------------------------------

/** Fetches ONE month from Google and archives it (about fifteen seconds). Only ever called from the Refresh button. */
export const refreshReport = (period: string) => post<ReportResponse>(`refresh${query({ period })}`);

/** Starts the backfill of the missing months in the background (202); poll getFetchProgress. Only ever called from Fetch now. */
export const startFetch = () => post<StartFetchResponse>('fetch');

export const putNote = (period: string, noteText: string | null) => put<NoteResponse>(`note${query({ period })}`, { text: noteText });

export const putTargets = (terms: string[]) => put<TargetsResponse>('targets', { terms });

// ---- Settings (Settings access) ---------------------------------------------------------------------------------------------

export const getSettings = () => get<SettingsResponse>('settings');
export const putSettings = (body: PutSettingsRequest) => put<SettingsResponse>('settings', body);
export const testConnection = (body: ConnectionTestRequest) => post<ConnectionTestResult>('settings/test', body);
export const getUserGroups = () => get<UserGroupItem[]>('user-groups');
