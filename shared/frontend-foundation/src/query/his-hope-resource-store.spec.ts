import { Observable, of, throwError } from "rxjs";
import { HisHopeResourceStore } from "./his-hope-resource-store";

describe("HisHopeResourceStore", () => {
  it("loads and reloads data for the current query", () => {
    const store = new HisHopeResourceStore<string, string>(
      (query: string) => of(query.toUpperCase()),
      "one",
    );
    store.load();
    expect(store.data()).toBe("ONE");
    store.setQuery("two");
    expect(store.data()).toBe("TWO");
    expect(store.loading()).toBeFalse();
  });

  it("captures loader failures and releases subscriptions on destroy", () => {
    const failure = new Error("failed");
    let unsubscribed = false;
    const source = new Observable<string>(() => () => (unsubscribed = true));
    const store = new HisHopeResourceStore<string | undefined, string>(
      () => throwError(() => failure),
      undefined,
    );
    store.load();
    expect(store.error()).toBe(failure);
    expect(store.loading()).toBeFalse();
    const inFlightStore = new HisHopeResourceStore<string | undefined, string>(
      () => source,
      undefined,
    );
    inFlightStore.load();
    inFlightStore.destroy();
    expect(unsubscribed).toBeTrue();
  });
});
