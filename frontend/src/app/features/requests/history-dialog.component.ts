import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { HistoryStore } from './history.store';

export interface HistoryDialogData {
  requestId: number;
  title: string;
}

/** Shows a request's status changes, newest first (the API orders them). Times render in the browser's local time. */
@Component({
  selector: 'app-history-dialog',
  imports: [DatePipe, MatButtonModule, MatDialogModule, MatProgressBarModule],
  providers: [HistoryStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>History of request {{ data.requestId }}</h2>
    <mat-dialog-content>
      <p class="subtitle">{{ data.title }}</p>
      @switch (store.view().status) {
        @case ('loading') {
          <mat-progress-bar mode="indeterminate" aria-label="Loading history" />
        }
        @case ('error') {
          <div role="alert">
            <p>{{ store.view().error?.message }}</p>
            <button mat-flat-button type="button" (click)="reload()">Retry</button>
          </div>
        }
        @case ('empty') {
          <p role="status">No status changes yet</p>
        }
        @case ('loaded') {
          <ul>
            @for (entry of store.view().items; track entry.id) {
              <li>
                <time [attr.datetime]="entry.changedAt">{{ entry.changedAt | date: 'medium' }}</time>
                <span>{{ entry.previousStatus }} → {{ entry.newStatus }}</span>
                <span class="by">by {{ entry.changedBy }}</span>
              </li>
            }
          </ul>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    .subtitle {
      margin-top: 0;
      color: var(--rt-muted);
      font-weight: 500;
    }
    ul {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    li {
      display: flex;
      flex-wrap: wrap;
      gap: 0.25rem 1rem;
      padding: 0.75rem 0.5rem;
      border-bottom: 1px solid var(--rt-border);
    }
    li:last-child {
      border-bottom: 0;
    }
    time {
      min-width: 10rem;
      color: var(--rt-muted);
    }
    li span:nth-of-type(1) {
      font-weight: 600;
    }
    .by {
      color: var(--rt-muted);
    }
  `,
})
export class HistoryDialogComponent {
  protected readonly data = inject<HistoryDialogData>(MAT_DIALOG_DATA);
  protected readonly store = inject(HistoryStore);

  constructor() {
    this.reload();
  }

  protected reload(): void {
    this.store.load(this.data.requestId);
  }
}
