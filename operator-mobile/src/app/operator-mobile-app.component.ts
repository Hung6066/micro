import { Component, inject } from "@angular/core";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { OperatorMobileTenantContextService } from "./core/operator-mobile-tenant-context.service";

@Component({
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: "./operator-mobile-app.component.html",
  styleUrls: ["./operator-mobile-app.component.scss"],
})
export class OperatorMobileAppComponent {
  readonly tenant = inject(OperatorMobileTenantContextService);

  async selectTenant(event: Event): Promise<void> {
    await this.tenant.setActiveTenant((event.target as HTMLSelectElement).value);
  }
}
