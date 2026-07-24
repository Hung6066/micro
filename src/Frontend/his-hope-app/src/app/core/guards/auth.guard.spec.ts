import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { of } from 'rxjs';
import { AuthGuard } from './auth.guard';
import { AuthService } from '@core/services/auth.service';

describe('AuthGuard', () => {
  let guard: AuthGuard;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['isLoggedIn']);
    const routerSpy = jasmine.createSpyObj('Router', ['createUrlTree']);

    TestBed.configureTestingModule({
      providers: [
        AuthGuard,
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });

    guard = TestBed.inject(AuthGuard);
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
  });

  it('should allow activation for authenticated user', (done) => {
    authService.isLoggedIn.and.returnValue(of(true));

    guard.canActivate({} as ActivatedRouteSnapshot, { url: '/dashboard' } as RouterStateSnapshot).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });

  it('should redirect unauthenticated user to login', (done) => {
    authService.isLoggedIn.and.returnValue(of(false));
    router.createUrlTree.and.returnValue('/auth/login?returnUrl=%2Fdashboard' as unknown as UrlTree);

    guard.canActivate({} as ActivatedRouteSnapshot, { url: '/dashboard' } as RouterStateSnapshot).subscribe((result) => {
      expect(router.createUrlTree).toHaveBeenCalledWith(['/auth/login'], {
        queryParams: { returnUrl: '/dashboard' },
      });
      done();
    });
  });

  it('should preserve returnUrl in query params', (done) => {
    authService.isLoggedIn.and.returnValue(of(false));
    router.createUrlTree.and.returnValue('/auth/login?returnUrl=%2Fsettings' as unknown as UrlTree);

    guard.canActivate({} as ActivatedRouteSnapshot, { url: '/settings' } as RouterStateSnapshot).subscribe((result) => {
      expect(router.createUrlTree).toHaveBeenCalledWith(['/auth/login'], {
        queryParams: { returnUrl: '/settings' },
      });
      done();
    });
  });
});
