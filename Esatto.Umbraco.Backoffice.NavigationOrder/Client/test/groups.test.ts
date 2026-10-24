import { describe, expect, it } from 'vitest';
import { membersOf, SECTION_ALIAS_CONDITION, sectionsOfTab } from '../src/core/groups';
import { dashboard, section, sectionView } from './fixtures';

describe('sectionsOfTab', () => {
  it('reads a match condition', () => {
    expect(sectionsOfTab(dashboard('D', 'Umb.Section.Settings'))).toEqual(['Umb.Section.Settings']);
  });

  it('reads oneOf and match together as one list', () => {
    const manifest = {
      ...dashboard('D', null),
      conditions: [
        { alias: SECTION_ALIAS_CONDITION, match: 'A' },
        { alias: SECTION_ALIAS_CONDITION, oneOf: ['B', 'A'] },
      ],
    };
    expect(sectionsOfTab(manifest)).toEqual(['A', 'B']);
  });

  it('is every section without a section condition', () => {
    expect(sectionsOfTab(dashboard('D', null))).toBe('all');
    expect(sectionsOfTab({ ...dashboard('D', null), conditions: undefined })).toBe('all');
    expect(sectionsOfTab({ ...dashboard('D', null), conditions: [{ alias: 'Umb.Condition.CurrentUser.IsAdmin' }] })).toBe('all');
  });
});

describe('membersOf', () => {
  const manifests = [
    section('Umb.Section.Settings', 800),
    section('Umb.Section.Content', 1000),
    dashboard('Settings.Welcome', 'Umb.Section.Settings'),
    dashboard('Everywhere', null),
    dashboard('Content.Only', 'Umb.Section.Content'),
    dashboard('Both', ['Umb.Section.Content', 'Umb.Section.Settings']),
    sectionView('Settings.View', 'Umb.Section.Settings'),
  ];

  it('lists every section for the top bar', () => {
    expect(membersOf(manifests, { kind: 'sections' }).map((m) => m.alias)).toEqual(['Umb.Section.Settings', 'Umb.Section.Content']);
  });

  it("lists a section's dashboards, including ones shown in every section", () => {
    expect(membersOf(manifests, { kind: 'tabs', section: 'Umb.Section.Settings', type: 'dashboard' }).map((m) => m.alias)).toEqual([
      'Settings.Welcome',
      'Everywhere',
      'Both',
    ]);
  });

  it("lists a section's views apart from its dashboards", () => {
    expect(membersOf(manifests, { kind: 'tabs', section: 'Umb.Section.Settings', type: 'sectionView' }).map((m) => m.alias)).toEqual(['Settings.View']);
  });
});
