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
    const authSpy = jasmine.createSpyObj('AuthService', ['isLoggedIn', 'getUserPermissions'], {
      currentUser$: of(mockUser),
    });
    const routerSpy = jasmine.createSpyObj('Router', ['parseUrl']);

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
    authService.getUserPermissions.and.returnValue(['patients.view', 'patients.write']);
    const route = { data: { permissions: ['patients.view'] } } as any as ActivatedRouteSnapshot;

    guard.canActivate(route, {} as any).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });

  it('should redirect to access-denied when user lacks permissions', (done) => {
    authService.getUserPermissions.and.returnValue(['patients.view']);
    router.parseUrl.and.returnValue('/access-denied' as any);
    const route = { data: { permissions: ['patients.write'] } } as any as ActivatedRouteSnapshot;

    guard.canActivate(route, {} as any).subscribe((result) => {
      expect(router.parseUrl).toHaveBeenCalledWith('/access-denied');
      done();
    });
  });

  it('should redirect to login when user is not authenticated', (done) => {
    const authSpy = jasmine.createSpyObj('AuthService', ['isLoggedIn', 'getUserPermissions'], {
      currentUser$: of(null),
    });
    const routerSpy = jasmine.createSpyObj('Router', ['parseUrl']);
    routerSpy.parseUrl.and.returnValue('/auth/login' as any);
    const route = { data: { permissions: ['patients.view'] } } as any as ActivatedRouteSnapshot;

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        PermissionGuard,
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });
    const localGuard = TestBed.inject(PermissionGuard);
    localGuard.canActivate(route, {} as any).subscribe((result) => {
      expect(routerSpy.parseUrl).toHaveBeenCalledWith('/auth/login');
      done();
    });
  });
});
