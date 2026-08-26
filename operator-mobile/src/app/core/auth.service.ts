import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { BehaviorSubject, Observable, catchError, map, of, switchMap, take, tap } from 'rxjs';
import { NativeCapabilityService } from './native-capability.service';
import { DpopProofService } from './dpop-proof.service';
import { environment } from '../../environments/environment';
import { rewriteHisHopeNativeOidcUrl } from './mobile-runtime';

@Injectable({ providedIn: 'root' })
export class MobileAuthService {
  private readonly oidc = inject(OidcSecurityService);
  private readonly http = inject(HttpClient);
  private readonly native = inject(NativeCapabilityService);
  private readonly dpop = inject(DpopProofService);
  private readonly mfaStatusUrl = environment.adminApiUrl.replace(/\/admin$/, '/auth/mfa/status');
  readonly isAuthenticated$ = this.oidc.isAuthenticated$.pipe(map(state => state.isAuthenticated));
  readonly userData$ = this.oidc.userData$;
  readonly loginError = signal('');
  readonly loginInProgress = signal(false);
  private readonly authState = new BehaviorSubject<boolean | null>(null);
  private callbackInProgress = false;

  checkAuth(): Observable<boolean> {
    return this.oidc.checkAuth().pipe(
      switchMap(result => {
        if (!result.isAuthenticated) return of(false);
        return this.http.get<{ requiresMfa: boolean }>(this.mfaStatusUrl).pipe(
          map(status => {
            if (!status.requiresMfa) return true;
            this.clearLocalSession();
            return false;
          }),
          catchError(() => {
            this.clearLocalSession();
            return of(false);
          }),
        );
      }),
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
      take(1),
      switchMap(state => state === null ? this.checkAuth() : of(state)),
    );
  }

  completeCallback(callbackUrl?: string): Observable<boolean> {
    this.callbackInProgress = true;
    const nativeCallbackUrl = this.native.consumeAuthCallbackUrl();
    return this.oidc.checkAuth(callbackUrl ?? nativeCallbackUrl ?? undefined).pipe(
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
    if (this.loginInProgress()) return;
    this.loginError.set('');
    this.loginInProgress.set(true);
    // authorize() loads discovery before creating the PKCE URL. This is
    // required on a cold native start where no discovery is cached yet.
    // The OIDC library subscribes internally and exposes no error callback;
    // preflight discovery here so network/CORS failures become actionable UI.
    this.http.get(`${environment.oidc.authority}/.well-known/openid-configuration`).pipe(
      take(1),
      catchError(error => {
        this.loginInProgress.set(false);
        this.loginError.set(this.describeError(error, 'Unable to reach the identity service.'));
        return of(null);
      }),
    ).subscribe(discovery => {
      if (!discovery) return;
      try {
        this.oidc.authorize(undefined, {
          urlHandler: url => {
            void this.native.openAuthBrowser(
              rewriteHisHopeNativeOidcUrl(url, environment.oidc.authority),
            );
          },
        });
      } catch (error) {
        this.loginInProgress.set(false);
        this.loginError.set(this.describeError(error, 'Unable to start secure sign-in.'));
      }
    });
  }
  async unlockWithBiometric(): Promise<boolean> { return this.native.authenticateBiometric(); }
  logout(): void {
    void this.dpop.clear();
    void this.native.secureRemove('his-hope.mobile.session');
    this.oidc.logoff(undefined, {
      urlHandler: url => {
        void this.native.openAuthBrowser(
          rewriteHisHopeNativeOidcUrl(url, environment.oidc.authority),
        );
      },
    }).subscribe();
  }

  private publishAuthState(isAuthenticated: boolean): void {
    this.authState.next(isAuthenticated);
    if (isAuthenticated) void this.native.secureSet('his-hope.mobile.session', 'authenticated');
  }

  private clearLocalSession(): void {
    this.oidc.logoffLocal();
    void this.native.secureRemove('his-hope.mobile.session');
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
