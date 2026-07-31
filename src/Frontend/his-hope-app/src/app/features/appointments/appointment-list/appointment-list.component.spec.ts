import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { AppointmentListComponent } from './appointment-list.component';
import { AppointmentService } from '@core/services/appointment.service';
import { createMockAppointment, createMockPagedResult } from '@testing/mock-data';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HisHopePageQuery } from '@his-hope/frontend-foundation';

describe('AppointmentListComponent', () => {
  let component: AppointmentListComponent;
  let fixture: ComponentFixture<AppointmentListComponent>;
  let appointmentService: jasmine.SpyObj<AppointmentService>;

  const mockAppointments = [createMockAppointment(), createMockAppointment()];

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('AppointmentService', ['list', 'search']);
    spy.list.and.returnValue(of(createMockPagedResult(mockAppointments, 2)));
    spy.search.and.returnValue(of(createMockPagedResult([], 0)));

    await TestBed.configureTestingModule({
    imports: [
        AppointmentListComponent, RouterTestingModule, NoopAnimationsModule],
    providers: [
        { provide: AppointmentService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(AppointmentListComponent);
    component = fixture.componentInstance;
    appointmentService = TestBed.inject(AppointmentService) as jasmine.SpyObj<AppointmentService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load appointments on init', () => {
    expect(appointmentService.list).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Lịch hẹn');
  });

  it('should render schedule button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const btn = compiled.querySelector('button[routerLink="/appointments/new"]');
    expect(btn).toBeTruthy();
  });

  it('should display appointment rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('.hh-data-table tbody tr');
    expect(rows.length).toBe(2);
  });

  it('should have search field', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('input[placeholder="Tìm theo mã bệnh nhân..."]');
    expect(input).toBeTruthy();
  });

  it('should search appointments when the query changes', () => {
    const query: HisHopePageQuery = { page: 1, pageSize: 20, search: 'pat-001' };
    component.onQueryChange(query);
    expect(appointmentService.search).toHaveBeenCalledWith('pat-001', 1, 20);
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
