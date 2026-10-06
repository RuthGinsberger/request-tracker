import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { SummaryViewModel } from './summary.store';

/** Presentational: total plus counts by status and priority, with loading and error states. */
@Component({
  selector: 'app-summary',
  imports: [MatButtonModule, MatCardModule, MatProgressBarModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (view().status) {
      @case ('loading') {
        <mat-progress-bar mode="indeterminate" aria-label="Loading summary" />
      }
      @case ('error') {
        <div class="error" role="alert">
          <span>{{ view().error?.message }}</span>
          <button mat-stroked-button type="button" (click)="retry.emit()">Retry</button>
        </div>
      }
      @case ('loaded') {
        @if (view().data; as data) {
          <div class="cards">
            <mat-card appearance="outlined" class="total">
              <mat-card-header><mat-card-title>Total</mat-card-title></mat-card-header>
              <mat-card-content class="number">{{ data.totalCount }}</mat-card-content>
            </mat-card>
            <mat-card appearance="outlined">
              <mat-card-header><mat-card-title>By status</mat-card-title></mat-card-header>
              <mat-card-content>
                <dl>
                  @for (row of data.byStatus; track row.status) {
                    <dt>{{ row.status }}</dt>
                    <dd>{{ row.count }}</dd>
                  }
                </dl>
              </mat-card-content>
            </mat-card>
            <mat-card appearance="outlined">
              <mat-card-header><mat-card-title>By priority</mat-card-title></mat-card-header>
              <mat-card-content>
                <dl>
                  @for (row of data.byPriority; track row.priority) {
                    <dt>{{ row.priority }}</dt>
                    <dd>{{ row.count }}</dd>
                  }
                </dl>
              </mat-card-content>
            </mat-card>
          </div>
        }
      }
    }
  `,
  styles: `
    .cards {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
    }
    mat-card {
      min-width: 12rem;
    }
    .number {
      font-size: 2rem;
      padding-top: 0.5rem;
    }
    dl {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 0.25rem 1.5rem;
      margin: 0.5rem 0 0;
    }
    dd {
      margin: 0;
      text-align: right;
      font-variant-numeric: tabular-nums;
    }
    .error {
      display: flex;
      align-items: center;
      gap: 1rem;
    }
  `,
})
export class SummaryComponent {
  readonly view = input.required<SummaryViewModel>();
  readonly retry = output<void>();
}
