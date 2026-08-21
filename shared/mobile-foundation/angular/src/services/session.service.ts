import { Injectable, inject, signal } from "@angular/core";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HIS_HOPE_MOBILE_AUTH } from "../tokens";

/** Tracks hard session loss (401) for the session-expired dialog. */
@Injectable({ providedIn: "root" })
export class HisHopeMobileSessionService {
  private readonly auth = inject(HIS_HOPE_MOBILE_AUTH);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly expiredState = signal(false);

  readonly expired = this.expiredState.asReadonly();

  handleUnauthorized(): void {
    if (this.expiredState()) return;
    this.permissions.recordAuthorizationFailure(401);
    this.expiredState.set(true);
  }

  dismiss(): void {
    this.expiredState.set(false);
  }

  reLogin(): void {
    this.expiredState.set(false);
    this.auth.login();
  }
}
