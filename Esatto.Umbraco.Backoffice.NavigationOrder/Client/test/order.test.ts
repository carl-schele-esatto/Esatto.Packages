import { describe, expect, it } from 'vitest';
import { defaultOrder, effectiveOrder, keepUninstalled, moveAlias, savedListOf, targetWeights, withList } from '../src/core/order';
import { EMPTY_ORDER, type NavigationOrderData } from '../src/core/types';
import { dashboard, originalOf, section, sectionView } from './fixtures';

describe('defaultOrder', () => {
  it('puts the highest weight first, keeps ties in registration order and counts no weight as 0', () => {
    const members = [section('A', 10), section('B'), section('C', 1000), section('D', 10)];
    expect(defaultOrder(members, originalOf(members))).toEqual(['C', 'A', 'D', 'B']);
  });
});

describe('effectiveOrder', () => {
  const members = [section('Content', 1000), section('Media', 900), section('Forms', 0)];

  it('takes saved aliases first, skips ones that are gone, and adds the rest in default order', () => {
    expect(effectiveOrder(members, ['Forms', 'Gone', 'Content'], originalOf(members))).toEqual(['Forms', 'Content', 'Media']);
  });

  it('puts a returning extension back in its saved place', () => {
    const back = [...members, section('Gone', 5)];
    expect(effectiveOrder(back, ['Forms', 'Gone', 'Content'], originalOf(back))).toEqual(['Forms', 'Gone', 'Content', 'Media']);
  });

  it('is the default order when nothing is saved', () => {
    expect(effectiveOrder(members, null, originalOf(members))).toEqual(['Content', 'Media', 'Forms']);
  });
});

describe('savedListOf', () => {
  const order: NavigationOrderData = { sections: ['A'], tabs: [{ section: 'S', aliases: ['X'] }] };

  it('finds the sections list and a section tab list', () => {
    expect(savedListOf(order, { kind: 'sections' })).toEqual(['A']);
    expect(savedListOf(order, { kind: 'tabs', section: 'S', type: 'sectionView' })).toEqual(['X']);
    expect(savedListOf(order, { kind: 'tabs', section: 'Other', type: 'dashboard' })).toBeNull();
    expect(savedListOf(EMPTY_ORDER, { kind: 'sections' })).toBeNull();
  });
});

describe('targetWeights', () => {
  it('weighs a saved top bar by position and leaves tabs on their own weights', () => {
    const manifests = [section('Content', 1000), section('Media', 900), section('Forms', 0), dashboard('D', 'Content', 30)];
    const weights = targetWeights(manifests, { sections: ['Forms'], tabs: [] }, originalOf(manifests));
    expect(Object.fromEntries(weights)).toEqual({ Forms: 30, Content: 20, Media: 10, D: 30 });
  });

  it('gives every manifest its original weight when nothing is saved', () => {
    const manifests = [section('Content', 1000), section('AI'), dashboard('D', 'Content', 30)];
    const weights = targetWeights(manifests, EMPTY_ORDER, originalOf(manifests));
    expect(Object.fromEntries(weights)).toEqual({ Content: 1000, AI: 0, D: 30 });
  });

  describe('a dashboard shown in several sections', () => {
    const manifests = [
      section('Content', 1000),
      section('Settings', 800),
      dashboard('C1', 'Content', 10),
      dashboard('S1', 'Settings', 10),
      dashboard('All', null, 5),
    ];
    const tabs = [
      { section: 'Content', aliases: ['All', 'C1'] },
      { section: 'Settings', aliases: ['S1', 'All'] },
    ];

    it('is weighed by the section that comes first in the top bar', () => {
      expect(targetWeights(manifests, { sections: null, tabs }, originalOf(manifests)).get('All')).toBe(20);
      expect(targetWeights(manifests, { sections: ['Settings', 'Content'], tabs }, originalOf(manifests)).get('All')).toBe(10);
    });

    it('gets the same weights every time', () => {
      const order = { sections: null, tabs };
      expect(targetWeights(manifests, order, originalOf(manifests))).toEqual(targetWeights(manifests, order, originalOf(manifests)));
    });
  });

  it("orders a section's dashboards and views separately from one saved list", () => {
    const manifests = [
      section('S', 100),
      dashboard('D1', 'S', 10),
      dashboard('D2', 'S', 20),
      sectionView('V1', 'S', 5),
      sectionView('V2', 'S', 6),
    ];
    const weights = targetWeights(manifests, { sections: null, tabs: [{ section: 'S', aliases: ['D1', 'V1', 'D2', 'V2'] }] }, originalOf(manifests));
    expect(Object.fromEntries(weights)).toEqual({ S: 100, D1: 20, D2: 10, V1: 20, V2: 10 });
  });
});

