import { tryExecute } from "@umbraco-cms/backoffice/resources";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import {
  searchDictionaryFilterValues,
  type DictionaryFilterValuesQuery,
} from "./api/dictionary-filter-values.service.js";
import {
  mapItem,
  type DictionaryFilterValuesResponse,
  type UmbDictionaryCollectionItemModel,
} from "./map-item.logic.js";

/**
 * Collection data source that fetches dictionary items (matched by key OR
 * translation value, server-side) from our authenticated endpoint and maps them
 * to the built-in dictionary collection item model.
 */
export class EsattoDictionaryFilterValuesCollectionServerDataSource {
  #host: UmbControllerHost;

  constructor(host: UmbControllerHost) {
    this.#host = host;
  }

  async getCollection(query: DictionaryFilterValuesQuery) {
    const { data, error } = await tryExecute(this.#host, searchDictionaryFilterValues(query));

    if (data) {
      const payload = data as unknown as DictionaryFilterValuesResponse;
      // Defensive: a 200 with a missing items array must not throw past the
      // {data}/{error} contract the collection context expects.
      const items: UmbDictionaryCollectionItemModel[] = (payload.items ?? []).map(mapItem);
      return { data: { items, total: payload.total ?? items.length } };
    }

    return { error };
  }
}
