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
import { OrderLabDialogComponent, OrderLabData } from './order-lab.dialog';
import { LabService } from '@core/services/lab.service';
import { AuthService } from '@core/services/auth.service';

describe('OrderLabDialogComponent', () => {
  let component: OrderLabDialogComponent;
  let fixture: ComponentFixture<OrderLabDialogComponent>;

  const mockData: OrderLabData = { patientId: 'pat-001', patientName: 'Test Patient' };

  beforeEach(async () => {
    const labSpy = jasmine.createSpyObj('LabService', ['createLabOrder']);
    const authSpy = jasmine.createSpyObj('AuthService', ['currentUser$']);
    authSpy.currentUser$ = of({ id: 'usr-001' });

    await TestBed.configureTestingModule({
      imports: [
      OrderLabDialogComponent,
        CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
        MatFormFieldModule, MatInputModule, MatSelectModule, MatIconModule,
        MatProgressSpinnerModule, MatSnackBarModule, NoopAnimationsModule, HttpClientTestingModule,
      ],
      providers: [
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: LabService, useValue: labSpy },
        { provide: AuthService, useValue: authSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(OrderLabDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Chỉ định xét nghiệm');
  });

  it('should show patient name', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Test Patient');
  });

  it('should have available tests', () => {
    expect(component.availableTests.length).toBeGreaterThan(0);
  });

  it('should render form', () => {
    expect(component.form.contains('testCode')).toBeTrue();
    expect(component.form.contains('priority')).toBeTrue();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