describe('moveAlias', () => {
  it('moves before or after the target', () => {
    expect(moveAlias(['A', 'B', 'C', 'D'], 'D', 'B', 'before')).toEqual(['A', 'D', 'B', 'C']);
    expect(moveAlias(['A', 'B', 'C', 'D'], 'A', 'C', 'after')).toEqual(['B', 'C', 'A', 'D']);
  });

  it('keeps members the admin cannot see in their places', () => {
    expect(moveAlias(['A', 'H', 'B', 'C'], 'C', 'B', 'before')).toEqual(['A', 'H', 'C', 'B']);
  });

  it('changes nothing when moving onto itself or with an unknown alias', () => {
    const list = ['A', 'B'];
    expect(moveAlias(list, 'A', 'A', 'after')).toEqual(['A', 'B']);
    expect(moveAlias(list, 'X', 'A', 'before')).toEqual(['A', 'B']);
    expect(moveAlias(list, 'A', 'X', 'before')).toEqual(['A', 'B']);
    expect(moveAlias(list, 'A', 'B', 'after')).not.toBe(list);
  });
});

describe('withList', () => {
  it('sets the top bar', () => {
    const order = withList({ sections: null, tabs: [{ section: 'S', aliases: ['X'] }] }, { kind: 'sections' }, ['B', 'A']);
    expect(order).toEqual({ sections: ['B', 'A'], tabs: [{ section: 'S', aliases: ['X'] }] });
  });

  it("keeps a section's views after its new dashboard order", () => {
    const order = withList(EMPTY_ORDER, { kind: 'tabs', section: 'S', type: 'dashboard' }, ['D2', 'D1'], ['V1']);
    expect(order.tabs).toEqual([{ section: 'S', aliases: ['D2', 'D1', 'V1'] }]);
  });

  it("keeps a section's dashboards before its new view order, replacing the old list", () => {
    const start: NavigationOrderData = { sections: null, tabs: [{ section: 'S', aliases: ['V1', 'D1', 'V2'] }, { section: 'T', aliases: ['Y'] }] };
    const order = withList(start, { kind: 'tabs', section: 'S', type: 'sectionView' }, ['V2', 'V1'], ['D1']);
    expect(order.tabs).toEqual([{ section: 'S', aliases: ['D1', 'V2', 'V1'] }, { section: 'T', aliases: ['Y'] }]);
    expect(start.tabs[0].aliases).toEqual(['V1', 'D1', 'V2']);
  });

  it('keeps an uninstalled extension in its saved place when a move rewrites the list', () => {
    const start: NavigationOrderData = { sections: ['Forms', 'Gone', 'Content'], tabs: [{ section: 'S', aliases: ['Missing', 'D1', 'D2'] }] };
    expect(withList(start, { kind: 'sections' }, ['Content', 'Forms', 'Media']).sections).toEqual(['Content', 'Forms', 'Gone', 'Media']);
    expect(withList(start, { kind: 'tabs', section: 'S', type: 'dashboard' }, ['D2', 'D1']).tabs[0].aliases).toEqual(['Missing', 'D2', 'D1']);
  });
});

describe('keepUninstalled', () => {
  it('puts each saved alias missing from the new list after the alias it followed', () => {
    expect(keepUninstalled(['A', 'Gone', 'B', 'Lost'], ['B', 'A', 'C'])).toEqual(['B', 'Lost', 'A', 'Gone', 'C']);
  });

  it('puts a missing alias that led the saved list first', () => {
    expect(keepUninstalled(['Gone', 'A', 'B'], ['B', 'A'])).toEqual(['Gone', 'B', 'A']);
  });

  it('changes nothing without a saved list or missing aliases', () => {
    expect(keepUninstalled(null, ['A', 'B'])).toEqual(['A', 'B']);
    expect(keepUninstalled(['B', 'A'], ['A', 'B'])).toEqual(['A', 'B']);
  });
});
