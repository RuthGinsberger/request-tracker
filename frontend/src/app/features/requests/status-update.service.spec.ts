import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, Subject, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RequestsApiService } from '../../core/api/requests-api.service';
import { ApiError, ApiErrorCode, RequestItem, RequestStatus, allowedTargets } from '../../core/models';
import { CurrentUserService } from '../../core/user/current-user.service';
import { RequestsListStore } from './requests-list.store';
import { CONFLICT_MESSAGE, NO_USER_MESSAGE, StatusUpdateService } from './status-update.service';
import { SummaryStore } from './summary.store';

function row(overrides: Partial<RequestItem> = {}): RequestItem {
  return {
    id: 7,
    title: 'Printer',
    organizationName: 'Acme',
    status: 'New',
    priority: 'Low',
    assignedTo: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    version: 'AAAAAAAAB9E=',
    ...overrides,
  };
}

function problem(status: number, errorCode: ApiErrorCode, detail = 'server detail'): ApiError {
  return new ApiError(status, errorCode, 'title', detail);
}

describe('StatusUpdateService', () => {
  let updateStatus: ReturnType<typeof vi.fn<(id: number, status: RequestStatus, version: string) => Observable<RequestItem>>>;
  let get: ReturnType<typeof vi.fn<(id: number) => Observable<RequestItem>>>;
  let list: {
    replaceItem: ReturnType<typeof vi.fn>;
    retry: ReturnType<typeof vi.fn>;
    needsReloadAfterChange: ReturnType<typeof vi.fn>;
    view: ReturnType<typeof signal<{ status: string }>>;
  };
  let summary: { refresh: ReturnType<typeof vi.fn> };
  let snackBar: { open: ReturnType<typeof vi.fn> };
  let userSet: ReturnType<typeof signal<boolean>>;
  let service: StatusUpdateService;

  beforeEach(() => {
    TestBed.resetTestingModule();
    updateStatus = vi.fn();
    get = vi.fn();
    list = {
      replaceItem: vi.fn(),
      retry: vi.fn(),
      needsReloadAfterChange: vi.fn().mockReturnValue(false),
      view: signal({ status: 'loaded' }),
    };
    summary = { refresh: vi.fn() };
    snackBar = { open: vi.fn() };
    userSet = signal(true);

    TestBed.configureTestingModule({
      providers: [
        StatusUpdateService,
        { provide: RequestsApiService, useValue: { updateStatus, get } },
        { provide: CurrentUserService, useValue: { isSet: userSet } },
        { provide: RequestsListStore, useValue: list },
        { provide: SummaryStore, useValue: summary },
        { provide: MatSnackBar, useValue: snackBar },
      ],
    });
    service = TestBed.inject(StatusUpdateService);
  });

  it('on 200 sends the row version, updates the row in place, refreshes the summary and shows success', () => {
    const updated = row({ status: 'InProgress', version: 'NEWVERSION=' });
    updateStatus.mockReturnValue(of(updated));

    service.change(row(), 'InProgress');

    expect(updateStatus).toHaveBeenCalledWith(7, 'InProgress', 'AAAAAAAAB9E=');
    expect(list.replaceItem).toHaveBeenCalledWith(updated);
    expect(summary.refresh).toHaveBeenCalledTimes(1);
    expect(snackBar.open.mock.calls[0][0]).toContain('InProgress');
    expect(list.retry).not.toHaveBeenCalled();
    expect(service.pendingIds().size).toBe(0);
  });

  it('reloads the current page when the updated row no longer fits the filter', () => {
    list.needsReloadAfterChange.mockReturnValue(true);
    updateStatus.mockReturnValue(of(row({ status: 'InProgress' })));

    service.change(row(), 'InProgress');

    expect(list.retry).toHaveBeenCalledTimes(1);
  });

  it('restarts a list load that was in flight during the update', () => {
    list.view.set({ status: 'loading' });
    updateStatus.mockReturnValue(of(row({ status: 'InProgress' })));

    service.change(row(), 'InProgress');

    expect(list.retry).toHaveBeenCalledTimes(1);
  });

  it('on 409 shows the conflict message, refreshes the row from the server and does not retry the PATCH', () => {
    const fresh = row({ status: 'InProgress', version: 'FRESH=' });
    updateStatus.mockReturnValue(throwError(() => problem(409, 'version-conflict')));
    get.mockReturnValue(of(fresh));

    service.change(row(), 'InProgress');

    expect(snackBar.open.mock.calls[0][0]).toBe(CONFLICT_MESSAGE);
    expect(get).toHaveBeenCalledWith(7);
    expect(list.replaceItem).toHaveBeenCalledWith(fresh);
    expect(summary.refresh).toHaveBeenCalledTimes(1);
    expect(updateStatus).toHaveBeenCalledTimes(1);
    expect(service.pendingIds().size).toBe(0);
  });

  it('after a 409 the user can retry on the fresh row and succeeds', () => {
    const fresh = row({ status: 'InProgress', version: 'FRESH=' });
    updateStatus.mockReturnValueOnce(throwError(() => problem(409, 'version-conflict')));
    get.mockReturnValue(of(fresh));
    service.change(row(), 'InProgress');

    updateStatus.mockReturnValueOnce(of(row({ status: 'Waiting', version: 'V3=' })));
    service.change(fresh, 'Waiting');

    expect(updateStatus).toHaveBeenLastCalledWith(7, 'Waiting', 'FRESH=');
    expect(list.replaceItem).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Waiting' }));
  });

  it('on 409 falls back to reloading the list when the row refresh fails', () => {
    updateStatus.mockReturnValue(throwError(() => problem(409, 'version-conflict')));
    get.mockReturnValue(throwError(() => problem(500, 'internal-error')));

    service.change(row(), 'InProgress');

    expect(list.retry).toHaveBeenCalledTimes(1);
  });

  it('on 400 invalid transition shows the server message and refreshes the row', () => {
    updateStatus.mockReturnValue(
      throwError(() => problem(400, 'invalid-status-transition', 'Cannot move from New to Completed.')),
    );
    get.mockReturnValue(of(row()));

    service.change(row(), 'Completed');

    expect(snackBar.open.mock.calls[0][0]).toBe('Cannot move from New to Completed.');
    expect(get).toHaveBeenCalledWith(7);
  });

  it('on 404 shows a message and reloads the list', () => {
    updateStatus.mockReturnValue(throwError(() => problem(404, 'not-found')));

    service.change(row(), 'InProgress');

    expect(snackBar.open.mock.calls[0][0]).toContain('no longer exists');
    expect(list.retry).toHaveBeenCalledTimes(1);
    expect(list.replaceItem).not.toHaveBeenCalled();
  });

  it('on a network failure shows the message, reloads the list and releases the row', () => {
    updateStatus.mockReturnValue(throwError(() => new ApiError(0, 'network-error', 'Network', 'Cannot reach the server.')));

    service.change(row(), 'InProgress');

    expect(snackBar.open.mock.calls[0][0]).toBe('Cannot reach the server.');
    expect(list.retry).toHaveBeenCalledTimes(1);
    expect(service.pendingIds().size).toBe(0);
  });

  it('without a current user it does not call the API and asks for one', () => {
    userSet.set(false);

    service.change(row(), 'InProgress');

    expect(updateStatus).not.toHaveBeenCalled();
    expect(snackBar.open.mock.calls[0][0]).toBe(NO_USER_MESSAGE);
  });

  it('ignores a second submit while the first is in flight', () => {
    const response = new Subject<RequestItem>();
    updateStatus.mockReturnValue(response);

    service.change(row(), 'InProgress');
    expect(service.pendingIds().has(7)).toBe(true);
    service.change(row(), 'InProgress');

    expect(updateStatus).toHaveBeenCalledTimes(1);

    response.next(row({ status: 'InProgress' }));
    response.complete();
    expect(service.pendingIds().has(7)).toBe(false);
  });
});

describe('allowedTargets (UX mirror of the server transition table)', () => {
  const table: Record<RequestStatus, RequestStatus[]> = {
    New: ['InProgress'],
    InProgress: ['Waiting', 'Completed'],
    Waiting: ['InProgress', 'Completed'],
    Completed: [],
  };

  for (const [from, targets] of Object.entries(table) as [RequestStatus, RequestStatus[]][]) {
    it(`${from} -> ${targets.join(', ') || 'nothing'}`, () => {
      expect([...allowedTargets(from)]).toEqual(targets);
    });
  }

  it('never offers the same status or New', () => {
    for (const from of Object.keys(table) as RequestStatus[]) {
      expect(allowedTargets(from)).not.toContain(from);
      expect(allowedTargets(from)).not.toContain('New');
    }
  });
});
