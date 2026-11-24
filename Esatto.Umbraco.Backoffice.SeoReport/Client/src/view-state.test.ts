import { describe, expect, it, vi } from 'vitest';
import { NAVIGATE_EVENT, TAB_IDS, navigate } from './navigate';
import { fetchStatus, info, report, searchBlock, status, warning } from './testing/fixtures';
import type { Report, StatusResponse } from './types';
import {
  activeTab,
  chooseTab,
  hasContent,
  lastErrorOf,
  periodsNewestFirst,
  pinSettings,
  progressOf,
  runOutcomeOf,
  selectedPeriod,
  shouldAcceptPeriodChange,
  tabsFor,
  viewStateOf,
  visibleTabs,
  type TabChoice,
} from './view-state';

describe('viewStateOf', () => {
  it('is notConnected until the package is set up, whatever else is true', () => {
    for (const setup of ['notConfigured', 'keyUnreadable'] as const) {
      expect(viewStateOf(status({ setup }), report())).toBe('notConnected');
      expect(viewStateOf(status({ setup, fetch: fetchStatus({ running: true }) }), null)).toBe('notConnected');
    }
  });

  it('is progress while a fetch is running, with or without a report', () => {
    const running = status({ fetch: fetchStatus({ running: true, done: 2, total: 13 }) });

    expect(viewStateOf(running, null)).toBe('progress');
    expect(viewStateOf(running, report())).toBe('progress');
  });

  it('is progress for a run that is only starting (running, nothing counted yet)', () => {
    expect(viewStateOf(status({ fetch: fetchStatus({ running: true, total: 0 }) }), null)).toBe('progress');
  });

  it('is progress, never empty charts, when the archive is behind and there is no report', () => {
    expect(viewStateOf(status({ upToDate: false, periods: [] }), null)).toBe('progress');
  });

  it('is ready when the archive is behind but there is a report to show', () => {
    expect(viewStateOf(status({ upToDate: false }), report())).toBe('ready');
  });

  it('is noData when the report says that neither source returned anything', () => {
    expect(viewStateOf(status(), report({ data_status: 'noData' }))).toBe('noData');
  });

  it('is noData, not empty charts, when the archive is current but the month has no report', () => {
    expect(viewStateOf(status({ upToDate: true }), null)).toBe('noData');
  });

  it('is ready for a live report, and for one whose data_status is null (which reads as live)', () => {
    expect(viewStateOf(status(), report())).toBe('ready');
    expect(viewStateOf(status(), report({ data_status: null }))).toBe('ready');
  });
});

describe('hasContent', () => {
  it('is true for a report that is not noData', () => {
    expect(hasContent(report())).toBe(true);
    expect(hasContent(report({ data_status: null }))).toBe(true);
  });

  it('is false for noData and for none', () => {
    expect(hasContent(report({ data_status: 'noData' }))).toBe(false);
    expect(hasContent(null)).toBe(false);
  });
});

describe('visibleTabs', () => {
  const all = ['analysis', 'traffic', 'queries', 'topPages', 'toFix', 'comparison', 'settings'];

  it('lists the tabs in order, with To fix when the report has warnings and Settings for a Settings user', () => {
    expect(visibleTabs(report({}, searchBlock({ annotations: [warning()] })), true)).toEqual(all);
  });

  it('leaves out To fix when there are no warnings (notes do not count)', () => {
    const without = all.filter((tab) => tab !== 'toFix');

    expect(visibleTabs(report({}, searchBlock({ annotations: [info()] })), true)).toEqual(without);
    expect(visibleTabs(report({}, null), true)).toEqual(without);
  });

  it('leaves out Settings for a user without the Settings section', () => {
    expect(visibleTabs(report({}, searchBlock({ annotations: [warning()] })), false)).toEqual(all.filter((tab) => tab !== 'settings'));
  });

  it('only ever lists tabs the dashboard knows', () => {
    expect(visibleTabs(report({}, searchBlock({ annotations: [warning()] })), true).every((tab) => TAB_IDS.includes(tab))).toBe(true);
  });
});

