import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  signal,
} from "@angular/core";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({
  selector: "hh-offline-banner",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (!online()) {
    <div class="hh-offline" role="alert">
      {{ "state.offline" | hhTranslate }}
    </div>
  }`,
  styles: [
    `
      .hh-offline {
        position: sticky;
        top: 0;
        z-index: 100;
        padding: 10px 16px;
        background: var(--surface-warning);
        color: var(--text-primary);
        font-size: var(--font-size-body);
        text-align: center;
      }
    `,
  ],
})
export class HisHopeOfflineBannerComponent implements OnDestroy {
  readonly online = signal(
    typeof navigator === "undefined" ? true : navigator.onLine,
  );
  private readonly onOnline = () => this.online.set(true);
  private readonly onOffline = () => this.online.set(false);
  constructor() {
    if (typeof window !== "undefined") {
      window.addEventListener("online", this.onOnline);
      window.addEventListener("offline", this.onOffline);
    }
  }
  ngOnDestroy(): void {
    if (typeof window !== "undefined") {
      window.removeEventListener("online", this.onOnline);
      window.removeEventListener("offline", this.onOffline);
    }
  }
}
