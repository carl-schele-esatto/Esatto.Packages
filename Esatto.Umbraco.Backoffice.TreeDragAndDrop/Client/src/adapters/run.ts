// Run Management API calls for an adapter: no Umbraco toast (the host shows
// its own), throw on error so the host's error path runs.

import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

export async function run<T>(host: UmbControllerHost, request: Promise<T>): Promise<void> {
  const { error } = await tryExecute(host, request, { disableNotifications: true });
  if (error) throw error;
}

// Same, for calls whose response body is needed.
export async function read<T extends { data?: unknown }>(host: UmbControllerHost, request: Promise<T>): Promise<NonNullable<T['data']>> {
  const { data, error } = await tryExecute(host, request, { disableNotifications: true });
  if (error) throw error;
  if (data === undefined || data === null) throw new Error('Empty response from the server');
  return data as NonNullable<T['data']>;
}
