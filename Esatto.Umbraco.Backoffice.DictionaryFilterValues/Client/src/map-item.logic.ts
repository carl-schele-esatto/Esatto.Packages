// Pure mapping between our server response and the built-in dictionary collection
// item model. Kept separate so it is trivially unit-testable and is the single
// definition of the response<->model shape contract.

/** One item as returned by our server endpoint (mirrors the built-in dictionary shape). */
export interface DictionaryFilterValuesResponseItem {
  id: string;
  name?: string;
  parent?: { id: string } | null;
  translatedIsoCodes?: string[];
}

/** The envelope our server endpoint returns. */
export interface DictionaryFilterValuesResponse {
  items: DictionaryFilterValuesResponseItem[];
  total: number;
}

/**
 * The shape Umbraco's dictionary collection expects. `entityType` is hard-coded
 * `'dictionary'` rather than imported from a non-exported core constant.
 */
export interface UmbDictionaryCollectionItemModel {
  entityType: string;
  unique: string;
  name: string;
  parentUnique: string | null;
  translatedIsoCodes: string[];
}

const DICTIONARY_ENTITY_TYPE = "dictionary";

export function mapItem(item: DictionaryFilterValuesResponseItem): UmbDictionaryCollectionItemModel {
  return {
    entityType: DICTIONARY_ENTITY_TYPE,
    unique: item.id,
    name: item.name ?? "",
    parentUnique: item.parent ? item.parent.id : null,
    translatedIsoCodes: item.translatedIsoCodes ?? [],
  };
}
