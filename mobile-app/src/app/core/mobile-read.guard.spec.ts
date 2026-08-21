import { TestBed } from "@angular/core/testing";
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from "@angular/router";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { createHisHopePermissionReadGuard } from "@his-hope/mobile-foundation/angular";
import { readPermission } from "./authorization/mobile-read-permissions";
import { MobileResource } from "./contracts/mobile.contracts";

describe("mobileReadGuard", () => {
  let permissions: jasmine.SpyObj<HisHopePermissionService>;
  let router: jasmine.SpyObj<Router>;
  const state = {} as RouterStateSnapshot;
  const mobileReadGuard = createHisHopePermissionReadGuard({
    forbiddenPath: "/admin/forbidden",
    resolvePermission: (route) => {
      const resource = route.data["resource"] as MobileResource | undefined;
      return (
        (route.data["readPermission"] as string | undefined) ??
        (resource ? readPermission(resource) : undefined)
      );
    },
  });

  beforeEach(() => {
    permissions = jasmine.createSpyObj("HisHopePermissionService", ["has"]);
    router = jasmine.createSpyObj("Router", ["createUrlTree"]);
    TestBed.configureTestingModule({
      providers: [
        { provide: HisHopePermissionService, useValue: permissions },
        { provide: Router, useValue: router },
      ],
    });
  });

  it("allows access when the read permission is granted", () => {
    permissions.has.and.returnValue(true);
    const route = {
      data: { resource: "clients" },
    } as unknown as ActivatedRouteSnapshot;
    const result = TestBed.runInInjectionContext(() =>
      mobileReadGuard(route, state),
    );
    expect(result).toBeTrue();
    expect(permissions.has).toHaveBeenCalledWith("admin.clients.read");
  });

  it("redirects to forbidden when the read permission is missing", () => {
    permissions.has.and.returnValue(false);
    router.createUrlTree.and.returnValue({} as UrlTree);
    const route = {
      data: { resource: "users" },
    } as unknown as ActivatedRouteSnapshot;
    TestBed.runInInjectionContext(() => mobileReadGuard(route, state));
    expect(router.createUrlTree).toHaveBeenCalledWith(["/admin/forbidden"], {
      queryParams: { resource: "users" },
    });
  });
});
