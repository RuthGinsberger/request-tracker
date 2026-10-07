import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import {
  RequestItem,
  RequestSortField,
  RequestStatus,
  SORT_FIELDS,
  SortDirection,
  allowedTargets,
} from '../../core/models';
import { PAGE_SIZE_OPTIONS } from './requests-list.state';

export interface StatusChangeRequest {
  item: RequestItem;
  target: RequestStatus;
}

export interface SortChange {
  sortBy: RequestSortField;
  sortDirection: SortDirection;
}

export interface PageChange {
  page: number;
  pageSize: number;
}

/**
 * Presentational table. Sorting and paging only report the user's choice; the rows arrive already
 * sorted and paged by the server, and this component never reorders or slices them.
 */
@Component({
  selector: 'app-requests-table',
  imports: [DatePipe, MatButtonModule, MatMenuModule, MatTableModule, MatSortModule, MatPaginatorModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-scroll">
      <table
        mat-table
        matSort
        matSortDisableClear
        [dataSource]="items()"
        [matSortActive]="sortBy()"
        [matSortDirection]="sortDirection()"
        (matSortChange)="onSort($event)"
      >
        <ng-container matColumnDef="id">
          <th mat-header-cell *matHeaderCellDef>Id</th>
          <td mat-cell *matCellDef="let row">{{ row.id }}</td>
        </ng-container>
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="title">Title</th>
          <td mat-cell *matCellDef="let row">{{ row.title }}</td>
        </ng-container>
        <ng-container matColumnDef="organizationName">
          <th mat-header-cell *matHeaderCellDef>Organization</th>
          <td mat-cell *matCellDef="let row">{{ row.organizationName }}</td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="status">Status</th>
          <td mat-cell *matCellDef="let row">
            <span class="chip" [class]="'chip status-' + row.status">{{ row.status }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="priority">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="priority">Priority</th>
          <td mat-cell *matCellDef="let row">
            <span class="chip" [class]="'chip priority-' + row.priority">{{ row.priority }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="assignedTo">
          <th mat-header-cell *matHeaderCellDef>Assigned to</th>
          <td mat-cell *matCellDef="let row">{{ row.assignedTo ?? '—' }}</td>
        </ng-container>
        <ng-container matColumnDef="createdAt">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="createdAt">Created</th>
          <td mat-cell *matCellDef="let row">{{ row.createdAt | date: 'short' }}</td>
        </ng-container>
        <ng-container matColumnDef="updatedAt">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="updatedAt">Updated</th>
          <td mat-cell *matCellDef="let row">{{ row.updatedAt | date: 'short' }}</td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef>Actions</th>
          <td mat-cell *matCellDef="let row">
            <div class="actions">
              @if (targetsOf(row).length > 0) {
                <button
                  mat-stroked-button
                  type="button"
                  [matMenuTriggerFor]="statusMenu"
                  [matMenuTriggerData]="{ row: row }"
                  [disabled]="pendingIds().has(row.id)"
                  [attr.aria-label]="'Change status of request ' + row.id"
                >
                  Change status
                </button>
              }
              <button mat-button type="button" (click)="historyRequested.emit(row)">History</button>
            </div>
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="columns; sticky: true"></tr>
        <tr mat-row *matRowDef="let row; columns: columns; trackBy: trackById"></tr>
      </table>
    </div>

    <mat-menu #statusMenu="matMenu">
      <ng-template matMenuContent let-row="row">
        @for (target of targetsOf(row); track target) {
          <button mat-menu-item type="button" (click)="statusChangeRequested.emit({ item: row, target })">
            {{ target }}
          </button>
        }
      </ng-template>
    </mat-menu>

    <mat-paginator
      showFirstLastButtons
      [length]="totalCount()"
      [pageIndex]="page() - 1"
      [pageSize]="pageSize()"
      [pageSizeOptions]="pageSizeOptions"
      (page)="onPage($event)"
    />
  `,
  styles: `
    :host {
      display: block;
      background: var(--rt-surface);
      border: 1px solid var(--rt-border);
      border-radius: var(--rt-radius);
      box-shadow: var(--rt-shadow-md);
      overflow: hidden;
    }
    .table-scroll {
      overflow-x: auto;
    }
    table {
      width: 100%;
    }
    th.mat-mdc-header-cell {
      background: #f8f9fe;
      font-weight: 600;
      font-size: 0.72rem;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: var(--rt-muted);
      border-bottom: 1px solid var(--rt-border);
    }
    td.mat-mdc-cell {
      border-bottom: 1px solid #f0f1f8;
      color: var(--rt-ink);
    }
    td.mat-column-id {
      color: var(--rt-muted);
      font-variant-numeric: tabular-nums;
    }
    td.mat-column-title {
      font-weight: 500;
    }
    td.mat-column-createdAt,
    td.mat-column-updatedAt {
      color: var(--rt-muted);
      white-space: nowrap;
    }
    tr.mat-mdc-row {
      transition: background 0.15s ease;
    }
    tr.mat-mdc-row:hover {
      background: #eff8ff;
    }
    tr.mat-mdc-row:last-child td.mat-mdc-cell {
      border-bottom: 0;
    }
    .actions {
      display: grid;
      grid-template-columns: 9rem auto;
      align-items: center;
      gap: 0.25rem;
      white-space: nowrap;
    }
    .actions button[mat-stroked-button] {
      width: 100%;
    }
    /* History stays in the same column when there is no Change status button. */
    .actions > button:only-child {
      grid-column: 2;
    }
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.15rem 0.7rem;
      border-radius: 999px;
      font-size: 0.76rem;
      font-weight: 600;
      line-height: 1.5;
      white-space: nowrap;
      background: #eef0f6;
      color: #475066;
    }
    .chip::before {
      content: '';
      width: 0.45rem;
      height: 0.45rem;
      border-radius: 50%;
      background: currentColor;
    }
    .status-New { background: #e6efff; color: #1d4ed8; }
    .status-InProgress { background: #fff1d6; color: #9a5b00; }
    .status-Waiting { background: #f0e7ff; color: #6d28d9; }
    .status-Completed { background: #dcf5e6; color: #15803d; }
    .priority-Low { background: #eef0f6; color: #5b6480; }
    .priority-Medium { background: #fff1d6; color: #9a5b00; }
    .priority-High { background: #fde6e6; color: #c0262d; }
  `,
})
export class RequestsTableComponent {
  readonly items = input.required<readonly RequestItem[]>();
  readonly totalCount = input.required<number>();
  readonly sortBy = input.required<RequestSortField>();
  readonly sortDirection = input.required<SortDirection>();
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  /** Requests with an update in flight; their action is disabled. */
  readonly pendingIds = input<ReadonlySet<number>>(new Set());

  readonly sortChange = output<SortChange>();
  readonly pageChange = output<PageChange>();
  readonly statusChangeRequested = output<StatusChangeRequest>();
  readonly historyRequested = output<RequestItem>();

  protected readonly columns = [
    'id',
    'title',
    'organizationName',
    'status',
    'priority',
    'assignedTo',
    'createdAt',
    'updatedAt',
    'actions',
  ];
  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;

  protected targetsOf(row: RequestItem): readonly RequestStatus[] {
    return allowedTargets(row.status);
  }

  protected trackById(_: number, row: RequestItem): number {
    return row.id;
  }

  /** The sortable headers carry the API field name as their id, so the event maps straight to the API whitelist. */
  protected onSort(event: Sort): void {
    const sortBy = SORT_FIELDS.find((field) => field === event.active);
    if (sortBy !== undefined && event.direction !== '') {
      this.sortChange.emit({ sortBy, sortDirection: event.direction });
    }
  }

  protected onPage(event: PageEvent): void {
    this.pageChange.emit({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }
}
