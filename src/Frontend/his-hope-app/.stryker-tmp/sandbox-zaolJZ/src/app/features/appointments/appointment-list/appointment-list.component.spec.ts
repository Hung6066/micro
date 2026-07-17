// @ts-nocheck
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { AppointmentListComponent } from './appointment-list.component';
import { AppointmentService } from '@core/services/appointment.service';
import { createMockAppointment, createMockPagedResult } from '@testing/mock-data';

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
      declarations: [AppointmentListComponent],
      imports: [
        RouterTestingModule, NoopAnimationsModule, HttpClientTestingModule,
        MatTableModule, MatPaginatorModule, MatCardModule, MatFormFieldModule,
        MatInputModule, MatIconModule, MatButtonModule, MatTooltipModule,
        ReactiveFormsModule, CommonModule,
      ],
      providers: [
        { provide: AppointmentService, useValue: spy },
      ],
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
    expect(compiled.querySelector('h1')?.textContent).toContain('Appointments');
  });

  it('should render schedule button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const btn = compiled.querySelector('button[routerLink="/appointments/new"]');
    expect(btn).toBeTruthy();
  });

  it('should display appointment rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('.mat-mdc-row');
    expect(rows.length).toBe(2);
  });

  it('should have search field', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const input = compiled.querySelector('input[placeholder="Type to search..."]');
    expect(input).toBeTruthy();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
