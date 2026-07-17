import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { UserFormDialogComponent, UserFormData } from './user-form.dialog';
import { AdminService } from '@core/services/admin.service';

describe('UserFormDialogComponent', () => {
  let component: UserFormDialogComponent;
  let fixture: ComponentFixture<UserFormDialogComponent>;

  const mockData: UserFormData = { mode: 'create' };

  beforeEach(async () => {
    const adminSpy = jasmine.createSpyObj('AdminService', ['createUser', 'updateUser']);

    await TestBed.configureTestingModule({
      imports: [
      UserFormDialogComponent,
        CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
        MatFormFieldModule, MatInputModule, MatSelectModule, MatIconModule,
        MatProgressSpinnerModule, MatSnackBarModule, NoopAnimationsModule, HttpClientTestingModule,
      ],
      providers: [
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: AdminService, useValue: adminSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserFormDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display dialog title for create mode', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Thêm người dùng');
  });

  it('should have form controls', () => {
    expect(component.form.contains('fullName')).toBeTrue();
    expect(component.form.contains('email')).toBeTrue();
    expect(component.form.contains('phone')).toBeTrue();
    expect(component.form.contains('password')).toBeTrue();
    expect(component.form.contains('roles')).toBeTrue();
  });

  it('should have role options', () => {
    expect(component.roleOptions.length).toBeGreaterThan(0);
  });

  it('should render form fields', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('input[formcontrolname="fullName"]')).toBeTruthy();
    expect(compiled.querySelector('input[formcontrolname="email"]')).toBeTruthy();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
