import { ChangeDetectionStrategy, Component, computed, effect, input, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  Lookups,
  REQUEST_PRIORITIES,
  REQUEST_STATUSES,
  RequestPriority,
  RequestStatus,
} from '../../core/models';
import { formatIsoDate, parseIsoDate } from './iso-date';
import { MAX_SEARCH_LENGTH, RequestsListState } from './requests-list.state';

export interface DateRangeChange {
  from: string | null;
  to: string | null;
}

/** Presentational: shows the current filters and reports what the user changed. It never loads or filters data. */
@Component({
  selector: 'app-request-filters',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="filters" role="search">
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search">
        <mat-label>Search title or organization</mat-label>
        <input matInput type="search" [formControl]="searchControl" [maxlength]="maxSearchLength" />
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Status</mat-label>
        <mat-select multiple [value]="value().status" (selectionChange)="statusChange.emit($event.value)">
          @for (status of statuses; track status) {
            <mat-option [value]="status">{{ status }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Priority</mat-label>
        <mat-select multiple [value]="value().priority" (selectionChange)="priorityChange.emit($event.value)">
          @for (priority of priorities; track priority) {
            <mat-option [value]="priority">{{ priority }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Organization</mat-label>
        <mat-select [value]="value().organizationName" (selectionChange)="organizationChange.emit($event.value)">
          <mat-option [value]="null">Any</mat-option>
          @for (name of lookups().organizations; track name) {
            <mat-option [value]="name">{{ name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Assigned to</mat-label>
        <mat-select [value]="value().assignedTo" (selectionChange)="assigneeChange.emit($event.value)">
          <mat-option [value]="null">Any</mat-option>
          @for (name of lookups().assignees; track name) {
            <mat-option [value]="name">{{ name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Created between</mat-label>
        <mat-date-range-input [formGroup]="range" [rangePicker]="picker">
          <input matStartDate formControlName="from" placeholder="From" (dateChange)="onDatesChanged(picker.opened)" />
          <input matEndDate formControlName="to" placeholder="To" (dateChange)="onDatesChanged(picker.opened)" />
        </mat-date-range-input>
        <mat-datepicker-toggle matIconSuffix [for]="picker" />
        <mat-date-range-picker #picker (closed)="emitDateRange()" />
        @if (range.invalid) {
          <mat-error>Enter a valid range: the start date must not be after the end date.</mat-error>
        }
      </mat-form-field>

      <button mat-stroked-button type="button" [disabled]="!hasActiveFilters()" (click)="clear.emit()">
        Clear filters
      </button>
    </div>
  `,
  styles: `
    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      align-items: center;
    }
    .search {
      flex: 1 1 18rem;
    }
    mat-form-field:not(.search) {
      width: 12rem;
    }
  `,
})
export class RequestFiltersComponent {
  readonly value = input.required<RequestsListState>();
  readonly lookups = input.required<Lookups>();
  readonly hasActiveFilters = input.required<boolean>();

  readonly searchChange = output<string>();
  readonly statusChange = output<RequestStatus[]>();
  readonly priorityChange = output<RequestPriority[]>();
  readonly organizationChange = output<string | null>();
  readonly assigneeChange = output<string | null>();
  readonly dateRangeChange = output<DateRangeChange>();
  readonly clear = output<void>();

  protected readonly statuses = REQUEST_STATUSES;
  protected readonly priorities = REQUEST_PRIORITIES;
  protected readonly maxSearchLength = MAX_SEARCH_LENGTH;

  /** Local text of the box: it runs ahead of the committed (debounced) search in the state. */
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly range = new FormGroup({
    from: new FormControl<Date | null>(null),
    to: new FormControl<Date | null>(null),
  });

  private readonly committedSearch = computed(() => this.value().search);
  private readonly committedFrom = computed(() => this.value().createdFrom);
  private readonly committedTo = computed(() => this.value().createdTo);

  constructor() {
    this.searchControl.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((term) => this.searchChange.emit(term));

    // Mirror state changes the user did not type (clear filters, URL on load) into the controls.
    effect(() => {
      const committed = this.committedSearch();
      if (this.searchControl.value.trim() !== committed) {
        this.searchControl.setValue(committed, { emitEvent: false });
      }
    });
    effect(() => {
      this.range.setValue(
        { from: parseIsoDate(this.committedFrom()), to: parseIsoDate(this.committedTo()) },
        { emitEvent: false },
      );
    });
  }

  /** Typing a date reports it at once; picking in the calendar waits for the picker to close (one call, not two). */
  protected onDatesChanged(pickerOpen: boolean): void {
    if (!pickerOpen) {
      this.emitDateRange();
    }
  }

  protected emitDateRange(): void {
    const { from, to } = this.range.getRawValue();
    if (this.range.invalid || (from !== null && to !== null && from > to)) {
      return; // an invalid range is never sent
    }
    this.dateRangeChange.emit({ from: formatIsoDate(from), to: formatIsoDate(to) });
  }
}
