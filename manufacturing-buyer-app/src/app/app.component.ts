import { Component, inject, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import { Router, RouterModule, NavigationEnd } from "@angular/router";
import { FormsModule } from "@angular/forms";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { BehaviorSubject, Observable, filter } from "rxjs";
import { AuthService } from "./core/services/auth.service";
import {
  HisHopeOfflineBannerComponent,
  HisHopeToastComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeThemeService } from "@his-hope/frontend-foundation";
import {
  HisHopeLanguageSwitcherComponent,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { NewsletterSignupComponent } from "./core/components/newsletter-signup.component";

interface NavItem {
  route: string;
  label: string;
  authRequired?: boolean;
}

interface CategoryLink {
  id: string;
  label: string;
  icon: string;
  anchor: string;
}

@Component({
  selector: "app-root",
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    HisHopeOfflineBannerComponent,
    HisHopeToastComponent,
    HisHopeLanguageSwitcherComponent,
    HisHopeTranslatePipe,
    NewsletterSignupComponent,
  ],
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.scss"],
})
export class AppComponent {
  readonly authService = inject(AuthService);
  readonly theme = inject(HisHopeThemeService);
  readonly isAuthenticated$: Observable<boolean> = this.authService.isAuthenticated$;
  readonly mobileMenuOpen = signal(false);
  readonly userMenuOpen = signal(false);
  readonly searchQuery = signal("");
  readonly isHome = signal(true);

  readonly categoryLinks: readonly CategoryLink[] = [
    { id: "xoai", label: "buyer.category.xoai", icon: "eco", anchor: "xoai" },
    { id: "thom", label: "buyer.category.thom", icon: "spa", anchor: "thom" },
    { id: "chanh-day", label: "buyer.category.chanh-day", icon: "local_florist", anchor: "chanh-day" },
    { id: "mix", label: "buyer.category.mix", icon: "breakfast_dining", anchor: "mix" },
    { id: "tac", label: "buyer.category.tac", icon: "brightness_5", anchor: "tac" },
    { id: "chom", label: "buyer.category.chom", icon: "forest", anchor: "chom" },
  ];

  readonly navItems: readonly NavItem[] = [
    { route: "/home", label: "buyer.nav.home" },
    { route: "/catalog", label: "buyer.nav.catalog", authRequired: true },
    { route: "/rfq", label: "buyer.nav.rfq", authRequired: true },
    { route: "/orders", label: "buyer.nav.orders", authRequired: true },
    { route: "/profile", label: "buyer.nav.account", authRequired: true },
  ];

  private readonly router = inject(Router);

  constructor() {
    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => {
        this.isHome.set(this.router.url.startsWith("/home") || this.router.url === "/");
        this.mobileMenuOpen.set(false);
      });
  }

  onLogin(returnUrl?: string): void {
    this.userMenuOpen.set(false);
    this.authService.login(returnUrl);
  }

  onLogout(): void {
    this.userMenuOpen.set(false);
    this.authService.logout();
  }

  toggleUserMenu(): void {
    this.userMenuOpen.update((open) => !open);
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update((open) => !open);
  }

  toggleTheme(): void {
    this.theme.setTheme(this.theme.resolvedTheme() === "dark" ? "light" : "dark");
  }

  submitSearch(): void {
    const query = this.searchQuery().trim();
    if (!query) {
      void this.router.navigateByUrl("/catalog");
      return;
    }
    void this.router.navigate(["/catalog"], { queryParams: { q: query } });
  }

  scrollToCategory(anchor: string): void {
    if (this.isHome()) {
      document.getElementById(anchor)?.scrollIntoView({ behavior: "smooth", block: "start" });
      return;
    }
    void this.router.navigate(["/home"], { fragment: anchor });
  }
}
