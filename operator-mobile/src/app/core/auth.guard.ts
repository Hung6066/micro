import { inject } from '@angular/core';
import { Router, UrlTree } from '@angular/router';
import { map, Observable } from 'rxjs';
import { MobileAuthService } from './auth.service';

export const mobileAuthGuard = (): Observable<boolean | UrlTree> => {
  const auth = inject(MobileAuthService);
  const router = inject(Router);
  return auth.ensureAuthenticated().pipe(map(isAuthenticated => isAuthenticated ? true : router.parseUrl('/auth/login')));
};
