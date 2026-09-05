import { TestBed } from "@angular/core/testing";
import { ActivatedRouteSnapshot, Router, UrlTree } from "@angular/router";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { firstValueFrom, Observable, of, throwError } from "rxjs";
import { MobileAdminApiService } from "./admin-api.service";
import { mobileReadGuard } from "./mobile-read.guard";

describe("mobileReadGuard", () => {
  let permissions: jasmine.SpyObj<HisHopePermissionService>;
  let router: jasmine.SpyObj<Router>;
  let api: jasmine.SpyObj<MobileAdminApiService>;

  beforeEach(() => {
    permissions = jasmine.createSpyObj("HisHopePermissionService", [
      "has",
      "setPermissions",
      "hasSnapshot",
    ]);
    permissions.hasSnapshot.and.returnValue(true);
    router = jasmine.createSpyObj("Router", ["createUrlTree"]);
    api = jasmine.createSpyObj("MobileAdminApiService", ["getMyPermissions"]);
    TestBed.configureTestingModule({
      providers: [
        { provide: HisHopePermissionService, useValue: permissions },
        { provide: Router, useValue: router },
        { provide: MobileAdminApiService, useValue: api },
      ],
    });
  });

  it("allows access when the read permission is granted", () => {
    permissions.has.and.returnValue(true);
    const route = {
      data: { area: "production" },
    } as unknown as ActivatedRouteSnapshot;
    const result = TestBed.runInInjectionContext(() => mobileReadGuard(route));
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
    TestBed.runInInjectionContext(() => mobileReadGuard(route));
    expect(router.createUrlTree).toHaveBeenCalledWith(
      ["/operations/forbidden"],
      {
        queryParams: { resource: "quality" },
      },
    );
  });

  it("loads permissions before evaluating a cold route guard", async () => {
    permissions.hasSnapshot.and.returnValue(false);
    permissions.has.and.returnValue(true);
    api.getMyPermissions.and.returnValue(
      of({ permissions: ["manufacturing.production.execute"], roles: [] }),
    );
    const route = {
      data: { area: "production" },
    } as unknown as ActivatedRouteSnapshot;

    const result = TestBed.runInInjectionContext(() => mobileReadGuard(route));
    await expectAsync(
      firstValueFrom(result as Observable<boolean | UrlTree>),
    ).toBeResolvedTo(true);
    expect(api.getMyPermissions).toHaveBeenCalled();
  });

  it("fails closed when permission hydration fails", async () => {
    permissions.hasSnapshot.and.returnValue(false);
    api.getMyPermissions.and.returnValue(
      throwError(() => new Error("offline")),
    );
    router.createUrlTree.and.returnValue({} as UrlTree);
    const route = {
      data: { area: "maintenance" },
    } as unknown as ActivatedRouteSnapshot;

    const result = TestBed.runInInjectionContext(() => mobileReadGuard(route));
    await expectAsync(
      firstValueFrom(result as Observable<boolean | UrlTree>),
    ).toBeResolvedTo({} as UrlTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(
      ["/operations/forbidden"],
      {
        queryParams: { resource: "maintenance" },
      },
    );
  });
});
