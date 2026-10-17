import { firstValueFrom, of, throwError } from "rxjs";
import { OperatorMobileReadCacheService } from "./operator-mobile-read-cache.service";

describe("OperatorMobileReadCacheService", () => {
  it("reuses a fresh value without invoking the loader", async () => {
    const cache = new OperatorMobileReadCacheService();
    let loads = 0;
    const loader = () => { loads += 1; return of({ value: 1 }); };

    expect(await firstValueFrom(cache.getOrLoad("tenant-a:lots", loader))).toEqual({ value: 1 });
    expect(await firstValueFrom(cache.getOrLoad("tenant-a:lots", loader))).toEqual({ value: 1 });
    expect(loads).toBe(1);
    expect(cache.stale()).toBeFalse();
  });

  it("returns the last value and marks it stale after a failed refresh", async () => {
    const cache = new OperatorMobileReadCacheService();
    await firstValueFrom(cache.getOrLoad("tenant-a:lots", () => of(["LOT-1"])));
    const value = await firstValueFrom(cache.getOrLoad("tenant-a:lots", () => throwError(() => new Error("offline")), -1));

    expect(value).toEqual(["LOT-1"]);
    expect(cache.stale()).toBeTrue();
    expect(cache.lastReadAt()).toBeTruthy();
  });

  it("does not mask authorization failures with stale data", async () => {
    const cache = new OperatorMobileReadCacheService();
    await firstValueFrom(cache.getOrLoad("tenant-a:lots", () => of(["LOT-1"])));
    await expectAsync(firstValueFrom(cache.getOrLoad("tenant-a:lots", () => throwError(() => ({ status: 401 })), -1))).toBeRejectedWith({ status: 401 });
    expect(cache.stale()).toBeFalse();
  });
});
