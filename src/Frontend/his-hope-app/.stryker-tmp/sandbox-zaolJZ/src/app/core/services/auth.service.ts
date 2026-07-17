// @ts-nocheck
function stryNS_9fa48() {
  var g = typeof globalThis === 'object' && globalThis && globalThis.Math === Math && globalThis || new Function("return this")();
  var ns = g.__stryker__ || (g.__stryker__ = {});
  if (ns.activeMutant === undefined && g.process && g.process.env && g.process.env.__STRYKER_ACTIVE_MUTANT__) {
    ns.activeMutant = g.process.env.__STRYKER_ACTIVE_MUTANT__;
  }
  function retrieveNS() {
    return ns;
  }
  stryNS_9fa48 = retrieveNS;
  return retrieveNS();
}
stryNS_9fa48();
function stryCov_9fa48() {
  var ns = stryNS_9fa48();
  var cov = ns.mutantCoverage || (ns.mutantCoverage = {
    static: {},
    perTest: {}
  });
  function cover() {
    var c = cov.static;
    if (ns.currentTestId) {
      c = cov.perTest[ns.currentTestId] = cov.perTest[ns.currentTestId] || {};
    }
    var a = arguments;
    for (var i = 0; i < a.length; i++) {
      c[a[i]] = (c[a[i]] || 0) + 1;
    }
  }
  stryCov_9fa48 = cover;
  cover.apply(null, arguments);
}
function stryMutAct_9fa48(id) {
  var ns = stryNS_9fa48();
  function isActive(id) {
    if (ns.activeMutant === id) {
      if (ns.hitCount !== void 0 && ++ns.hitCount > ns.hitLimit) {
        throw new Error('Stryker: Hit count limit reached (' + ns.hitCount + ')');
      }
      return true;
    }
    return false;
  }
  stryMutAct_9fa48 = isActive;
  return isActive(id);
}
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, of, throwError } from 'rxjs';
import { catchError, map, retry, shareReplay, tap, distinctUntilChanged } from 'rxjs/operators';
import { LoginRequest, RegisterRequest, User } from '@core/models/auth.model';
import { environment } from '@env/environment';
@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly baseUrl = stryMutAct_9fa48("608") ? `` : (stryCov_9fa48("608"), `${environment.apiUrl}/auth`);
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  currentUser$ = this.currentUserSubject.asObservable();
  constructor(private http: HttpClient) {}
  login(request: LoginRequest): Observable<User> {
    if (stryMutAct_9fa48("609")) {
      {}
    } else {
      stryCov_9fa48("609");
      return this.http.post<any>(stryMutAct_9fa48("610") ? `` : (stryCov_9fa48("610"), `${this.baseUrl}/login`), request, stryMutAct_9fa48("611") ? {} : (stryCov_9fa48("611"), {
        withCredentials: stryMutAct_9fa48("612") ? false : (stryCov_9fa48("612"), true)
      })).pipe(tap(response => {
        if (stryMutAct_9fa48("613")) {
          {}
        } else {
          stryCov_9fa48("613");
          if (stryMutAct_9fa48("615") ? false : stryMutAct_9fa48("614") ? true : (stryCov_9fa48("614", "615"), response.accessToken)) {
            if (stryMutAct_9fa48("616")) {
              {}
            } else {
              stryCov_9fa48("616");
              this.storeAccessToken(response.accessToken);
            }
          }
          if (stryMutAct_9fa48("618") ? false : stryMutAct_9fa48("617") ? true : (stryCov_9fa48("617", "618"), response.user)) {
            if (stryMutAct_9fa48("619")) {
              {}
            } else {
              stryCov_9fa48("619");
              this.currentUserSubject.next(response.user);
            }
          }
        }
      }), map(stryMutAct_9fa48("620") ? () => undefined : (stryCov_9fa48("620"), response => response.user as User)), catchError(this.handleError));
    }
  }
  register(request: RegisterRequest): Observable<User> {
    if (stryMutAct_9fa48("621")) {
      {}
    } else {
      stryCov_9fa48("621");
      return this.http.post<User>(stryMutAct_9fa48("622") ? `` : (stryCov_9fa48("622"), `${this.baseUrl}/register`), request, stryMutAct_9fa48("623") ? {} : (stryCov_9fa48("623"), {
        withCredentials: stryMutAct_9fa48("624") ? false : (stryCov_9fa48("624"), true)
      })).pipe(tap(stryMutAct_9fa48("625") ? () => undefined : (stryCov_9fa48("625"), user => this.currentUserSubject.next(user))), catchError(this.handleError));
    }
  }
  refreshToken(): Observable<User> {
    if (stryMutAct_9fa48("626")) {
      {}
    } else {
      stryCov_9fa48("626");
      return this.http.post<User>(stryMutAct_9fa48("627") ? `` : (stryCov_9fa48("627"), `${this.baseUrl}/refresh`), {}, stryMutAct_9fa48("628") ? {} : (stryCov_9fa48("628"), {
        withCredentials: stryMutAct_9fa48("629") ? false : (stryCov_9fa48("629"), true)
      })).pipe(tap(stryMutAct_9fa48("630") ? () => undefined : (stryCov_9fa48("630"), user => this.currentUserSubject.next(user))), retry(1), catchError(this.handleError));
    }
  }
  logout(): Observable<void> {
    if (stryMutAct_9fa48("631")) {
      {}
    } else {
      stryCov_9fa48("631");
      return this.http.post<void>(stryMutAct_9fa48("632") ? `` : (stryCov_9fa48("632"), `${this.baseUrl}/logout`), {}, stryMutAct_9fa48("633") ? {} : (stryCov_9fa48("633"), {
        withCredentials: stryMutAct_9fa48("634") ? false : (stryCov_9fa48("634"), true)
      })).pipe(tap(() => {
        if (stryMutAct_9fa48("635")) {
          {}
        } else {
          stryCov_9fa48("635");
          this.currentUserSubject.next(null);
          this.clearStoredAccessToken();
        }
      }), retry(1), catchError(this.handleError));
    }
  }
  getCurrentUser(): Observable<User> {
    if (stryMutAct_9fa48("636")) {
      {}
    } else {
      stryCov_9fa48("636");
      return this.http.get<User>(stryMutAct_9fa48("637") ? `` : (stryCov_9fa48("637"), `${this.baseUrl}/me`), stryMutAct_9fa48("638") ? {} : (stryCov_9fa48("638"), {
        withCredentials: stryMutAct_9fa48("639") ? false : (stryCov_9fa48("639"), true)
      })).pipe(tap(stryMutAct_9fa48("640") ? () => undefined : (stryCov_9fa48("640"), user => this.currentUserSubject.next(user))), shareReplay(1), retry(1), catchError(this.handleError));
    }
  }
  isLoggedIn(): Observable<boolean> {
    if (stryMutAct_9fa48("641")) {
      {}
    } else {
      stryCov_9fa48("641");
      return this.http.get<{
        authenticated: boolean;
      }>(stryMutAct_9fa48("642") ? `` : (stryCov_9fa48("642"), `${this.baseUrl}/verify`), stryMutAct_9fa48("643") ? {} : (stryCov_9fa48("643"), {
        withCredentials: stryMutAct_9fa48("644") ? false : (stryCov_9fa48("644"), true)
      })).pipe(map(stryMutAct_9fa48("645") ? () => undefined : (stryCov_9fa48("645"), res => res.authenticated)), retry(1), catchError(stryMutAct_9fa48("646") ? () => undefined : (stryCov_9fa48("646"), () => of(stryMutAct_9fa48("647") ? true : (stryCov_9fa48("647"), false)))));
    }
  }

  // ─── Role Methods ─────────────────────────────────────────────────

  /** Extract roles from the current user object */
  getUserRoles(): string[] {
    if (stryMutAct_9fa48("648")) {
      {}
    } else {
      stryCov_9fa48("648");
      const user = this.currentUserSubject.value;
      return stryMutAct_9fa48("649") ? user?.roles && [] : (stryCov_9fa48("649"), (stryMutAct_9fa48("650") ? user.roles : (stryCov_9fa48("650"), user?.roles)) ?? (stryMutAct_9fa48("651") ? ["Stryker was here"] : (stryCov_9fa48("651"), [])));
    }
  }

  /** Extract permissions from the current user object or JWT claims */
  getUserPermissions(): string[] {
    if (stryMutAct_9fa48("652")) {
      {}
    } else {
      stryCov_9fa48("652");
      const user = this.currentUserSubject.value;
      if (stryMutAct_9fa48("655") ? user?.permissions || user.permissions.length > 0 : stryMutAct_9fa48("654") ? false : stryMutAct_9fa48("653") ? true : (stryCov_9fa48("653", "654", "655"), (stryMutAct_9fa48("656") ? user.permissions : (stryCov_9fa48("656"), user?.permissions)) && (stryMutAct_9fa48("659") ? user.permissions.length <= 0 : stryMutAct_9fa48("658") ? user.permissions.length >= 0 : stryMutAct_9fa48("657") ? true : (stryCov_9fa48("657", "658", "659"), user.permissions.length > 0)))) {
        if (stryMutAct_9fa48("660")) {
          {}
        } else {
          stryCov_9fa48("660");
          return user.permissions;
        }
      }
      // Fallback: try to decode JWT from sessionStorage
      const token = this.getStoredAccessToken();
      if (stryMutAct_9fa48("662") ? false : stryMutAct_9fa48("661") ? true : (stryCov_9fa48("661", "662"), token)) {
        if (stryMutAct_9fa48("663")) {
          {}
        } else {
          stryCov_9fa48("663");
          try {
            if (stryMutAct_9fa48("664")) {
              {}
            } else {
              stryCov_9fa48("664");
              const payload = JSON.parse(atob(token.split(stryMutAct_9fa48("665") ? "" : (stryCov_9fa48("665"), '.'))[1]));
              return stryMutAct_9fa48("666") ? (payload.permissions ?? payload.roles) && [] : (stryCov_9fa48("666"), (stryMutAct_9fa48("667") ? payload.permissions && payload.roles : (stryCov_9fa48("667"), payload.permissions ?? payload.roles)) ?? (stryMutAct_9fa48("668") ? ["Stryker was here"] : (stryCov_9fa48("668"), [])));
            }
          } catch {
            if (stryMutAct_9fa48("669")) {
              {}
            } else {
              stryCov_9fa48("669");
              return stryMutAct_9fa48("670") ? ["Stryker was here"] : (stryCov_9fa48("670"), []);
            }
          }
        }
      }
      return stryMutAct_9fa48("671") ? ["Stryker was here"] : (stryCov_9fa48("671"), []);
    }
  }

  /** Check if user has a specific role */
  hasRole(role: string | string[]): boolean {
    if (stryMutAct_9fa48("672")) {
      {}
    } else {
      stryCov_9fa48("672");
      const userRoles = this.getUserRoles();
      if (stryMutAct_9fa48("675") ? typeof role !== 'string' : stryMutAct_9fa48("674") ? false : stryMutAct_9fa48("673") ? true : (stryCov_9fa48("673", "674", "675"), typeof role === (stryMutAct_9fa48("676") ? "" : (stryCov_9fa48("676"), 'string')))) {
        if (stryMutAct_9fa48("677")) {
          {}
        } else {
          stryCov_9fa48("677");
          return userRoles.includes(role);
        }
      }
      return stryMutAct_9fa48("678") ? role.every(r => userRoles.includes(r)) : (stryCov_9fa48("678"), role.some(stryMutAct_9fa48("679") ? () => undefined : (stryCov_9fa48("679"), r => userRoles.includes(r))));
    }
  }

  /** Check if user has a specific permission */
  hasPermission(permission: string | string[]): boolean {
    if (stryMutAct_9fa48("680")) {
      {}
    } else {
      stryCov_9fa48("680");
      const userPermissions = this.getUserPermissions();
      if (stryMutAct_9fa48("683") ? typeof permission !== 'string' : stryMutAct_9fa48("682") ? false : stryMutAct_9fa48("681") ? true : (stryCov_9fa48("681", "682", "683"), typeof permission === (stryMutAct_9fa48("684") ? "" : (stryCov_9fa48("684"), 'string')))) {
        if (stryMutAct_9fa48("685")) {
          {}
        } else {
          stryCov_9fa48("685");
          return userPermissions.includes(permission);
        }
      }
      return stryMutAct_9fa48("686") ? permission.some(p => userPermissions.includes(p)) : (stryCov_9fa48("686"), permission.every(stryMutAct_9fa48("687") ? () => undefined : (stryCov_9fa48("687"), p => userPermissions.includes(p))));
    }
  }

  /** Observable of current user roles, emits on change */
  getCurrentUserRoles(): Observable<string[]> {
    if (stryMutAct_9fa48("688")) {
      {}
    } else {
      stryCov_9fa48("688");
      return this.currentUser$.pipe(map(stryMutAct_9fa48("689") ? () => undefined : (stryCov_9fa48("689"), user => stryMutAct_9fa48("690") ? user?.roles && [] : (stryCov_9fa48("690"), (stryMutAct_9fa48("691") ? user.roles : (stryCov_9fa48("691"), user?.roles)) ?? (stryMutAct_9fa48("692") ? ["Stryker was here"] : (stryCov_9fa48("692"), []))))), distinctUntilChanged(stryMutAct_9fa48("693") ? () => undefined : (stryCov_9fa48("693"), (a, b) => stryMutAct_9fa48("696") ? JSON.stringify(a) !== JSON.stringify(b) : stryMutAct_9fa48("695") ? false : stryMutAct_9fa48("694") ? true : (stryCov_9fa48("694", "695", "696"), JSON.stringify(a) === JSON.stringify(b)))));
    }
  }

  // ─── Token Storage ──────────────────────────────────────────────────

  /** Store the JWT access token (for Bearer header usage) */
  storeAccessToken(token: string): void {
    if (stryMutAct_9fa48("697")) {
      {}
    } else {
      stryCov_9fa48("697");
      try {
        if (stryMutAct_9fa48("698")) {
          {}
        } else {
          stryCov_9fa48("698");
          sessionStorage.setItem(stryMutAct_9fa48("699") ? "" : (stryCov_9fa48("699"), 'hishope_access_token'), token);
        }
      } catch {
        // sessionStorage may be unavailable in some environments
      }
    }
  }

  /** Retrieve the stored JWT access token */
  getStoredAccessToken(): string | null {
    if (stryMutAct_9fa48("700")) {
      {}
    } else {
      stryCov_9fa48("700");
      try {
        if (stryMutAct_9fa48("701")) {
          {}
        } else {
          stryCov_9fa48("701");
          return sessionStorage.getItem(stryMutAct_9fa48("702") ? "" : (stryCov_9fa48("702"), 'hishope_access_token'));
        }
      } catch {
        if (stryMutAct_9fa48("703")) {
          {}
        } else {
          stryCov_9fa48("703");
          return null;
        }
      }
    }
  }

  /** Remove the stored JWT access token */
  clearStoredAccessToken(): void {
    if (stryMutAct_9fa48("704")) {
      {}
    } else {
      stryCov_9fa48("704");
      try {
        if (stryMutAct_9fa48("705")) {
          {}
        } else {
          stryCov_9fa48("705");
          sessionStorage.removeItem(stryMutAct_9fa48("706") ? "" : (stryCov_9fa48("706"), 'hishope_access_token'));
        }
      } catch {
        // noop
      }
    }
  }
  private handleError(error: HttpErrorResponse): Observable<never> {
    if (stryMutAct_9fa48("707")) {
      {}
    } else {
      stryCov_9fa48("707");
      let errorMessage = stryMutAct_9fa48("708") ? "" : (stryCov_9fa48("708"), 'An unknown error occurred');
      if (stryMutAct_9fa48("710") ? false : stryMutAct_9fa48("709") ? true : (stryCov_9fa48("709", "710"), error.error instanceof ErrorEvent)) {
        if (stryMutAct_9fa48("711")) {
          {}
        } else {
          stryCov_9fa48("711");
          errorMessage = stryMutAct_9fa48("712") ? `` : (stryCov_9fa48("712"), `Client error: ${error.error.message}`);
        }
      } else {
        if (stryMutAct_9fa48("713")) {
          {}
        } else {
          stryCov_9fa48("713");
          errorMessage = stryMutAct_9fa48("714") ? `` : (stryCov_9fa48("714"), `Server error: ${error.status} - ${error.message}`);
        }
      }
      console.error(stryMutAct_9fa48("715") ? "" : (stryCov_9fa48("715"), '[AuthService]'), errorMessage);
      return throwError(stryMutAct_9fa48("716") ? () => undefined : (stryCov_9fa48("716"), () => error));
    }
  }
}