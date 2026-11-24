import { describe, expect, it } from 'vitest';
import en from './lang/en';
import { GUIDE_STEPS } from './setup-steps';

const texts = en.esattoSeoReport as Record<string, unknown>;

describe('the setup guide', () => {
  it('has the six steps: project, APIs, service account and key, access, the form, Test connection', () => {
    expect(GUIDE_STEPS.map((step) => step.textKey)).toEqual([
      'uiGuideStepProjectText',
      'uiGuideStepApisText',
      'uiGuideStepAccountText',
      'uiGuideStepAccessText',
      'uiGuideStepFormText',
      'uiGuideStepTestText',
    ]);
  });

  it('has the words of every step and every link in en.ts', () => {
    for (const step of GUIDE_STEPS) {
      expect(typeof texts[step.textKey], step.textKey).toMatch(/^(string|function)$/);
      for (const link of step.links) expect(typeof texts[link.labelKey], link.labelKey).toBe('string');
    }
  });

  it('links only to Google\'s own pages, over https', () => {
    const hosts = new Set(['docs.cloud.google.com', 'console.cloud.google.com', 'support.google.com', 'developers.google.com']);

    for (const link of GUIDE_STEPS.flatMap((step) => step.links)) {
      const url = new URL(link.href);

      expect(url.protocol, link.href).toBe('https:');
      expect(hosts.has(url.host), link.href).toBe(true);
    }
  });

  it('writes the service account\'s address into the step that grants access, and only there', () => {
    expect(GUIDE_STEPS.filter((step) => step.withEmail).map((step) => step.textKey)).toEqual(['uiGuideStepAccessText']);
  });
});
