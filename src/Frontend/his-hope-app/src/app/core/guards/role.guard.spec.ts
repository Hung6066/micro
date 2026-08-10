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
    const authSpy = jasmine.createSpyObj('AuthService', ['ensureCurrentUser']);
    const routerSpy = jasmine.createSpyObj('Router', ['createUrlTree']);

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
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    const route = { data: { roles: ['admin'] } } as any as ActivatedRouteSnapshot;

    guard.canActivate(route, {} as any).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });

  it('should redirect to access-denied when user lacks role', (done) => {
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    router.createUrlTree.and.returnValue('/access-denied' as any);
    const route = { data: { roles: ['superadmin'] } } as any as ActivatedRouteSnapshot;

    guard.canActivate(route, {} as any).subscribe((result) => {
      expect(router.createUrlTree).toHaveBeenCalledWith(['/access-denied']);
      done();
    });
  });

  it('should redirect to login when no user', (done) => {
    const authSpy = jasmine.createSpyObj('AuthService', ['ensureCurrentUser']);
    const routerSpy = jasmine.createSpyObj('Router', ['createUrlTree']);
    routerSpy.createUrlTree.and.returnValue('/auth/login?returnUrl=%2Fadmin' as any);
    const route = { data: { roles: ['admin'] } } as any as ActivatedRouteSnapshot;
    const state = { url: '/admin' } as any;

    authSpy.ensureCurrentUser.and.returnValue(of(null));

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        RoleGuard,
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });
    const localGuard = TestBed.inject(RoleGuard);
    localGuard.canActivate(route, state).subscribe((result) => {
      expect(routerSpy.createUrlTree).toHaveBeenCalledWith(['/auth/login'], {
        queryParams: { returnUrl: '/admin' },
      });
      done();
    });
  });

  it('should allow activation when no roles are required once hydrated', (done) => {
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    const route = { data: {} } as any as ActivatedRouteSnapshot;

    guard.canActivate(route, { url: '/admin' } as any).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });
});
