import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router } from '@angular/router';
import { RequestItem } from '../../core/models';
import { HistoryDialogComponent, HistoryDialogData } from './history-dialog.component';
import { RequestFiltersComponent } from './request-filters.component';
import { parseListState, toQueryParams } from './requests-list.query-params';
import { RequestsListStore, errorMessage } from './requests-list.store';
import { RequestsTableComponent } from './requests-table.component';
import { StatusUpdateService } from './status-update.service';
import { SummaryComponent } from './summary.component';
import { SummaryStore } from './summary.store';

/** Container: wires the store to the presentational components and keeps the URL in sync with the query. */
@Component({
  selector: 'app-requests-page',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatProgressBarModule,
    RequestFiltersComponent,
    RequestsTableComponent,
    SummaryComponent,
  ],
  providers: [RequestsListStore, SummaryStore, StatusUpdateService],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <h1>Requests</h1>
      <p class="page-subtitle">Track, filter and update every request in one place.</p>
    </header>

    <app-summary [view]="summaryStore.view()" (retry)="summaryStore.load()" />

    <app-request-filters
      [value]="store.state()"
      [lookups]="store.lookups()"
      [hasActiveFilters]="store.hasActiveFilters()"
      (searchChange)="store.setSearch($event)"
      (statusChange)="store.setStatus($event)"
      (priorityChange)="store.setPriority($event)"
      (organizationChange)="store.setOrganization($event)"
      (assigneeChange)="store.setAssignee($event)"
      (dateRangeChange)="store.setDateRange($event.from, $event.to)"
      (clear)="store.clearFilters()"
    />

    <div class="progress">
      @if (store.view().status === 'loading') {
        <mat-progress-bar mode="indeterminate" aria-label="Loading requests" />
      }
    </div>

    @switch (store.view().status) {
      @case ('error') {
        <div class="message" role="alert">
          <p>{{ message() }}</p>
          <button mat-flat-button type="button" (click)="store.retry()">Retry</button>
        </div>
      }
      @case ('empty') {
        <div class="message" role="status">
          @if (store.hasActiveFilters()) {
            <p>No requests match the current filters.</p>
            <button mat-stroked-button type="button" (click)="store.clearFilters()">Clear filters</button>
          } @else {
            <p>There are no requests yet.</p>
          }
        </div>
      }
    }

    @if (store.view().items.length > 0) {
      <app-requests-table
        [items]="store.view().items"
        [totalCount]="store.view().totalCount"
        [sortBy]="store.state().sortBy"
        [sortDirection]="store.state().sortDirection"
        [page]="store.state().page"
        [pageSize]="store.state().pageSize"
        (sortChange)="store.setSort($event.sortBy, $event.sortDirection)"
        [pendingIds]="updater.pendingIds()"
        (pageChange)="store.setPage($event.page, $event.pageSize)"
        (statusChangeRequested)="updater.change($event.item, $event.target)"
        (historyRequested)="openHistory($event)"
      />
    }
  `,
  styles: `
    .page-header {
      margin-bottom: 1.5rem;
    }
    .page-subtitle {
      margin: 0;
      color: var(--rt-muted);
      font-size: 1rem;
    }
    .progress {
      height: 4px;
      margin: 0.75rem 0;
    }
    .message {
      padding: 3rem 1rem;
      margin-top: 0.5rem;
      text-align: center;
      color: var(--rt-muted);
      background: var(--rt-surface);
      border: 1px dashed #cfd3ea;
      border-radius: var(--rt-radius);
    }
    .message p {
      margin: 0 0 1rem;
      font-size: 1.05rem;
    }
  `,
})
export class RequestsPageComponent {
  protected readonly store = inject(RequestsListStore);
  protected readonly summaryStore = inject(SummaryStore);
  protected readonly updater = inject(StatusUpdateService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  constructor() {
    this.store.start(parseListState(this.route.snapshot.queryParamMap));
    this.summaryStore.load();

    // State to URL: refresh and shared links reproduce the view; replaceUrl keeps the back button for real navigation.
    effect(() => {
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: toQueryParams(this.store.state()),
        replaceUrl: true,
      });
    });
  }

  protected openHistory(item: RequestItem): void {
    const data: HistoryDialogData = { requestId: item.id, title: item.title };
    this.dialog.open(HistoryDialogComponent, { data, width: '32rem', maxWidth: '95vw' });
  }

  protected message(): string {
    const error = this.store.view().error;
    return error === null ? '' : errorMessage(error);
  }
}
