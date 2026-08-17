import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { roleGuard } from './role.guard';

describe('roleGuard', () => {
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  const mockUser = {
    id: 'usr-001',
    username: 'admin',
    email: 'admin@hishope.vn',
    roles: ['admin'],
  };

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['ensureCurrentUser']);
    const routerSpy = jasmine.createSpyObj('Router', ['createUrlTree']);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
  });

  it('allows activation when user has required role', (done) => {
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    const route = { data: { roles: ['admin'] } } as any as ActivatedRouteSnapshot;
    TestBed.runInInjectionContext(() => roleGuard(route, {} as any)).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });

  it('redirects when user lacks role', (done) => {
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    router.createUrlTree.and.returnValue('/access-denied' as any);
    const route = { data: { roles: ['superadmin'] } } as any as ActivatedRouteSnapshot;
    TestBed.runInInjectionContext(() => roleGuard(route, {} as any)).subscribe(() => {
      expect(router.createUrlTree).toHaveBeenCalledWith(['/access-denied']);
      done();
    });
  });

  it('redirects unauthenticated users to login', (done) => {
    authService.ensureCurrentUser.and.returnValue(of(null));
    router.createUrlTree.and.returnValue('/auth/login' as any);
    const route = { data: { roles: ['admin'] } } as any as ActivatedRouteSnapshot;
    TestBed.runInInjectionContext(() => roleGuard(route, { url: '/admin' } as any)).subscribe(() => {
      expect(router.createUrlTree).toHaveBeenCalledWith(['/auth/login'], {
        queryParams: { returnUrl: '/admin' },
      });
      done();
    });
  });

  it('allows activation when no role is required', (done) => {
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    const route = { data: {} } as any as ActivatedRouteSnapshot;
    TestBed.runInInjectionContext(() => roleGuard(route, {} as any)).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });
});