describe('tabsFor', () => {
  it('is the full strip when there is something to show', () => {
    expect(tabsFor(report(), true)).toEqual(visibleTabs(report(), true));
  });

  it('is only Settings, for a Settings user, when there is nothing to show yet: that is where the cure is', () => {
    expect(tabsFor(null, true)).toEqual(['settings']);
    expect(tabsFor(report({ data_status: 'noData' }), true)).toEqual(['settings']);
  });

  it('is no tab at all for a user without Settings when there is nothing to show', () => {
    expect(tabsFor(null, false)).toEqual([]);
    expect(tabsFor(report({ data_status: 'noData' }), false)).toEqual([]);
  });
});

describe('activeTab', () => {
  it('is the selected tab when it is on the strip', () => {
    expect(activeTab('traffic', ['analysis', 'traffic'], 'ready')).toBe('traffic');
  });

  it('is the first tab when the selected one is not on the strip (To fix after the findings went away)', () => {
    expect(activeTab('toFix', ['analysis', 'traffic'], 'ready')).toBe('analysis');
  });

  it('is none when there are no tabs', () => {
    expect(activeTab('analysis', [], 'ready')).toBeNull();
  });

  it('is none, not Settings, before the person has asked for it while the package is not connected', () => {
    expect(activeTab('analysis', ['settings'], 'notConnected')).toBeNull();
    expect(activeTab('settings', ['settings'], 'notConnected')).toBe('settings');
  });

  it('is Settings whenever Settings is pinned and on the strip, whatever was selected', () => {
    expect(activeTab('analysis', ['analysis', 'traffic', 'settings'], 'ready', true)).toBe('settings');
    expect(activeTab('analysis', ['settings'], 'notConnected', true)).toBe('settings');
    expect(activeTab('analysis', ['analysis', 'traffic'], 'ready', true)).toBe('analysis');
  });
});

// Settings can be the tab on the screen without having been chosen: it is the only tab while a first backfill runs or the month has no data. When
// the strip changes (the report arrives, a removal makes the package not connected) the tab follows the strip, unless the person is in the middle of
// something on Settings: then Settings is pinned and stays, with what they typed and where their focus is. Choosing another tab releases the pin.
describe('the Settings tab keeps its place while the person works on it', () => {
  const ready = status();
  const firstRun = status({ periods: [], upToDate: false, fetch: fetchStatus({ running: true, done: 1, total: 13 }) });
  const notConnected = status({ setup: 'notConfigured', missing: ['key'], periods: [] });
  const noData = report({ data_status: 'noData' });

  // What the shell shows for a status and a report, with the choice it holds (Settings access: yes unless said otherwise).
  const shown = (choice: TabChoice, current: StatusResponse, held: Report | null, access = true) =>
    activeTab(choice.tab, tabsFor(held, access), viewStateOf(current, held), choice.pinned);

  const initial: TabChoice = { tab: 'analysis', pinned: false };

  it('follows the strip when Settings was only the fallback: the report replaces it once it arrives', () => {
    expect(shown(initial, firstRun, null)).toBe('settings');
    expect(shown(initial, ready, report())).toBe('analysis');
  });

  it('stays on Settings when the report arrives while the person works on it', () => {
    const pinned = pinSettings(initial);

    expect(shown(pinned, firstRun, null)).toBe('settings');
    expect(shown(pinned, ready, report())).toBe('settings');
  });

  it('stays on Settings when a removal makes the package not connected, with or without a report held', () => {
    const pinned = pinSettings(initial);

    expect(shown(initial, ready, noData)).toBe('settings'); // a month without data: Settings is the only tab
    expect(shown(pinned, notConnected, noData)).toBe('settings');
    expect(shown(pinned, notConnected, report())).toBe('settings');
    expect(shown(pinned, notConnected, null)).toBe('settings');
    expect(shown(initial, notConnected, noData)).toBeNull(); // not pinned: the not-connected card, and no tab until asked for
  });

  it('releases the pin when the person chooses another tab, and keeps it when they choose Settings', () => {
    const pinned = pinSettings(initial);

    expect(chooseTab(pinned, 'traffic')).toEqual({ tab: 'traffic', pinned: false });
    expect(shown(chooseTab(pinned, 'traffic'), ready, report())).toBe('traffic');
    expect(chooseTab(pinned, 'settings')).toEqual({ tab: 'settings', pinned: true });
  });

  it('changes nothing for a user without Settings: there is no Settings tab to keep', () => {
    const pinned = pinSettings(initial);

    expect(shown(pinned, ready, report(), false)).toBe('analysis');
    expect(shown(pinned, notConnected, null, false)).toBeNull();
  });
});

