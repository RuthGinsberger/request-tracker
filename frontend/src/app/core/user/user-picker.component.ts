import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { catchError, map, of, tap } from 'rxjs';
import { RequestsApiService } from '../api/requests-api.service';
import { CurrentUserService } from './current-user.service';

type AssigneesState = { status: 'loading' } | { status: 'error' } | { status: 'ready'; assignees: string[] };

/** Toolbar control for choosing the acting user (A8). Presentation only; state lives in CurrentUserService. */
@Component({
  selector: 'app-user-picker',
  imports: [MatFormFieldModule, MatSelectModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="user-picker">
      <mat-label>Acting as</mat-label>
      <mat-select
        [value]="currentUser.userName()"
        [disabled]="state().status !== 'ready'"
        (selectionChange)="currentUser.set($event.value)"
      >
        @for (name of assignees(); track name) {
          <mat-option [value]="name">{{ name }}</mat-option>
        }
      </mat-select>
      @if (state().status === 'error') {
        <mat-hint>Could not load users</mat-hint>
      }
    </mat-form-field>
  `,
  styles: `
    .user-picker {
      width: 14rem;
    }
  `,
})
export class UserPickerComponent {
  protected readonly currentUser = inject(CurrentUserService);
  private readonly api = inject(RequestsApiService);

  protected readonly state = toSignal(
    this.api.lookups().pipe(
      tap((lookups) => this.currentUser.validateAgainst(lookups.assignees)),
      map((lookups): AssigneesState => ({ status: 'ready', assignees: lookups.assignees })),
      catchError(() => of<AssigneesState>({ status: 'error' })),
    ),
    { initialValue: { status: 'loading' } as AssigneesState },
  );

  protected readonly assignees = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.assignees : [];
  });
}
