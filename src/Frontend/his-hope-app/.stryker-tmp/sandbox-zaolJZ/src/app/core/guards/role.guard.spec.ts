// @ts-nocheck
import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRouteSnapshot } from '@angular/router';
import { of } from 'rxjs';
import { RoleGuard } from './role.guard';
import { AuthService } from '@core/services/auth.service';

describe('RoleGuard', () => {
  let guard: RoleGuard;
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  const mockUser = {
    id: 'usr-001',
    username: 'admin',
    email: 'admin@hishope.vn',
    firstName: 'Admin',
    lastName: 'User',
    fullName: 'Admin User',
    roles: ['admin'],
  };

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['isLoggedIn'], {
      currentUser$: of(mockUser),
    });
    const routerSpy = jasmine.createSpyObj('Router', ['parseUrl']);

    TestBed.configureTestingModule({
      providers: [
        RoleGuard,
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });

    guard = TestBed.inject(RoleGuard);
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
  });

  it('should allow activation when user has required role', (done) => {
    const route = { data: { roles: ['admin'] } } as any as ActivatedRouteSnapshot;

    guard.canActivate(route, {} as any).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });

  it('should redirect to access-denied when user lacks role', (done) => {
    router.parseUrl.and.returnValue('/access-denied' as any);
    const route = { data: { roles: ['superadmin'] } } as any as ActivatedRouteSnapshot;

    guard.canActivate(route, {} as any).subscribe((result) => {
      expect(router.parseUrl).toHaveBeenCalledWith('/access-denied');
      done();
    });
  });

  it('should redirect to login when no user', (done) => {
    const authSpy = jasmine.createSpyObj('AuthService', ['isLoggedIn'], {
      currentUser$: of(null),
    });
    router.parseUrl.and.returnValue('/auth/login' as any);
    const route = { data: { roles: ['admin'] } } as any as ActivatedRouteSnapshot;

    const localGuard = new RoleGuard(authSpy, router);
    localGuard.canActivate(route, {} as any).subscribe((result) => {
      expect(router.parseUrl).toHaveBeenCalledWith('/auth/login');
      done();
    });
  });
});
