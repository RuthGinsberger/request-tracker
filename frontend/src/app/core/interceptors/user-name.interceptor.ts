import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CurrentUserService } from '../user/current-user.service';

const READ_ONLY_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

/** Adds X-User-Name to mutating calls. Without a selected user nothing is added and the server answers 400. */
export const userNameInterceptor: HttpInterceptorFn = (req, next) => {
  const userName = inject(CurrentUserService).userName();
  if (READ_ONLY_METHODS.has(req.method) || userName === null) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { 'X-User-Name': userName } }));
};
