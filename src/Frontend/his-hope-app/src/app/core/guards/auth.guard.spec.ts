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
    const authSpy = jasmine.createSpyObj('AuthService', ['ensureCurrentUser']);
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
    authService.ensureCurrentUser.and.returnValue(of({ id: 'usr-001' } as any));

    guard.canActivate({} as any, { url: '/dashboard' } as any).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });

  it('should redirect unauthenticated user to login', (done) => {
    authService.ensureCurrentUser.and.returnValue(of(null));
    router.createUrlTree.and.returnValue('/auth/login?returnUrl=%2Fdashboard' as any);

    guard.canActivate({} as any, { url: '/dashboard' } as any).subscribe((result) => {
      expect(router.createUrlTree).toHaveBeenCalledWith(['/auth/login'], {
        queryParams: { returnUrl: '/dashboard' },
      });
      done();
    });
  });

  it('should wait for hydrated auth state before activating', (done) => {
    authService.ensureCurrentUser.and.returnValue(of({ id: 'usr-001' } as any));

    guard.canActivate({} as any, { url: '/dashboard' } as any).subscribe((result) => {
      expect(result).toBeTrue();
      expect(authService.ensureCurrentUser).toHaveBeenCalled();
      done();
    });
  });
});
