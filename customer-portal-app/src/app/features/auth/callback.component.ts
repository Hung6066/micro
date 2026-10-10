import { Component, OnInit, inject } from "@angular/core";
import { Router } from "@angular/router";
import { AuthService } from "../../core/services/auth.service";
import { HisHopeStateComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({
  standalone: true,
  imports: [HisHopeStateComponent, HisHopeTranslatePipe],
  template: `
    <hh-state
      kind="loading"
      [message]="'customerPortal.completingSignIn' | hhTranslate"
    />
  `,
})
export class CallbackComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  ngOnInit(): void {
    this.auth.handleCallback().subscribe((ok) => {
      void this.router.navigateByUrl(ok ? "/dashboard" : "/auth/login");
    });
  }
}
