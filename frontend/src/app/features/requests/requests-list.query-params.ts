import { Params } from '@angular/router';
import {
  REQUEST_PRIORITIES,
  REQUEST_STATUSES,
  RequestPriority,
  RequestSortField,
  RequestStatus,
  SORT_FIELDS,
  SortDirection,
} from '../../core/models';
import {
  DEFAULT_LIST_STATE,
  MAX_SEARCH_LENGTH,
  PAGE_SIZE_OPTIONS,
  RequestsListState,
} from './requests-list.state';

/** The read side of Angular's ParamMap; lets tests pass a plain object. */
export interface QueryParamSource {
  get(name: string): string | null;
  getAll(name: string): string[];
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const MAX_PAGE = 1_000_000;

/** Reads the list state from the URL; every invalid or unknown value falls back to its default. */
export function parseListState(params: QueryParamSource): RequestsListState {
  const defaults = DEFAULT_LIST_STATE;
  const from = parseDate(params.get('createdFrom'));
  const to = parseDate(params.get('createdTo'));
  const rangeIsValid = from === null || to === null || from <= to;

  return {
    search: (params.get('search') ?? '').trim().slice(0, MAX_SEARCH_LENGTH),
    status: parseEnumList<RequestStatus>(params.getAll('status'), REQUEST_STATUSES),
    priority: parseEnumList<RequestPriority>(params.getAll('priority'), REQUEST_PRIORITIES),
    organizationName: parseText(params.get('organizationName'), 200),
    assignedTo: parseText(params.get('assignedTo'), 100),
    createdFrom: rangeIsValid ? from : null,
    createdTo: rangeIsValid ? to : null,
    sortBy: parseOneOf<RequestSortField>(params.get('sortBy'), SORT_FIELDS) ?? defaults.sortBy,
    sortDirection: parseOneOf<SortDirection>(params.get('sortDirection'), ['asc', 'desc']) ?? defaults.sortDirection,
    page: parseInteger(params.get('page'), 1, MAX_PAGE) ?? defaults.page,
    pageSize: parsePageSize(params.get('pageSize')) ?? defaults.pageSize,
  };
}

/** Writes the list state to URL parameters; defaults are omitted so the plain URL stays clean. */
export function toQueryParams(state: RequestsListState): Params {
  const defaults = DEFAULT_LIST_STATE;
  return {
    search: state.search || null,
    status: state.status.length > 0 ? [...state.status] : null,
    priority: state.priority.length > 0 ? [...state.priority] : null,
    organizationName: state.organizationName,
    assignedTo: state.assignedTo,
    createdFrom: state.createdFrom,
    createdTo: state.createdTo,
    sortBy: state.sortBy === defaults.sortBy ? null : state.sortBy,
    sortDirection: state.sortDirection === defaults.sortDirection ? null : state.sortDirection,
    page: state.page === defaults.page ? null : state.page,
    pageSize: state.pageSize === defaults.pageSize ? null : state.pageSize,
  };
}

function parseOneOf<T extends string>(value: string | null, allowed: readonly T[]): T | null {
  return allowed.find((candidate) => candidate === value) ?? null;
}

function parseEnumList<T extends string>(values: readonly string[], allowed: readonly T[]): T[] {
  // Allowed order, duplicates and unknown values dropped.
  return allowed.filter((candidate) => values.includes(candidate));
}

function parseText(value: string | null, maxLength: number): string | null {
  const text = value?.trim() ?? '';
  return text === '' || text.length > maxLength ? null : text;
}

function parseInteger(value: string | null, min: number, max: number): number | null {
  if (value === null || !/^\d+$/.test(value)) {
    return null;
  }
  const number = Number(value);
  return number >= min && number <= max ? number : null;
}

function parsePageSize(value: string | null): number | null {
  const size = parseInteger(value, 1, 100);
  return size !== null && (PAGE_SIZE_OPTIONS as readonly number[]).includes(size) ? size : null;
}

/** Accepts only real calendar dates written as yyyy-MM-dd. */
function parseDate(value: string | null): string | null {
  if (value === null || !ISO_DATE.test(value)) {
    return null;
  }
  const parsed = new Date(`${value}T00:00:00Z`);
  return !Number.isNaN(parsed.getTime()) && parsed.toISOString().startsWith(value) ? value : null;
}
