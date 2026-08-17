import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { of } from 'rxjs';
import { authGuard } from './auth.guard';
import { AuthService } from '@core/services/auth.service';

describe('authGuard', () => {
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['checkAuth', 'isLoggedIn']);
    const routerSpy = jasmine.createSpyObj('Router', ['parseUrl']);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
    authService.checkAuth.and.returnValue(of(undefined));
  });

  it('allows activation for authenticated user', (done) => {
    authService.isLoggedIn.and.returnValue(of(true));
    TestBed.runInInjectionContext(() => authGuard()).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });

  it('redirects unauthenticated user to login', (done) => {
    authService.isLoggedIn.and.returnValue(of(false));
    router.parseUrl.and.returnValue('/auth/login' as unknown as UrlTree);
    TestBed.runInInjectionContext(() => authGuard()).subscribe(() => {
      expect(router.parseUrl).toHaveBeenCalledWith('/auth/login');
      done();
    });
  });
});
