import { Component, OnInit, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { Router, RouterModule } from "@angular/router";
import { MatSidenavModule } from "@angular/material/sidenav";
import { MatToolbarModule } from "@angular/material/toolbar";
import { MatListModule } from "@angular/material/list";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { BehaviorSubject, Observable, filter, switchMap } from "rxjs";
import { AuthService } from "./core/services/auth.service";
import { TenantContextService } from "./core/services/tenant-context.service";
import { TenantSwitcherComponent } from "./core/components/tenant-switcher.component";
import {
  HisHopeBrandComponent,
  HisHopeCommandPaletteComponent,
  HisHopeOfflineBannerComponent,
  HisHopeToastComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeLanguageSwitcherComponent,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeThemeService } from "@his-hope/frontend-foundation";
import { environment } from "../environments/environment";

interface PortalNavItem {
  id: string;
  route: string;
  icon: string;
  labelKey: string;
  fallback: string;
}

@Component({
  selector: "app-root",
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    HisHopeBrandComponent,
    HisHopeCommandPaletteComponent,
    HisHopeOfflineBannerComponent,
    HisHopeLanguageSwitcherComponent,
    HisHopeToastComponent,
    HisHopeTranslatePipe,
    TenantSwitcherComponent,
  ],
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.scss"],
})
export class AppComponent implements OnInit {
  readonly authService = inject(AuthService);
  readonly tenantContext = inject(TenantContextService);
  readonly shellTitle = environment.shellTitle;
  readonly isAuthenticated$: Observable<boolean>;
  readonly isMobile$: Observable<boolean>;
  readonly sidenavOpened$ = new BehaviorSubject<boolean>(true);
  readonly tenantOptions$ = this.tenantContext.tenantOptions$;
  paletteOpen = false;
  userMenuOpen = false;
  activeTenantKey: string | null = this.tenantContext.getActiveTenantKey();

  readonly navItems: readonly PortalNavItem[] = [
    {
      id: "dashboard",
      route: "/dashboard",
      icon: "dashboard",
      labelKey: "customerPortal.dashboard",
      fallback: "Dashboard",
    },
    {
      id: "users",
      route: "/users",
      icon: "people",
      labelKey: "customerPortal.users",
      fallback: "Users",
    },
    {
      id: "orders",
      route: "/orders",
      icon: "receipt_long",
      labelKey: "customerPortal.orders",
      fallback: "Orders",
    },
  ];

  private readonly i18n = inject(HisHopeI18nService);
  private readonly router = inject(Router);
  private readonly themeService = inject(HisHopeThemeService);
  private readonly isMobileSubject = new BehaviorSubject<boolean>(
    window.innerWidth <= 768,
  );

  get commands() {
    this.i18n.locale();
    return this.navItems.map((item) => ({
      id: item.id,
      label: this.i18n.t(item.labelKey, item.fallback),
      keywords: item.id === "dashboard" ? ["overview"] : ["people", "accounts"],
    }));
  }

  constructor() {
    this.isAuthenticated$ = this.authService.isAuthenticated$;
    this.isMobile$ = this.isMobileSubject.asObservable();

    const mediaQuery = window.matchMedia("(max-width: 768px)");
    const handleChange = (e: MediaQueryListEvent | MediaQueryList): void => {
      const mobile = e.matches;
      this.isMobileSubject.next(mobile);
      this.sidenavOpened$.next(!mobile);
    };
    handleChange(mediaQuery);
    mediaQuery.addEventListener(
      "change",
      handleChange as (e: MediaQueryListEvent) => void,
    );
  }

  ngOnInit(): void {
    this.authService.isAuthenticated$
      .pipe(
        filter((isAuth) => isAuth),
        switchMap(() => this.tenantContext.initialize()),
      )
      .subscribe();

    this.tenantContext.activeTenantKey$.subscribe((tenantKey) => {
      this.activeTenantKey = tenantKey;
    });
  }

  toggleSidenav(): void {
    this.sidenavOpened$.next(!this.sidenavOpened$.value);
  }

  onLogout(): void {
    this.userMenuOpen = false;
    this.authService.logout();
  }

  onLogin(): void {
    this.userMenuOpen = false;
    this.authService.login();
  }

  toggleUserMenu(): void {
    this.userMenuOpen = !this.userMenuOpen;
  }

  toggleTheme(): void {
    this.themeService.setTheme(
      this.themeService.resolvedTheme() === "dark" ? "light" : "dark",
    );
  }

  onCommand(id: string): void {
    const item = this.navItems.find((candidate) => candidate.id === id);
    void this.router.navigateByUrl(item?.route ?? "/dashboard");
  }

  onTenantChange(tenantKey: string): void {
    this.tenantContext.setActiveTenant(tenantKey);
  }
}
