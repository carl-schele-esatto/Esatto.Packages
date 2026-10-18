import { describe, it, expect, beforeEach } from 'vitest';
import { registerAdapter, adapterForEntityType, clearAdapters } from '../src/core/adapters';
import type { TreeAdapter } from '../src/core/types';

const fake = (entityType: string): TreeAdapter => ({
  entityType,
  rootEntityType: `${entityType}-root`,
  innerTag: 'umb-default-tree-item',
  move: async () => {},
});

describe('adapter registry', () => {
  beforeEach(() => clearAdapters());

  it('finds a registered adapter by entity type', () => {
    const dictionary = fake('dictionary');
    registerAdapter(dictionary);
    expect(adapterForEntityType('dictionary')).toBe(dictionary);
  });

  it('returns undefined for unregistered or missing entity types', () => {
    registerAdapter(fake('dictionary'));
    expect(adapterForEntityType('media')).toBeUndefined();
    expect(adapterForEntityType(null)).toBeUndefined();
    expect(adapterForEntityType(undefined)).toBeUndefined();
  });

  // Review Focus 5: an entry evaluated twice must not leave two adapters behind.
  it('registering the same entity type twice keeps exactly one adapter', () => {
    const first = fake('dictionary');
    const second = fake('dictionary');
    registerAdapter(first);
    registerAdapter(second);
    expect(adapterForEntityType('dictionary')).toBe(second);
  });
});
