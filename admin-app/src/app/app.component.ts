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
import { HisHopeBrandComponent, HisHopeCommandPaletteComponent, HisHopeLanguageSwitcherComponent, HisHopeOfflineBannerComponent, HisHopePermissionService, HisHopeThemeService, HisHopeToastComponent, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';

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
  readonly commands = [
    { id: 'clients', label: 'Open clients', keywords: ['oidc', 'applications'] },
    { id: 'users', label: 'Open users', keywords: ['accounts'] },
    { id: 'roles', label: 'Open roles', keywords: ['permissions'] },
  ];
  private readonly isMobileSubject = new BehaviorSubject<boolean>(window.innerWidth <= 768);
  private readonly router = inject(Router);
  private readonly themeService = inject(HisHopeThemeService);
  private readonly adminApi = inject(AdminApiService);
  private readonly permissionService = inject(HisHopePermissionService);

  constructor() {
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

  toggleSidenav(): void {
    this.sidenavOpened$.next(!this.sidenavOpened$.value);
  }

  onLogout(): void {
    this.authService.logout();
  }

  onLogin(): void {
    this.authService.login();
  }

  toggleTheme(): void { this.themeService.setTheme(this.themeService.theme() === 'dark' ? 'light' : 'dark'); }
  onCommand(id: string): void { this.router.navigate(['/', id]); }
}
