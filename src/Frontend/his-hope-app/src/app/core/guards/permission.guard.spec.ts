import { TestBed } from "@angular/core/testing";
import { ActivatedRouteSnapshot, Router } from "@angular/router";
import { of } from "rxjs";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { AuthService } from "@core/services/auth.service";
import { permissionGuard } from "./permission.guard";

describe("permissionGuard", () => {
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  const mockUser = {
    id: "usr-001",
    username: "admin",
    email: "admin@hishope.vn",
    roles: ["admin"],
    permissions: ["patients.view", "patients.write"],
  };

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj("AuthService", ["ensureCurrentUser"]);
    const routerSpy = jasmine.createSpyObj("Router", ["createUrlTree"]);
    TestBed.configureTestingModule({
      providers: [
        {
          provide: HisHopePermissionService,
          useClass: HisHopePermissionService,
        },
        { provide: AuthService, useValue: authSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
  });

  it("allows activation when the shared snapshot has the permission", (done) => {
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    TestBed.inject(HisHopePermissionService).setPermissions(["patients.view"]);
    const route = {
      data: { permissions: ["patients.view"] },
    } as any as ActivatedRouteSnapshot;

    TestBed.runInInjectionContext(() =>
      permissionGuard(route, {} as any),
    ).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });

  it("redirects when the shared snapshot lacks the permission", (done) => {
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    TestBed.inject(HisHopePermissionService).setPermissions(["patients.view"]);
    router.createUrlTree.and.returnValue("/access-denied" as any);
    const route = {
      data: { permissions: ["patients.write"] },
    } as any as ActivatedRouteSnapshot;

    TestBed.runInInjectionContext(() =>
      permissionGuard(route, {} as any),
    ).subscribe(() => {
      expect(router.createUrlTree).toHaveBeenCalledWith(["/access-denied"]);
      done();
    });
  });

  it("redirects unauthenticated users to login", (done) => {
    authService.ensureCurrentUser.and.returnValue(of(null));
    router.createUrlTree.and.returnValue(
      "/auth/login?returnUrl=%2Fpatients" as any,
    );
    const route = {
      data: { permissions: ["patients.view"] },
    } as any as ActivatedRouteSnapshot;

    TestBed.runInInjectionContext(() =>
      permissionGuard(route, { url: "/patients" } as any),
    ).subscribe(() => {
      expect(router.createUrlTree).toHaveBeenCalledWith(["/auth/login"], {
        queryParams: { returnUrl: "/patients" },
      });
      done();
    });
  });

  it("allows activation when no permission is required", (done) => {
    authService.ensureCurrentUser.and.returnValue(of(mockUser as any));
    const route = { data: {} } as any as ActivatedRouteSnapshot;

    TestBed.runInInjectionContext(() =>
      permissionGuard(route, { url: "/patients" } as any),
    ).subscribe((result) => {
      expect(result).toBeTrue();
      done();
    });
  });
});
