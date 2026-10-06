import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RequestsApiService } from '../../core/api/requests-api.service';
import { ApiError, RequestSummary, StatusHistoryItem } from '../../core/models';
import { HistoryStore } from './history.store';
import { SummaryStore } from './summary.store';

const summary = (total: number): RequestSummary => ({
  totalCount: total,
  byStatus: [{ status: 'New', count: total }],
  byPriority: [{ priority: 'Low', count: total }],
  generatedAtUtc: '2026-01-01T00:00:00Z',
});

describe('SummaryStore', () => {
  const api = { summary: vi.fn() };
  let store: SummaryStore;

  beforeEach(() => {
    TestBed.resetTestingModule();
    api.summary.mockReset();
    TestBed.configureTestingModule({
      providers: [SummaryStore, { provide: RequestsApiService, useValue: api }],
    });
    store = TestBed.inject(SummaryStore);
  });

  it('starts loading, then shows the data', () => {
    const response = new Subject<RequestSummary>();
    api.summary.mockReturnValue(response);

    store.load();
    expect(store.view().status).toBe('loading');

    response.next(summary(5));
    expect(store.view().status).toBe('loaded');
    expect(store.view().data?.totalCount).toBe(5);
  });

  it('refresh fetches again and shows the new numbers', () => {
    api.summary.mockReturnValueOnce(of(summary(5))).mockReturnValueOnce(of(summary(6)));

    store.load();
    store.refresh();

    expect(api.summary).toHaveBeenCalledTimes(2);
    expect(store.view().data?.totalCount).toBe(6);
  });

  it('a first-load failure is an error state; retry recovers', () => {
    api.summary.mockReturnValueOnce(throwError(() => new ApiError(500, 'internal-error', 't', 'boom')));
    store.load();
    expect(store.view().status).toBe('error');
    expect(store.view().error?.message).toBe('boom');

    api.summary.mockReturnValueOnce(of(summary(1)));
    store.load();
    expect(store.view().status).toBe('loaded');
  });

  it('a failed refresh keeps the last known numbers', () => {
    api.summary.mockReturnValueOnce(of(summary(5)));
    store.load();
    api.summary.mockReturnValueOnce(throwError(() => new ApiError(0, 'network-error', 't', 'offline')));
    store.refresh();

    expect(store.view().status).toBe('loaded');
    expect(store.view().data?.totalCount).toBe(5);
  });
});

describe('HistoryStore', () => {
  const api = { history: vi.fn() };
  let store: HistoryStore;
  const entry: StatusHistoryItem = {
    id: 1,
    requestId: 7,
    previousStatus: 'New',
    newStatus: 'InProgress',
    changedAt: '2026-01-01T10:00:00Z',
    changedBy: 'dana',
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    api.history.mockReset();
    TestBed.configureTestingModule({
      providers: [HistoryStore, { provide: RequestsApiService, useValue: api }],
    });
    store = TestBed.inject(HistoryStore);
  });

  it('shows entries', () => {
    api.history.mockReturnValue(of([entry]));
    store.load(7);
    expect(api.history).toHaveBeenCalledWith(7);
    expect(store.view().status).toBe('loaded');
    expect(store.view().items).toEqual([entry]);
  });

  it('shows the empty state', () => {
    api.history.mockReturnValue(of([]));
    store.load(7);
    expect(store.view().status).toBe('empty');
  });

  it('shows an error and can retry', () => {
    api.history.mockReturnValueOnce(throwError(() => new ApiError(500, 'internal-error', 't', 'boom')));
    store.load(7);
    expect(store.view().status).toBe('error');

    api.history.mockReturnValueOnce(of([entry]));
    store.load(7);
    expect(store.view().status).toBe('loaded');
  });
});
