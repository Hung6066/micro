// @ts-nocheck
import { Component, DebugElement } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { HasPermissionDirective } from './has-permission.directive';
import { AuthService } from '@core/services/auth.service';
import { Subject } from 'rxjs';

@Component({
  template: `
    <div *hasPermission="'patients.view'" id="perm-single">Has Permission</div>
    <div *hasPermission="['patients.view', 'patients.write']" id="perm-multi">Has Multiple</div>
    <div *hasPermission="'nonexistent'" id="perm-none">No Permission</div>
  `,
})
class TestHostComponent {}

describe('HasPermissionDirective', () => {
  let fixture: ComponentFixture<TestHostComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let currentUserSubject: Subject<any>;

  beforeEach(() => {
    currentUserSubject = new Subject<any>();
    const authSpy = jasmine.createSpyObj('AuthService', ['hasPermission'], {
      currentUser$: currentUserSubject.asObservable(),
    });

    TestBed.configureTestingModule({
      declarations: [TestHostComponent],
      imports: [HasPermissionDirective],
      providers: [{ provide: AuthService, useValue: authSpy }],
    });

    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    fixture = TestBed.createComponent(TestHostComponent);
  });

  it('should show element when user has permission', () => {
    authService.hasPermission.and.returnValue(true);
    fixture.detectChanges();

    const el = fixture.debugElement.query(By.css('#perm-single'));
    expect(el).toBeTruthy();
    expect(authService.hasPermission).toHaveBeenCalledWith(['patients.view']);
  });

  it('should hide element when user lacks permission', () => {
    authService.hasPermission.and.returnValue(false);
    fixture.detectChanges();

    const el = fixture.debugElement.query(By.css('#perm-single'));
    expect(el).toBeNull();
  });

  it('should check multiple permissions with AND logic', () => {
    authService.hasPermission.and.callFake((perms: string[]) =>
      perms.every((p) => ['patients.view', 'patients.write'].includes(p)),
    );
    fixture.detectChanges();

    const el = fixture.debugElement.query(By.css('#perm-multi'));
    expect(el).toBeTruthy();
  });
});
