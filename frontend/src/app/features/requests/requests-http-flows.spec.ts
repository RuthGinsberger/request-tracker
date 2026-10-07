import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { errorInterceptor } from '../../core/interceptors/error.interceptor';
import { PagedResult, RequestItem } from '../../core/models';
import { CurrentUserService } from '../../core/user/current-user.service';
import { DEFAULT_LIST_STATE } from './requests-list.state';
import { RequestsListStore, SEARCH_DEBOUNCE_MS } from './requests-list.store';
import { CONFLICT_MESSAGE, StatusUpdateService } from './status-update.service';
import { SummaryStore } from './summary.store';

/**
 * The client-side search pipeline and the status-update flow, driven through the real store, services and
 * HttpClient with HttpTestingController. Only the network and the clock are fake, so what is asserted
 * (pending, cancelled, flushed requests) is what the browser would do.
 */

const LIST_URL = '/api/requests';

function item(id: number, overrides: Partial<RequestItem> = {}): RequestItem {
  return {
    id,
    title: `Request ${id}`,
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

function pageOf(items: RequestItem[], page = 1): PagedResult<RequestItem> {
  return { items, page, pageSize: 20, totalCount: items.length, totalPages: 1 };
}

describe('search pipeline and status update over HTTP', () => {
  let http: HttpTestingController;
  let store: RequestsListStore;
  let snackBar: { open: ReturnType<typeof vi.fn> };

  /** All list calls currently outstanding (the list URL, not summary/lookups/by-id). */
  const listRequests = (): TestRequest[] => http.match((req) => req.url === LIST_URL);
  const expectOneList = (): TestRequest => http.expectOne((req) => req.url === LIST_URL);

  /** Starts the store and answers the lookups and the first list load, so tests begin from a settled page. */
  function startLoaded(rows: RequestItem[] = [item(1)]): void {
    store.start(DEFAULT_LIST_STATE);
    http.expectOne('/api/lookups').flush({ organizations: [], assignees: [] });
    expectOneList().flush(pageOf(rows));
  }

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.resetTestingModule();
    localStorage.clear();
    snackBar = { open: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        RequestsListStore,
        SummaryStore,
        StatusUpdateService,
        { provide: MatSnackBar, useValue: snackBar },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    store = TestBed.inject(RequestsListStore);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
    localStorage.clear();
  });

  it('debounce: several terms typed within 300 ms produce exactly one request, with the last term', () => {
    startLoaded();

    for (const term of ['a', 'ac', 'acm', 'acme']) {
      store.setSearch(term);
      vi.advanceTimersByTime(100); // each keystroke arrives before the debounce elapses
    }
    expect(listRequests()).toHaveLength(0);

    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);

    const req = expectOneList();
    expect(req.request.params.get('search')).toBe('acme');
    req.flush(pageOf([item(2)]));
  });

  it('cancellation: a new term while a request is pending cancels it and only the latest response is rendered', () => {
    startLoaded();

    store.setSearch('ab');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    const stale = expectOneList();
    expect(stale.request.params.get('search')).toBe('ab');
    expect(stale.cancelled).toBe(false); // still in flight, not answered

    store.setSearch('abc');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);

    expect(stale.cancelled).toBe(true); // the browser request for 'ab' was aborted
    const latest = expectOneList();
    expect(latest.request.params.get('search')).toBe('abc');

    latest.flush(pageOf([item(42, { title: 'Result for abc' })]));

    expect(store.view().status).toBe('loaded');
    expect(store.view().items.map((row) => row.id)).toEqual([42]);
  });

  it('distinct: the same term after trimming triggers no new request', () => {
    startLoaded();
    store.setSearch('acme');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    expectOneList().flush(pageOf([item(2)]));

    store.setSearch('  acme  ');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);

    expect(listRequests()).toHaveLength(0);
    expect(store.view().items.map((row) => row.id)).toEqual([2]);
  });

  it('filter change: resets to page 1 and requests immediately, without waiting for the debounce', () => {
    startLoaded();
    store.setPage(3, 20);
    expectOneList().flush(pageOf([item(3)], 3));

    store.setPriority(['High']); // no timer advance

    const req = expectOneList();
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.getAll('priority')).toEqual(['High']);
    req.flush(pageOf([item(4)]));
  });

  it('error: a failed load shows the error state, and Retry reloads into the loaded state', () => {
    startLoaded();
    store.setStatus(['New']);
    expectOneList().flush(
      { title: 'Server error', detail: 'Boom', errorCode: 'internal-error' },
      { status: 500, statusText: 'Internal Server Error' },
    );

    expect(store.view().status).toBe('error');
    expect(store.view().error?.message).toBe('Boom');

    store.retry();
    expect(store.view().status).toBe('loading');
    const retried = expectOneList();
    expect(retried.request.params.getAll('status')).toEqual(['New']); // same query as before
    retried.flush(pageOf([item(9)]));

    expect(store.view().status).toBe('loaded');
    expect(store.view().items.map((row) => row.id)).toEqual([9]);
  });

  describe('status update', () => {
    let service: StatusUpdateService;

    beforeEach(() => {
      TestBed.inject(CurrentUserService).set('dana');
      service = TestBed.inject(StatusUpdateService);
    });

    it('409: shows the conflict message, refreshes the row from the server and never retries the PATCH', () => {
      startLoaded([item(7)]);

      service.change(item(7), 'InProgress');
      const patch = http.expectOne((req) => req.method === 'PATCH');
      expect(patch.request.url).toBe('/api/requests/7/status');
      expect(patch.request.headers.get('If-Match')).toBe('"AAAAAAAAB9E="');
      patch.flush(
        { title: 'Conflict', detail: 'Version mismatch', errorCode: 'version-conflict' },
        { status: 409, statusText: 'Conflict' },
      );

      expect(snackBar.open.mock.calls[0][0]).toBe(CONFLICT_MESSAGE);

      // The row is re-read from the server and replaced in the list.
      const refresh = http.expectOne('/api/requests/7');
      expect(refresh.request.method).toBe('GET');
      refresh.flush(item(7, { status: 'Waiting', version: 'FRESH=' }));
      http.expectOne('/api/requests/summary').flush({});

      expect(store.view().items[0]).toMatchObject({ status: 'Waiting', version: 'FRESH=' });

      // No automatic retry: no further PATCH is ever issued (verified in afterEach as well).
      vi.advanceTimersByTime(60_000);
      expect(http.match((req) => req.method === 'PATCH')).toHaveLength(0);
      expect(service.pendingIds().size).toBe(0);
    });

    it('200: updates the row in place and requests a summary refresh', () => {
      startLoaded([item(6), item(7), item(8)]);

      service.change(item(7), 'InProgress');
      const patch = http.expectOne((req) => req.method === 'PATCH');
      expect(patch.request.body).toEqual({ status: 'InProgress' });
      patch.flush(item(7, { status: 'InProgress', version: 'V2=' }));

      expect(store.view().items.map((row) => row.id)).toEqual([6, 7, 8]); // same order, same page
      expect(store.view().items[1]).toMatchObject({ status: 'InProgress', version: 'V2=' });
      expect(store.view().items[0].status).toBe('New');
      http.expectOne('/api/requests/summary').flush({}); // summary refresh was requested
      expect(listRequests()).toHaveLength(0); // no list reload: the row still fits
    });
  });
});
