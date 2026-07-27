import { Injectable, inject, signal } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { BehaviorSubject, Observable, catchError, filter, map, of, take, tap } from 'rxjs';
import { NativeCapabilityService } from './native-capability.service';

@Injectable({ providedIn: 'root' })
export class MobileAuthService {
  private readonly oidc = inject(OidcSecurityService);
  private readonly native = inject(NativeCapabilityService);
  readonly isAuthenticated$ = this.oidc.isAuthenticated$.pipe(map(state => state.isAuthenticated));
  readonly userData$ = this.oidc.userData$;
  readonly loginError = signal('');
  private readonly authState = new BehaviorSubject<boolean | null>(null);
  private callbackInProgress = false;

  checkAuth(): Observable<boolean> {
    return this.oidc.checkAuth().pipe(
      map(result => result.isAuthenticated),
      tap(isAuthenticated => {
        // Do not let the startup check overwrite a callback that is already
        // exchanging its authorization code for tokens.
        if (!this.callbackInProgress) this.publishAuthState(isAuthenticated);
      }),
      catchError(() => {
        if (!this.callbackInProgress) this.publishAuthState(false);
        return of(false);
      }),
      take(1),
    );
  }

  ensureAuthenticated(): Observable<boolean> {
    return this.authState.pipe(
      filter((state): state is boolean => state !== null),
      take(1),
    );
  }

  completeCallback(): Observable<boolean> {
    this.callbackInProgress = true;
    const callbackUrl = this.native.consumeAuthCallbackUrl();
    return this.oidc.checkAuth(callbackUrl ?? undefined).pipe(
      map(result => result.isAuthenticated),
      tap(isAuthenticated => {
        this.callbackInProgress = false;
        this.publishAuthState(isAuthenticated);
      }),
      catchError(error => {
        this.callbackInProgress = false;
        this.publishAuthState(false);
        throw error;
      }),
      take(1),
    );
  }
  login(): void {
    this.loginError.set('');
    // authorize() loads discovery before creating the PKCE URL. This is
    // required on a cold native start where no discovery is cached yet.
    try {
      this.oidc.authorize(undefined, {
        urlHandler: url => { void this.native.openAuthBrowser(url); },
      });
    } catch (error) {
      this.loginError.set(this.describeError(error, 'Unable to reach the identity service.'));
    }
  }
  async unlockWithBiometric(): Promise<boolean> { return this.native.authenticateBiometric(); }
  logout(): void { void this.native.secureRemove('his-hope.mobile.session'); this.oidc.logoff().subscribe(); }

  private publishAuthState(isAuthenticated: boolean): void {
    this.authState.next(isAuthenticated);
    if (isAuthenticated) void this.native.secureSet('his-hope.mobile.session', 'authenticated');
  }

  private describeError(error: unknown, fallback: string): string {
    if (error instanceof Error && error.message) return error.message;
    if (typeof error === 'string' && error.trim()) return error;
    if (error && typeof error === 'object') {
      const value = error as Record<string, unknown>;
      const message = value['message'] ?? value['error'] ?? value['error_description'];
      if (typeof message === 'string' && message.trim()) return message;
      try { return JSON.stringify(error); } catch { return fallback; }
    }
    return fallback;
  }
}
