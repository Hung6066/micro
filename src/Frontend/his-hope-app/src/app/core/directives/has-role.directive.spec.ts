import { Component, DebugElement } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { HasRoleDirective } from './has-role.directive';
import { AuthService } from '@core/services/auth.service';
import { Subject } from 'rxjs';

@Component({
    template: `
    <div *hasRole="'admin'" id="role-single">Has Role</div>
    <div *hasRole="['admin', 'doctor']" id="role-multi">Has Multiple</div>
    <div *hasRole="'nonexistent'" id="role-none">No Role</div>
  `,
    standalone: false
})
class TestHostComponent {}

describe('HasRoleDirective', () => {
  let fixture: ComponentFixture<TestHostComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let currentUserSubject: Subject<any>;

  beforeEach(() => {
    currentUserSubject = new Subject<any>();
    const authSpy = jasmine.createSpyObj('AuthService', ['hasRole'], {
      currentUser$: currentUserSubject.asObservable(),
    });

    TestBed.configureTestingModule({
      declarations: [TestHostComponent],
      imports: [HasRoleDirective],
      providers: [{ provide: AuthService, useValue: authSpy }],
    });

    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    fixture = TestBed.createComponent(TestHostComponent);
  });

  it('should show element when user has role', () => {
    authService.hasRole.and.returnValue(true);
    fixture.detectChanges();

    const el = fixture.debugElement.query(By.css('#role-single'));
    expect(el).toBeTruthy();
    expect(authService.hasRole).toHaveBeenCalledWith(['admin']);
  });

  it('should hide element when user lacks role', () => {
    authService.hasRole.and.returnValue(false);
    fixture.detectChanges();

    const el = fixture.debugElement.query(By.css('#role-single'));
    expect(el).toBeNull();
  });

  it('should check multiple roles with OR logic', () => {
    authService.hasRole.and.callFake((roles: string[]) =>
      roles.some((r) => ['admin', 'doctor'].includes(r)),
    );
    fixture.detectChanges();

    const el = fixture.debugElement.query(By.css('#role-multi'));
    expect(el).toBeTruthy();
  });
});
