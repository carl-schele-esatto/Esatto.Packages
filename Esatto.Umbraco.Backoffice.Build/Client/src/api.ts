// The dashboard's one endpoint (BuildInfoController). Its shape is in types.ts.

import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import type { BuildInfo } from './types';

export const BASE = '/umbraco/management/api/v1/esatto/build-info';

// `security: [bearer]` makes the shared backoffice client authenticate the request.
const security = () => [{ scheme: 'bearer' as const, type: 'http' as const }];

export const getBuildInfo = () => umbHttpClient.get<{ 200: BuildInfo }>({ url: BASE, security: security() });
