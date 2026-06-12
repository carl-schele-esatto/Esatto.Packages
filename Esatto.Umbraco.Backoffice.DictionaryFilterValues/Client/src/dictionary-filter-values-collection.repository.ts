import { UmbRepositoryBase } from "@umbraco-cms/backoffice/repository";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { EsattoDictionaryFilterValuesCollectionServerDataSource } from "./dictionary-filter-values-collection.server.data-source.js";
import type { DictionaryFilterValuesQuery } from "./api/dictionary-filter-values.service.js";

/**
 * Drop-in replacement for the built-in dictionary collection repository. The stock
 * collection element resolves this by repositoryAlias (we swap that alias to ours
 * in the entry point), so the rest of the collection UI keeps working unchanged.
 */
export class EsattoDictionaryFilterValuesCollectionRepository extends UmbRepositoryBase {
  #source: EsattoDictionaryFilterValuesCollectionServerDataSource;

  constructor(host: UmbControllerHost) {
    super(host);
    this.#source = new EsattoDictionaryFilterValuesCollectionServerDataSource(host);
  }

  async requestCollection(filter: DictionaryFilterValuesQuery) {
    return this.#source.getCollection(filter);
  }
}

export default EsattoDictionaryFilterValuesCollectionRepository;
