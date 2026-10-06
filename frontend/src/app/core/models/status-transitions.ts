import { RequestStatus } from './enums';

/**
 * UX-only mirror of the server's transition table (ARCHITECTURE section 3), used to offer only sensible targets.
 * The server (StatusTransitionPolicy plus the RowVersion check) is the authority and re-validates every update.
 */
const ALLOWED_TARGETS: Readonly<Record<RequestStatus, readonly RequestStatus[]>> = {
  New: ['InProgress'],
  InProgress: ['Waiting', 'Completed'],
  Waiting: ['InProgress', 'Completed'],
  Completed: [],
};

export function allowedTargets(from: RequestStatus): readonly RequestStatus[] {
  return ALLOWED_TARGETS[from];
}
