import { convertToParamMap } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { parseListState, toQueryParams } from './requests-list.query-params';
import { DEFAULT_LIST_STATE } from './requests-list.state';

const parse = (params: Record<string, string | string[]>) => parseListState(convertToParamMap(params));

describe('list query params', () => {
  it('returns the defaults for an empty URL', () => {
    expect(parse({})).toEqual(DEFAULT_LIST_STATE);
  });

  it('reads valid values', () => {
    const state = parse({
      search: ' acme ',
      status: ['Waiting', 'New'],
      priority: 'High',
      organizationName: 'Acme Ltd',
      assignedTo: 'dana',
      createdFrom: '2026-01-01',
      createdTo: '2026-02-28',
      sortBy: 'title',
      sortDirection: 'asc',
      page: '3',
      pageSize: '50',
    });

    expect(state).toEqual({
      search: 'acme',
      status: ['New', 'Waiting'],
      priority: ['High'],
      organizationName: 'Acme Ltd',
      assignedTo: 'dana',
      createdFrom: '2026-01-01',
      createdTo: '2026-02-28',
      sortBy: 'title',
      sortDirection: 'asc',
      page: 3,
      pageSize: 50,
    });
  });

  it('falls back to defaults for invalid values', () => {
    const state = parse({
      status: ['Bogus', 'New'],
      priority: 'Urgent',
      sortBy: 'password',
      sortDirection: 'sideways',
      page: '-2',
      pageSize: '5000',
      createdFrom: '2026-13-45',
      createdTo: 'yesterday',
    });

    expect(state).toEqual({ ...DEFAULT_LIST_STATE, status: ['New'] });
  });

  it('rejects a page size outside the offered options and a non-numeric page', () => {
    expect(parse({ pageSize: '37', page: '1e3' })).toMatchObject({ pageSize: 20, page: 1 });
  });

  it('drops both dates when the range is inverted', () => {
    const state = parse({ createdFrom: '2026-03-01', createdTo: '2026-01-01' });

    expect(state.createdFrom).toBeNull();
    expect(state.createdTo).toBeNull();
  });

  it('round-trips a state and omits defaults from the URL', () => {
    const state = { ...DEFAULT_LIST_STATE, search: 'acme', status: ['New' as const], page: 2 };
    const params = toQueryParams(state);

    expect(params['sortBy']).toBeNull();
    expect(params['pageSize']).toBeNull();
    expect(params['page']).toBe(2);
    expect(parse({ search: 'acme', status: ['New'], page: '2' })).toEqual(state);
  });
});
