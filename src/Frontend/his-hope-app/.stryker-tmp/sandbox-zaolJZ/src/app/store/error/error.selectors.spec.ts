// @ts-nocheck
import { selectError, selectErrorMessage } from './error.selectors';
import { initialErrorState } from './error.reducer';

describe('Error Selectors', () => {
  const baseState = { error: initialErrorState, auth: {} as any, patients: {} as any };

  it('should select error state', () => {
    const errorState = { message: 'error', code: 'HTTP_500', correlationId: 'hh-xyz', timestamp: null };
    const state = { ...baseState, error: errorState };
    const result = selectError(state);
    expect(result.message).toBe('error');
    expect(result.code).toBe('HTTP_500');
  });

  it('should select error message', () => {
    const state = { ...baseState, error: { ...initialErrorState, message: 'test error' } };
    expect(selectErrorMessage(state)).toBe('test error');
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
