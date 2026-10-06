import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  BulkStatusRequest,
  BulkStatusResponse,
  Lookups,
  PagedResult,
  RequestItem,
  RequestListQuery,
  RequestStatus,
  RequestSummary,
  StatusHistoryItem,
} from '../models';
import { ifMatchHeader, toHttpParams } from './http-params';

/** Owns every HTTP call to the API; returns cold observables so callers can cancel by unsubscribing. */
@Injectable({ providedIn: 'root' })
export class RequestsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/requests`;

  list(query: RequestListQuery): Observable<PagedResult<RequestItem>> {
    return this.http.get<PagedResult<RequestItem>>(this.baseUrl, { params: toHttpParams(query) });
  }

  get(id: number): Observable<RequestItem> {
    return this.http.get<RequestItem>(`${this.baseUrl}/${id}`);
  }

  summary(): Observable<RequestSummary> {
    return this.http.get<RequestSummary>(`${this.baseUrl}/summary`);
  }

  lookups(): Observable<Lookups> {
    return this.http.get<Lookups>(`${environment.apiBaseUrl}/lookups`);
  }

  updateStatus(id: number, status: RequestStatus, version: string): Observable<RequestItem> {
    return this.http.patch<RequestItem>(
      `${this.baseUrl}/${id}/status`,
      { status },
      { headers: new HttpHeaders({ 'If-Match': ifMatchHeader(version) }) },
    );
  }

  history(id: number): Observable<StatusHistoryItem[]> {
    return this.http.get<StatusHistoryItem[]>(`${this.baseUrl}/${id}/history`);
  }

  bulkUpdateStatus(request: BulkStatusRequest): Observable<BulkStatusResponse> {
    return this.http.post<BulkStatusResponse>(`${this.baseUrl}/bulk-status`, request);
  }
}
