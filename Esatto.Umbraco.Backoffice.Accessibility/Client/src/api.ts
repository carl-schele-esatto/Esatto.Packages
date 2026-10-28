// The dashboard's endpoints (AccessibilityController). Shapes are in types.ts.

import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import type {
  AccessibilitySettings, ChecklistItem, DonePage, IssueFilter, IssueGroup, Knowledge, ManualStatus, Overview,
  PageResult, ScanPage, ScanSummary, SettingsResponse, SiteInfo,
} from './types';

export const BASE = '/umbraco/management/api/v1/esatto/accessibility';

// `security: [bearer]` makes the shared backoffice client authenticate the request.
const security = () => [{ scheme: 'bearer' as const, type: 'http' as const }];

const site = (key: string) => `site=${encodeURIComponent(key)}`;

export function issueQuery(siteKey: string, f: IssueFilter): string {
  const q = new URLSearchParams({ site: siteKey });
  if (f.principle) q.set('principle', String(f.principle));
  if (f.level) q.set('level', f.level);
  if (f.owner) q.set('owner', f.owner);
  if (f.impact) q.set('impact', f.impact);
  if (f.criterion) q.set('criterion', f.criterion);
  if (f.falsePositives) q.set('falsePositives', 'true');
  if (f.ruleId) q.set('ruleId', f.ruleId);
  if (f.kind) q.set('kind', f.kind);
  return q.toString();
}

export const getKnowledge = () => umbHttpClient.get<{ 200: Knowledge }>({ url: `${BASE}/knowledge`, security: security() });
export const getSites = () => umbHttpClient.get<{ 200: SiteInfo[] }>({ url: `${BASE}/sites`, security: security() });
export const getPages = (siteKey: string) => umbHttpClient.get<{ 200: ScanPage[] }>({ url: `${BASE}/pages?${site(siteKey)}`, security: security() });
export const getSettings = () => umbHttpClient.get<{ 200: SettingsResponse }>({ url: `${BASE}/settings`, security: security() });
export const putSettings = (body: AccessibilitySettings) =>
  umbHttpClient.put<{ 200: SettingsResponse }>({ url: `${BASE}/settings`, body, security: security(), headers: { 'Content-Type': 'application/json' } });

export const startScan = (siteKey: string, mobile: boolean, pagesTotal: number) =>
  umbHttpClient.post<{ 200: { scanId: number } }>({ url: `${BASE}/scans`, body: { siteKey, mobile, pagesTotal }, security: security(), headers: { 'Content-Type': 'application/json' } });
export const getDone = (scanId: number) => umbHttpClient.get<{ 200: DonePage[] }>({ url: `${BASE}/scans/${scanId}/done`, security: security() });
export const postPage = (scanId: number, body: PageResult) =>
  umbHttpClient.post<{ 204: void }>({ url: `${BASE}/scans/${scanId}/pages`, body, security: security(), headers: { 'Content-Type': 'application/json' } });
export const finishScan = (scanId: number, complete: boolean) =>
  umbHttpClient.post<{ 200: ScanSummary }>({ url: `${BASE}/scans/${scanId}/finish`, body: { complete }, security: security(), headers: { 'Content-Type': 'application/json' } });
export const recheck = (siteKey: string, body: PageResult) =>
  umbHttpClient.post<{ 204: void }>({ url: `${BASE}/recheck?${site(siteKey)}`, body, security: security(), headers: { 'Content-Type': 'application/json' } });

export const getOverview = (siteKey: string) => umbHttpClient.get<{ 200: Overview }>({ url: `${BASE}/overview?${site(siteKey)}`, security: security() });
export const getIssues = (siteKey: string, f: IssueFilter) =>
  umbHttpClient.get<{ 200: IssueGroup[] }>({ url: `${BASE}/issues?${issueQuery(siteKey, f)}`, security: security() });
export const getIssuesCsv = (siteKey: string, f: IssueFilter) =>
  umbHttpClient.get<{ 200: Blob }>({ url: `${BASE}/issues/csv?${issueQuery(siteKey, f)}`, security: security(), parseAs: 'blob' });

export const getChecklist = (siteKey: string) => umbHttpClient.get<{ 200: ChecklistItem[] }>({ url: `${BASE}/checklist?${site(siteKey)}`, security: security() });
export const setChecklist = (siteKey: string, criterionId: string, status: ManualStatus, note: string | null) =>
  umbHttpClient.put<{ 204: void }>({ url: `${BASE}/checklist/${encodeURIComponent(criterionId)}?${site(siteKey)}`, body: { status, note }, security: security(), headers: { 'Content-Type': 'application/json' } });

export const clearChecklist = (siteKey: string, criterionId: string) =>
  umbHttpClient.delete<{ 204: void }>({ url: `${BASE}/checklist/${encodeURIComponent(criterionId)}?${site(siteKey)}`, security: security() });

export const addFalsePositive = (siteKey: string, body: { ruleId: string; contentKey: string; selector: string | null; note: string }) =>
  umbHttpClient.post<{ 200: { id: number } }>({ url: `${BASE}/false-positives?${site(siteKey)}`, body, security: security(), headers: { 'Content-Type': 'application/json' } });
export const removeFalsePositive = (id: number) =>
  umbHttpClient.delete<{ 204: void }>({ url: `${BASE}/false-positives/${id}`, security: security() });
