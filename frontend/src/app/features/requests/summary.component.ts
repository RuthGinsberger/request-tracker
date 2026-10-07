import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { SummaryViewModel } from './summary.store';

/** Presentational: total plus counts by status and priority, with loading and error states. */
@Component({
  selector: 'app-summary',
  imports: [DecimalPipe, MatButtonModule, MatCardModule, MatProgressBarModule],
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
              <mat-card-content class="number">{{ data.totalCount | number }}</mat-card-content>
            </mat-card>
            <mat-card appearance="outlined">
              <mat-card-header><mat-card-title>By status</mat-card-title></mat-card-header>
              <mat-card-content>
                <dl>
                  @for (row of data.byStatus; track row.status) {
                    <dt>{{ row.status }}</dt>
                    <dd>{{ row.count | number }}</dd>
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
                    <dd>{{ row.count | number }}</dd>
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
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr));
      gap: 1.25rem;
      margin-bottom: 1.5rem;
    }
    mat-card {
      background: var(--rt-surface);
      padding-bottom: 0.5rem;
    }
    mat-card.total {
      background: linear-gradient(135deg, var(--rt-brand-1), var(--rt-brand-2));
      border-color: transparent;
      box-shadow: 0 10px 28px rgb(2 132 199 / 35%);
    }
    mat-card.total mat-card-title,
    mat-card.total .number {
      color: #fff;
    }
    mat-card.total mat-card-title {
      opacity: 0.85;
    }
    mat-card-title {
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      color: var(--rt-muted);
    }
    .number {
      font-size: 2.75rem;
      font-weight: 700;
      letter-spacing: -0.03em;
      line-height: 1.1;
      padding-top: 0.5rem;
      color: var(--rt-brand-1);
      font-variant-numeric: tabular-nums;
    }
    dt {
      color: var(--rt-muted);
    }
    dd {
      font-weight: 600;
      color: var(--rt-ink);
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
      padding: 0.75rem 1rem;
      margin-bottom: 1rem;
      background: #fde6e6;
      color: #c0262d;
      border-radius: 12px;
    }
  `,
})
export class SummaryComponent {
  readonly view = input.required<SummaryViewModel>();
  readonly retry = output<void>();
}
