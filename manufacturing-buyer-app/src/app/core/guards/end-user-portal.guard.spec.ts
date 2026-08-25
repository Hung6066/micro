import { TestBed } from "@angular/core/testing";
import { HttpClientTestingModule, HttpTestingController } from "@angular/common/http/testing";
import { Router, UrlTree } from "@angular/router";
import { OidcSecurityService } from "angular-auth-oidc-client";
import { of } from "rxjs";
import { endUserPortalGuard } from "./end-user-portal.guard";

describe("endUserPortalGuard", () => {
  let oidc: jasmine.SpyObj<OidcSecurityService>;
  let router: jasmine.SpyObj<Router>;
  let http: HttpTestingController;

  beforeEach(() => {
    oidc = jasmine.createSpyObj("OidcSecurityService", ["getPayloadFromAccessToken"]);
    router = jasmine.createSpyObj("Router", ["parseUrl"]);
    router.parseUrl.and.returnValue({} as UrlTree);

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        { provide: OidcSecurityService, useValue: oidc },
        { provide: Router, useValue: router },
      ],
    });

    http = TestBed.inject(HttpTestingController);
  });

  it("allows end_user portal_class from access token", (done) => {
    oidc.getPayloadFromAccessToken.and.returnValue(of({ portal_class: "end_user" }));

    TestBed.runInInjectionContext(() => {
      endUserPortalGuard({} as never, {} as never).subscribe((result) => {
        expect(result).toBe(true);
        done();
      });
    });
  });

  it("rejects operator portal_class from access token", (done) => {
    oidc.getPayloadFromAccessToken.and.returnValue(of({ portal_class: "operator" }));

    TestBed.runInInjectionContext(() => {
      endUserPortalGuard({} as never, {} as never).subscribe(() => {
        expect(router.parseUrl).toHaveBeenCalledWith("/auth/login");
        done();
      });
    });
  });

  it("falls back to commerce session for BFF cookie sessions", (done) => {
    oidc.getPayloadFromAccessToken.and.returnValue(of(null));

    TestBed.runInInjectionContext(() => {
      endUserPortalGuard({} as never, {} as never).subscribe((result) => {
        expect(result).toBe(true);
        done();
      });
    });

    const req = http.expectOne("/api/v1/commerce/session");
    req.flush({ portalClass: "end_user" });
  });
});
