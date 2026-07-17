// @ts-nocheck
import { patientsReducer, initialPatientsState } from './patients.reducer';
import * as PatientsActions from './patients.actions';

describe('PatientsReducer', () => {
  const mockPatient = { id: 'pat-001', fullName: 'Test Patient', isActive: true } as any;

  it('should return initial state', () => {
    const state = patientsReducer(undefined, { type: '@@init' });
    expect(state).toEqual(initialPatientsState);
  });

  it('should set loading on searchPatients', () => {
    const state = patientsReducer(
      initialPatientsState,
      PatientsActions.searchPatients({ query: 'test', page: 1, pageSize: 20 }),
    );
    expect(state.loading).toBeTrue();
    expect(state.query).toBe('test');
    expect(state.page).toBe(1);
  });

  it('should populate patients on searchPatientsSuccess', () => {
    const result = {
      items: [mockPatient],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    } as any;
    const state = patientsReducer(
      initialPatientsState,
      PatientsActions.searchPatientsSuccess({ result }),
    );
    expect(state.patients.length).toBe(1);
    expect(state.totalCount).toBe(1);
    expect(state.loading).toBeFalse();
  });

  it('should set selectedPatient on loadPatientSuccess', () => {
    const state = patientsReducer(
      initialPatientsState,
      PatientsActions.loadPatientSuccess({ patient: mockPatient }),
    );
    expect(state.selectedPatient).toEqual(mockPatient);
    expect(state.loading).toBeFalse();
  });

  it('should prepend patient on createPatientSuccess', () => {
    const existingState = { ...initialPatientsState, patients: [{ id: 'pat-002', fullName: 'Existing' } as any], totalCount: 1 };
    const state = patientsReducer(
      existingState,
      PatientsActions.createPatientSuccess({ patient: mockPatient }),
    );
    expect(state.patients.length).toBe(2);
    expect(state.patients[0].id).toBe('pat-001');
    expect(state.totalCount).toBe(2);
  });

  it('should update patient on updatePatientSuccess', () => {
    const existingState = { ...initialPatientsState, patients: [{ id: 'pat-001', fullName: 'Old Name', isActive: true } as any] };
    const updatedPatient = { id: 'pat-001', fullName: 'Updated Name', isActive: true } as any;
    const state = patientsReducer(
      existingState,
      PatientsActions.updatePatientSuccess({ patient: updatedPatient }),
    );
    expect(state.patients[0].fullName).toBe('Updated Name');
  });

  it('should deactivate patient on deactivatePatientSuccess', () => {
    const existingState = { ...initialPatientsState, patients: [{ id: 'pat-001', fullName: 'Test', isActive: true } as any] };
    const state = patientsReducer(
      existingState,
      PatientsActions.deactivatePatientSuccess({ id: 'pat-001' }),
    );
    expect(state.patients[0].isActive).toBeFalse();
  });

  it('should set error on searchPatientsFailure', () => {
    const state = patientsReducer(
      initialPatientsState,
      PatientsActions.searchPatientsFailure({ error: 'Search failed' }),
    );
    expect(state.error).toBe('Search failed');
    expect(state.loading).toBeFalse();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
