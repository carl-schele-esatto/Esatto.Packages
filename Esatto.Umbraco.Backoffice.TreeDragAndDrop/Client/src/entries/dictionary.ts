// backofficeEntryPoint for the Dictionary tree (Umb.Section.Translation).
// Emitted by the C# manifest reader only when Esatto:TreeDragAndDrop:Dictionary is true.

import { registerTree } from '../core/register';
import { dictionaryAdapter } from '../adapters/dictionary';

registerTree(dictionaryAdapter);
