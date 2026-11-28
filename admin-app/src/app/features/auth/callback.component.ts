import { Component, OnInit, inject } from "@angular/core";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { AuthService } from "../../core/services/auth.service";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({
  standalone: true,
  imports: [MatProgressSpinnerModule, HisHopeTranslatePipe],
  template: `<div class="callback-state">
    <mat-spinner diameter="40"></mat-spinner>
    <p>{{ "admin.completingSignIn" | hhTranslate }}</p>
  </div>`,
  styles: [
    `
      .callback-state {
        display: grid;
        place-items: center;
        align-content: center;
        min-height: 100dvh;
        gap: var(--space-md);
        color: var(--text-secondary);
        font-family: var(--font-sans);
        font-size: var(--font-size-body);
      }
    `,
  ],
})
export class CallbackComponent implements OnInit {
  private authService = inject(AuthService);

  ngOnInit(): void {
    this.authService.handleCallback().subscribe();
  }
}
