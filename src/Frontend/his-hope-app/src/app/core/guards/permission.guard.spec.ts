import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRouteSnapshot } from '@angular/router';
import { of } from 'rxjs';
import { PermissionGuard } from './permission.guard';
import { AuthService } from '@core/services/auth.service';

describe('PermissionGuard', () => {
  let guard: PermissionGuard;
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
    permissions: ['patients.view', 'patients.write'],
  };

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', [
      'ensureCurrentUser', 'hasPermission',
    ]);
    const routerSpy = jasmine.createSpyObj('Router', ['createUrlTree']);

    TestBed.configureTestingModule({
      providers: [
        PermissionGuard,
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });

    guard = TestBed.inject(PermissionGuard);
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
  });

  it('should allow activation when user has required permissions', (done) => {
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    authService.hasPermission.and.returnValue(true);
    const route = { data: { permissions: ['patients.view'] } } as any as ActivatedRouteSnapshot;

    guard.canActivate(route, {} as any).subscribe((result) => {
      expect(result).toBeTrue();
      expect(authService.hasPermission).toHaveBeenCalledWith(['patients.view']);
      done();
    });
  });

  it('should redirect to access-denied when user lacks permissions', (done) => {
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    authService.hasPermission.and.returnValue(false);
    router.createUrlTree.and.returnValue('/access-denied' as any);
    const route = { data: { permissions: ['patients.write'] } } as any as ActivatedRouteSnapshot;

    guard.canActivate(route, {} as any).subscribe((result) => {
      expect(router.createUrlTree).toHaveBeenCalledWith(['/access-denied']);
      done();
    });
  });

  it('should redirect to login when no user', (done) => {
    const authSpy = jasmine.createSpyObj('AuthService', [
      'ensureCurrentUser', 'hasPermission',
    ]);
    authSpy.ensureCurrentUser.and.returnValue(of(null));
    const routerSpy = jasmine.createSpyObj('Router', ['createUrlTree']);
    routerSpy.createUrlTree.and.returnValue('/auth/login?returnUrl=%2Fpatients' as any);
    const route = { data: { permissions: ['patients.view'] } } as any as ActivatedRouteSnapshot;
    const state = { url: '/patients' } as any;

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        PermissionGuard,
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });
    const localGuard = TestBed.inject(PermissionGuard);
    localGuard.canActivate(route, state).subscribe((result) => {
      expect(routerSpy.createUrlTree).toHaveBeenCalledWith(['/auth/login'], {
        queryParams: { returnUrl: '/patients' },
      });
      done();
    });
  });

  it('should allow activation when no permissions are required once hydrated', (done) => {
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    const route = { data: {} } as any as ActivatedRouteSnapshot;

    guard.canActivate(route, { url: '/patients' } as any).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });
});
