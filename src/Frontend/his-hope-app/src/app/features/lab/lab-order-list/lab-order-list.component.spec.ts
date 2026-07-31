import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { LabOrderListComponent } from './lab-order-list.component';
import { LabService } from '@core/services/lab.service';
import { LabCriticalAlertStreamService } from '@core/services/lab-critical-alert-stream.service';
import { createMockLabOrder, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HisHopePageQuery } from '@his-hope/frontend-foundation';

describe('LabOrderListComponent', () => {
  let component: LabOrderListComponent;
  let fixture: ComponentFixture<LabOrderListComponent>;
  let labService: jasmine.SpyObj<LabService>;
  let streamService: Partial<LabCriticalAlertStreamService>;

  const mockOrders = [createMockLabOrder(), createMockLabOrder()];

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('LabService', ['searchLabOrders']);
    spy.searchLabOrders.and.returnValue(of(createMockPagedResult(mockOrders, 2)));
    streamService = {
      unreadCount$: of(2),
      latestAlert$: of(null),
      connect: jasmine.createSpy('connect').and.returnValue(Promise.resolve()),
      disconnect: jasmine.createSpy('disconnect').and.returnValue(Promise.resolve()),
    } as any;

    await TestBed.configureTestingModule({
    imports: [
        LabOrderListComponent, RouterTestingModule, NoopAnimationsModule],
    providers: [
        { provide: LabService, useValue: spy },
        { provide: LabCriticalAlertStreamService, useValue: streamService },
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

  it('should render the critical alert badge in the header', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('2 cảnh báo mới');
  });

  it('should display lab order rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('.hh-data-table tbody tr');
    expect(rows.length).toBe(2);
  });

  it('should search lab orders when the query changes', () => {
    const query: HisHopePageQuery = { page: 1, pageSize: 20, search: 'lab' };
    component.onQueryChange(query);
    expect(labService.searchLabOrders).toHaveBeenCalledWith(jasmine.objectContaining({ searchTerm: 'lab' }));
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
