import { Component, OnInit, inject, signal } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { HisHopeStateComponent } from "@his-hope/frontend-foundation/ui";
import { catchError, of, take } from "rxjs";
import { MobileAuthService } from "./core/auth.service";
import { NativeCapabilityService } from "./core/native-capability.service";

@Component({
  standalone: true,
  imports: [HisHopeStateComponent],
  template: `
    @if (status() === "loading") {
      <hh-state kind="loading" message="Completing secure sign-in..." />
    } @else if (status() === "success") {
      <section class="callback-success" role="status" aria-live="polite">
        <span aria-hidden="true">&#10003;</span>
        <p>Signed in successfully. Opening operations...</p>
      </section>
    } @else {
      <hh-state
        kind="error"
        message="Sign-in could not be completed. Please try again."
      />
    }
  `,
})
export class MobileCallbackComponent implements OnInit {
  private readonly auth = inject(MobileAuthService);
  private readonly native = inject(NativeCapabilityService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly status = signal<"loading" | "success" | "error">("loading");

  ngOnInit(): void {
    // Native deep-link delivery and Angular navigation are separate async
    // operations. If the in-memory handoff was consumed during a cold start,
    // reconstruct the registered custom-scheme callback from route params.
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(
      this.route.snapshot.queryParams,
    )) {
      query.set(key, value);
    }
    const callbackUrl =
      this.native.isNative && query.toString()
        ? `hishope://auth/callback?${query.toString()}`
        : undefined;

    this.auth
      .completeCallback(callbackUrl)
      .pipe(
        take(1),
        catchError(() => of(false)),
      )
      .subscribe((isAuthenticated) => {
        if (!isAuthenticated) {
          this.status.set("error");
          return;
        }
        this.status.set("success");
        setTimeout(
          () =>
            void this.router.navigateByUrl("/operations", {
              replaceUrl: true,
            }),
          450,
        );
      });
  }
}
