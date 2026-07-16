import { createFeatureSelector, createSelector } from '@ngrx/store';
import { PatientsState } from './patients.reducer';

export const selectPatientsState = createFeatureSelector<PatientsState>('patients');

export const selectAllPatients = createSelector(
  selectPatientsState,
  (state) => state.patients,
);

export const selectSelectedPatient = createSelector(
  selectPatientsState,
  (state) => state.selectedPatient,
);

export const selectPatientsTotalCount = createSelector(
  selectPatientsState,
  (state) => state.totalCount,
);

export const selectPatientsLoading = createSelector(
  selectPatientsState,
  (state) => state.loading,
);

export const selectPatientsError = createSelector(
  selectPatientsState,
  (state) => state.error,
);

export const selectPatientsQuery = createSelector(
  selectPatientsState,
  (state) => state.query,
);

export const selectPatientsPage = createSelector(
  selectPatientsState,
  (state) => state.page,
);

export const selectPatientsPageSize = createSelector(
  selectPatientsState,
  (state) => state.pageSize,
);
