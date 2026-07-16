import { createReducer, on } from '@ngrx/store';
import { Patient } from '@core/models/patient.model';
import * as PatientsActions from './patients.actions';

export interface PatientsState {
  patients: Patient[];
  selectedPatient: Patient | null;
  totalCount: number;
  page: number;
  pageSize: number;
  query: string;
  loading: boolean;
  error: string | null;
}

export const initialPatientsState: PatientsState = {
  patients: [],
  selectedPatient: null,
  totalCount: 0,
  page: 1,
  pageSize: 20,
  query: '',
  loading: false,
  error: null,
};

export const patientsReducer = createReducer(
  initialPatientsState,

  on(PatientsActions.searchPatients, (state, { query, page, pageSize }) => ({
    ...state,
    query,
    page,
    pageSize,
    loading: true,
    error: null,
  })),
  on(PatientsActions.searchPatientsSuccess, (state, { result }) => ({
    ...state,
    patients: result.items,
    totalCount: result.totalCount,
    loading: false,
    error: null,
  })),
  on(PatientsActions.searchPatientsFailure, (state, { error }) => ({
    ...state,
    loading: false,
    error,
  })),

  on(PatientsActions.loadPatient, (state) => ({
    ...state,
    loading: true,
    error: null,
  })),
  on(PatientsActions.loadPatientSuccess, (state, { patient }) => ({
    ...state,
    selectedPatient: patient,
    loading: false,
    error: null,
  })),
  on(PatientsActions.loadPatientFailure, (state, { error }) => ({
    ...state,
    loading: false,
    error,
  })),

  on(PatientsActions.createPatientSuccess, (state, { patient }) => ({
    ...state,
    patients: [patient, ...state.patients],
    totalCount: state.totalCount + 1,
    loading: false,
    error: null,
  })),
  on(PatientsActions.createPatientFailure, (state, { error }) => ({
    ...state,
    loading: false,
    error,
  })),

  on(PatientsActions.updatePatientSuccess, (state, { patient }) => ({
    ...state,
    selectedPatient: patient,
    patients: state.patients.map((p) => (p.id === patient.id ? patient : p)),
    loading: false,
    error: null,
  })),
  on(PatientsActions.updatePatientFailure, (state, { error }) => ({
    ...state,
    loading: false,
    error,
  })),

  on(PatientsActions.deactivatePatientSuccess, (state, { id }) => ({
    ...state,
    patients: state.patients.map((p) =>
      p.id === id ? { ...p, isActive: false } : p,
    ),
    selectedPatient:
      state.selectedPatient?.id === id
        ? { ...state.selectedPatient, isActive: false }
        : state.selectedPatient,
    loading: false,
    error: null,
  })),
  on(PatientsActions.deactivatePatientFailure, (state, { error }) => ({
    ...state,
    loading: false,
    error,
  })),

  on(PatientsActions.reactivatePatientSuccess, (state, { id }) => ({
    ...state,
    patients: state.patients.map((p) =>
      p.id === id ? { ...p, isActive: true } : p,
    ),
    selectedPatient:
      state.selectedPatient?.id === id
        ? { ...state.selectedPatient, isActive: true }
        : state.selectedPatient,
    loading: false,
    error: null,
  })),
  on(PatientsActions.reactivatePatientFailure, (state, { error }) => ({
    ...state,
    loading: false,
    error,
  })),

  on(PatientsActions.clearSelectedPatient, (state) => ({
    ...state,
    selectedPatient: null,
  })),
);
