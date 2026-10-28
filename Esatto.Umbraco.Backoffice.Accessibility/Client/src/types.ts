// camelCase mirrors of the C# records the AccessibilityController returns and accepts.

export type WcagVersion = 'V20' | 'V21' | 'V22';
export type WcagLevel = 'A' | 'AA' | 'AAA';
export type Owner = 'Editor' | 'Developer' | 'Both';
export type Impact = 'critical' | 'serious' | 'moderate' | 'minor';
export type ManualStatus = 'pass' | 'fail' | 'na';

export interface WcagTarget { version: WcagVersion; level: WcagLevel }

export interface Criterion {
  id: string;
  title: string;
  level: WcagLevel;
  since: WcagVersion;
  obsoleteIn: WcagVersion | null;
  principle: number;
  guideline: string;
  fullyAutomatable: boolean;
  owner: Owner;
  why: string;
  howToFix: string;
  caveats: string;
  howToTest: string;
  understandingUrl: string;
}

export interface AxeRuleGuide { ruleId: string; owner: Owner | null; howToFix: string | null; caveats: string | null }
export interface Knowledge { criteria: Criterion[]; rules: AxeRuleGuide[] }

export interface SiteInfo { key: string; name: string; hosts: string[] }

export interface ScanPage { contentKey: string; name: string; culture: string; url: string; skipReason: string | null }

/** One page at one viewport width: the unit the scanner works on. */
export interface Job { contentKey: string; name: string; culture: string; url: string; viewport: number }

export interface ScanSettings { excludePaths: string[]; mobileWidth: boolean; bestPractice: boolean; recheckMonths: number }
export interface PanelSettings {
  enabled: boolean;
  position: 'bottom-right' | 'bottom-left';
  largerText: boolean;
  spacing: boolean;
  highContrast: boolean;
  reduceMotion: boolean;
  readableFont: boolean;
  highlightLinks: boolean;
  readingGuide: boolean;
  texts: Record<string, Record<string, string>>;
}
export interface VisitorSettings { followOsMotion: boolean; followOsContrast: boolean; panel: PanelSettings }
export interface AccessibilitySettings { version: WcagVersion; level: WcagLevel; scan: ScanSettings; visitor: VisitorSettings }
export interface SettingsResponse { settings: AccessibilitySettings; locked: string[]; target: WcagTarget; canEdit: boolean }

export interface NodeResult { selector: string; html: string }
export interface RuleResult {
  ruleId: string;
  kind: 'violation' | 'incomplete';
  impact: string | null;
  help: string;
  helpUrl: string;
  tags: string[];
  nodes: NodeResult[];
}
export interface PassedRule { ruleId: string; tags: string[] }
export interface PageResult {
  contentKey: string;
  name: string;
  culture: string;
  url: string;
  viewport: number;
  status: 'ok' | 'skipped' | 'failed';
  reason: string | null;
  passes: PassedRule[];
  rules: RuleResult[];
  documentLinks: string[];
}

export interface DonePage { contentKey: string; culture: string; viewport: number }

export interface ScanSummary {
  id: number;
  startedUtc: string;
  finishedUtc: string | null;
  status: 'running' | 'complete' | 'incomplete';
  target: string;
  score: number | null;
  pagesTotal: number;
  pagesDone: number;
  pagesSkipped: number;
  pagesFailed: number;
  userName: string;
  /** The scan also loaded every page at 320 px; a resume keeps it that way. */
  mobile: boolean;
}

export interface ScoreResult {
  percent: number | null;
  pass: number;
  fail: number;
  notApplicable: number;
  notVerified: number;
  automatedPass: number;
  automatedFail: number;
  /** The part of notVerified the scanner checked without finding violations. */
  scannerChecked: number;
}

export interface PrincipleTile { principle: number; name: string; fail: number; toCheck: number }

export interface Overview {
  target: WcagTarget;
  score: ScoreResult;
  principles: PrincipleTile[];
  lastScan: ScanSummary | null;
  history: ScanSummary[];
  manualChecked: number;
  manualTotal: number;
}

export interface IssueNode { findingId: number; selector: string; html: string; mediaKey: string | null; falsePositiveId: number | null }
/** A page with the issue; `viewports` are the widths the scan loaded it at (a re-check covers them all). */
export interface IssuePage { contentKey: string; name: string; culture: string; url: string; nodes: IssueNode[]; viewports: number[] }
export interface IssueGroup {
  ruleId: string;
  title: string;
  criteria: string[];
  level: WcagLevel | null;
  owner: Owner;
  impact: string | null;
  kind: 'violation' | 'incomplete';
  bestPractice: boolean;
  pageCount: number;
  elementCount: number;
  why: string;
  howToFix: string;
  caveats: string;
  learnMoreUrl: string;
  pages: IssuePage[];
}

export interface IssueFilter {
  principle?: number;
  level?: WcagLevel;
  owner?: Owner;
  impact?: string;
  falsePositives?: boolean;
  criterion?: string;
  /** One group (with its elements): rule id and kind. */
  ruleId?: string;
  kind?: 'violation' | 'incomplete';
}

export interface ChecklistItem {
  criterion: Criterion;
  status: ManualStatus | null;
  note: string | null;
  userName: string | null;
  updatedUtc: string | null;
  needsRecheck: boolean;
  automatedFindings: number;
  documentLinks: number;
  /** The latest scan ran rules for this criterion and found no violations: a person only has to confirm it. */
  scannerChecked: boolean;
}
