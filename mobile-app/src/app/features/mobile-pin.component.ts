import { Component, inject, signal } from "@angular/core";
import { MobilePlatformService } from "../core/mobile-platform.service";
import { Capacitor } from "@capacitor/core";

@Component({
  selector: "app-mobile-pin",
  standalone: true,
  template: `
    @if (native) {
      <section class="pin-card" aria-labelledby="pin-title">
        <h2 id="pin-title">App PIN</h2>
        <p>Use a local PIN as an additional lock for this device.</p>
        <label>PIN<input inputmode="numeric" autocomplete="new-password" maxlength="12" [value]="draft()" (input)="draft.set($any($event.target).value)" /></label>
        <button type="button" [disabled]="draft().length < 6" (click)="save()">{{ configured() ? "Update PIN" : "Set PIN" }}</button>
        @if (message()) { <small role="status">{{ message() }}</small> }
      </section>
    }
  `,
  styles: [`
    :host { display:block; min-width:0; }
    .pin-card { display:grid; gap:10px; width:100%; box-sizing:border-box; min-width:0; padding:12px; border:1px solid var(--border-default); border-radius:14px; background:var(--surface-white); }
    .pin-card h2,.pin-card p { margin:0; }
    .pin-card h2 { font-size:18px; line-height:1.25; }
    .pin-card p,.pin-card small { color:var(--text-secondary); overflow-wrap:anywhere; }
    .pin-card label { display:grid; gap:6px; font-size:13px; }
    .pin-card input { display:block; width:100%; box-sizing:border-box; min-height:44px; padding:0 12px; border:1px solid var(--border-default); border-radius:10px; font:inherit; }
    .pin-card button { width:100%; min-height:44px; border:0; border-radius:10px; background:var(--color-primary); color:white; font:inherit; font-weight:600; }
  `],
})
export class MobilePinComponent {
  private readonly platform = inject(MobilePlatformService);
  readonly native = Capacitor.isNativePlatform();
  readonly draft = signal("");
  readonly configured = signal(false);
  readonly message = signal("");
  constructor() { void this.load(); }
  private async load(): Promise<void> { this.configured.set(await this.platform.isPinConfigured()); }
  async save(): Promise<void> {
    try { await this.platform.setAppPin(this.draft()); this.configured.set(true); this.message.set("PIN saved on this device."); this.draft.set(""); }
    catch { this.message.set("PIN could not be saved."); }
  }
}
