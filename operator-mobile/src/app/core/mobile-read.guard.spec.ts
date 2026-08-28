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
import type { OperatorMobileArea } from "./contracts/mobile.contracts";

describe("mobileReadGuard", () => {
  let permissions: jasmine.SpyObj<HisHopePermissionService>;
  let router: jasmine.SpyObj<Router>;
  const state = {} as RouterStateSnapshot;
  const mobileReadGuard = createHisHopePermissionReadGuard({
    forbiddenPath: "/operations/forbidden",
    resolvePermission: (route) => {
      const area = route.data["area"] as OperatorMobileArea | undefined;
      return (
        (route.data["readPermission"] as string | undefined) ??
        (area ? readPermission(area) : undefined)
      );
    },
    resolveForbiddenQuery: (route, permission) => ({
      resource:
        (route.data["area"] as string | undefined) ??
        (route.data["resource"] as string | undefined) ??
        permission,
    }),
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
      data: { area: "production" },
    } as unknown as ActivatedRouteSnapshot;
    const result = TestBed.runInInjectionContext(() =>
      mobileReadGuard(route, state),
    );
    expect(result).toBeTrue();
    expect(permissions.has).toHaveBeenCalledWith(
      "manufacturing.production.execute",
    );
  });

  it("redirects to forbidden when the read permission is missing", () => {
    permissions.has.and.returnValue(false);
    router.createUrlTree.and.returnValue({} as UrlTree);
    const route = {
      data: { area: "quality" },
    } as unknown as ActivatedRouteSnapshot;
    TestBed.runInInjectionContext(() => mobileReadGuard(route, state));
    expect(router.createUrlTree).toHaveBeenCalledWith(["/operations/forbidden"], {
      queryParams: { resource: "quality" },
    });
  });
});
