import { DestroyRef } from "@angular/core";
import { HttpErrorResponse } from "@angular/common/http";
import { Observable, of, throwError } from "rxjs";
import { HisHopeResourceState } from "./his-hope-resource-state";

describe("HisHopeResourceState", () => {
  it("stores data and clears loading after success", () => {
    const state = new HisHopeResourceState<string>();

    state.load(of("ready"));

    expect(state.data()).toBe("ready");
    expect(state.loading()).toBeFalse();
    expect(state.error()).toBeNull();
  });

  it("stores the error and clears loading after failure", () => {
    const failure = new Error("request failed");
    const state = new HisHopeResourceState<string>();

    state.load(throwError(() => failure));

    expect(state.data()).toBeNull();
    expect(state.loading()).toBeFalse();
    expect(state.error()?.detail).toBe("request failed");
  });

  it("normalizes RFC 7807 HTTP errors and exposes their message", () => {
    const state = new HisHopeResourceState<string>();
    const failure = new HttpErrorResponse({
      status: 409,
      statusText: "Conflict",
      error: {
        type: "https://his.hope/problems/conflict",
        title: "Conflict",
        detail: "The client already exists.",
        errorCode: "client_already_exists",
        correlationId: "corr-123",
      },
    });

    state.load(throwError(() => failure));

    expect(state.error()).toEqual(jasmine.objectContaining(failure.error));
    expect(state.error()?.status).toBe(409);
    expect(state.errorMessage()).toBe("The client already exists.");
  });

  it("replaces data on reload", () => {
    const state = new HisHopeResourceState<string>();
    state.load(of("old"));

    state.load(of("new"));

    expect(state.data()).toBe("new");
    expect(state.loading()).toBeFalse();
  });

  it("cancels an in-flight source when destroyed", () => {
    const state = new HisHopeResourceState<string>();
    let unsubscribed = false;
    const source = new (class extends Observable<string> {
      constructor() {
        super(() => () => {
          unsubscribed = true;
        });
      }
    })();

    state.load(source);
    state.destroy();

    expect(unsubscribed).toBeTrue();
  });

  it("cancels an in-flight source from the Angular destroy hook", () => {
    let unsubscribed = false;
    const destroyCallbacks: Array<() => void> = [];
    const destroyRef = {
      destroyed: false,
      onDestroy: (callback: () => void) => {
        destroyCallbacks.push(callback);
      },
    } as DestroyRef;
    const source = new (class extends Observable<string> {
      constructor() {
        super(() => () => {
          unsubscribed = true;
        });
      }
    })();
    const state = new HisHopeResourceState<string>(destroyRef);

    state.load(source);
    destroyCallbacks[0]();

    expect(unsubscribed).toBeTrue();
  });
});
