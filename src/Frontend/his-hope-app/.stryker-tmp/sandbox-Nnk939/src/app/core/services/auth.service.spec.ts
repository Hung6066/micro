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
import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { LoginRequest, RegisterRequest, User } from '@core/models/auth.model';
import { environment } from '@env/environment';
import { HttpErrorResponse } from '@angular/common/http';
describe(stryMutAct_9fa48("452") ? "" : (stryCov_9fa48("452"), 'AuthService'), () => {
  if (stryMutAct_9fa48("453")) {
    {}
  } else {
    stryCov_9fa48("453");
    let service: AuthService;
    let httpMock: HttpTestingController;
    const mockUser: User = stryMutAct_9fa48("454") ? {} : (stryCov_9fa48("454"), {
      id: stryMutAct_9fa48("455") ? "" : (stryCov_9fa48("455"), 'usr-001'),
      username: stryMutAct_9fa48("456") ? "" : (stryCov_9fa48("456"), 'admin'),
      email: stryMutAct_9fa48("457") ? "" : (stryCov_9fa48("457"), 'admin@hishope.vn'),
      firstName: stryMutAct_9fa48("458") ? "" : (stryCov_9fa48("458"), 'Admin'),
      lastName: stryMutAct_9fa48("459") ? "" : (stryCov_9fa48("459"), 'User'),
      fullName: stryMutAct_9fa48("460") ? "" : (stryCov_9fa48("460"), 'Admin User'),
      roles: stryMutAct_9fa48("461") ? [] : (stryCov_9fa48("461"), [stryMutAct_9fa48("462") ? "" : (stryCov_9fa48("462"), 'admin')]),
      permissions: stryMutAct_9fa48("463") ? [] : (stryCov_9fa48("463"), [stryMutAct_9fa48("464") ? "" : (stryCov_9fa48("464"), 'patients.view'), stryMutAct_9fa48("465") ? "" : (stryCov_9fa48("465"), 'patients.write')])
    });
    beforeEach(() => {
      if (stryMutAct_9fa48("466")) {
        {}
      } else {
        stryCov_9fa48("466");
        TestBed.configureTestingModule(stryMutAct_9fa48("467") ? {} : (stryCov_9fa48("467"), {
          imports: stryMutAct_9fa48("468") ? [] : (stryCov_9fa48("468"), [HttpClientTestingModule])
        }));
        service = TestBed.inject(AuthService);
        httpMock = TestBed.inject(HttpTestingController);
        sessionStorage.clear();
      }
    });
    afterEach(() => {
      if (stryMutAct_9fa48("469")) {
        {}
      } else {
        stryCov_9fa48("469");
        httpMock.verify();
        sessionStorage.clear();
      }
    });
    it(stryMutAct_9fa48("470") ? "" : (stryCov_9fa48("470"), 'should login, store token, and return user'), () => {
      if (stryMutAct_9fa48("471")) {
        {}
      } else {
        stryCov_9fa48("471");
        const request: LoginRequest = stryMutAct_9fa48("472") ? {} : (stryCov_9fa48("472"), {
          username: stryMutAct_9fa48("473") ? "" : (stryCov_9fa48("473"), 'admin'),
          password: stryMutAct_9fa48("474") ? "" : (stryCov_9fa48("474"), 'secret')
        });
        const response = stryMutAct_9fa48("475") ? {} : (stryCov_9fa48("475"), {
          accessToken: stryMutAct_9fa48("476") ? "" : (stryCov_9fa48("476"), 'jwt-token'),
          user: mockUser
        });
        service.login(request).subscribe(user => {
          if (stryMutAct_9fa48("477")) {
            {}
          } else {
            stryCov_9fa48("477");
            expect(user).toEqual(mockUser);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("478") ? `` : (stryCov_9fa48("478"), `${environment.apiUrl}/auth/login`));
        expect(req.request.method).toBe(stryMutAct_9fa48("479") ? "" : (stryCov_9fa48("479"), 'POST'));
        expect(req.request.withCredentials).toBeTrue();
        req.flush(response);
        expect(sessionStorage.getItem(stryMutAct_9fa48("480") ? "" : (stryCov_9fa48("480"), 'hishope_access_token'))).toBe(stryMutAct_9fa48("481") ? "" : (stryCov_9fa48("481"), 'jwt-token'));
      }
    });
    it(stryMutAct_9fa48("482") ? "" : (stryCov_9fa48("482"), 'should register and return user'), () => {
      if (stryMutAct_9fa48("483")) {
        {}
      } else {
        stryCov_9fa48("483");
        const request: RegisterRequest = stryMutAct_9fa48("484") ? {} : (stryCov_9fa48("484"), {
          username: stryMutAct_9fa48("485") ? "" : (stryCov_9fa48("485"), 'newuser'),
          email: stryMutAct_9fa48("486") ? "" : (stryCov_9fa48("486"), 'new@hishope.vn'),
          password: stryMutAct_9fa48("487") ? "" : (stryCov_9fa48("487"), 'secret'),
          firstName: stryMutAct_9fa48("488") ? "" : (stryCov_9fa48("488"), 'New'),
          lastName: stryMutAct_9fa48("489") ? "" : (stryCov_9fa48("489"), 'User')
        });
        service.register(request).subscribe(user => {
          if (stryMutAct_9fa48("490")) {
            {}
          } else {
            stryCov_9fa48("490");
            expect(user).toEqual(mockUser);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("491") ? `` : (stryCov_9fa48("491"), `${environment.apiUrl}/auth/register`));
        expect(req.request.method).toBe(stryMutAct_9fa48("492") ? "" : (stryCov_9fa48("492"), 'POST'));
        req.flush(mockUser);
      }
    });
    it(stryMutAct_9fa48("493") ? "" : (stryCov_9fa48("493"), 'should refresh token'), () => {
      if (stryMutAct_9fa48("494")) {
        {}
      } else {
        stryCov_9fa48("494");
        service.refreshToken().subscribe(user => {
          if (stryMutAct_9fa48("495")) {
            {}
          } else {
            stryCov_9fa48("495");
            expect(user).toEqual(mockUser);
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("496") ? `` : (stryCov_9fa48("496"), `${environment.apiUrl}/auth/refresh`));
        expect(req.request.method).toBe(stryMutAct_9fa48("497") ? "" : (stryCov_9fa48("497"), 'POST'));
        req.flush(mockUser);
      }
    });
    it(stryMutAct_9fa48("498") ? "" : (stryCov_9fa48("498"), 'should logout and clear token'), () => {
      if (stryMutAct_9fa48("499")) {
        {}
      } else {
        stryCov_9fa48("499");
        sessionStorage.setItem(stryMutAct_9fa48("500") ? "" : (stryCov_9fa48("500"), 'hishope_access_token'), stryMutAct_9fa48("501") ? "" : (stryCov_9fa48("501"), 'existing-token'));
        service.logout().subscribe();
        const req = httpMock.expectOne(stryMutAct_9fa48("502") ? `` : (stryCov_9fa48("502"), `${environment.apiUrl}/auth/logout`));
        expect(req.request.method).toBe(stryMutAct_9fa48("503") ? "" : (stryCov_9fa48("503"), 'POST'));
        req.flush(null);
        expect(sessionStorage.getItem(stryMutAct_9fa48("504") ? "" : (stryCov_9fa48("504"), 'hishope_access_token'))).toBeNull();
      }
    });
    it(stryMutAct_9fa48("505") ? "" : (stryCov_9fa48("505"), 'should return logged in status when token exists'), () => {
      if (stryMutAct_9fa48("506")) {
        {}
      } else {
        stryCov_9fa48("506");
        service.isLoggedIn().subscribe(loggedIn => {
          if (stryMutAct_9fa48("507")) {
            {}
          } else {
            stryCov_9fa48("507");
            expect(loggedIn).toBeTrue();
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("508") ? `` : (stryCov_9fa48("508"), `${environment.apiUrl}/auth/verify`));
        req.flush(stryMutAct_9fa48("509") ? {} : (stryCov_9fa48("509"), {
          authenticated: stryMutAct_9fa48("510") ? false : (stryCov_9fa48("510"), true)
        }));
      }
    });
    it(stryMutAct_9fa48("511") ? "" : (stryCov_9fa48("511"), 'should return false for isLoggedIn on error'), () => {
      if (stryMutAct_9fa48("512")) {
        {}
      } else {
        stryCov_9fa48("512");
        service.isLoggedIn().subscribe(loggedIn => {
          if (stryMutAct_9fa48("513")) {
            {}
          } else {
            stryCov_9fa48("513");
            expect(loggedIn).toBeFalse();
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("514") ? `` : (stryCov_9fa48("514"), `${environment.apiUrl}/auth/verify`));
        req.error(new ProgressEvent(stryMutAct_9fa48("515") ? "" : (stryCov_9fa48("515"), 'Network error')));
        // isLoggedIn() pipes retry(1), so handle the retry
        const retryReq = httpMock.expectOne(stryMutAct_9fa48("516") ? `` : (stryCov_9fa48("516"), `${environment.apiUrl}/auth/verify`));
        retryReq.error(new ProgressEvent(stryMutAct_9fa48("517") ? "" : (stryCov_9fa48("517"), 'Network error')));
      }
    });
    it(stryMutAct_9fa48("518") ? "" : (stryCov_9fa48("518"), 'should return user roles from subject'), () => {
      if (stryMutAct_9fa48("519")) {
        {}
      } else {
        stryCov_9fa48("519");
        service[stryMutAct_9fa48("520") ? "" : (stryCov_9fa48("520"), 'currentUserSubject')].next(mockUser);
        expect(service.getUserRoles()).toEqual(stryMutAct_9fa48("521") ? [] : (stryCov_9fa48("521"), [stryMutAct_9fa48("522") ? "" : (stryCov_9fa48("522"), 'admin')]));
      }
    });
    it(stryMutAct_9fa48("523") ? "" : (stryCov_9fa48("523"), 'should return empty roles when no user'), () => {
      if (stryMutAct_9fa48("524")) {
        {}
      } else {
        stryCov_9fa48("524");
        service[stryMutAct_9fa48("525") ? "" : (stryCov_9fa48("525"), 'currentUserSubject')].next(null);
        expect(service.getUserRoles()).toEqual(stryMutAct_9fa48("526") ? ["Stryker was here"] : (stryCov_9fa48("526"), []));
      }
    });
    it(stryMutAct_9fa48("527") ? "" : (stryCov_9fa48("527"), 'should check permission'), () => {
      if (stryMutAct_9fa48("528")) {
        {}
      } else {
        stryCov_9fa48("528");
        service[stryMutAct_9fa48("529") ? "" : (stryCov_9fa48("529"), 'currentUserSubject')].next(mockUser);
        expect(service.hasPermission(stryMutAct_9fa48("530") ? "" : (stryCov_9fa48("530"), 'patients.view'))).toBeTrue();
        expect(service.hasPermission(stryMutAct_9fa48("531") ? "" : (stryCov_9fa48("531"), 'nonexistent'))).toBeFalse();
      }
    });
    it(stryMutAct_9fa48("532") ? "" : (stryCov_9fa48("532"), 'should check multiple permissions with AND logic'), () => {
      if (stryMutAct_9fa48("533")) {
        {}
      } else {
        stryCov_9fa48("533");
        service[stryMutAct_9fa48("534") ? "" : (stryCov_9fa48("534"), 'currentUserSubject')].next(mockUser);
        expect(service.hasPermission(stryMutAct_9fa48("535") ? [] : (stryCov_9fa48("535"), [stryMutAct_9fa48("536") ? "" : (stryCov_9fa48("536"), 'patients.view'), stryMutAct_9fa48("537") ? "" : (stryCov_9fa48("537"), 'patients.write')]))).toBeTrue();
        expect(service.hasPermission(stryMutAct_9fa48("538") ? [] : (stryCov_9fa48("538"), [stryMutAct_9fa48("539") ? "" : (stryCov_9fa48("539"), 'patients.view'), stryMutAct_9fa48("540") ? "" : (stryCov_9fa48("540"), 'nonexistent')]))).toBeFalse();
      }
    });
    it(stryMutAct_9fa48("541") ? "" : (stryCov_9fa48("541"), 'should handleHttpError and transform'), () => {
      if (stryMutAct_9fa48("542")) {
        {}
      } else {
        stryCov_9fa48("542");
        service.login(stryMutAct_9fa48("543") ? {} : (stryCov_9fa48("543"), {
          username: stryMutAct_9fa48("544") ? "Stryker was here!" : (stryCov_9fa48("544"), ''),
          password: stryMutAct_9fa48("545") ? "Stryker was here!" : (stryCov_9fa48("545"), '')
        })).subscribe(stryMutAct_9fa48("546") ? {} : (stryCov_9fa48("546"), {
          error: (error: HttpErrorResponse) => {
            if (stryMutAct_9fa48("547")) {
              {}
            } else {
              stryCov_9fa48("547");
              expect(error).toBeTruthy();
            }
          }
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("548") ? `` : (stryCov_9fa48("548"), `${environment.apiUrl}/auth/login`));
        req.flush(stryMutAct_9fa48("549") ? "" : (stryCov_9fa48("549"), 'Unauthorized'), stryMutAct_9fa48("550") ? {} : (stryCov_9fa48("550"), {
          status: 401,
          statusText: stryMutAct_9fa48("551") ? "" : (stryCov_9fa48("551"), 'Unauthorized')
        }));
      }
    });
    it(stryMutAct_9fa48("552") ? "" : (stryCov_9fa48("552"), 'should return null from getStoredAccessToken when no token'), () => {
      if (stryMutAct_9fa48("553")) {
        {}
      } else {
        stryCov_9fa48("553");
        sessionStorage.removeItem(stryMutAct_9fa48("554") ? "" : (stryCov_9fa48("554"), 'hishope_access_token'));
        expect(service.getStoredAccessToken()).toBeNull();
      }
    });
    it(stryMutAct_9fa48("555") ? "" : (stryCov_9fa48("555"), 'should clear stored access token'), () => {
      if (stryMutAct_9fa48("556")) {
        {}
      } else {
        stryCov_9fa48("556");
        sessionStorage.setItem(stryMutAct_9fa48("557") ? "" : (stryCov_9fa48("557"), 'hishope_access_token'), stryMutAct_9fa48("558") ? "" : (stryCov_9fa48("558"), 'test-token'));
        service.clearStoredAccessToken();
        expect(sessionStorage.getItem(stryMutAct_9fa48("559") ? "" : (stryCov_9fa48("559"), 'hishope_access_token'))).toBeNull();
      }
    });
    it(stryMutAct_9fa48("560") ? "" : (stryCov_9fa48("560"), 'should check hasRole with string array'), () => {
      if (stryMutAct_9fa48("561")) {
        {}
      } else {
        stryCov_9fa48("561");
        service[stryMutAct_9fa48("562") ? "" : (stryCov_9fa48("562"), 'currentUserSubject')].next(stryMutAct_9fa48("563") ? {} : (stryCov_9fa48("563"), {
          ...mockUser,
          roles: stryMutAct_9fa48("564") ? [] : (stryCov_9fa48("564"), [stryMutAct_9fa48("565") ? "" : (stryCov_9fa48("565"), 'admin'), stryMutAct_9fa48("566") ? "" : (stryCov_9fa48("566"), 'doctor')])
        }));
        expect(service.hasRole(stryMutAct_9fa48("567") ? "" : (stryCov_9fa48("567"), 'admin'))).toBeTrue();
        expect(service.hasRole(stryMutAct_9fa48("568") ? "" : (stryCov_9fa48("568"), 'nurse'))).toBeFalse();
        expect(service.hasRole(stryMutAct_9fa48("569") ? [] : (stryCov_9fa48("569"), [stryMutAct_9fa48("570") ? "" : (stryCov_9fa48("570"), 'admin'), stryMutAct_9fa48("571") ? "" : (stryCov_9fa48("571"), 'nurse')]))).toBeTrue();
      }
    });
    it(stryMutAct_9fa48("572") ? "" : (stryCov_9fa48("572"), 'should store access token'), () => {
      if (stryMutAct_9fa48("573")) {
        {}
      } else {
        stryCov_9fa48("573");
        service.storeAccessToken(stryMutAct_9fa48("574") ? "" : (stryCov_9fa48("574"), 'new-jwt-token'));
        expect(sessionStorage.getItem(stryMutAct_9fa48("575") ? "" : (stryCov_9fa48("575"), 'hishope_access_token'))).toBe(stryMutAct_9fa48("576") ? "" : (stryCov_9fa48("576"), 'new-jwt-token'));
      }
    });
    it(stryMutAct_9fa48("577") ? "" : (stryCov_9fa48("577"), 'should getCurrentUser'), () => {
      if (stryMutAct_9fa48("578")) {
        {}
      } else {
        stryCov_9fa48("578");
        service.getCurrentUser().subscribe(user => {
          if (stryMutAct_9fa48("579")) {
            {}
          } else {
            stryCov_9fa48("579");
            expect(user).toBeTruthy();
          }
        });
        const req = httpMock.expectOne(stryMutAct_9fa48("580") ? `` : (stryCov_9fa48("580"), `${environment.apiUrl}/auth/me`));
        expect(req.request.method).toBe(stryMutAct_9fa48("581") ? "" : (stryCov_9fa48("581"), 'GET'));
        req.flush(stryMutAct_9fa48("582") ? {} : (stryCov_9fa48("582"), {
          id: stryMutAct_9fa48("583") ? "" : (stryCov_9fa48("583"), 'usr-001'),
          username: stryMutAct_9fa48("584") ? "" : (stryCov_9fa48("584"), 'admin')
        }));
      }
    });
    it(stryMutAct_9fa48("585") ? "" : (stryCov_9fa48("585"), 'should getCurrentUserRoles observable'), () => {
      if (stryMutAct_9fa48("586")) {
        {}
      } else {
        stryCov_9fa48("586");
        service.getCurrentUserRoles().subscribe(roles => {
          if (stryMutAct_9fa48("587")) {
            {}
          } else {
            stryCov_9fa48("587");
            expect(roles).toEqual(stryMutAct_9fa48("588") ? ["Stryker was here"] : (stryCov_9fa48("588"), []));
          }
        });
      }
    });
    it(stryMutAct_9fa48("589") ? "" : (stryCov_9fa48("589"), 'should getUserPermissions from JWT when no user'), () => {
      if (stryMutAct_9fa48("590")) {
        {}
      } else {
        stryCov_9fa48("590");
        sessionStorage.setItem(stryMutAct_9fa48("591") ? "" : (stryCov_9fa48("591"), 'hishope_access_token'), stryMutAct_9fa48("592") ? "" : (stryCov_9fa48("592"), 'eyJhbGciOiJIUzI1NiJ9.eyJwZXJtaXNzaW9ucyI6WyJwYXRpZW50cy52aWV3Il19.dGVzdA'));
        const perms = service.getUserPermissions();
        expect(perms).toEqual(stryMutAct_9fa48("593") ? [] : (stryCov_9fa48("593"), [stryMutAct_9fa48("594") ? "" : (stryCov_9fa48("594"), 'patients.view')]));
      }
    });
    it(stryMutAct_9fa48("595") ? "" : (stryCov_9fa48("595"), 'should return empty permissions when no token or user'), () => {
      if (stryMutAct_9fa48("596")) {
        {}
      } else {
        stryCov_9fa48("596");
        sessionStorage.removeItem(stryMutAct_9fa48("597") ? "" : (stryCov_9fa48("597"), 'hishope_access_token'));
        service[stryMutAct_9fa48("598") ? "" : (stryCov_9fa48("598"), 'currentUserSubject')].next(null);
        expect(service.getUserPermissions()).toEqual(stryMutAct_9fa48("599") ? ["Stryker was here"] : (stryCov_9fa48("599"), []));
      }
    });
    it(stryMutAct_9fa48("600") ? "" : (stryCov_9fa48("600"), 'should handle register error'), () => {
      if (stryMutAct_9fa48("601")) {
        {}
      } else {
        stryCov_9fa48("601");
        service.register({} as any).subscribe(stryMutAct_9fa48("602") ? {} : (stryCov_9fa48("602"), {
          error: stryMutAct_9fa48("603") ? () => undefined : (stryCov_9fa48("603"), error => expect(error).toBeTruthy())
        }));
        const req = httpMock.expectOne(stryMutAct_9fa48("604") ? `` : (stryCov_9fa48("604"), `${environment.apiUrl}/auth/register`));
        req.flush(stryMutAct_9fa48("605") ? "" : (stryCov_9fa48("605"), 'Error'), stryMutAct_9fa48("606") ? {} : (stryCov_9fa48("606"), {
          status: 400,
          statusText: stryMutAct_9fa48("607") ? "" : (stryCov_9fa48("607"), 'Bad Request')
        }));
      }
    });
  }
});