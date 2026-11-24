// The six steps of the "How to connect" guide on the Settings tab: the key of each step's text in en.ts and the Google help pages it links to.
//
// The links are Google's own documentation pages (and the two API Library pages of the Cloud console), checked on 2026-11-20 together with the
// wording of the steps: the Cloud docs now live under docs.cloud.google.com (cloud.google.com redirects there). Google renames its menus now and
// then; when a step stops matching the console, its text in en.ts is what changes.

export interface GuideLink {
  labelKey: string;
  href: string;
}

export interface GuideStep {
  textKey: string;
  /** The step's text is written with the service account's address (`email`). */
  withEmail?: boolean;
  links: readonly GuideLink[];
}

export const GUIDE_STEPS: readonly GuideStep[] = [
  {
    textKey: 'uiGuideStepProjectText',
    links: [{ labelKey: 'uiGuideLinkProjectLabel', href: 'https://docs.cloud.google.com/resource-manager/docs/creating-managing-projects' }],
  },
  {
    textKey: 'uiGuideStepApisText',
    links: [
      { labelKey: 'uiGuideLinkApisLabel', href: 'https://support.google.com/googleapi/answer/6158841' },
      { labelKey: 'uiGuideLinkAnalyticsApiLabel', href: 'https://console.cloud.google.com/apis/library/analyticsdata.googleapis.com' },
      { labelKey: 'uiGuideLinkSearchConsoleApiLabel', href: 'https://console.cloud.google.com/apis/library/searchconsole.googleapis.com' },
    ],
  },
  {
    textKey: 'uiGuideStepAccountText',
    links: [
      { labelKey: 'uiGuideLinkServiceAccountLabel', href: 'https://docs.cloud.google.com/iam/docs/service-accounts-create' },
      { labelKey: 'uiGuideLinkKeyLabel', href: 'https://docs.cloud.google.com/iam/docs/keys-create-delete' },
    ],
  },
  {
    textKey: 'uiGuideStepAccessText',
    withEmail: true,
    links: [
      { labelKey: 'uiGuideLinkAnalyticsUsersLabel', href: 'https://support.google.com/analytics/answer/9305788' },
      { labelKey: 'uiGuideLinkSearchConsoleUsersLabel', href: 'https://support.google.com/webmasters/answer/7687615' },
    ],
  },
  {
    textKey: 'uiGuideStepFormText',
    links: [
      { labelKey: 'uiGuideLinkPropertyIdLabel', href: 'https://developers.google.com/analytics/devguides/reporting/data/v1/property-id' },
      { labelKey: 'uiGuideLinkPropertyTypesLabel', href: 'https://support.google.com/webmasters/answer/34592' },
    ],
  },
  { textKey: 'uiGuideStepTestText', links: [] },
];
