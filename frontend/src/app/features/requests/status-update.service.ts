import { Injectable, inject, signal } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RequestsApiService } from '../../core/api/requests-api.service';
import { CurrentUserService } from '../../core/user/current-user.service';
import { ApiError, RequestItem, RequestStatus } from '../../core/models';
import { RequestsListStore } from './requests-list.store';
import { SummaryStore } from './summary.store';

export const CONFLICT_MESSAGE =
  'This request was changed by someone else. It has been refreshed; review it and try again.';
export const NO_USER_MESSAGE = 'Select a user in the toolbar before changing a status.';

/**
 * The status update flow (R28). The UI offers only allowed targets, but this is UX: the server decides.
 *  - requires a current user, sends PATCH with If-Match from the row's version;
 *  - a request id is "pending" while in flight, so the action is disabled and cannot be submitted twice;
 *  - 200: row updated in place, summary refreshed, success message; the list is reloaded if the row no longer fits;
 *  - 409: message, the row is refreshed from the server, the user decides whether to retry (never an automatic retry);
 *  - 400: the server's message; 404: message and list reload; network/other: message and list reload (outcome unknown).
 */
@Injectable()
export class StatusUpdateService {
  private readonly api = inject(RequestsApiService);
  private readonly user = inject(CurrentUserService);
  private readonly list = inject(RequestsListStore);
  private readonly summary = inject(SummaryStore);
  private readonly snackBar = inject(MatSnackBar);

  private readonly pending = signal<ReadonlySet<number>>(new Set());
  /** Ids with an update in flight; the table disables their action. */
  readonly pendingIds = this.pending.asReadonly();

  change(item: RequestItem, target: RequestStatus): void {
    if (!this.user.isSet()) {
      this.notify(NO_USER_MESSAGE);
      return;
    }
    if (this.pending().has(item.id)) {
      return;
    }
    this.setPending(item.id, true);

    this.api.updateStatus(item.id, target, item.version).subscribe({
      next: (updated) => {
        this.setPending(item.id, false);
        this.onUpdated(updated);
      },
      error: (error: unknown) => {
        this.setPending(item.id, false);
        this.onFailed(item, error);
      },
    });
  }

  private onUpdated(updated: RequestItem): void {
    this.list.replaceItem(updated);
    this.summary.refresh();
    this.notify(`Request ${updated.id} is now ${updated.status}.`);
    // The row may no longer belong here (status filter / sort), or a list load that started before this
    // update may still be in flight with the old status: reloading the current page (cancels it) keeps it consistent.
    if (this.list.needsReloadAfterChange(updated.status) || this.list.view().status === 'loading') {
      this.list.retry();
    }
  }

  private onFailed(item: RequestItem, error: unknown): void {
    if (!(error instanceof ApiError)) {
      this.notify('Something went wrong while updating the request.');
      this.list.retry();
      return;
    }
    if (error.isConflict) {
      this.notify(CONFLICT_MESSAGE);
      this.refreshRow(item.id);
      return;
    }
    switch (error.status) {
      case 400:
        this.notify(error.detail || error.title);
        // An invalid transition means our row was out of date; show what the server has.
        this.refreshRow(item.id);
        return;
      case 404:
        this.notify('This request no longer exists. The list was reloaded.');
        this.list.retry();
        return;
      default:
        // Includes network failure (status 0): the update may or may not have been applied, so reload.
        this.notify(error.message);
        this.list.retry();
        this.summary.refresh();
    }
  }

  /** Loads the current server state of one row (the `currentStatus`/`currentVersion` extensions lack the other fields). */
  private refreshRow(id: number): void {
    this.api.get(id).subscribe({
      next: (fresh) => {
        this.list.replaceItem(fresh);
        this.summary.refresh();
      },
      error: () => this.list.retry(),
    });
  }

  private setPending(id: number, inFlight: boolean): void {
    this.pending.update((current) => {
      const next = new Set(current);
      if (inFlight) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }

  private notify(message: string): void {
    this.snackBar.open(message, 'OK', { duration: 6000 });
  }
}
