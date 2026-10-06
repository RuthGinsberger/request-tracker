import { BulkItemOutcome, RequestStatus } from './enums';

export interface BulkStatusItem {
  id: number;
  version: string;
}

export interface BulkStatusRequest {
  targetStatus: RequestStatus;
  items: BulkStatusItem[];
}

export interface BulkItemResult {
  id: number;
  outcome: BulkItemOutcome;
  newVersion?: string;
  message?: string;
}

export interface BulkSummary {
  requested: number;
  updated: number;
  notFound: number;
  conflict: number;
  invalidTransition: number;
}

export interface BulkStatusResponse {
  summary: BulkSummary;
  results: BulkItemResult[];
}
