import { ChangeDetectionStrategy, Component, inject } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { HisHopeMobileForbiddenPageComponent } from "@his-hope/frontend-foundation/ui";

@Component({
  standalone: true,
  imports: [HisHopeMobileForbiddenPageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-mobile-forbidden-page [resource]="resource" (back)="goBack()" />
  `,
})
export class MobileForbiddenPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly resource =
    this.route.snapshot.queryParamMap.get("resource") ?? "";

  goBack(): void {
    void this.router.navigateByUrl("/admin/dashboard");
  }
}
