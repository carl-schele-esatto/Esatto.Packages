import { NavigationOrderApplier } from './apply/applier';
import { createRegistrySource } from './apply/registry-source';

/** The one applier for the page, shared by the entry point and the drag code. */
export const navigationOrder = new NavigationOrderApplier(createRegistrySource());
