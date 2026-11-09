// The dashboard's endpoints (SeoHealthController). Shapes are in types.ts.

import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import type { IssueFilter, IssueGroup, Knowledge, Overview, ScanProgress, SeoSettings, SettingsResponse, SiteInfo } from './types';

export const BASE = '/umbraco/management/api/v1/esatto/seo-health';

// `security: [bearer]` makes the shared backoffice client authenticate the request.
const security = () => [{ scheme: 'bearer' as const, type: 'http' as const }];
const json = { 'Content-Type': 'application/json' };
const site = (key: string) => `site=${encodeURIComponent(key)}`;

export function issueQuery(siteKey: string, f: IssueFilter): string {
  const q = new URLSearchParams({ site: siteKey });
  if (f.severity) q.set('severity', f.severity);
  if (f.owner) q.set('owner', f.owner);
  if (f.culture) q.set('culture', f.culture);
  if (f.checkId) q.set('checkId', f.checkId);
  if (f.ignored) q.set('ignored', 'true');
  if (f.search?.trim()) q.set('search', f.search.trim());
  return q.toString();
}

export const getKnowledge = () => umbHttpClient.get<{ 200: Knowledge }>({ url: `${BASE}/knowledge`, security: security() });
export const getSites = () => umbHttpClient.get<{ 200: SiteInfo[] }>({ url: `${BASE}/sites`, security: security() });
export const getSettings = () => umbHttpClient.get<{ 200: SettingsResponse }>({ url: `${BASE}/settings`, security: security() });
export const putSettings = (body: SeoSettings) =>
  umbHttpClient.put<{ 200: SettingsResponse }>({ url: `${BASE}/settings`, body, security: security(), headers: json });

export const getOverview = (siteKey: string) => umbHttpClient.get<{ 200: Overview }>({ url: `${BASE}/overview?${site(siteKey)}`, security: security() });
export const getIssues = (siteKey: string, f: IssueFilter) =>
  umbHttpClient.get<{ 200: IssueGroup[] }>({ url: `${BASE}/issues?${issueQuery(siteKey, f)}`, security: security() });
export const getIssuesCsv = (siteKey: string, f: IssueFilter) =>
  umbHttpClient.get<{ 200: Blob }>({ url: `${BASE}/issues/csv?${issueQuery(siteKey, f)}`, security: security(), parseAs: 'blob' });

export const startScan = (siteKey: string) => umbHttpClient.post<{ 200: { started: boolean } }>({ url: `${BASE}/scans?${site(siteKey)}`, security: security() });
export const getProgress = (siteKey: string) =>
  umbHttpClient.get<{ 200: ScanProgress; 204: void }>({ url: `${BASE}/scans/progress?${site(siteKey)}`, security: security() });
export const cancelScan = (siteKey: string) => umbHttpClient.delete<{ 204: void }>({ url: `${BASE}/scans/current?${site(siteKey)}`, security: security() });

export const recheck = (siteKey: string, contentKey: string, culture: string) =>
  umbHttpClient.post<{ 204: void }>({ url: `${BASE}/recheck?${site(siteKey)}`, body: { contentKey, culture }, security: security(), headers: json });

export const addIgnore = (siteKey: string, body: { checkId: string; contentKey: string | null; culture: string | null; note: string }) =>
  umbHttpClient.post<{ 200: { id: number } }>({ url: `${BASE}/ignores?${site(siteKey)}`, body, security: security(), headers: json });
export const removeIgnore = (id: number) => umbHttpClient.delete<{ 204: void }>({ url: `${BASE}/ignores/${id}`, security: security() });
