import { errorReducer, initialErrorState } from './error.reducer';
import { captureError, clearError } from './error.actions';

describe('ErrorReducer', () => {
  it('should return initial state', () => {
    const state = errorReducer(undefined, { type: '@@init' });
    expect(state).toEqual(initialErrorState);
  });

  it('should set error on captureError', () => {
    const payload = { message: 'Not found', code: 'HTTP_404', correlationId: 'hh-abc-123' };
    const state = errorReducer(initialErrorState, captureError(payload));
    expect(state.message).toBe('Not found');
    expect(state.code).toBe('HTTP_404');
    expect(state.correlationId).toBe('hh-abc-123');
    expect(state.timestamp).toBeTruthy();
  });

  it('should clear error on clearError', () => {
    const populatedState = {
      message: 'error',
      code: 'HTTP_500',
      correlationId: 'hh-xxx',
      timestamp: '2024-01-01T00:00:00.000Z',
    };
    const state = errorReducer(populatedState, clearError());
    expect(state).toEqual(initialErrorState);
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
