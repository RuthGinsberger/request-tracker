import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RequestsApiService } from './requests-api.service';
import { ifMatchHeader, toHttpParams } from './http-params';

describe('toHttpParams', () => {
  it('repeats status and priority and keeps the other values', () => {
    const params = toHttpParams({
      search: 'acme',
      status: ['New', 'Waiting'],
      priority: ['High'],
      organizationName: 'Acme Ltd',
      createdFrom: '2026-01-01',
      sortBy: 'title',
      sortDirection: 'asc',
      page: 2,
      pageSize: 50,
    });

    expect(params.getAll('status')).toEqual(['New', 'Waiting']);
    expect(params.getAll('priority')).toEqual(['High']);
    expect(params.get('search')).toBe('acme');
    expect(params.get('organizationName')).toBe('Acme Ltd');
    expect(params.get('createdFrom')).toBe('2026-01-01');
    expect(params.get('sortBy')).toBe('title');
    expect(params.get('sortDirection')).toBe('asc');
    expect(params.get('page')).toBe('2');
    expect(params.get('pageSize')).toBe('50');
  });

  it('omits empty, blank and undefined values', () => {
    const params = toHttpParams({ search: '   ', status: [], assignedTo: '', createdTo: undefined });

    expect(params.keys()).toEqual([]);
  });
});

describe('ifMatchHeader', () => {
  it('wraps the version in quotes (strong ETag)', () => {
    expect(ifMatchHeader('AAAAAAAAB9E=')).toBe('"AAAAAAAAB9E="');
  });
});

describe('RequestsApiService', () => {
  let api: RequestsApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(RequestsApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends the list query as query-string parameters', () => {
    api.list({ status: ['New', 'Waiting'], page: 3 }).subscribe();

    const req = http.expectOne((r) => r.url === '/api/requests');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.getAll('status')).toEqual(['New', 'Waiting']);
    expect(req.request.params.get('page')).toBe('3');
    req.flush({ items: [], page: 3, pageSize: 20, totalCount: 0, totalPages: 0 });
  });

  it('gets a request, the summary, the lookups and the history from their endpoints', () => {
    api.get(7).subscribe();
    api.summary().subscribe();
    api.lookups().subscribe();
    api.history(7).subscribe();

    http.expectOne('/api/requests/7').flush({});
    http.expectOne('/api/requests/summary').flush({});
    http.expectOne('/api/lookups').flush({});
    http.expectOne('/api/requests/7/history').flush([]);
  });

  it('updates the status with the version in If-Match', () => {
    api.updateStatus(7, 'InProgress', 'AAAAAAAAB9E=').subscribe();

    const req = http.expectOne('/api/requests/7/status');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.headers.get('If-Match')).toBe('"AAAAAAAAB9E="');
    expect(req.request.body).toEqual({ status: 'InProgress' });
    req.flush({});
  });

  it('posts the bulk update body as given', () => {
    const body = { targetStatus: 'Completed' as const, items: [{ id: 1, version: 'AAAAAAAAB9E=' }] };
    api.bulkUpdateStatus(body).subscribe();

    const req = http.expectOne('/api/requests/bulk-status');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ summary: {}, results: [] });
  });
});
