import type { ManagedManifest } from '../core/types';

/**
 * A raw manifest with its kind's weight filled in when it has none of its own: Umbraco sorts by the
 * kind-merged manifest, e.g. a section with `kind: 'default'` and no weight sits at 1000
 * (Umb.Kind.Section.Default), not 0. `merged` is the manifest as umbExtensionsRegistry.getByAlias returns it.
 */
export function withKindWeight(raw: ManagedManifest, merged: { weight?: number } | undefined): ManagedManifest {
  return raw.weight === undefined && merged?.weight !== undefined ? { ...raw, weight: merged.weight } : raw;
}
