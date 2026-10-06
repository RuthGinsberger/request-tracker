/** Error codes the API puts in ProblemDetails.errorCode. */
export type ApiErrorCode =
  | 'validation-failed'
  | 'invalid-version'
  | 'invalid-status-transition'
  | 'not-found'
  | 'version-conflict'
  | 'precondition-required'
  | 'internal-error'
  | 'network-error'
  | 'unknown';

/** Extensions that only some problems carry (409, 400 invalid transition). */
export interface ApiErrorExtensions {
  currentStatus?: string;
  currentVersion?: string;
  requestedStatus?: string;
}

/** The only error type the rest of the app sees; built by the error interceptor. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly errorCode: ApiErrorCode,
    readonly title: string,
    readonly detail: string,
    readonly fieldErrors: Readonly<Record<string, readonly string[]>> = {},
    readonly extensions: ApiErrorExtensions = {},
    readonly traceId?: string,
  ) {
    super(detail || title);
    this.name = 'ApiError';
  }

  get isConflict(): boolean {
    return this.status === 409;
  }
}
