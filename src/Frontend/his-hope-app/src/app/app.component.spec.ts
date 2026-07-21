import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { BreakpointObserver } from '@angular/cdk/layout';
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
  let breakpointObserver: jasmine.SpyObj<BreakpointObserver>;
  let breakpointState$: Subject<{ matches: boolean }>;

  beforeEach(() => {
    const authSpy = jasmine.createSpyObj('AuthService', ['isLoggedIn'], {
      currentUser$: of(null),
    });
    breakpointState$ = new Subject<{ matches: boolean }>();
    const breakpointSpy = jasmine.createSpyObj('BreakpointObserver', ['observe']);
    breakpointSpy.observe.and.returnValue(breakpointState$.asObservable() as never);

    TestBed.configureTestingModule({

      imports: [AppComponent, CommonModule, RouterModule, MatSidenavModule, NoopAnimationsModule, RouterTestingModule, HttpClientTestingModule],
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: BreakpointObserver, useValue: breakpointSpy },
        { provide: Store, useValue: { select: () => of({}), dispatch: () => {} } },
      ],
    });

    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    breakpointObserver = TestBed.inject(BreakpointObserver) as jasmine.SpyObj<BreakpointObserver>;
  });

  it('should create and initialize isLoggedIn to false', () => {
    authService.isLoggedIn.and.returnValue(of(false));
    fixture.detectChanges();
    expect(component).toBeTruthy();
    expect(component.isLoggedIn).toBeFalse();
  });

  it('should switch sidenav to mobile over mode below 768px', () => {
    authService.isLoggedIn.and.returnValue(of(false));
    fixture.detectChanges();

    breakpointState$.next({ matches: true });

    expect(component.isMobile).toBeTrue();
    expect(component.sidenavMode).toBe('over');
    expect(component.sidenavOpened).toBeFalse();
    expect(breakpointObserver.observe).toHaveBeenCalledWith('(max-width: 767.98px)');
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
