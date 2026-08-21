import { Component, inject, signal } from "@angular/core";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { MobilePlatformService } from "../core/mobile-platform.service";
import { Capacitor } from "@capacitor/core";

@Component({
  selector: "app-mobile-pin",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  template: `
    @if (native) {
      <section class="pin-card" aria-labelledby="pin-title">
        <h2 id="pin-title">{{ "mobile.pinTitle" | hhTranslate }}</h2>
        <p>{{ "mobile.pinDescription" | hhTranslate }}</p>
        <label
          >{{ "mobile.pinPlaceholder" | hhTranslate
          }}<input
            inputmode="numeric"
            autocomplete="new-password"
            maxlength="12"
            [value]="draft()"
            (input)="draft.set($any($event.target).value)"
        /></label>
        <button type="button" [disabled]="draft().length < 6" (click)="save()">
          {{
            configured()
              ? ("mobile.pinUpdate" | hhTranslate)
              : ("mobile.pinSave" | hhTranslate)
          }}
        </button>
        @if (message()) {
          <small role="status">{{ message() }}</small>
        }
      </section>
    }
  `,
  styles: [
    `
      :host {
        display: block;
        min-width: 0;
      }
      .pin-card {
        display: grid;
        gap: var(--space-inset);
        width: 100%;
        box-sizing: border-box;
        min-width: 0;
        padding: var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-feature);
        background: var(--surface-white);
      }
      .pin-card h2,
      .pin-card p {
        margin: 0;
      }
      .pin-card h2 {
        font-size: var(--font-size-icon-sm);
        line-height: 1.25;
      }
      .pin-card p,
      .pin-card small {
        color: var(--text-secondary);
        overflow-wrap: anywhere;
      }
      .pin-card label {
        display: grid;
        gap: var(--space-xs);
        font-size: var(--font-size-label);
      }
      .pin-card input {
        display: block;
        width: 100%;
        box-sizing: border-box;
        min-height: var(--touch-target);
        padding: 0 var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-chip);
        font: inherit;
      }
      .pin-card button {
        width: 100%;
        min-height: var(--touch-target);
        border: 0;
        border-radius: var(--radius-chip);
        background: var(--color-primary);
        color: white;
        font: inherit;
        font-weight: 600;
      }
    `,
  ],
})
export class MobilePinComponent {
  private readonly platform = inject(MobilePlatformService);
  private readonly i18n = inject(HisHopeI18nService);
  readonly native = Capacitor.isNativePlatform();
  readonly draft = signal("");
  readonly configured = signal(false);
  readonly message = signal("");
  constructor() {
    void this.load();
  }
  private async load(): Promise<void> {
    this.configured.set(await this.platform.isPinConfigured());
  }
  async save(): Promise<void> {
    try {
      await this.platform.setAppPin(this.draft());
      this.configured.set(true);
      this.message.set(
        this.i18n.t("mobile.pinSaved", "PIN saved on this device."),
      );
      this.draft.set("");
    } catch {
      this.message.set(
        this.i18n.t("mobile.pinSaveFailed", "PIN could not be saved."),
      );
    }
  }
}
