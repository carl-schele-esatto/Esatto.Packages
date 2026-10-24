import { describe, expect, it } from 'vitest';
import { dragOverEffect, dropPosition, findTabTarget, keyboardStep, type PathNode } from '../src/drag/tab-target';
import { dashboard, section, sectionView } from './fixtures';

const node = (localName: string, attributes: Record<string, string> = {}, id?: string): PathNode => ({
  localName,
  id,
  getAttribute: (name) => attributes[name] ?? null,
});
const shadowRoot: PathNode = {}; // ShadowRoots in a composed path have no localName

const manifests = [
  section('Umb.Section.Settings', 800, 'settings'),
  section('Umb.Section.Content', 1000, 'content'),
  section('Umb.Section.Packages', 700, 'packages'),
  section('Umb.Section.Forms', 0, 'forms'),
  dashboard('Esatto.ScheduledTasks', 'Umb.Section.Settings', 10, { label: 'Scheduled tasks', pathname: 'scheduled-tasks' }),
  dashboard('Settings.Welcome', 'Umb.Section.Settings', 20, { label: 'Welcome', pathname: 'welcome' }),
  dashboard('Content.Welcome', 'Umb.Section.Content', 20, { label: 'Welcome', pathname: 'welcome' }),
  dashboard('Settings.Health', 'Umb.Section.Settings', 5, { label: 'Health Check' }),
  sectionView('Packages.Installed', 'Umb.Section.Packages', 5, { label: 'Installed', pathname: 'installed' }),
];
const folderName = (name: string) => name.toLowerCase().replace(/\s+/g, '');

const headerPath = (dataMark: string) => [
  node('a'),
  shadowRoot,
  node('uui-tab', { 'data-mark': dataMark }),
  node('slot'),
  node('uui-tab-group', {}, 'tabs'),
  shadowRoot,
  node('umb-backoffice-header-sections'),
];
const tabPath = (href: string, groupId: 'dashboards' | 'views') => [
  node('a'),
  shadowRoot,
  node('uui-tab', { href }),
  node('uui-tab-group', {}, groupId),
  node('umb-section-main-views'),
];

describe('findTabTarget', () => {
  it('recognises a top-bar section from its data-mark, also from the inner link', () => {
    const target = findTabTarget(headerPath('section-link:Umb.Section.Forms'), manifests, folderName);
    expect(target?.bar).toEqual({ kind: 'sections' });
    expect(target?.alias).toBe('Umb.Section.Forms');
    expect(target?.tab.localName).toBe('uui-tab');
    expect(target?.group.id).toBe('tabs');
  });

  it("recognises a dashboard by its section's and its own pathname", () => {
    const target = findTabTarget(tabPath('/umbraco/section/settings/dashboard/scheduled-tasks', 'dashboards'), manifests, folderName);
    expect(target).toMatchObject({ bar: { kind: 'tabs', section: 'Umb.Section.Settings', type: 'dashboard' }, alias: 'Esatto.ScheduledTasks' });
  });

  it('tells same-named dashboards of different sections apart', () => {
    expect(findTabTarget(tabPath('/umbraco/section/content/dashboard/welcome', 'dashboards'), manifests, folderName)?.alias).toBe('Content.Welcome');
    expect(findTabTarget(tabPath('/umbraco/section/settings/dashboard/welcome/', 'dashboards'), manifests, folderName)?.alias).toBe('Settings.Welcome');
  });

  it('matches a dashboard without a pathname through its label', () => {
    expect(findTabTarget(tabPath('/umbraco/section/settings/dashboard/healthcheck', 'dashboards'), manifests, folderName)?.alias).toBe('Settings.Health');
  });

  it('recognises a section view', () => {
    const target = findTabTarget(tabPath('/umbraco/section/packages/view/installed', 'views'), manifests, folderName);
    expect(target).toMatchObject({ bar: { kind: 'tabs', section: 'Umb.Section.Packages', type: 'sectionView' }, alias: 'Packages.Installed' });
  });

  it('ignores anything it cannot place', () => {
    expect(findTabTarget(headerPath('section-link:Unknown'), manifests, folderName)).toBeNull();
    expect(findTabTarget(headerPath('something-else'), manifests, folderName)).toBeNull();
    expect(findTabTarget(tabPath('/umbraco/section/settings/dashboard/nope', 'dashboards'), manifests, folderName)).toBeNull();
    expect(findTabTarget(tabPath('/umbraco/section/nowhere/dashboard/welcome', 'dashboards'), manifests, folderName)).toBeNull();
    expect(findTabTarget(tabPath('/umbraco/section/settings/view/welcome', 'dashboards'), manifests, folderName)).toBeNull();
    expect(findTabTarget([node('uui-tab', { 'data-mark': 'section-link:Umb.Section.Forms' })], manifests, folderName)).toBeNull();
    expect(findTabTarget([node('uui-tab', { href: 'x' }), node('uui-tab-group', {}, 'workspace-views')], manifests, folderName)).toBeNull();
    expect(findTabTarget([node('div'), node('uui-tab-group', {}, 'tabs')], manifests, folderName)).toBeNull();
  });
});

describe('dragOverEffect', () => {
  const forms = findTabTarget(headerPath('section-link:Umb.Section.Forms'), manifests, folderName)!;
  const content = findTabTarget(headerPath('section-link:Umb.Section.Content'), manifests, folderName)!;
  const tasks = findTabTarget(tabPath('/umbraco/section/settings/dashboard/scheduled-tasks', 'dashboards'), manifests, folderName)!;

  it('ignores drags that are not ours', () => {
    expect(dragOverEffect(undefined, content)).toBe('ignore');
  });

  it('moves onto another tab of the same bar', () => {
    expect(dragOverEffect(forms, content)).toBe('move');
  });

  it('refuses — and so blocks the browser opening the dragged link — everywhere else', () => {
    expect(dragOverEffect(forms, null)).toBe('none');
    expect(dragOverEffect(forms, forms)).toBe('none');
    expect(dragOverEffect(forms, tasks)).toBe('none');
  });
});

describe('dropPosition', () => {
  it('is before left of the middle and after from the middle on', () => {
    expect(dropPosition(149, { left: 100, width: 100 })).toBe('before');
    expect(dropPosition(150, { left: 100, width: 100 })).toBe('after');
  });
});

describe('keyboardStep', () => {
  const key = (k: string, mods: Partial<{ altKey: boolean; shiftKey: boolean; ctrlKey: boolean; metaKey: boolean }> = {}) => ({
    key: k,
    altKey: false,
    shiftKey: false,
    ctrlKey: false,
    metaKey: false,
    ...mods,
  });

  it('moves with Alt+Shift+arrow', () => {
    expect(keyboardStep(key('ArrowLeft', { altKey: true, shiftKey: true }))).toBe(-1);
    expect(keyboardStep(key('ArrowRight', { altKey: true, shiftKey: true }))).toBe(1);
  });

  it('ignores other keys and modifiers', () => {
    expect(keyboardStep(key('ArrowLeft'))).toBe(0);
    expect(keyboardStep(key('ArrowLeft', { altKey: true }))).toBe(0);
    expect(keyboardStep(key('ArrowRight', { shiftKey: true }))).toBe(0);
    expect(keyboardStep(key('ArrowRight', { altKey: true, shiftKey: true, ctrlKey: true }))).toBe(0);
    expect(keyboardStep(key('ArrowLeft', { altKey: true, shiftKey: true, metaKey: true }))).toBe(0);
    expect(keyboardStep(key('ArrowUp', { altKey: true, shiftKey: true }))).toBe(0);
  });
});
