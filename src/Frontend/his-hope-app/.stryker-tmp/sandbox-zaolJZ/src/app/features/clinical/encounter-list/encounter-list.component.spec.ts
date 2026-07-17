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
import { EncounterListComponent } from './encounter-list.component';
import { ClinicalService } from '@core/services/clinical.service';
import { createMockEncounter, createMockPagedResult } from '@testing/mock-data';

describe('EncounterListComponent', () => {
  let component: EncounterListComponent;
  let fixture: ComponentFixture<EncounterListComponent>;
  let clinicalService: jasmine.SpyObj<ClinicalService>;

  const mockEncounters = [createMockEncounter(), createMockEncounter()];

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('ClinicalService', ['list', 'search']);
    spy.list.and.returnValue(of(createMockPagedResult(mockEncounters, 2)));
    spy.search.and.returnValue(of(createMockPagedResult([], 0)));

    await TestBed.configureTestingModule({
      declarations: [EncounterListComponent],
      imports: [
        RouterTestingModule, NoopAnimationsModule, HttpClientTestingModule,
        MatTableModule, MatPaginatorModule, MatCardModule, MatFormFieldModule,
        MatInputModule, MatIconModule, MatButtonModule, MatTooltipModule,
        ReactiveFormsModule, CommonModule,
      ],
      providers: [
        { provide: ClinicalService, useValue: spy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(EncounterListComponent);
    component = fixture.componentInstance;
    clinicalService = TestBed.inject(ClinicalService) as jasmine.SpyObj<ClinicalService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load encounters on init', () => {
    expect(clinicalService.list).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Clinical Encounters');
  });

  it('should display encounter rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('.mat-mdc-row');
    expect(rows.length).toBe(2);
  });

  it('should have search field', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('input[placeholder="Type to search..."]')).toBeTruthy();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
