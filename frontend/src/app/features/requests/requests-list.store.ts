import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  BehaviorSubject,
  Subject,
  catchError,
  debounce,
  distinctUntilChanged,
  map,
  merge,
  of,
  skip,
  startWith,
  switchMap,
  timer,
} from 'rxjs';
import { RequestsApiService } from '../../core/api/requests-api.service';
import {
  ApiError,
  Lookups,
  PagedResult,
  RequestItem,
  RequestPriority,
  RequestSortField,
  RequestStatus,
  SortDirection,
} from '../../core/models';
import {
  DEFAULT_LIST_STATE,
  RequestsListState,
  hasActiveFilters,
  statesEqual,
  toApiQuery,
  withoutFilters,
} from './requests-list.state';

export const SEARCH_DEBOUNCE_MS = 300;

export type RequestsListStatus = 'loading' | 'loaded' | 'empty' | 'error';

export interface RequestsListViewModel {
  status: RequestsListStatus;
  items: readonly RequestItem[];
  totalCount: number;
  error: ApiError | null;
}

type LoadEvent =
  | { kind: 'loading' }
  | { kind: 'loaded'; result: PagedResult<RequestItem> }
  | { kind: 'error'; error: ApiError };

const EMPTY_LOOKUPS: Lookups = { organizations: [], assignees: [] };

/**
 * Owns the list's query state and the RxJS pipeline that turns it into server calls (R24-R26).
 * Provided by the page component, so each page instance has its own state and the store dies with it.
 * Components only call the `set*` methods and read the signals; they never touch HTTP.
 */
@Injectable()
export class RequestsListStore {
  private readonly api = inject(RequestsApiService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly searchInput$ = new Subject<string>();
  private readonly retry$ = new Subject<void>();
  private readonly state$ = new BehaviorSubject<RequestsListState>(DEFAULT_LIST_STATE);
  private started = false;

  /** The current query (committed search, filters, sort, page). */
  readonly state = toSignal(this.state$, { requireSync: true });
  readonly hasActiveFilters = computed(() => hasActiveFilters(this.state()));

  private readonly viewState = signal<RequestsListViewModel>({
    status: 'loading',
    items: [],
    totalCount: 0,
    error: null,
  });
  readonly view = this.viewState.asReadonly();

  /** Options for the organization and assignee selects; empty when the call fails (the filters then just offer nothing). */
  readonly lookups = toSignal(
    this.api.lookups().pipe(catchError(() => of(EMPTY_LOOKUPS))),
    { initialValue: EMPTY_LOOKUPS },
  );

  /** Starts the pipeline with the state read from the URL. Called once by the page. */
  start(initial: RequestsListState): void {
    if (this.started) {
      return;
    }
    this.started = true;
    // Set before subscribing, so the first request already uses the URL state (no wasted default call).
    this.state$.next(initial);

    // Search: debounce, trim, ignore repeats. An emptied box reloads at once; there is nothing to wait for.
    this.searchInput$
      .pipe(
        debounce((term) => (term.trim() === '' ? of(0) : timer(SEARCH_DEBOUNCE_MS))),
        map((term) => term.trim()),
        startWith(initial.search),
        distinctUntilChanged(),
        skip(1),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((search) => this.change({ search }));

    // Every state change and every retry goes through the same switchMap: a new query cancels the in-flight HTTP call.
    // catchError sits INSIDE the switchMap so an error ends only that inner request and the stream keeps working.
    merge(this.state$, this.retry$.pipe(map(() => this.state$.value)))
      .pipe(
        switchMap((state) =>
          this.api.list(toApiQuery(state)).pipe(
            map((result): LoadEvent => ({ kind: 'loaded', result })),
            catchError((error: unknown) => of<LoadEvent>({ kind: 'error', error: toApiError(error) })),
            startWith<LoadEvent>({ kind: 'loading' }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((event) => this.apply(event));
  }

  /** Raw search box text; the pipeline debounces it. */
  setSearch(term: string): void {
    this.searchInput$.next(term);
  }

  setStatus(status: readonly RequestStatus[]): void {
    this.change({ status });
  }

  setPriority(priority: readonly RequestPriority[]): void {
    this.change({ priority });
  }

  setOrganization(organizationName: string | null): void {
    this.change({ organizationName });
  }

  setAssignee(assignedTo: string | null): void {
    this.change({ assignedTo });
  }

  setDateRange(createdFrom: string | null, createdTo: string | null): void {
    if (createdFrom !== null && createdTo !== null && createdFrom > createdTo) {
      return; // the UI prevents this; the server would answer 400
    }
    this.change({ createdFrom, createdTo });
  }

  setSort(sortBy: RequestSortField, sortDirection: SortDirection): void {
    this.change({ sortBy, sortDirection });
  }

  /** Paging keeps every filter; a new page size starts again from page 1. */
  setPage(page: number, pageSize: number): void {
    const current = this.state$.value;
    this.update({ ...current, page: pageSize === current.pageSize ? page : 1, pageSize });
  }

  clearFilters(): void {
    this.update(withoutFilters(this.state$.value));
    // Drops a term that is still waiting in the debounce so it cannot come back after the clear.
    this.searchInput$.next('');
  }

  /** Re-runs the current query (the Retry button). */
  retry(): void {
    this.retry$.next();
  }

  /** Replaces one row in place (after a status update or a conflict refresh); other rows and the total are untouched. */
  replaceItem(updated: RequestItem): void {
    this.viewState.update((view) => ({
      ...view,
      items: view.items.map((row) => (row.id === updated.id ? updated : row)),
    }));
  }

  /** True when a row with this status would no longer belong on the current page (status filter, or a sort that moves it). */
  needsReloadAfterChange(newStatus: RequestStatus): boolean {
    const { status, sortBy } = this.state$.value;
    const leavesFilter = status.length > 0 && !status.includes(newStatus);
    return leavesFilter || sortBy === 'status' || sortBy === 'updatedAt';
  }

  /** A change to search, filters or sort always goes back to page 1. */
  private change(partial: Partial<RequestsListState>): void {
    this.update({ ...this.state$.value, ...partial, page: 1 });
  }

  private update(next: RequestsListState): void {
    if (!statesEqual(this.state$.value, next)) {
      this.state$.next(next);
    }
  }

  private apply(event: LoadEvent): void {
    switch (event.kind) {
      case 'loading':
        // Keep the previous rows visible; the page shows a progress bar over them.
        this.viewState.update((view) => ({ ...view, status: 'loading', error: null }));
        return;
      case 'error':
        this.viewState.set({ status: 'error', items: [], totalCount: 0, error: event.error });
        return;
      case 'loaded': {
        const { items, totalCount } = event.result;
        const state = this.state$.value;
        if (items.length === 0 && totalCount > 0 && state.page > 1) {
          // The filter shrank the result below the current page: go back to page 1 (this triggers a new load).
          this.update({ ...state, page: 1 });
          return;
        }
        this.viewState.set({ status: items.length === 0 ? 'empty' : 'loaded', items, totalCount, error: null });
        return;
      }
    }
  }
}

/** The error interceptor already produces ApiError; this only guards against anything else reaching the pipeline. */
function toApiError(error: unknown): ApiError {
  return error instanceof ApiError
    ? error
    : new ApiError(0, 'unknown', 'Unexpected error', 'Something went wrong while loading the requests.');
}

/** Message for the error state: the server's validation messages when it sent them, else its detail. */
export function errorMessage(error: ApiError): string {
  const fieldMessages = Object.values(error.fieldErrors).flat();
  return fieldMessages.length > 0 ? fieldMessages.join(' ') : error.message;
}
