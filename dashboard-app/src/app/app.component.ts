import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Router } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { BehaviorSubject, Observable } from 'rxjs';
import { AuthService } from './core/services/auth.service';
import { AlertPanelComponent } from './shared/alert-panel/alert-panel.component';
import { AlertToastService } from './shared/alert-toast/alert-toast.service';
import { HisHopeBrandComponent, HisHopeCommandPaletteComponent, HisHopeI18nService, HisHopeLanguageSwitcherComponent, HisHopeOfflineBannerComponent, HisHopeThemeService, HisHopeToastComponent, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';

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
    AlertPanelComponent,
    HisHopeBrandComponent,
    HisHopeCommandPaletteComponent,
    HisHopeOfflineBannerComponent,
    HisHopeTranslatePipe,
    HisHopeLanguageSwitcherComponent,
    HisHopeToastComponent,
  ],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  readonly isAuthenticated$: Observable<boolean>;

  readonly isMobile$: Observable<boolean>;
  readonly sidenavOpened$ = new BehaviorSubject<boolean>(true);

  private readonly isMobileSubject = new BehaviorSubject<boolean>(window.innerWidth <= 768);

  private readonly alertToast = inject(AlertToastService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly themeService = inject(HisHopeThemeService);
  private readonly router = inject(Router);
  readonly isDarkMode$ = new BehaviorSubject<boolean>(this.themeService.resolvedTheme() === 'dark');
  paletteOpen = false;

  get commands() { this.i18n.locale(); return [
    { id: 'resources', label: this.i18n.t('app.dashboard.openResources'), keywords: ['services', 'health'] },
    { id: 'logs', label: this.i18n.t('app.dashboard.openLogs'), keywords: ['audit'] },
    { id: 'traces', label: this.i18n.t('app.dashboard.openTraces'), keywords: ['telemetry'] },
  ]; }

  constructor(private readonly authService: AuthService) {
    this.isAuthenticated$ = this.authService.isAuthenticated$;
    this.isMobile$ = this.isMobileSubject.asObservable();
    // Listen for viewport width changes
    const mediaQuery = window.matchMedia('(max-width: 768px)');
    const handleChange = (e: MediaQueryListEvent | MediaQueryList): void => {
      const mobile = e.matches;
      this.isMobileSubject.next(mobile);
      this.sidenavOpened$.next(!mobile);
    };
    handleChange(mediaQuery);
    mediaQuery.addEventListener('change', handleChange as (e: MediaQueryListEvent) => void);

  }

  toggleTheme(): void {
    const next = this.themeService.resolvedTheme() === 'dark' ? 'light' : 'dark';
    this.themeService.setTheme(next);
    this.isDarkMode$.next(next === 'dark');
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

  onCommand(id: string): void { this.router.navigate(['/', id]); }
}
