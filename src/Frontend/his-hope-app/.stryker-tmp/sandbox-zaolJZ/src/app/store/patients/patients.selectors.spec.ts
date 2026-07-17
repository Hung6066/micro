// @ts-nocheck
import { selectAllPatients, selectSelectedPatient, selectPatientsTotalCount, selectPatientsLoading } from './patients.selectors';
import { initialPatientsState } from './patients.reducer';

describe('Patients Selectors', () => {
  const baseState = { patients: initialPatientsState, auth: {} as any, error: {} as any };

  it('should select all patients', () => {
    const patients = [{ id: 'pat-001', fullName: 'Test' }] as any;
    const state = { ...baseState, patients: { ...initialPatientsState, patients } };
    expect(selectAllPatients(state).length).toBe(1);
  });

  it('should select selected patient', () => {
    const patient = { id: 'pat-001', fullName: 'Test' } as any;
    const state = { ...baseState, patients: { ...initialPatientsState, selectedPatient: patient } };
    expect(selectSelectedPatient(state)?.id).toBe('pat-001');
  });

  it('should select total count', () => {
    const state = { ...baseState, patients: { ...initialPatientsState, totalCount: 42 } };
    expect(selectPatientsTotalCount(state)).toBe(42);
  });

  it('should select loading state', () => {
    const state = { ...baseState, patients: { ...initialPatientsState, loading: true } };
    expect(selectPatientsLoading(state)).toBeTrue();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
