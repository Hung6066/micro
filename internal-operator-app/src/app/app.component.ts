import { Component, OnInit, computed, inject, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import { Router, RouterModule } from "@angular/router";
import { BehaviorSubject, Observable, filter, switchMap } from "rxjs";
import { AuthService } from "./core/services/auth.service";
import { TenantContextService } from "./core/services/tenant-context.service";
import { TenantSwitcherComponent } from "./core/components/tenant-switcher.component";
import { WorkspaceSwitcherComponent } from "./core/components/workspace-switcher.component";
import {
  HisHopeBrandComponent,
  HisHopeAppShellComponent,
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
  fragment?: string;
  icon: string;
  labelKey: string;
  fallback: string;
}

interface PortalNavSection {
  id: string;
  labelKey: string;
  fallback: string;
  items: readonly PortalNavItem[];
}

const BASE_NAV: readonly PortalNavItem[] = [
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
  {
    id: "content",
    route: "/content",
    icon: "article",
    labelKey: "operator.content.nav",
    fallback: "Web content",
  },
  {
    id: "rfqs",
    route: "/rfqs",
    icon: "request_quote",
    labelKey: "operator.rfq.nav",
    fallback: "B2B RFQs",
  },
];

const MANUFACTURING_NAV: readonly PortalNavItem[] = [
  {
    id: "inventory",
    route: "/inventory/lots",
    icon: "inventory_2",
    labelKey: "customerPortal.inventory",
    fallback: "Inventory",
  },
  {
    id: "production",
    route: "/production",
    icon: "precision_manufacturing",
    labelKey: "customerPortal.production",
    fallback: "Production",
  },
  {
    id: "procurement",
    route: "/procurement",
    icon: "local_shipping",
    labelKey: "customerPortal.procurement",
    fallback: "Procurement",
  },
  {
    id: "master-data",
    route: "/master-data",
    icon: "dataset",
    labelKey: "customerPortal.masterDataTitle",
    fallback: "Master data",
  },
  {
    id: "traceability",
    route: "/traceability",
    icon: "account_tree",
    labelKey: "customerPortal.traceabilityTitle",
    fallback: "Traceability",
  },
  {
    id: "recipes",
    route: "/recipes",
    icon: "menu_book",
    labelKey: "customerPortal.recipes",
    fallback: "Recipes",
  },
  {
    id: "product-specifications",
    route: "/product-specifications",
    icon: "fact_check",
    labelKey: "customerPortal.specifications",
    fallback: "Product specifications",
  },
  {
    id: "quality-inspections",
    route: "/quality-inspections",
    icon: "fact_check",
    labelKey: "customerPortal.inspections",
    fallback: "Quality inspections",
  },
  {
    id: "deviations",
    route: "/deviations",
    icon: "report_problem",
    labelKey: "customerPortal.deviations",
    fallback: "Deviations",
  },
  {
    id: "capas",
    route: "/capas",
    icon: "rule",
    labelKey: "customerPortal.capas",
    fallback: "CAPA & supplier evaluation",
  },
  {
    id: "forecast",
    route: "/forecast",
    icon: "query_stats",
    labelKey: "customerPortal.forecast",
    fallback: "Forecast & planning",
  },
  {
    id: "sales-allocation",
    route: "/sales-allocation",
    icon: "shopping_cart_checkout",
    labelKey: "customerPortal.salesAllocation",
    fallback: "Sales allocation",
  },
  {
    id: "maintenance",
    route: "/maintenance",
    icon: "build_circle",
    labelKey: "customerPortal.maintenance",
    fallback: "Maintenance",
  },
];

function buildNavItems(workspace: OperatorWorkspace): readonly PortalNavItem[] {
  const hasManufacturing =
    "manufacturingApiUrl" in environment && !!environment.manufacturingApiUrl;
  const byId = (ids: readonly string[]) =>
    [...BASE_NAV, ...MANUFACTURING_NAV].filter((item) => ids.includes(item.id));
  if (workspace === "buyer") {
    return byId(["dashboard", "master-data", "content", "rfqs", "orders", "users"]);
  }
  return hasManufacturing
    ? byId(["dashboard", "inventory", "production", "procurement", "traceability", "recipes", "product-specifications", "quality-inspections", "deviations", "capas", "forecast", "sales-allocation", "maintenance"])
    : byId(["dashboard", "users", "orders", "content", "rfqs"]);
}

function buildNavSections(items: readonly PortalNavItem[], workspace: OperatorWorkspace): readonly PortalNavSection[] {
  const byId = (ids: readonly string[]) => items.filter((item) => ids.includes(item.id));
  if (workspace === "buyer") {
    return [
      { id: "overview", labelKey: "admin.menuOverview", fallback: "Overview", items: byId(["dashboard"]) },
      { id: "catalog", labelKey: "buyer.nav.catalog", fallback: "Catalog & pricing", items: byId(["master-data"]) },
      { id: "content", labelKey: "operator.content.nav", fallback: "Content", items: byId(["content"]) },
      { id: "sales", labelKey: "customerPortal.salesSection", fallback: "Sales", items: byId(["rfqs", "orders"]) },
      { id: "workspace", labelKey: "customerPortal.identityAdministration", fallback: "Workspace", items: byId(["users"]) },
    ].filter((section) => section.items.length > 0);
  }
  return [
    { id: "overview", labelKey: "admin.menuOverview", fallback: "Overview", items: byId(["dashboard"]) },
    { id: "planning", labelKey: "customerPortal.planningSection", fallback: "Planning", items: byId(["forecast"]) },
    { id: "procurement", labelKey: "customerPortal.procurementSection", fallback: "Procurement", items: byId(["procurement"]) },
    { id: "inventory", labelKey: "customerPortal.inventorySection", fallback: "Inventory", items: byId(["inventory", "traceability"]) },
    { id: "production", labelKey: "customerPortal.productionSection", fallback: "Production", items: byId(["production", "recipes", "product-specifications"]) },
    { id: "quality", labelKey: "customerPortal.qualitySection", fallback: "Quality", items: byId(["quality-inspections", "deviations", "capas"]) },
    { id: "assets", labelKey: "customerPortal.assetsSection", fallback: "Assets", items: byId(["maintenance"]) },
    { id: "master-data", labelKey: "customerPortal.masterDataSection", fallback: "Master data", items: byId(["master-data"]) },
    { id: "sales", labelKey: "customerPortal.salesSection", fallback: "Sales", items: byId(["sales-allocation"]) },
    { id: "workspace", labelKey: "customerPortal.identityAdministration", fallback: "Workspace", items: byId(["users", "orders"]) },
  ].filter((section) => section.items.length > 0);
}

type OperatorWorkspace = "buyer" | "manufacturing";

interface WorkspaceOption {
  id: OperatorWorkspace;
  labelKey: string;
  fallback: string;
  icon: string;
}

const WORKSPACE_STORAGE_KEY = "his-hope.operator.workspace";
const WORKSPACE_OPTIONS: readonly WorkspaceOption[] = [
  { id: "buyer", labelKey: "customerPortal.buyerCommerceSection", fallback: "Buyer Commerce", icon: "storefront" },
  { id: "manufacturing", labelKey: "customerPortal.manufacturingSection", fallback: "Manufacturing Operations", icon: "precision_manufacturing" },
];

@Component({
  selector: "app-root",
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    HisHopeAppShellComponent,
    HisHopeBrandComponent,
    HisHopeCommandPaletteComponent,
    HisHopeOfflineBannerComponent,
    HisHopeLanguageSwitcherComponent,
    HisHopeToastComponent,
    HisHopeTranslatePipe,
    TenantSwitcherComponent,
    WorkspaceSwitcherComponent,
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

  readonly workspaceOptions = WORKSPACE_OPTIONS;
  readonly workspace = signal<OperatorWorkspace>(readWorkspace());
  readonly navItems = computed(() => buildNavItems(this.workspace()));
  readonly navSections = computed(() => buildNavSections(this.navItems(), this.workspace()));

  private readonly i18n = inject(HisHopeI18nService);
  private readonly router = inject(Router);
  private readonly themeService = inject(HisHopeThemeService);
  private readonly isMobileSubject = new BehaviorSubject<boolean>(
    window.innerWidth <= 768,
  );

  get localizedShellTitle(): string {
    this.i18n.locale();
    return this.i18n.t(
      "customerPortal.shellTitleManufacturing",
      this.shellTitle,
    );
  }

  get commands() {
    this.i18n.locale();
    return this.navItems().map((item) => ({
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
    const item = this.navItems().find((candidate) => candidate.id === id);
    void this.router.navigateByUrl(item?.route ?? "/dashboard");
  }

  onWorkspaceChange(workspaceId: string): void {
    const selected = this.workspaceOptions.find((candidate) => candidate.id === workspaceId);
    if (!selected) return;
    this.workspace.set(selected.id);
    localStorage.setItem(WORKSPACE_STORAGE_KEY, selected.id);
    if (!this.navItems().some((item) => this.router.url.startsWith(item.route))) {
      void this.router.navigateByUrl("/dashboard");
    }
  }

  onTenantChange(tenantKey: string): void {
    this.tenantContext.setActiveTenant(tenantKey);
  }
}

function readWorkspace(): OperatorWorkspace {
  const stored = localStorage.getItem(WORKSPACE_STORAGE_KEY);
  return stored === "buyer" || stored === "manufacturing" ? stored : "manufacturing";
}
