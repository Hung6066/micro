import { Component, inject } from "@angular/core";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { HisHopeThemeService } from "@his-hope/frontend-foundation";
import { HisHopeLanguageSwitcherComponent, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeMobileIconComponent } from "@his-hope/frontend-foundation/ui";
import { OperatorMobileTenantContextService } from "./core/operator-mobile-tenant-context.service";

@Component({
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, HisHopeLanguageSwitcherComponent, HisHopeMobileIconComponent, HisHopeTranslatePipe],
  templateUrl: "./operator-mobile-app.component.html",
  styleUrls: ["./operator-mobile-app.component.scss"],
})
export class OperatorMobileAppComponent {
  readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly theme = inject(HisHopeThemeService);
  menuOpen = false;

  async selectTenant(event: Event): Promise<void> {
    await this.tenant.setActiveTenant((event.target as HTMLSelectElement).value);
  }

  toggleTheme(): void {
    this.theme.setTheme(this.theme.resolvedTheme() === "dark" ? "light" : "dark");
  }
}
