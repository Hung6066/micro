import { TestBed } from "@angular/core/testing";
import { Router, UrlTree } from "@angular/router";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { firstValueFrom, isObservable, of, throwError } from "rxjs";
import { adminReadGuard } from "./admin-read.guard";
import { AdminPermissionsApiService } from "../services/admin-permissions-api.service";

async function runGuard(path: string, data: Record<string, unknown> = {}) {
  const result = TestBed.runInInjectionContext(() =>
    adminReadGuard(
      {
        data,
        routeConfig: { path },
      } as never,
      {} as never,
    ),
  );
  return isObservable(result) ? firstValueFrom(result) : result;
}

describe("adminReadGuard", () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: HisHopePermissionService,
          useValue: {
            has: jasmine.createSpy("has").and.returnValue(false),
            hasSnapshot: jasmine.createSpy("hasSnapshot").and.returnValue(false),
            setSnapshot: jasmine.createSpy("setSnapshot"),
          },
        },
        {
          provide: AdminPermissionsApiService,
          useValue: {
            getCurrent: jasmine
              .createSpy("getCurrent")
              .and.returnValue(of({ permissions: [] })),
          },
        },
        {
          provide: Router,
          useValue: {
            createUrlTree: jasmine
              .createSpy("createUrlTree")
              .and.callFake((_parts, opts) => ({ forbidden: true, opts }) as unknown as UrlTree),
          },
        },
      ],
    });
  });

  it("allows dashboard without a mapped read permission", async () => {
    await expectAsync(runGuard("dashboard")).toBeResolvedTo(true);
  });

  it("redirects to forbidden when permission is missing after hydration", async () => {
    const result = await runGuard("roles");
    expect(result).toEqual(jasmine.objectContaining({ forbidden: true }));
  });

  it("redirects to forbidden when hydration fails", async () => {
    TestBed.overrideProvider(AdminPermissionsApiService, {
      useValue: {
        getCurrent: jasmine
          .createSpy("getCurrent")
          .and.returnValue(throwError(() => new Error("offline"))),
      },
    });
    const result = await runGuard("users");
    expect(result).toEqual(jasmine.objectContaining({ forbidden: true }));
  });
});
