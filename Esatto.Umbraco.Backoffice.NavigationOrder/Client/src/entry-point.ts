import type { UmbEntryPointOnInit } from '@umbraco-cms/backoffice/extension-api';
import { fetchNavigationOrder } from './api';
import { readCachedOrder, writeCachedOrder } from './apply/cache';
import { startDragging } from './drag/drag-controller';
import { navigationOrder } from './navigation-order';

export const onInit: UmbEntryPointOnInit = (host) => {
  navigationOrder.start();

  // The cached order first, so a reload doesn't flash Umbraco's default order; then the server's.
  const cached = readCachedOrder();
  if (cached) navigationOrder.setOrder(cached);

  fetchNavigationOrder(host)
    .then((order) => {
      navigationOrder.setOrder(order);
      writeCachedOrder(order);
    })
    .catch((error) => console.warn('[Esatto NavigationOrder] Could not load the saved order.', error));

  startDragging(host);
};
