import { Component, inject, signal } from "@angular/core";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HIS_HOPE_APP_PIN, HIS_HOPE_MOBILE_NATIVE } from "../tokens";

/** Native app-PIN setup card for mobile account menus. */
@Component({
  selector: "hh-mobile-pin",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  template: `
    @if (native) {
      <section class="pin-card" aria-labelledby="pin-title">
        <h2 id="pin-title">{{ "mobile.pinTitle" | hhTranslate }}</h2>
        <p>{{ "mobile.pinDescription" | hhTranslate }}</p>
        <label>
          {{ "mobile.pinPlaceholder" | hhTranslate }}
          <input
            inputmode="numeric"
            autocomplete="new-password"
            maxlength="12"
            [value]="draft()"
            (input)="draft.set($any($event.target).value)"
          />
        </label>
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
        gap: var(--space-inset, 0.75rem);
        width: 100%;
        box-sizing: border-box;
        min-width: 0;
        padding: var(--space-md, 0.75rem);
        border: 1px solid var(--border-default, #c7d1cb);
        border-radius: var(--radius-feature, 0.75rem);
        background: var(--surface-white, #fff);
      }
      .pin-card h2,
      .pin-card p {
        margin: 0;
      }
      .pin-card h2 {
        font-size: var(--font-size-icon-sm, 0.875rem);
        line-height: 1.25;
      }
      .pin-card p,
      .pin-card small {
        color: var(--text-secondary, #68756d);
        overflow-wrap: anywhere;
      }
      .pin-card label {
        display: grid;
        gap: var(--space-xs, 0.35rem);
        font-size: var(--font-size-label, 0.8125rem);
      }
      .pin-card input {
        display: block;
        width: 100%;
        box-sizing: border-box;
        min-height: var(--touch-target, 2.75rem);
        padding: 0 var(--space-md, 0.75rem);
        border: 1px solid var(--border-default, #c7d1cb);
        border-radius: var(--radius-chip, 0.5rem);
        font: inherit;
      }
      .pin-card button {
        width: 100%;
        min-height: var(--touch-target, 2.75rem);
        border: 0;
        border-radius: var(--radius-chip, 0.5rem);
        background: var(--color-primary, #006b4f);
        color: white;
        font: inherit;
        font-weight: 600;
      }
    `,
  ],
})
export class HisHopeMobilePinComponent {
  private readonly appPin = inject(HIS_HOPE_APP_PIN);
  private readonly i18n = inject(HisHopeI18nService);
  readonly native = inject(HIS_HOPE_MOBILE_NATIVE);
  readonly draft = signal("");
  readonly configured = signal(false);
  readonly message = signal("");

  constructor() {
    void this.load();
  }

  private async load(): Promise<void> {
    if (!this.native) return;
    this.configured.set(await this.appPin.isConfigured());
  }

  async save(): Promise<void> {
    try {
      await this.appPin.setPin(this.draft());
      this.configured.set(true);
      this.message.set(this.i18n.t("mobile.pinSaved", "PIN saved on this device."));
      this.draft.set("");
    } catch {
      this.message.set(this.i18n.t("mobile.pinSaveFailed", "PIN could not be saved."));
    }
  }
}