describe('progressOf', () => {
  it('is starting for a run that is running but has counted no months yet', () => {
    expect(progressOf(fetchStatus({ running: true, done: 0, total: 0 }))).toEqual({ phase: 'starting', done: 0, total: 0, percent: null, currentPeriod: null });
  });

  it('is the month count and the share done for a run that is going', () => {
    expect(progressOf(fetchStatus({ running: true, done: 3, total: 12, currentPeriod: '2026-04' }))).toEqual({
      phase: 'running',
      done: 3,
      total: 12,
      percent: 25,
      currentPeriod: '2026-04',
    });
  });

  it('never goes past a hundred percent', () => {
    expect(progressOf(fetchStatus({ running: true, done: 14, total: 12 })).percent).toBe(100);
  });

  it('is idle when nothing is running', () => {
    expect(progressOf(fetchStatus()).phase).toBe('idle');
  });
});

describe('runOutcomeOf', () => {
  it('says there was nothing to fetch for a run that ended having counted no months', () => {
    expect(runOutcomeOf(fetchStatus({ running: false, total: 0, done: 0 }))).toBe('nothingToFetch');
  });

  it('says the run failed when it ended with a code, even if it got part of the way', () => {
    expect(runOutcomeOf(fetchStatus({ running: false, total: 12, done: 4, errorCode: 'quota' }))).toBe('failed');
    expect(runOutcomeOf(fetchStatus({ running: false, total: 0, errorCode: 'notConfigured' }))).toBe('failed');
  });

  it('says the months were fetched for a run that ended having done its months', () => {
    expect(runOutcomeOf(fetchStatus({ running: false, total: 12, done: 12 }))).toBe('fetched');
  });
});

describe('lastErrorOf (why the last fetch did not finish)', () => {
  const earlier = '2026-11-01T06:00:00+00:00';
  const later = '2026-11-02T06:00:00+00:00';

  it('hides a recorded failure that a later success has put right', () => {
    expect(lastErrorOf(fetchStatus({ lastErrorCode: 'noAccess', lastFailureUtc: earlier, lastSuccessUtc: later }))).toBeNull();
  });

  it('shows a recorded failure that came after the last success', () => {
    expect(lastErrorOf(fetchStatus({ lastErrorCode: 'noAccess', lastFailureUtc: later, lastSuccessUtc: earlier }))).toBe('noAccess');
  });

  it('shows a recorded failure when nothing ever worked', () => {
    expect(lastErrorOf(fetchStatus({ lastErrorCode: 'quota', lastFailureUtc: earlier }))).toBe('quota');
  });

  it('shows nothing when there is only a success, or a code without the time of a failure', () => {
    expect(lastErrorOf(fetchStatus({ lastSuccessUtc: later }))).toBeNull();
    expect(lastErrorOf(fetchStatus({ lastErrorCode: 'quota' }))).toBeNull();
    expect(lastErrorOf(fetchStatus({ lastErrorCode: 'quota', lastSuccessUtc: later }))).toBeNull();
  });

  it('lets the success win when both happened at the same moment, however the time is written', () => {
    expect(lastErrorOf(fetchStatus({ lastErrorCode: 'quota', lastFailureUtc: later, lastSuccessUtc: later }))).toBeNull();
    expect(lastErrorOf(fetchStatus({ lastErrorCode: 'quota', lastFailureUtc: '2026-11-02T08:00:00+02:00', lastSuccessUtc: later }))).toBeNull();
  });

  it('always shows the code the latest run in this process ended with', () => {
    expect(lastErrorOf(fetchStatus({ errorCode: 'apiDisabled', lastErrorCode: 'noAccess', lastFailureUtc: earlier, lastSuccessUtc: later }))).toBe('apiDisabled');
  });
});

describe('navigate', () => {
  it('dispatches an event that bubbles through shadow roots and names the tab', () => {
    const dispatchEvent = vi.fn();

    navigate({ dispatchEvent } as unknown as EventTarget, 'settings');

    const event = dispatchEvent.mock.calls[0][0] as CustomEvent<{ tab: string }>;
    expect(event.type).toBe(NAVIGATE_EVENT);
    expect(event.detail).toEqual({ tab: 'settings' });
    expect(event.bubbles).toBe(true);
    expect(event.composed).toBe(true);
  });
});

