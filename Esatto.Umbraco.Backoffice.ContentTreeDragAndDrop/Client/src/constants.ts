// Backoffice.ContentTreeDnd — shared constants.

export const ATTACHED_FLAG = 'backofficeContentTreeDndAttached';
export const HOVER_EXPAND_MS = 700;
export const ENTITY_TYPE_DOCUMENT = 'document';
export const DRAG_MIME = 'application/x-backoffice-content-tree-dnd';

// Guard against re-evaluation. If anything ever causes a backofficeEntryPoint
// to load twice (Bellissima remount, auth refresh, manifest re-eval), every
// additional load would add another wrapper layer onto attachShadow — every
// shadow root would then get N MutationObservers.
export const PATCH_FLAG = '__backofficeContentTreeDndPatched';
