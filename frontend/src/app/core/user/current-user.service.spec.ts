import { TestBed } from '@angular/core/testing';
import { CurrentUserService } from './current-user.service';

describe('CurrentUserService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  it('starts unset', () => {
    const service = TestBed.inject(CurrentUserService);

    expect(service.isSet()).toBe(false);
    expect(service.userName()).toBeNull();
  });

  it('persists the selection and restores it in a new instance', () => {
    TestBed.inject(CurrentUserService).set('dana');
    TestBed.resetTestingModule();

    const restored = TestBed.inject(CurrentUserService);

    expect(restored.userName()).toBe('dana');
    expect(restored.isSet()).toBe(true);
  });

  it('drops a stored user that is not a known assignee', () => {
    const service = TestBed.inject(CurrentUserService);
    service.set('ghost');

    service.validateAgainst(['dana', 'lee']);

    expect(service.isSet()).toBe(false);
  });

  it('keeps a stored user that is a known assignee', () => {
    const service = TestBed.inject(CurrentUserService);
    service.set('dana');

    service.validateAgainst(['dana', 'lee']);

    expect(service.userName()).toBe('dana');
  });
});
