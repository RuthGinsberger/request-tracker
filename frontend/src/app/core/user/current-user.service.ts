import { Injectable, computed, signal } from '@angular/core';

const STORAGE_KEY = 'requesttracker.userName';

/** The acting user (A8: no auth, the name goes in X-User-Name). Signal state, persisted per browser. */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly name = signal<string | null>(readStored());

  readonly userName = this.name.asReadonly();
  readonly isSet = computed(() => this.name() !== null);

  set(userName: string): void {
    this.name.set(userName);
    write(userName);
  }

  clear(): void {
    this.name.set(null);
    write(null);
  }

  /** Drops a stored name that is no longer a known assignee (for example after re-seeding). */
  validateAgainst(assignees: readonly string[]): void {
    const current = this.name();
    if (current !== null && !assignees.includes(current)) {
      this.clear();
    }
  }
}

function readStored(): string | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY)?.trim();
    return value ? value : null;
  } catch {
    return null;
  }
}

function write(value: string | null): void {
  try {
    if (value === null) {
      localStorage.removeItem(STORAGE_KEY);
    } else {
      localStorage.setItem(STORAGE_KEY, value);
    }
  } catch {
    // Storage can be unavailable (private mode); the selection then lasts for this session only.
  }
}
