import { Component, inject } from "@angular/core";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeBrandComponent } from "@his-hope/frontend-foundation/ui";
import { MobileAuthService } from "./core/auth.service";

@Component({
  standalone: true,
  imports: [HisHopeBrandComponent, HisHopeTranslatePipe],
  template: `
    <main class="mobile-auth">
      <section class="mobile-auth__card" aria-labelledby="mobile-login-title">
        <hh-brand />
        <p class="mobile-auth__eyebrow">His.Hope Mobile</p>
        <h1 id="mobile-login-title">
          {{ "mobile.clinicalAccess" | hhTranslate }}
        </h1>
        <p>{{ "mobile.signInContinue" | hhTranslate }}</p>
        @if (auth.loginError(); as message) {
          <p class="mobile-auth__error" role="alert">{{ message }}</p>
        }
        <button
          class="hh-button hh-button--primary"
          type="button"
          [disabled]="auth.loginInProgress()"
          (click)="login()"
        >
          {{
            auth.loginInProgress()
              ? ("mobile.connecting" | hhTranslate)
              : ("mobile.signInSecurely" | hhTranslate)
          }}
        </button>
      </section>
    </main>
  `,
  styles: [
    `
      :host {
        display: block;
        min-height: 100dvh;
      }
      .mobile-auth {
        display: grid;
        place-items: center;
        min-height: 100dvh;
        padding: 24px;
        box-sizing: border-box;
        background: var(--bg-warm);
      }
      .mobile-auth__card {
        display: grid;
        gap: 16px;
        width: min(100%, 420px);
        padding: 28px;
        border: 1px solid var(--border-default);
        border-radius: 24px;
        background: var(--surface-white);
        box-shadow: var(--shadow-card);
      }
      .mobile-auth__eyebrow {
        margin: 16px 0 0;
        color: var(--color-primary);
        font-size: var(--font-size-label);
        font-weight: var(--font-weight-semibold);
        letter-spacing: 0.08em;
        text-transform: uppercase;
      }
      h1 {
        margin: 0;
        color: var(--text-primary);
        font-size: clamp(28px, 8vw, 40px);
        line-height: 1.1;
      }
      p {
        margin: 0;
        color: var(--text-secondary);
      }
      .mobile-auth__error {
        padding: 12px;
        border: 1px solid
          color-mix(in srgb, var(--color-danger) 32%, var(--border-default));
        border-radius: var(--radius-input);
        background: color-mix(
          in srgb,
          var(--color-danger) 8%,
          var(--surface-white)
        );
        color: var(--color-danger);
        line-height: 1.45;
      }
      .hh-button {
        min-height: 48px;
      }
    `,
  ],
})
export class MobileLoginComponent {
  readonly auth = inject(MobileAuthService);
  login(): void {
    this.auth.login();
  }
}
