import { TestBed } from "@angular/core/testing";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HIS_HOPE_MOBILE_AUTH } from "@his-hope/mobile-foundation/angular";
import { MobileSessionService } from "./mobile-session.service";

describe("MobileSessionService", () => {
  let auth: jasmine.SpyObj<{ login: () => void }>;
  let permissions: jasmine.SpyObj<HisHopePermissionService>;
  let session: MobileSessionService;

  beforeEach(() => {
    auth = jasmine.createSpyObj("MobileAuth", ["login"]);
    permissions = jasmine.createSpyObj("HisHopePermissionService", [
      "recordAuthorizationFailure",
    ]);
    TestBed.configureTestingModule({
      providers: [
        MobileSessionService,
        { provide: HIS_HOPE_MOBILE_AUTH, useValue: auth },
        { provide: HisHopePermissionService, useValue: permissions },
      ],
    });
    session = TestBed.inject(MobileSessionService);
  });

  it("opens the expired dialog once for repeated 401s", () => {
    session.handleUnauthorized();
    session.handleUnauthorized();
    expect(session.expired()).toBeTrue();
    expect(permissions.recordAuthorizationFailure).toHaveBeenCalledTimes(1);
  });

  it("starts a fresh login flow from the expired dialog", () => {
    session.handleUnauthorized();
    session.reLogin();
    expect(session.expired()).toBeFalse();
    expect(auth.login).toHaveBeenCalled();
  });
});
