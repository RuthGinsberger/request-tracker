export const REQUEST_STATUSES = ['New', 'InProgress', 'Waiting', 'Completed'] as const;
export type RequestStatus = (typeof REQUEST_STATUSES)[number];

export const REQUEST_PRIORITIES = ['Low', 'Medium', 'High'] as const;
export type RequestPriority = (typeof REQUEST_PRIORITIES)[number];

export const SORT_FIELDS = ['createdAt', 'updatedAt', 'priority', 'status', 'title'] as const;
export type RequestSortField = (typeof SORT_FIELDS)[number];

export type SortDirection = 'asc' | 'desc';

export type BulkItemOutcome = 'updated' | 'notFound' | 'conflict' | 'invalidTransition';
