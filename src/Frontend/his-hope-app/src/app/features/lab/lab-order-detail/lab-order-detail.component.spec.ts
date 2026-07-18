import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { LabOrderDetailComponent } from './lab-order-detail.component';
import { LabService } from '@core/services/lab.service';
import { createMockLabOrder } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('LabOrderDetailComponent', () => {
  let component: LabOrderDetailComponent;
  let fixture: ComponentFixture<LabOrderDetailComponent>;
  let labService: jasmine.SpyObj<LabService>;

  const mockLabOrder = createMockLabOrder();

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('LabService', ['getLabOrder', 'submitLabOrder', 'collectSpecimen', 'cancelLabOrder', 'recordResult']);
    spy.getLabOrder.and.returnValue(of(mockLabOrder));

    await TestBed.configureTestingModule({
    declarations: [LabOrderDetailComponent],
    imports: [RouterTestingModule, NoopAnimationsModule,
        ReactiveFormsModule, MatCardModule, MatTableModule, MatButtonModule,
        MatIconModule, MatSnackBarModule, MatProgressSpinnerModule,
        MatFormFieldModule, MatInputModule, MatSelectModule, CommonModule],
    providers: [
        { provide: LabService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(LabOrderDetailComponent);
    component = fixture.componentInstance;
    labService = TestBed.inject(LabService) as jasmine.SpyObj<LabService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load lab order on init', () => {
    expect(labService.getLabOrder).toHaveBeenCalled();
  });

  it('should display lab order info', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Phiếu xét nghiệm');
  });

  it('should render tests table', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const tables = compiled.querySelectorAll('mat-table');
    expect(tables.length).toBeGreaterThanOrEqual(1);
  });

  it('should have result form initialized', () => {
    expect(component.resultForm.contains('value')).toBeTrue();
    expect(component.resultForm.contains('unit')).toBeTrue();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
