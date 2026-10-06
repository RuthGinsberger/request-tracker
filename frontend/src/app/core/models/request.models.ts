import { RequestPriority, RequestSortField, RequestStatus, SortDirection } from './enums';

/** Mirrors RequestListItemDto and RequestDetailsDto. */
export interface RequestItem {
  id: number;
  title: string;
  organizationName: string;
  status: RequestStatus;
  priority: RequestPriority;
  assignedTo: string | null;
  createdAt: string;
  updatedAt: string;
  /** Base64 rowversion, sent back as the If-Match value. */
  version: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Query for GET /api/requests; every property is optional and omitted when empty. */
export interface RequestListQuery {
  search?: string;
  status?: readonly RequestStatus[];
  priority?: readonly RequestPriority[];
  organizationName?: string;
  assignedTo?: string;
  /** yyyy-MM-dd */
  createdFrom?: string;
  /** yyyy-MM-dd */
  createdTo?: string;
  sortBy?: RequestSortField;
  sortDirection?: SortDirection;
  page?: number;
  pageSize?: number;
}

export interface StatusHistoryItem {
  id: number;
  requestId: number;
  previousStatus: RequestStatus;
  newStatus: RequestStatus;
  changedAt: string;
  changedBy: string;
}
