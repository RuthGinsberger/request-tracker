import { Injectable, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { RequestsApiService } from '../../core/api/requests-api.service';
import { ApiError, RequestSummary } from '../../core/models';

export interface SummaryViewModel {
  status: 'loading' | 'loaded' | 'error';
  data: RequestSummary | null;
  error: ApiError | null;
}

/**
 * Owns the aggregation numbers (R29). `refresh()` is called after every successful status update:
 * the server invalidates its cache on commit, so the next read is current. The old numbers stay
 * visible while refreshing (no flicker), and a newer refresh cancels an older one still in flight.
 */
@Injectable()
export class SummaryStore {
  private readonly api = inject(RequestsApiService);
  private inFlight: Subscription | null = null;

  private readonly viewState = signal<SummaryViewModel>({ status: 'loading', data: null, error: null });
  readonly view = this.viewState.asReadonly();

  load(): void {
    this.inFlight?.unsubscribe();
    this.viewState.update((view) => ({ ...view, status: view.data === null ? 'loading' : 'loaded', error: null }));
    this.inFlight = this.api.summary().subscribe({
      next: (data) => this.viewState.set({ status: 'loaded', data, error: null }),
      error: (error: unknown) => {
        const apiError =
          error instanceof ApiError
            ? error
            : new ApiError(0, 'unknown', 'Unexpected error', 'Could not load the summary.');
        // A failed refresh keeps the last known numbers; only a first load becomes an error state.
        this.viewState.update((view) =>
          view.data === null ? { status: 'error', data: null, error: apiError } : { ...view, status: 'loaded' },
        );
      },
    });
  }

  refresh(): void {
    this.load();
  }
}
