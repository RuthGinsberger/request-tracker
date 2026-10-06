import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RequestsApiService } from '../../core/api/requests-api.service';
import { ApiError, PagedResult, RequestItem, RequestListQuery } from '../../core/models';
import { DEFAULT_LIST_STATE } from './requests-list.state';
import { RequestsListStore, SEARCH_DEBOUNCE_MS } from './requests-list.store';

function item(id: number): RequestItem {
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
  };
}

function pageOf(items: RequestItem[], totalCount = items.length, page = 1): PagedResult<RequestItem> {
  return { items, page, pageSize: 20, totalCount, totalPages: Math.ceil(totalCount / 20) };
}

/** Fake API: every list call is recorded and answered by the test through its own subject. */
class FakeApi {
  readonly calls: { query: RequestListQuery; response: Subject<PagedResult<RequestItem>> }[] = [];

  lookups = () => of({ organizations: ['Acme'], assignees: ['dana'] });

  list = (query: RequestListQuery): Observable<PagedResult<RequestItem>> =>
    new Observable((subscriber) => {
      const call = { query, response: new Subject<PagedResult<RequestItem>>() };
      this.calls.push(call);
      return call.response.subscribe(subscriber);
    });

  get last() {
    return this.calls[this.calls.length - 1];
  }
}

function setup(initial = DEFAULT_LIST_STATE) {
  const api = new FakeApi();
  TestBed.configureTestingModule({
    providers: [RequestsListStore, { provide: RequestsApiService, useValue: api }],
  });
  const store = TestBed.inject(RequestsListStore);
  store.start(initial);
  return { api, store };
}

describe('RequestsListStore', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.resetTestingModule();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('loads once on start with the URL state and shows loading until the response', () => {
    const { api, store } = setup({ ...DEFAULT_LIST_STATE, page: 3 });

    expect(api.calls).toHaveLength(1);
    expect(api.last.query.page).toBe(3);
    expect(store.view().status).toBe('loading');

    api.last.response.next(pageOf([item(1), item(2)], 50, 3));

    expect(store.view()).toMatchObject({ status: 'loaded', totalCount: 50 });
    expect(store.view().items).toHaveLength(2);
  });

  it('debounces the search: rapid typing sends one request for the last term', () => {
    const { api, store } = setup();
    api.last.response.next(pageOf([item(1)]));

    for (const term of ['a', 'ac', 'acm', 'acme']) {
      store.setSearch(term);
      vi.advanceTimersByTime(100); // always shorter than the debounce
    }
    expect(api.calls).toHaveLength(1); // nothing sent yet

    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);

    expect(api.calls).toHaveLength(2);
    expect(api.last.query.search).toBe('acme');
  });

  it('ignores a search that trims to the term already applied (distinct)', () => {
    const { api, store } = setup();
    store.setSearch('acme');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    expect(api.calls).toHaveLength(2);

    store.setSearch('  acme  ');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);

    expect(api.calls).toHaveLength(2);
  });

  it('reloads immediately when the search is cleared', () => {
    const { api, store } = setup();
    store.setSearch('acme');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    expect(api.calls).toHaveLength(2);

    store.setSearch('');

    expect(api.calls).toHaveLength(3); // no timer advance
    expect(api.last.query.search).toBeUndefined();
  });

  it('cancels the in-flight request when a newer query starts and renders only the last response', () => {
    const { api, store } = setup();
    const first = api.last;

    store.setStatus(['New']);
    const second = api.last;
    expect(api.calls).toHaveLength(2);
    expect(first.response.observed).toBe(false); // switchMap unsubscribed from the first call

    second.response.next(pageOf([item(2)]));
    first.response.next(pageOf([item(1)])); // a late answer to the cancelled call is ignored

    expect(store.view().items.map((i) => i.id)).toEqual([2]);
  });

  it('goes back to page 1 when search, a filter or the sort changes, and keeps filters when paging', () => {
    const { api, store } = setup();
    store.setPage(4, 20);
    expect(api.last.query.page).toBe(4);

    store.setPriority(['High']);
    expect(api.last.query).toMatchObject({ page: 1, priority: ['High'] });

    store.setPage(3, 20);
    expect(api.last.query).toMatchObject({ page: 3, priority: ['High'] });

    store.setSort('title', 'asc');
    expect(api.last.query).toMatchObject({ page: 1, sortBy: 'title', sortDirection: 'asc', priority: ['High'] });

    store.setPage(2, 20);
    store.setSearch('acme');
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS);
    expect(api.last.query).toMatchObject({ page: 1, search: 'acme' });
  });

  it('goes back to page 1 when the page size changes', () => {
    const { api, store } = setup();
    store.setPage(5, 20);

    store.setPage(5, 50);

    expect(api.last.query).toMatchObject({ page: 1, pageSize: 50 });
  });

  it('does not send an invalid date range', () => {
    const { api, store } = setup();

    store.setDateRange('2026-02-01', '2026-01-01');

    expect(api.calls).toHaveLength(1);
  });

  it('returns to page 1 when the page is beyond the end after filtering', () => {
    const { api, store } = setup({ ...DEFAULT_LIST_STATE, page: 5 });

    api.last.response.next(pageOf([], 12, 5)); // 200 with no items but a real total

    expect(api.calls).toHaveLength(2);
    expect(api.last.query.page).toBe(1);
    expect(store.view().status).toBe('loading');
  });

  it('shows empty when the server returns no rows', () => {
    const { api, store } = setup();

    api.last.response.next(pageOf([], 0));

    expect(store.view().status).toBe('empty');
  });

  it('keeps the previous rows visible while the next query loads', () => {
    const { api, store } = setup();
    api.last.response.next(pageOf([item(1), item(2)]));

    store.setStatus(['Waiting']);

    expect(store.view().status).toBe('loading');
    expect(store.view().items).toHaveLength(2);
  });

  it('recovers from an error: the stream survives and Retry repeats the same query', () => {
    const { api, store } = setup();
    store.setPriority(['High']);
    api.last.response.error(new ApiError(500, 'internal-error', 'Server error', 'Boom'));

    expect(store.view().status).toBe('error');
    expect(store.view().error?.message).toBe('Boom');

    store.retry();

    expect(api.calls).toHaveLength(3);
    expect(api.last.query).toMatchObject({ priority: ['High'], page: 1 });
    expect(store.view().status).toBe('loading');

    api.last.response.next(pageOf([item(7)]));
    expect(store.view().status).toBe('loaded');

    // and a normal filter change still works after the failure
    store.setStatus(['New']);
    expect(api.calls).toHaveLength(4);
  });

  it('turns a non-ApiError failure into a generic error state', () => {
    const { api, store } = setup();
    api.list = () => throwError(() => new Error('weird'));

    store.setStatus(['New']);

    expect(store.view().status).toBe('error');
    expect(store.view().error?.errorCode).toBe('unknown');
  });

  it('clearFilters resets filters and page but keeps sort and page size, with one request', () => {
    const { api, store } = setup();
    store.setSort('title', 'asc');
    store.setPage(1, 50);
    store.setStatus(['New']);
    store.setPage(3, 50);
    const before = api.calls.length;

    store.clearFilters();

    expect(api.calls).toHaveLength(before + 1);
    expect(api.last.query).toMatchObject({ page: 1, pageSize: 50, sortBy: 'title', sortDirection: 'asc' });
    expect(api.last.query.status).toEqual([]);
    expect(store.hasActiveFilters()).toBe(false);
  });
});
