import { Component, inject } from "@angular/core";
import { RouterOutlet } from "@angular/router";
import { HisHopeThemeService } from "@his-hope/frontend-foundation";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterOutlet],
  template: "<router-outlet />",
})
export class AppComponent {
  private readonly theme = inject(HisHopeThemeService);

  constructor() {
    this.theme.restore();
    this.theme.setPlatform("mobile");
  }
}
