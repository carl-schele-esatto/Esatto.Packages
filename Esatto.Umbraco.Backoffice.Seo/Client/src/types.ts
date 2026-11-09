// Shapes of SeoHealthController's JSON (camelCase; enums as camelCase strings).

export type Severity = 'notice' | 'warning' | 'critical';
export type Owner = 'editor' | 'developer';
export type CheckScope = 'page' | 'crossPage' | 'site';

export interface CheckInfo { id: string; severity: Severity; owner: Owner; scope: CheckScope }
export interface CheckText { title: string; why: string; fix: string; caveats: string | null; learnMore: string | null }
export interface Knowledge { texts: Record<string, CheckText>; checks: CheckInfo[] }

export interface SiteInfo { key: string; name: string; hosts: string[] }

export interface SeoSettings {
  schedule: { enabled: boolean; hour: number };
  excludePaths: string[];
  parallelism: number;
  delayMs: number;
  baseUrl: string | null;
}
export interface SettingsResponse { settings: SeoSettings; locked: string[]; canEdit: boolean; intentionallyHidden: boolean }

export interface ScanInfo {
  id: number; startedUtc: string; finishedUtc: string | null; trigger: 'manual' | 'scheduled'; userName: string;
  status: 'running' | 'complete' | 'incomplete' | 'failed' | 'cancelled';
  pagesTotal: number; pagesDone: number; pagesFailed: number; pagesSkipped: number; score: number | null; message: string | null;
}
export interface ScanProgress { scanId: number; done: number; total: number; currentUrl: string | null }
export interface IssueSummary { checkId: string; severity: Severity; owner: Owner; pages: number; occurrences: number; priority: number }
export interface TrendPoint { scanId: number; startedUtc: string; score: number; pages: number }
export interface SiteCheckRow { checkId: string; severity: Severity; passed: boolean | null; value: string | null }
export interface PageRow { contentKey: string; name: string; culture: string; url: string; reason: string }

export interface Overview {
  lastScan: ScanInfo | null;
  latestAttempt: ScanInfo | null;
  running: ScanProgress | null;
  score: number | null;
  trend: TrendPoint[];
  top: IssueSummary[];
  counts: { critical: number; warning: number; notice: number };
  siteChecks: SiteCheckRow[];
  hidden: PageRow[];
  skipped: PageRow[];
  siteHidden: boolean;
  intentionallyHidden: boolean;
}

export interface IssueFilter {
  severity?: Severity;
  owner?: Owner;
  culture?: string;
  checkId?: string;
  ignored?: boolean;
  search?: string;
}

export interface IssueOccurrence {
  contentKey: string | null; name: string; culture: string; url: string | null; value: string | null; ignored: boolean; ignoreId: number | null;
}
export interface IssueGroup { summary: IssueSummary; occurrences: IssueOccurrence[] }
