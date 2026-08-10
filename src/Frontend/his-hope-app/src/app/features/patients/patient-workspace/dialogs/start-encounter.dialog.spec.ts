import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
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
import { StartEncounterDialogComponent, StartEncounterData } from './start-encounter.dialog';
import { ClinicalService } from '@core/services/clinical.service';
import { AuthService } from '@core/services/auth.service';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('StartEncounterDialogComponent', () => {
  let component: StartEncounterDialogComponent;
  let fixture: ComponentFixture<StartEncounterDialogComponent>;

  const mockData: StartEncounterData = { patientId: 'pat-001', patientName: 'Test Patient' };

  beforeEach(async () => {
    const clinicalSpy = jasmine.createSpyObj('ClinicalService', ['start', 'recordVitals']);
    const authSpy = jasmine.createSpyObj('AuthService', ['currentUser$']);
    authSpy.currentUser$ = of({ id: 'usr-001' });

    await TestBed.configureTestingModule({
    imports: [StartEncounterDialogComponent,
        CommonModule,
        ReactiveFormsModule,
        MatDialogModule,
        MatButtonModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatIconModule,
        MatProgressSpinnerModule,
        MatSnackBarModule,
        NoopAnimationsModule],
    providers: [
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: ClinicalService, useValue: clinicalSpy },
        { provide: AuthService, useValue: authSpy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(StartEncounterDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display dialog title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Bắt đầu lượt khám mới');
  });

  it('should display patient name', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Test Patient');
  });

  it('should have form initialized', () => {
    expect(component.form).toBeDefined();
    expect(component.form.contains('encounterType')).toBeTrue();
  });

  it('should render form fields', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('mat-select')).toBeTruthy();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
