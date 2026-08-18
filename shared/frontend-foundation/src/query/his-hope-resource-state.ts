import { HttpErrorResponse } from "@angular/common/http";
import { DestroyRef, signal } from "@angular/core";
import { Observable, Subscription } from "rxjs";
import { HisHopeProblemDetails } from "@his-hope/frontend-foundation/contracts";

export type HisHopeResourceError = HisHopeProblemDetails;

/** Shared lifecycle for one async resource: loading, data and error. */
export class HisHopeResourceState<T> {
  readonly loading = signal(false);
  readonly data = signal<T | null>(null);
  readonly error = signal<HisHopeResourceError | null>(null);

  private subscription?: Subscription;

  constructor(private readonly destroyRef?: DestroyRef) {
    this.destroyRef?.onDestroy(() => this.destroy());
  }

  load(source: Observable<T>): void {
    this.subscription?.unsubscribe();
    this.subscription = undefined;
    this.loading.set(true);
    this.error.set(null);

    this.subscription = source.subscribe({
      next: (value) => this.data.set(value),
      error: (error: unknown) => {
        this.error.set(toProblemDetails(error));
        this.loading.set(false);
      },
      complete: () => this.loading.set(false),
    });
  }

  reset(): void {
    this.destroy();
    this.loading.set(false);
    this.data.set(null);
    this.error.set(null);
  }

  /** Release an in-flight request when the owning component is destroyed. */
  destroy(): void {
    this.subscription?.unsubscribe();
    this.subscription = undefined;
  }

  errorMessage(fallback = "Request failed"): string {
    const problem = this.error();
    return problem?.detail ?? problem?.title ?? fallback;
  }
}

function toProblemDetails(error: unknown): HisHopeResourceError {
  if (error instanceof HttpErrorResponse) {
    const body = error.error;
    if (body && typeof body === "object") {
      return {
        ...(body as HisHopeProblemDetails),
        status: (body as HisHopeProblemDetails).status ?? error.status,
        title: (body as HisHopeProblemDetails).title ?? error.statusText,
        detail: (body as HisHopeProblemDetails).detail ?? error.message,
      };
    }
    return {
      status: error.status,
      title: error.statusText,
      detail: String(body ?? error.message),
    };
  }

  if (error instanceof Error) {
    return { title: error.name, detail: error.message };
  }

  return { detail: typeof error === "string" ? error : "Request failed" };
}
