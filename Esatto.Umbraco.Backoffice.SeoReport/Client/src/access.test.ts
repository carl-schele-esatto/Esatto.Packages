import { describe, expect, it } from 'vitest';
import { hasSettingsAccess } from './access';

describe('hasSettingsAccess', () => {
  it('is true for a user whose sections include Settings', () => {
    expect(hasSettingsAccess(['Umb.Section.Content', 'Umb.Section.Settings'])).toBe(true);
  });

  it('is false for a user who has other sections but not Settings', () => {
    expect(hasSettingsAccess(['Umb.Section.Content', 'Umb.Section.Media'])).toBe(false);
    expect(hasSettingsAccess([])).toBe(false);
  });

  it('is not known until the current user has loaded: undefined, not false', () => {
    expect(hasSettingsAccess(undefined)).toBeUndefined();
  });

  it('matches the section by its whole alias', () => {
    expect(hasSettingsAccess(['Umb.Section.SettingsExtra'])).toBe(false);
  });
});
