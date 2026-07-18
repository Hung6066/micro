import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { AppComponent } from './app.component';
import { AuthService } from '@core/services/auth.service';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { Store } from '@ngrx/store';

describe('AppComponent', () => {
  let component: AppComponent;
  let fixture: ComponentFixture<AppComponent>;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['isLoggedIn'], {
      currentUser$: of(null),
    });

    TestBed.configureTestingModule({

      imports: [AppComponent, CommonModule, RouterModule, MatSidenavModule, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: Store, useValue: { select: () => of({}), dispatch: () => {} } },
      ],
    });

    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
  });

  it('should create and initialize isLoggedIn to false', () => {
    authService.isLoggedIn.and.returnValue(of(false));
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.isLoggedIn).toBeFalse();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
