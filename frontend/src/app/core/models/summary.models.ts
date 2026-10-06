import { RequestPriority, RequestStatus } from './enums';

export interface StatusCount {
  status: RequestStatus;
  count: number;
}

export interface PriorityCount {
  priority: RequestPriority;
  count: number;
}

export interface RequestSummary {
  totalCount: number;
  byStatus: StatusCount[];
  byPriority: PriorityCount[];
  generatedAtUtc: string;
}

export interface Lookups {
  organizations: string[];
  assignees: string[];
}
