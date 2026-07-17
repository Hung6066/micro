import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { AppointmentDetailComponent } from './appointment-detail.component';
import { AppointmentService } from '@core/services/appointment.service';
import { createMockAppointment } from '@testing/mock-data';

describe('AppointmentDetailComponent', () => {
  let component: AppointmentDetailComponent;
  let fixture: ComponentFixture<AppointmentDetailComponent>;
  let appointmentService: jasmine.SpyObj<AppointmentService>;

  const mockAppointment = createMockAppointment();

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('AppointmentService', ['getById']);
    spy.getById.and.returnValue(of(mockAppointment));

    await TestBed.configureTestingModule({
      declarations: [AppointmentDetailComponent],
      imports: [
        RouterTestingModule, NoopAnimationsModule, HttpClientTestingModule,
        MatCardModule, MatListModule, MatIconModule, CommonModule,
      ],
      providers: [
        { provide: AppointmentService, useValue: spy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppointmentDetailComponent);
    component = fixture.componentInstance;
    appointmentService = TestBed.inject(AppointmentService) as jasmine.SpyObj<AppointmentService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load appointment on init', () => {
    expect(appointmentService.getById).toHaveBeenCalled();
  });

  it('should display appointment details', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Appointment Details');
  });

  it('should render schedule card', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Schedule');
  });

  it('should render timeline', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Timeline');
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
