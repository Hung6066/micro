import { createAction, props } from '@ngrx/store';
import { Patient, CreatePatientRequest } from '@core/models/patient.model';
import { PagedResult } from '@core/models/paged-result.model';

export const searchPatients = createAction(
  '[Patients] Search',
  props<{ query: string; page: number; pageSize: number }>(),
);

export const searchPatientsSuccess = createAction(
  '[Patients] Search Success',
  props<{ result: PagedResult<Patient> }>(),
);

export const searchPatientsFailure = createAction(
  '[Patients] Search Failure',
  props<{ error: string }>(),
);

export const loadPatient = createAction(
  '[Patients] Load',
  props<{ id: string }>(),
);

export const loadPatientSuccess = createAction(
  '[Patients] Load Success',
  props<{ patient: Patient }>(),
);

export const loadPatientFailure = createAction(
  '[Patients] Load Failure',
  props<{ error: string }>(),
);

export const createPatient = createAction(
  '[Patients] Create',
  props<{ request: CreatePatientRequest }>(),
);

export const createPatientSuccess = createAction(
  '[Patients] Create Success',
  props<{ patient: Patient }>(),
);

export const createPatientFailure = createAction(
  '[Patients] Create Failure',
  props<{ error: string }>(),
);

export const updatePatient = createAction(
  '[Patients] Update',
  props<{ id: string; request: CreatePatientRequest }>(),
);

export const updatePatientSuccess = createAction(
  '[Patients] Update Success',
  props<{ patient: Patient }>(),
);

export const updatePatientFailure = createAction(
  '[Patients] Update Failure',
  props<{ error: string }>(),
);

export const deactivatePatient = createAction(
  '[Patients] Deactivate',
  props<{ id: string }>(),
);

export const deactivatePatientSuccess = createAction(
  '[Patients] Deactivate Success',
  props<{ id: string }>(),
);

export const deactivatePatientFailure = createAction(
  '[Patients] Deactivate Failure',
  props<{ error: string }>(),
);

export const reactivatePatient = createAction(
  '[Patients] Reactivate',
  props<{ id: string }>(),
);

export const reactivatePatientSuccess = createAction(
  '[Patients] Reactivate Success',
  props<{ id: string }>(),
);

export const reactivatePatientFailure = createAction(
  '[Patients] Reactivate Failure',
  props<{ error: string }>(),
);

export const clearSelectedPatient = createAction('[Patients] Clear Selected');
