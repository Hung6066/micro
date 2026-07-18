import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { LabOrderListComponent } from './lab-order-list.component';
import { LabService } from '@core/services/lab.service';
import { createMockLabOrder, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('LabOrderListComponent', () => {
  let component: LabOrderListComponent;
  let fixture: ComponentFixture<LabOrderListComponent>;
  let labService: jasmine.SpyObj<LabService>;

  const mockOrders = [createMockLabOrder(), createMockLabOrder()];

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('LabService', ['searchLabOrders']);
    spy.searchLabOrders.and.returnValue(of(createMockPagedResult(mockOrders, 2)));

    await TestBed.configureTestingModule({
    
    imports: [
        LabOrderListComponent, RouterTestingModule, NoopAnimationsModule,
        MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
        MatSelectModule, MatIconModule, MatButtonModule, MatProgressBarModule,
        ReactiveFormsModule, CommonModule],
    providers: [
        { provide: LabService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(LabOrderListComponent);
    component = fixture.componentInstance;
    labService = TestBed.inject(LabService) as jasmine.SpyObj<LabService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load lab orders on init', () => {
    expect(labService.searchLabOrders).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Phiếu xét nghiệm');
  });

  it('should show create button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const btn = compiled.querySelector('button[routerLink="/lab/new"]');
    expect(btn).toBeTruthy();
  });

  it('should display lab order rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('mat-row');
    expect(rows.length).toBe(2);
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
