import { Injectable } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { map, switchMap, catchError, tap } from 'rxjs/operators';
import { PatientService } from '@core/services/patient.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import * as PatientsActions from './patients.actions';

@Injectable()
export class PatientsEffects {
  searchPatients$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PatientsActions.searchPatients),
      switchMap(({ query, page, pageSize }) =>
        this.patientService.search(query, page, pageSize).pipe(
          map((result) => PatientsActions.searchPatientsSuccess({ result })),
          catchError((err) =>
            of(
              PatientsActions.searchPatientsFailure({
                error: err.error?.error || err.message || 'Search failed',
              }),
            ),
          ),
        ),
      ),
    ),
  );

  loadPatient$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PatientsActions.loadPatient),
      switchMap(({ id }) =>
        this.patientService.getById(id).pipe(
          map((patient) => PatientsActions.loadPatientSuccess({ patient })),
          catchError((err) =>
            of(
              PatientsActions.loadPatientFailure({
                error: err.error?.error || err.message || 'Failed to load patient',
              }),
            ),
          ),
        ),
      ),
    ),
  );

  createPatient$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PatientsActions.createPatient),
      switchMap(({ request }) =>
        this.patientService.create(request).pipe(
          map((patient) => PatientsActions.createPatientSuccess({ patient })),
          catchError((err) =>
            of(
              PatientsActions.createPatientFailure({
                error: err.error?.error || err.message || 'Failed to create patient',
              }),
            ),
          ),
        ),
      ),
    ),
  );

  createPatientSuccess$ = createEffect(
    () =>
      this.actions$.pipe(
        ofType(PatientsActions.createPatientSuccess),
        tap(({ patient }) => {
          this.snackBar.open('Patient created successfully', 'Close', {
            duration: 3000,
          });
          this.router.navigate(['/patients', patient.id]);
        }),
      ),
    { dispatch: false },
  );

  updatePatient$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PatientsActions.updatePatient),
      switchMap(({ id, request }) =>
        this.patientService.update(id, request).pipe(
          map((patient) => PatientsActions.updatePatientSuccess({ patient })),
          catchError((err) =>
            of(
              PatientsActions.updatePatientFailure({
                error: err.error?.error || err.message || 'Failed to update patient',
              }),
            ),
          ),
        ),
      ),
    ),
  );

  updatePatientSuccess$ = createEffect(
    () =>
      this.actions$.pipe(
        ofType(PatientsActions.updatePatientSuccess),
        tap(({ patient }) => {
          this.snackBar.open('Patient updated successfully', 'Close', {
            duration: 3000,
          });
          this.router.navigate(['/patients', patient.id]);
        }),
      ),
    { dispatch: false },
  );

  deactivatePatient$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PatientsActions.deactivatePatient),
      switchMap(({ id }) =>
        this.patientService.deactivate(id).pipe(
          map(() => PatientsActions.deactivatePatientSuccess({ id })),
          catchError((err) =>
            of(
              PatientsActions.deactivatePatientFailure({
                error: err.error?.error || err.message || 'Failed to deactivate patient',
              }),
            ),
          ),
        ),
      ),
    ),
  );

  reactivatePatient$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PatientsActions.reactivatePatient),
      switchMap(({ id }) =>
        this.patientService.reactivate(id).pipe(
          map(() => PatientsActions.reactivatePatientSuccess({ id })),
          catchError((err) =>
            of(
              PatientsActions.reactivatePatientFailure({
                error: err.error?.error || err.message || 'Failed to reactivate patient',
              }),
            ),
          ),
        ),
      ),
    ),
  );

  constructor(
    private actions$: Actions,
    private patientService: PatientService,
    private router: Router,
    private snackBar: MatSnackBar,
  ) {}
}
