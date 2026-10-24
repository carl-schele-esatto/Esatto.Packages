// The saved order's endpoint (NavigationOrderController). GET for every backoffice user, PUT for admins.

import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import type { NavigationOrderData } from './core/types';

const URL = '/umbraco/management/api/v1/esatto/navigation-order';

// `security: [bearer]` makes the shared backoffice client attach the user's token.
const security = () => [{ scheme: 'bearer' as const, type: 'http' as const }];

interface NavigationOrderResponse extends NavigationOrderData {
  updatedUtc: string | null;
  updatedBy: string | null;
}

const toOrder = (response: NavigationOrderResponse): NavigationOrderData => ({ sections: response.sections, tabs: response.tabs });

async function unwrap(request: Promise<{ data?: NavigationOrderResponse; error?: unknown }>, host: UmbControllerHost): Promise<NavigationOrderData> {
  // Notifications off: the callers decide what (if anything) the user sees.
  const { data, error } = await tryExecute(host, request, { disableNotifications: true });
  if (error || !data) throw error ?? new Error('The navigation order response was empty.');
  return toOrder(data as NavigationOrderResponse);
}

/** The saved order. Throws when it can't be fetched. */
export const fetchNavigationOrder = (host: UmbControllerHost): Promise<NavigationOrderData> =>
  unwrap(umbHttpClient.get<{ 200: NavigationOrderResponse }>({ url: URL, security: security() }), host);

/** Saves `order` for everyone and returns what the server stored. Throws when it isn't saved. */
export const saveNavigationOrder = (host: UmbControllerHost, order: NavigationOrderData): Promise<NavigationOrderData> =>
  unwrap(umbHttpClient.put<{ 200: NavigationOrderResponse }>({ url: URL, body: order, security: security() }), host);
