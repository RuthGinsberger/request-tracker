import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiError, ApiErrorCode, ApiErrorExtensions } from '../models';

const NETWORK_MESSAGE = 'Cannot reach the server. Check your connection and try again.';
const FALLBACK_MESSAGE = 'Something went wrong. Please try again.';

/** Maps every failed call to a typed ApiError and rethrows it; it never swallows errors. */
export const errorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: unknown) =>
      throwError(() => (error instanceof HttpErrorResponse ? toApiError(error) : error)),
    ),
  );

export function toApiError(response: HttpErrorResponse): ApiError {
  if (response.status === 0) {
    return new ApiError(0, 'network-error', 'Network error', NETWORK_MESSAGE);
  }

  const body = asRecord(response.error);
  if (body === null) {
    return new ApiError(response.status, 'unknown', FALLBACK_MESSAGE, FALLBACK_MESSAGE);
  }

  const title = stringOrUndefined(body['title']) ?? FALLBACK_MESSAGE;
  return new ApiError(
    response.status,
    (stringOrUndefined(body['errorCode']) as ApiErrorCode | undefined) ?? 'unknown',
    title,
    stringOrUndefined(body['detail']) ?? title,
    parseFieldErrors(body['errors']),
    parseExtensions(body),
    stringOrUndefined(body['traceId']),
  );
}

function parseFieldErrors(value: unknown): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  const record = asRecord(value);
  if (record === null) {
    return result;
  }
  for (const [field, messages] of Object.entries(record)) {
    if (Array.isArray(messages)) {
      result[field] = messages.filter((m): m is string => typeof m === 'string');
    }
  }
  return result;
}

function parseExtensions(body: Record<string, unknown>): ApiErrorExtensions {
  const extensions: ApiErrorExtensions = {};
  const currentStatus = stringOrUndefined(body['currentStatus']);
  const currentVersion = stringOrUndefined(body['currentVersion']);
  const requestedStatus = stringOrUndefined(body['requestedStatus']);
  if (currentStatus !== undefined) extensions.currentStatus = currentStatus;
  if (currentVersion !== undefined) extensions.currentVersion = currentVersion;
  if (requestedStatus !== undefined) extensions.requestedStatus = requestedStatus;
  return extensions;
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;
}

function stringOrUndefined(value: unknown): string | undefined {
  return typeof value === 'string' && value !== '' ? value : undefined;
}
