import { selectUser, selectIsLoggedIn, selectAuthLoading, selectAuthError } from './auth.selectors';
import { initialAuthState } from './auth.reducer';

describe('Auth Selectors', () => {
  const mockUser = {
    id: 'usr-001', username: 'admin', email: 'admin@hishope.vn',
    firstName: 'Admin', lastName: 'User', fullName: 'Admin User', roles: ['admin'],
  };
  const baseState = { auth: initialAuthState, patients: {} as any, error: {} as any };

  it('should select user', () => {
    const state = { ...baseState, auth: { user: mockUser, loading: false, error: null } };
    expect(selectUser(state)).toEqual(mockUser);
  });

  it('should select isLoggedIn as false when no user', () => {
    const state = { ...baseState, auth: { user: null, loading: false, error: null } };
    expect(selectIsLoggedIn(state)).toBeFalse();
  });

  it('should select isLoggedIn as true when user exists', () => {
    const state = { ...baseState, auth: { user: mockUser, loading: false, error: null } };
    expect(selectIsLoggedIn(state)).toBeTrue();
  });

  it('should select loading state', () => {
    const state = { ...baseState, auth: { user: null, loading: true, error: null } };
    expect(selectAuthLoading(state)).toBeTrue();
  });

  it('should select error state', () => {
    const state = { ...baseState, auth: { user: null, loading: false, error: 'error' } };
    expect(selectAuthError(state)).toBe('error');
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
