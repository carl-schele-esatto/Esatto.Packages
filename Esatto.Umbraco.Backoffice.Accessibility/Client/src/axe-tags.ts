import type { WcagLevel, WcagVersion } from './types';

const VERSIONS: WcagVersion[] = ['V20', 'V21', 'V22'];
const LEVELS: WcagLevel[] = ['A', 'AA', 'AAA'];

/**
 * axe-core's WCAG tags with the version and level each one belongs to. These are the only WCAG level
 * tags axe 4.14 uses (there is no wcag21aaa / wcag22a / wcag22aaa); a test checks every one exists, so
 * a target can never silently select zero rules.
 */
const TAGS: { tag: string; version: WcagVersion; level: WcagLevel }[] = [
  { tag: 'wcag2a', version: 'V20', level: 'A' },
  { tag: 'wcag2aa', version: 'V20', level: 'AA' },
  { tag: 'wcag2aaa', version: 'V20', level: 'AAA' },
  { tag: 'wcag21a', version: 'V21', level: 'A' },
  { tag: 'wcag21aa', version: 'V21', level: 'AA' },
  { tag: 'wcag22aa', version: 'V22', level: 'AA' },
];

/** The axe `runOnly` tags for a WCAG target, plus `best-practice` when enabled. */
export function axeTags(version: WcagVersion, level: WcagLevel, bestPractice: boolean): string[] {
  const tags = TAGS
    .filter((t) => VERSIONS.indexOf(t.version) <= VERSIONS.indexOf(version) && LEVELS.indexOf(t.level) <= LEVELS.indexOf(level))
    .map((t) => t.tag);
  return bestPractice ? [...tags, 'best-practice'] : tags;
}
