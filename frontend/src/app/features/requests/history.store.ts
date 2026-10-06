import { Injectable, inject, signal } from '@angular/core';
import { RequestsApiService } from '../../core/api/requests-api.service';
import { ApiError, StatusHistoryItem } from '../../core/models';

export interface HistoryViewModel {
  status: 'loading' | 'loaded' | 'empty' | 'error';
  items: readonly StatusHistoryItem[];
  error: ApiError | null;
}

/** Loads one request's change history (R30). Provided by the dialog, so it lives and dies with it. */
@Injectable()
export class HistoryStore {
  private readonly api = inject(RequestsApiService);

  private readonly viewState = signal<HistoryViewModel>({ status: 'loading', items: [], error: null });
  readonly view = this.viewState.asReadonly();

  load(requestId: number): void {
    this.viewState.set({ status: 'loading', items: [], error: null });
    this.api.history(requestId).subscribe({
      next: (items) => this.viewState.set({ status: items.length === 0 ? 'empty' : 'loaded', items, error: null }),
      error: (error: unknown) =>
        this.viewState.set({
          status: 'error',
          items: [],
          error:
            error instanceof ApiError
              ? error
              : new ApiError(0, 'unknown', 'Unexpected error', 'Could not load the history.'),
        }),
    });
  }
}
