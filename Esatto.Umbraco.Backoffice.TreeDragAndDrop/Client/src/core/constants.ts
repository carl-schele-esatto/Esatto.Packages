// Esatto.Umbraco.Backoffice.TreeDragAndDrop — shared constants.
//
// Every identifier is distinct from the legacy ContentTreeDragAndDrop /
// MediaTreeDragAndDrop packages so all of them can coexist during migration.

export const ATTACHED_FLAG = 'esattoTreeDndAttached';
export const HOVER_EXPAND_MS = 700;
export const DRAG_MIME = 'application/x-esatto-tree-dnd';
export const HOST_TAG = 'esatto-tree-dnd';
export const LOG_PREFIX = '[esatto-tree-dnd]';

// Guard against re-evaluation. If anything ever causes an entry point to load
// twice (Bellissima remount, auth refresh, manifest re-eval), every additional
// load would add another wrapper layer onto attachShadow — every shadow root
// would then get N MutationObservers.
export const PATCH_FLAG = '__esattoTreeDndPatched';
