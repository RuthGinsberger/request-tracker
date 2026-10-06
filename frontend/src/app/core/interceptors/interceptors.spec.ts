import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { ApiError } from '../models';
import { CurrentUserService } from '../user/current-user.service';
import { errorInterceptor } from './error.interceptor';
import { userNameInterceptor } from './user-name.interceptor';

function setup(): {
  http: HttpClient;
  controller: HttpTestingController;
  user: CurrentUserService;
} {
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([errorInterceptor, userNameInterceptor])),
      provideHttpClientTesting(),
    ],
  });
  return {
    http: TestBed.inject(HttpClient),
    controller: TestBed.inject(HttpTestingController),
    user: TestBed.inject(CurrentUserService),
  };
}

/** Subscribes, lets the test answer the request, then returns the error the subscriber received. */
function failWith(source: Observable<unknown>, answer: () => void): ApiError {
  let captured: unknown;
  source.subscribe({ error: (e: unknown) => (captured = e) });
  answer();
  expect(captured).toBeInstanceOf(ApiError);
  return captured as ApiError;
}

describe('userNameInterceptor', () => {
  it('adds X-User-Name to mutating requests', () => {
    const { http, controller, user } = setup();
    user.set('dana');

    http.patch('/api/x', {}).subscribe();

    expect(controller.expectOne('/api/x').request.headers.get('X-User-Name')).toBe('dana');
    controller.verify();
  });

  it('does not add it to GET requests', () => {
    const { http, controller, user } = setup();
    user.set('dana');

    http.get('/api/x').subscribe();

    expect(controller.expectOne('/api/x').request.headers.has('X-User-Name')).toBe(false);
  });

  it('adds nothing when no user is selected', () => {
    const { http, controller } = setup();

    http.post('/api/x', {}).subscribe();

    expect(controller.expectOne('/api/x').request.headers.has('X-User-Name')).toBe(false);
  });
});

describe('errorInterceptor', () => {
  it('maps a validation problem with field errors', () => {
    const { http, controller } = setup();

    const error = failWith(http.get('/api/x'), () =>
      controller.expectOne('/api/x').flush(
        {
          title: 'Validation failed',
          detail: 'One or more validation errors occurred.',
          errorCode: 'validation-failed',
          traceId: 'abc',
          errors: { pageSize: ['Must be between 1 and 100.'] },
        },
        { status: 400, statusText: 'Bad Request' },
      ),
    );

    expect(error.status).toBe(400);
    expect(error.errorCode).toBe('validation-failed');
    expect(error.fieldErrors['pageSize']).toEqual(['Must be between 1 and 100.']);
    expect(error.traceId).toBe('abc');
  });

  it('maps a 409 with currentStatus and currentVersion extensions', () => {
    const { http, controller } = setup();

    const error = failWith(http.patch('/api/x', {}), () =>
      controller.expectOne('/api/x').flush(
        {
          title: 'Version conflict',
          detail: 'The request was modified by someone else.',
          errorCode: 'version-conflict',
          currentStatus: 'Waiting',
          currentVersion: 'AAAAAAAAB9F=',
        },
        { status: 409, statusText: 'Conflict' },
      ),
    );

    expect(error.isConflict).toBe(true);
    expect(error.errorCode).toBe('version-conflict');
    expect(error.detail).toBe('The request was modified by someone else.');
    expect(error.extensions).toEqual({ currentStatus: 'Waiting', currentVersion: 'AAAAAAAAB9F=' });
  });

  it('maps a network error to a friendly message', () => {
    const { http, controller } = setup();

    const error = failWith(http.get('/api/x'), () =>
      controller.expectOne('/api/x').error(new ProgressEvent('error'), { status: 0 }),
    );

    expect(error.status).toBe(0);
    expect(error.errorCode).toBe('network-error');
    expect(error.detail).toContain('Cannot reach the server');
  });

  it('falls back to a generic message when the body is not ProblemDetails', () => {
    const { http, controller } = setup();

    const error = failWith(http.get('/api/x'), () =>
      controller
        .expectOne('/api/x')
        .flush('<html>Bad gateway</html>', { status: 502, statusText: 'Bad Gateway' }),
    );

    expect(error.status).toBe(502);
    expect(error.errorCode).toBe('unknown');
    expect(error.detail).toBe('Something went wrong. Please try again.');
  });
});