describe('periodsNewestFirst', () => {
  it('lists the archived months newest first, which is how the picker offers them (the server sends oldest first)', () => {
    expect(periodsNewestFirst(['2025-12', '2026-01', '2026-10'])).toEqual(['2026-10', '2026-01', '2025-12']);
  });

  it('does not rely on the order it was given, and lists a month once', () => {
    expect(periodsNewestFirst(['2026-10', '2025-12', '2026-10'])).toEqual(['2026-10', '2025-12']);
  });

  it('does not change the list it was given', () => {
    const given = ['2026-01', '2026-02'];

    periodsNewestFirst(given);

    expect(given).toEqual(['2026-01', '2026-02']);
  });
});

// A failed read of the report is not "no report": it says what failed and leaves the person the controls that can mend it.
describe('viewStateOf when the report could not be read', () => {
  const failed = true;

  it('is error, not progress and not noData, whether or not the archive is up to date', () => {
    for (const upToDate of [true, false]) {
      expect(viewStateOf(status({ upToDate }), null, failed), `upToDate ${upToDate}`).toBe('error');
    }
  });

  it('is error with months archived and with none', () => {
    expect(viewStateOf(status({ periods: ['2026-10'] }), null, failed)).toBe('error');
    expect(viewStateOf(status({ periods: [], upToDate: false }), null, failed)).toBe('error');
  });

  it('is still notConnected when the package is not set up, and progress while a run is going', () => {
    expect(viewStateOf(status({ setup: 'notConfigured' }), null, failed)).toBe('notConnected');
    expect(viewStateOf(status({ fetch: fetchStatus({ running: true }) }), null, failed)).toBe('progress');
  });

  it('is ignored when there is a report to show', () => {
    expect(viewStateOf(status(), report(), failed)).toBe('ready');
  });

  it('is not the default: a read that has not failed leaves the other rules as they were', () => {
    expect(viewStateOf(status({ upToDate: false }), null, false)).toBe('progress');
    expect(viewStateOf(status({ upToDate: false }), null)).toBe('progress');
  });
});

describe('the strip and the active tab in the error state', () => {
  it('keeps Settings on the strip for a Settings user, and none for anyone else, as with any report that cannot be shown', () => {
    expect(tabsFor(null, true)).toEqual(['settings']);
    expect(tabsFor(null, false)).toEqual([]);
  });

  it('does not open Settings by itself for a read that failed: only a person who asked for it sees it', () => {
    expect(activeTab('analysis', ['settings'], 'error')).toBeNull();
    expect(activeTab('settings', ['settings'], 'error')).toBe('settings');
    expect(activeTab('analysis', [], 'error')).toBeNull();
  });
});

describe('shouldAcceptPeriodChange', () => {
  const periods = ['2026-10', '2026-09', '2026-08'];

  it('accepts another archived month when nothing is going on', () => {
    expect(shouldAcceptPeriodChange(false, '2026-10', '2026-09', periods)).toBe(true);
  });

  it('ignores a change while a read or a refresh is going on: the control stays focusable, it just does nothing', () => {
    expect(shouldAcceptPeriodChange(true, '2026-10', '2026-09', periods)).toBe(false);
  });

  it('ignores the month that is already selected, and a month that is not archived', () => {
    expect(shouldAcceptPeriodChange(false, '2026-10', '2026-10', periods)).toBe(false);
    expect(shouldAcceptPeriodChange(false, '2026-10', '2020-01', periods)).toBe(false);
    expect(shouldAcceptPeriodChange(false, '2026-10', '', periods)).toBe(false);
  });
});

describe('selectedPeriod', () => {
  it('is the month that is selected', () => {
    expect(selectedPeriod('2026-09', ['2026-08', '2026-09', '2026-10'])).toBe('2026-09');
  });

  it('is the newest archived month when none has been read yet, so the picker has a month to stand on after a failed read', () => {
    expect(selectedPeriod('', ['2026-08', '2026-10', '2026-09'])).toBe('2026-10');
  });

  it('is empty when there is nothing archived', () => {
    expect(selectedPeriod('', [])).toBe('');
  });
});
