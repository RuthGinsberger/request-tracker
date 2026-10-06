import { HttpParams } from '@angular/common/http';
import { RequestListQuery } from '../models';

/** The single place where a list query becomes query-string parameters. */
export function toHttpParams(query: RequestListQuery): HttpParams {
  let params = new HttpParams();

  const append = (name: string, value: string | number | undefined): void => {
    if (value === undefined) {
      return;
    }
    const text = String(value).trim();
    if (text !== '') {
      params = params.append(name, text);
    }
  };

  append('search', query.search);
  query.status?.forEach((value) => append('status', value));
  query.priority?.forEach((value) => append('priority', value));
  append('organizationName', query.organizationName);
  append('assignedTo', query.assignedTo);
  append('createdFrom', query.createdFrom);
  append('createdTo', query.createdTo);
  append('sortBy', query.sortBy);
  append('sortDirection', query.sortDirection);
  append('page', query.page);
  append('pageSize', query.pageSize);

  return params;
}

/** The single place where a version becomes an If-Match value (strong ETag: quoted base64). */
export function ifMatchHeader(version: string): string {
  return `"${version}"`;
}
