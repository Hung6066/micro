// @ts-nocheck
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthGuard } from './auth.guard';
import { AuthService } from '@core/services/auth.service';

describe('AuthGuard', () => {
  let guard: AuthGuard;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['isLoggedIn']);
    const routerSpy = jasmine.createSpyObj('Router', ['parseUrl']);

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

    guard.canActivate().subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });

  it('should redirect unauthenticated user to login', (done) => {
    authService.isLoggedIn.and.returnValue(of(false));
    router.parseUrl.and.returnValue('/auth/login' as any);

    guard.canActivate().subscribe((result) => {
      expect(router.parseUrl).toHaveBeenCalledWith('/auth/login');
      done();
    });
  });

  it('should handle store-based auth state via isLoggedIn', (done) => {
    authService.isLoggedIn.and.returnValue(of(true));

    guard.canActivate().subscribe((result) => {
      expect(result).toBeTrue();
      expect(authService.isLoggedIn).toHaveBeenCalled();
      done();
    });
  });
});
