import {
  RequestListQuery,
  RequestPriority,
  RequestSortField,
  RequestStatus,
  SortDirection,
} from '../../core/models';

export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;
export const DEFAULT_PAGE_SIZE = 20;
/** Matches the API: search is at most 100 characters after trimming. */
export const MAX_SEARCH_LENGTH = 100;

/** Everything that defines which rows the list shows. Dates are yyyy-MM-dd strings (the API format). */
export interface RequestsListState {
  search: string;
  status: readonly RequestStatus[];
  priority: readonly RequestPriority[];
  organizationName: string | null;
  assignedTo: string | null;
  createdFrom: string | null;
  createdTo: string | null;
  sortBy: RequestSortField;
  sortDirection: SortDirection;
  page: number;
  pageSize: number;
}

export const DEFAULT_LIST_STATE: RequestsListState = {
  search: '',
  status: [],
  priority: [],
  organizationName: null,
  assignedTo: null,
  createdFrom: null,
  createdTo: null,
  sortBy: 'createdAt',
  sortDirection: 'desc',
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
};

/** True when any filter (not sort or paging) differs from the default. */
export function hasActiveFilters(state: RequestsListState): boolean {
  return (
    state.search !== '' ||
    state.status.length > 0 ||
    state.priority.length > 0 ||
    state.organizationName !== null ||
    state.assignedTo !== null ||
    state.createdFrom !== null ||
    state.createdTo !== null
  );
}

/** Filters back to default; sort and page size are the user's view preferences and stay. */
export function withoutFilters(state: RequestsListState): RequestsListState {
  return {
    ...DEFAULT_LIST_STATE,
    sortBy: state.sortBy,
    sortDirection: state.sortDirection,
    pageSize: state.pageSize,
  };
}

export function statesEqual(a: RequestsListState, b: RequestsListState): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

/** The API query for a state; empty values are omitted by `toHttpParams`. */
export function toApiQuery(state: RequestsListState): RequestListQuery {
  return {
    search: state.search || undefined,
    status: state.status,
    priority: state.priority,
    organizationName: state.organizationName ?? undefined,
    assignedTo: state.assignedTo ?? undefined,
    createdFrom: state.createdFrom ?? undefined,
    createdTo: state.createdTo ?? undefined,
    sortBy: state.sortBy,
    sortDirection: state.sortDirection,
    page: state.page,
    pageSize: state.pageSize,
  };
}
