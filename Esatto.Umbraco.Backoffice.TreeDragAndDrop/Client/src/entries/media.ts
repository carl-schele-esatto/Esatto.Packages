// backofficeEntryPoint for the Media tree (Umb.Section.Media).
// Emitted by the C# manifest reader only when Esatto:TreeDragAndDrop:Media is true
// and the legacy MediaTreeDragAndDrop package is not installed.

import { registerTree } from '../core/register';
import { mediaAdapter } from '../adapters/media';

registerTree(mediaAdapter);
