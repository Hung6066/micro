import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { AuthService } from './core/services/auth.service';
import { AdminApiService } from './core/services/admin-api.service';
import { HisHopeBrandComponent, HisHopeCommandPaletteComponent, HisHopeI18nService, HisHopeLanguageSwitcherComponent, HisHopeOfflineBannerComponent, HisHopePermissionService, HisHopeThemeService, HisHopeToastComponent, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';

interface AdminNavItem {
  id: string;
  route: string;
  icon: string;
  labelKey: string;
  fallback: string;
  permission?: string;
}

interface AdminNavSection {
  id: string;
  labelKey: string;
  fallback: string;
  items: readonly AdminNavItem[];
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    HisHopeBrandComponent,
    HisHopeCommandPaletteComponent,
    HisHopeOfflineBannerComponent,
    HisHopeTranslatePipe,
    HisHopeLanguageSwitcherComponent,
    HisHopeToastComponent,
  ],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
})
export class AppComponent {
  readonly authService = inject(AuthService);
  readonly isAuthenticated$: Observable<boolean>;
  readonly isMobile$: Observable<boolean>;
  readonly sidenavOpened$ = new BehaviorSubject<boolean>(true);
  paletteOpen = false;
  readonly navSections: readonly AdminNavSection[] = [
    {
      id: 'overview', labelKey: 'admin.menuOverview', fallback: 'Overview',
      items: [
        { id: 'dashboard', route: '/dashboard', icon: 'dashboard', labelKey: 'admin.dashboard', fallback: 'Dashboard' },
        { id: 'iam-overview', route: '/iam/overview', icon: 'security', labelKey: 'admin.iamOverview', fallback: 'IAM overview', permission: 'admin.roles.read' },
      ],
    },
    {
      id: 'identities', labelKey: 'admin.menuIdentities', fallback: 'Identities',
      items: [
        { id: 'workforce-users', route: '/iam/users', icon: 'people', labelKey: 'admin.workforceUsers', fallback: 'Workforce users', permission: 'admin.users.read' },
        { id: 'groups', route: '/iam/groups', icon: 'groups', labelKey: 'admin.groups', fallback: 'Groups', permission: 'admin.roles.read' },
        { id: 'roles', route: '/roles', icon: 'badge', labelKey: 'admin.roles', fallback: 'Roles & permission catalog', permission: 'admin.roles.read' },
        { id: 'external-identities', route: '/iam/external-identities', icon: 'public', labelKey: 'admin.externalIdentities', fallback: 'External identities', permission: 'admin.roles.read' },
        { id: 'service-principals', route: '/iam/service-principals', icon: 'smart_toy', labelKey: 'admin.servicePrincipals', fallback: 'Service principals', permission: 'admin.roles.read' },
        { id: 'consents', route: '/consents', icon: 'handshake', labelKey: 'admin.consents', fallback: 'User consents', permission: 'admin.users.read' },
        { id: 'identity-operations', route: '/identity-operations', icon: 'manage_accounts', labelKey: 'admin.identityOperations', fallback: 'Lifecycle & incident operations', permission: 'admin.users.read' },
      ],
    },
    {
      id: 'applications', labelKey: 'admin.menuApplications', fallback: 'Applications',
      items: [
        { id: 'oauth-clients', route: '/iam/clients', icon: 'vpn_key', labelKey: 'admin.oauthClients', fallback: 'OAuth clients', permission: 'admin.clients.read' },
        { id: 'api-audiences', route: '/iam/api-audiences', icon: 'api', labelKey: 'admin.apiAudiences', fallback: 'API audiences', permission: 'admin.roles.read' },
        { id: 'trusted-issuers', route: '/iam/trusted-issuers', icon: 'verified_user', labelKey: 'admin.trustedIssuers', fallback: 'Trusted issuers', permission: 'admin.roles.read' },
      ],
    },
    {
      id: 'authorization', labelKey: 'admin.menuAuthorization', fallback: 'Authorization',
      items: [
        { id: 'access-management', route: '/access-management', icon: 'admin_panel_settings', labelKey: 'admin.accessManagement', fallback: 'Fine-grained access overview', permission: 'admin.roles.read' },
        { id: 'organizations-tenants', route: '/iam/scopes', icon: 'account_tree', labelKey: 'admin.organizationsTenants', fallback: 'Organizations & tenants', permission: 'admin.roles.read' },
        { id: 'service-catalog', route: '/iam/services', icon: 'apps', labelKey: 'admin.services', fallback: 'Service catalog', permission: 'admin.roles.read' },
        { id: 'permission-sets', route: '/iam/permission-sets', icon: 'rule', labelKey: 'admin.permissionSets', fallback: 'Permission sets', permission: 'admin.roles.read' },
        { id: 'policies', route: '/iam/policies', icon: 'policy', labelKey: 'admin.policies', fallback: 'Policies', permission: 'admin.roles.read' },
        { id: 'boundaries', route: '/iam/boundaries', icon: 'border_style', labelKey: 'admin.boundaries', fallback: 'Boundaries', permission: 'admin.roles.read' },
        { id: 'resource-policies', route: '/iam/resource-policies', icon: 'description', labelKey: 'admin.resourcePolicies', fallback: 'Resource policies', permission: 'admin.roles.read' },
        { id: 'assignments', route: '/iam/assignments', icon: 'assignment_ind', labelKey: 'admin.assignments', fallback: 'Assignments', permission: 'admin.roles.read' },
        { id: 'workload-roles', route: '/iam/workload-roles', icon: 'smart_toy', labelKey: 'admin.workloadRoles', fallback: 'Workload roles', permission: 'admin.roles.read' },
      ],
    },
    {
      id: 'access-governance', labelKey: 'admin.menuAccessGovernance', fallback: 'Access governance',
      items: [
        { id: 'governance-workspace', route: '/iam/access-requests', icon: 'fact_check', labelKey: 'admin.governanceWorkspace', fallback: 'Requests & reviews', permission: 'admin.roles.read' },
        { id: 'jit-access', route: '/iam/jit-access', icon: 'timer', labelKey: 'admin.jitAccess', fallback: 'JIT access requests', permission: 'admin.roles.read' },
        { id: 'break-glass', route: '/iam/break-glass', icon: 'emergency', labelKey: 'admin.breakGlass', fallback: 'Break-glass access', permission: 'admin.roles.read' },
      ],
    },
    {
      id: 'sessions', labelKey: 'admin.menuSessions', fallback: 'Sessions & credentials',
      items: [
        { id: 'active-sessions', route: '/iam/sessions', icon: 'devices', labelKey: 'admin.activeSessions', fallback: 'Active sessions & revocation', permission: 'admin.users.read' },
        { id: 'workload-sessions', route: '/iam/workload-sessions', icon: 'sync_lock', labelKey: 'admin.workloadSessions', fallback: 'Workload sessions', permission: 'admin.roles.read' },
        { id: 'mobile-operations', route: '/mobile-operations', icon: 'phone_android', labelKey: 'admin.mobileOperations', fallback: 'Mobile devices', permission: 'admin.users.read' },
      ],
    },
    {
      id: 'analyzer', labelKey: 'admin.menuAnalyzer', fallback: 'Analyzer',
      items: [
        { id: 'effective-access', route: '/iam/analyzer/effective-access', icon: 'visibility', labelKey: 'admin.effectiveAccess', fallback: 'Effective access', permission: 'admin.policy.simulate' },
        { id: 'policy-simulator', route: '/iam/analyzer/policy-simulator', icon: 'science', labelKey: 'admin.policySimulator', fallback: 'Policy simulator', permission: 'admin.policy.simulate' },
        { id: 'new-access-diff', route: '/iam/analyzer/access-diff', icon: 'compare_arrows', labelKey: 'admin.newAccessDiff', fallback: 'New-access diff', permission: 'admin.policy.simulate' },
        { id: 'unused-permissions', route: '/iam/analyzer/unused-permissions', icon: 'find_in_page', labelKey: 'admin.unusedPermissions', fallback: 'Unused permissions', permission: 'admin.policy.simulate' },
      ],
    },
    {
      id: 'audit-integrations', labelKey: 'admin.menuAuditIntegrations', fallback: 'Audit & integrations',
      items: [
        { id: 'identity-capabilities', route: '/identity-capabilities', icon: 'admin_panel_settings', labelKey: 'admin.identityCapabilities', fallback: 'Integration, posture & audit controls', permission: 'admin.settings.read' },
        { id: 'database-platform', route: '/database-platform', icon: 'storage', labelKey: 'admin.databasePlatform', fallback: 'Database platform', permission: 'admin.settings.read' },
      ],
    },
  ];
  private readonly i18n: HisHopeI18nService;
  get commands() {
    this.i18n.locale();
    const keywords: Record<string, string[]> = {
      dashboard: ['overview', 'health'], clients: ['oidc', 'oauth', 'applications'], users: ['accounts', 'lifecycle'],
      roles: ['permissions', 'templates', 'rbac'], consents: ['delegated', 'oauth'], 'access-management': ['rbac', 'abac', 'reviews', 'sod'], 'iam-control-plane': ['iam', 'organizations', 'tenants', 'accounts', 'environments', 'groups', 'permission sets', 'boundaries', 'resource policies', 'access requests', 'reviews', 'jit', 'break glass', 'analyzer'],
      'security-providers': ['federation', 'saml', 'scim', 'entra', 'google'], 'identity-capabilities': ['audit', 'posture', 'provisioning', 'ssf'],
      'identity-operations': ['sessions', 'mfa', 'import', 'reconcile'], 'mobile-operations': ['device', 'mobile', 'push'], 'database-platform': ['database', 'continuity'],
    };
    return this.navSections.flatMap(section => this.visibleNavItems(section).map(item => ({
      id: item.id,
      label: this.i18n.t(item.labelKey, item.fallback),
      keywords: keywords[item.id] ?? [],
    })));
  }
  private readonly isMobileSubject = new BehaviorSubject<boolean>(window.innerWidth <= 768);
  private readonly router = inject(Router);
  private readonly themeService = inject(HisHopeThemeService);
  private readonly adminApi = inject(AdminApiService);
  private readonly permissionService = inject(HisHopePermissionService);

