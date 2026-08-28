import {
  Component,
  inject,
  input,
  OnDestroy,
  OnInit,
  output,
} from "@angular/core";
import { HisHopeMobileSessionExpiredDialogComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeMobileLockService } from "../services/lock.service";
import { HisHopeMobileSessionService } from "../services/session.service";
import { HisHopeMobileLockOverlayComponent } from "./his-hope-mobile-lock-overlay.component";

/**
 * Arms idle/background session lock and renders lock + session-expired overlays.
 * Place once in each authenticated mobile shell template.
 */
@Component({
  selector: "hh-mobile-shell-security",
  standalone: true,
  imports: [HisHopeMobileLockOverlayComponent, HisHopeMobileSessionExpiredDialogComponent],
  template: `
    <hh-mobile-lock-overlay (signOut)="signOut.emit()" />
    <hh-mobile-session-expired-dialog
      [open]="session.expired()"
      (reLogin)="session.reLogin()"
    />
  `,
})
export class HisHopeMobileShellSecurityComponent implements OnInit, OnDestroy {
  readonly idleMs = input<number | undefined>(undefined);
  readonly signOut = output<void>();

  readonly lock = inject(HisHopeMobileLockService);
  readonly session = inject(HisHopeMobileSessionService);

  ngOnInit(): void {
    this.lock.arm(this.idleMs());
  }

  ngOnDestroy(): void {
    this.lock.disarm();
  }
}
