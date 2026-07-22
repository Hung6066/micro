import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
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
  ],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  private readonly STORAGE_KEY = 'hishop-dark-mode';

  readonly isAuthenticated$: Observable<boolean | null>;

  readonly isMobile$: Observable<boolean>;
  readonly isDarkMode$: Observable<boolean>;
  readonly sidenavOpened$ = new BehaviorSubject<boolean>(true);

  private readonly isMobileSubject = new BehaviorSubject<boolean>(window.innerWidth <= 768);
  private readonly isDarkModeSubject = new BehaviorSubject<boolean>(this.getInitialTheme());

  private readonly alertToast = inject(AlertToastService);

  constructor(private readonly authService: AuthService) {
    this.isAuthenticated$ = this.authService.isAuthenticated$;
    this.isMobile$ = this.isMobileSubject.asObservable();
    this.isDarkMode$ = this.isDarkModeSubject.asObservable();

    // Listen for viewport width changes
    const mediaQuery = window.matchMedia('(max-width: 768px)');
    const handleChange = (e: MediaQueryListEvent | MediaQueryList): void => {
      const mobile = e.matches;
      this.isMobileSubject.next(mobile);
      this.sidenavOpened$.next(!mobile);
    };
    handleChange(mediaQuery);
    mediaQuery.addEventListener('change', handleChange as (e: MediaQueryListEvent) => void);

    // Apply initial theme
    this.applyTheme(this.isDarkModeSubject.value);
  }

  private getInitialTheme(): boolean {
    const stored = localStorage.getItem(this.STORAGE_KEY);
    if (stored !== null) return stored === 'true';
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
  }

  toggleTheme(): void {
    const next = !this.isDarkModeSubject.value;
    this.isDarkModeSubject.next(next);
    this.applyTheme(next);
    localStorage.setItem(this.STORAGE_KEY, String(next));
  }

  private applyTheme(dark: boolean): void {
    document.body.setAttribute('data-theme', dark ? 'dark' : 'light');
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
}