  constructor(i18n: HisHopeI18nService) {
    this.i18n = i18n;
    this.isAuthenticated$ = this.authService.isAuthenticated$;
    this.isMobile$ = this.isMobileSubject.asObservable();
    this.isAuthenticated$.pipe(
      switchMap(isAuthenticated => {
        if (!isAuthenticated) {
          this.permissionService.clear();
          return of(null);
        }
        return this.adminApi.getMyPermissions().pipe(catchError(() => of(null)));
      }),
    ).subscribe(result => {
      if (result) this.permissionService.setSnapshot(result);
    });

    const mediaQuery = window.matchMedia('(max-width: 768px)');
    const handleChange = (e: MediaQueryListEvent | MediaQueryList): void => {
      const mobile = e.matches;
      this.isMobileSubject.next(mobile);
      this.sidenavOpened$.next(!mobile);
    };
    handleChange(mediaQuery);
    mediaQuery.addEventListener('change', handleChange as (e: MediaQueryListEvent) => void);
  }

  isNavItemVisible(item: AdminNavItem): boolean {
    // Keep navigation discoverable while the permission snapshot is loading;
    // route guards/API policies remain authoritative and fail closed.
    return !item.permission || !this.permissionService.hasSnapshot() || this.permissionService.has(item.permission);
  }

  visibleNavItems(section: AdminNavSection): readonly AdminNavItem[] {
    return section.items.filter(item => this.isNavItemVisible(item));
  }

  routePath(route: string): string {
    return route.split('?', 1)[0];
  }

  routeQuery(route: string): Record<string, string> | null {
    const query = route.split('?')[1];
    if (!query) return null;
    return Object.fromEntries(new URLSearchParams(query).entries());
  }

  toggleSidenav(): void {
    this.sidenavOpened$.next(!this.sidenavOpened$.value);
  }

  onLogout(): void {
    this.authService.logout();
  }

  onLogin(): void {
    this.authService.login();
  }

  toggleTheme(): void { this.themeService.setTheme(this.themeService.resolvedTheme() === 'dark' ? 'light' : 'dark'); }
  onCommand(id: string): void {
    const item = this.navSections.flatMap(section => section.items).find(candidate => candidate.id === id);
    this.router.navigateByUrl(item?.route ?? '/dashboard');
  }
}
