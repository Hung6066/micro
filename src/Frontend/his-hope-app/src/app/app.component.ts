import { Component } from '@angular/core';

@Component({
  selector: 'app-root',
  template: `
    <app-sidebar *ngIf="isLoggedIn"></app-sidebar>
    <div class="main-content" [class.with-sidebar]="isLoggedIn">
      <router-outlet></router-outlet>
    </div>
  `,
  styles: [`
    .main-content { min-height: 100vh; }
    .main-content.with-sidebar { margin-left: 260px; padding: 24px; }
  `],
})
export class AppComponent {
  get isLoggedIn(): boolean {
    return !!localStorage.getItem('access_token');
  }
}
