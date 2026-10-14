// Run a Management API call for an adapter: no Umbraco toast (the host shows
// its own), throw on error so the host's error path runs.

import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

export async function run<T>(host: UmbControllerHost, request: Promise<T>): Promise<void> {
  const { error } = await tryExecute(host, request, { disableNotifications: true });
  if (error) throw error;
}
