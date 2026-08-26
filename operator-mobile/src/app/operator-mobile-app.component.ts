import { Component } from "@angular/core";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";

@Component({
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: "./operator-mobile-app.component.html",
  styleUrls: ["./operator-mobile-app.component.scss"],
})
export class OperatorMobileAppComponent {}
