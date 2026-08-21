import { DestroyRef } from "@angular/core";
import { Observable } from "rxjs";
import { HisHopeTranslateFn } from "@his-hope/frontend-foundation/contracts";
import { HisHopeResourceState } from "./his-hope-resource-state";

export interface HisHopeResourceStateOptions {
  readonly destroyRef: DestroyRef;
  readonly i18n?: HisHopeTranslateFn;
  readonly loadErrorMessageKey: string;
  readonly loadErrorFallback: string;
}

/** Shared lifecycle for aggregate-backed pages that are not paginated tables. */
export class HisHopeResourceStateController<T> {
  private readonly i18n?: HisHopeTranslateFn;
  private readonly loadErrorMessageKey: string;
  private readonly loadErrorFallback: string;
  private actionError = "";
  readonly resource: HisHopeResourceState<T>;

  constructor(options: HisHopeResourceStateOptions) {
    this.i18n = options.i18n;
    this.loadErrorMessageKey = options.loadErrorMessageKey;
    this.loadErrorFallback = options.loadErrorFallback;
    this.resource = new HisHopeResourceState<T>(options.destroyRef);
  }

  get loading(): boolean {
    return this.resource.loading();
  }

  get error(): string {
    return (
      this.actionError ||
      (this.resource.error()
        ? (this.i18n?.t(this.loadErrorMessageKey, this.loadErrorFallback) ??
          this.resource.errorMessage(this.loadErrorFallback))
        : "")
    );
  }

  load(source: Observable<T>): void {
    this.actionError = "";
    this.resource.load(source);
  }

  setActionError(message: string): void {
    this.actionError = message;
  }
}
