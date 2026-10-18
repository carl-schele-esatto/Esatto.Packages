// backofficeEntryPoint for the Content tree (Umb.Section.Content).
// Emitted by the C# manifest reader only when Esatto:TreeDragAndDrop:Content is true
// and the legacy ContentTreeDragAndDrop package is not installed.

import { registerTree } from '../core/register';
import { contentAdapter } from '../adapters/content';

registerTree(contentAdapter);
