// A failed request, as the client reads it.
//
// The server answers every refusal with ProblemDetails whose meaning is its `code` (and `args`, and the request's `traceId`), and
// sends no prose (Api/ApiProblems.cs). This turns whatever a failed request leaves behind into one typed error, so a dashboard never
// has to inspect HTTP details:
//
//   status   the HTTP status; 0 when nothing came back (a network failure) or the status is not known
//   code     ALWAYS a code the client has words for (error-codes.ts): the problem's own when it has one the client knows, otherwise
//            `unknown`. An answer without a code (a 500, a 502 from a gateway, the framework's own 400, a 403 with an empty body, a
//            body that is not JSON, no answer at all) is therefore `unknown`, and it is never worse than that
//   args     the code's arguments (string, number, boolean or null values only), or undefined
//   traceId  the server's id of the request, for an admin to find the log line; or undefined

import { displayCode, type ErrorCode } from './error-codes';
import type { TextArgs } from './types';

export class ApiError extends Error {
  readonly status: number;
  readonly code: ErrorCode;
  readonly args: TextArgs | undefined;
  readonly traceId: string | undefined;

  constructor(status: number, code: ErrorCode, args?: TextArgs, traceId?: string) {
    super(`The request failed (${status || 'no answer'}, ${code}).`);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.args = args;
    this.traceId = traceId;
  }
}

export const isApiError = (value: unknown): value is ApiError => value instanceof ApiError;

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value);

// The server only sends primitives in args (ApiProblems.PrimitivesOf); anything else is dropped here too, so a value that is not a
// plain figure or word can never reach a text.
function argsOf(value: unknown): TextArgs | undefined {
  if (!isRecord(value)) return undefined;

  const kept: TextArgs = {};
  for (const [name, item] of Object.entries(value)) {
    if (item === null || ['string', 'number', 'boolean'].includes(typeof item)) kept[name] = item as string | number | boolean | null;
  }
  return Object.keys(kept).length > 0 ? kept : undefined;
}

/**
 * The ApiError for what a failed request left behind. <c>failure</c> is the parsed body of an error answer (ProblemDetails), its text
 * when it was not JSON, or whatever a rejected request threw (a network failure is a TypeError); <c>status</c> is the response's HTTP
 * status when there was a response.
 */
export function toApiError(failure: unknown, status?: number): ApiError {
  if (isApiError(failure)) return failure;

  // Umbraco's UmbApiError carries the problem as `problemDetails`.
  const body = isRecord(failure) && isRecord(failure.problemDetails) ? failure.problemDetails : failure;
  if (!isRecord(body)) return new ApiError(status ?? 0, 'unknown');

  const fromBody = typeof body.status === 'number' ? body.status : 0;
  return new ApiError(
    status ?? fromBody,
    displayCode(body.code),
    argsOf(body.args),
    typeof body.traceId === 'string' ? body.traceId : undefined,
  );
}
