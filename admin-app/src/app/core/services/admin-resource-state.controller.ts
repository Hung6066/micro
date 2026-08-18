import { DestroyRef } from "@angular/core";
import { Observable } from "rxjs";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { HisHopeResourceState } from "@his-hope/frontend-foundation/query";

export interface AdminResourceStateOptions {
  readonly destroyRef: DestroyRef;
  readonly i18n?: HisHopeI18nService;
  readonly loadErrorMessageKey: string;
  readonly loadErrorFallback: string;
}

/** Shared lifecycle for aggregate-backed admin pages that are not paginated tables. */
export class AdminResourceStateController<T> {
  private readonly i18n?: HisHopeI18nService;
  private readonly loadErrorMessageKey: string;
  private readonly loadErrorFallback: string;
  private actionError = "";
  readonly resource: HisHopeResourceState<T>;

  constructor(options: AdminResourceStateOptions) {
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
