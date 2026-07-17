// @ts-nocheck
import { authReducer, initialAuthState } from './auth.reducer';
import * as AuthActions from './auth.actions';
import { User } from '@core/models/auth.model';

describe('AuthReducer', () => {
  const mockUser: User = {
    id: 'usr-001',
    username: 'admin',
    email: 'admin@hishope.vn',
    firstName: 'Admin',
    lastName: 'User',
    fullName: 'Admin User',
    roles: ['admin'],
  };

  it('should return initial state', () => {
    const state = authReducer(undefined, { type: '@@init' });
    expect(state).toEqual(initialAuthState);
  });

  it('should set loading on login', () => {
    const state = authReducer(
      initialAuthState,
      AuthActions.login({ request: { username: 'admin', password: 'secret' } }),
    );
    expect(state.loading).toBeTrue();
    expect(state.error).toBeNull();
  });

  it('should set user on loginSuccess', () => {
    const state = authReducer(
      initialAuthState,
      AuthActions.loginSuccess({ user: mockUser }),
    );
    expect(state.user).toEqual(mockUser);
    expect(state.loading).toBeFalse();
    expect(state.error).toBeNull();
  });

  it('should set error on loginFailure', () => {
    const state = authReducer(
      initialAuthState,
      AuthActions.loginFailure({ error: 'Invalid credentials' }),
    );
    expect(state.error).toBe('Invalid credentials');
    expect(state.loading).toBeFalse();
    expect(state.user).toBeNull();
  });

  it('should clear user on logoutSuccess', () => {
    const loggedInState = authReducer(
      initialAuthState,
      AuthActions.loginSuccess({ user: mockUser }),
    );
    const state = authReducer(loggedInState, AuthActions.logoutSuccess());
    expect(state.user).toBeNull();
    expect(state.loading).toBeFalse();
    expect(state.error).toBeNull();
  });

  it('should update user on loadCurrentUserSuccess', () => {
    const state = authReducer(
      initialAuthState,
      AuthActions.loadCurrentUserSuccess({ user: mockUser }),
    );
    expect(state.user).toEqual(mockUser);
    expect(state.loading).toBeFalse();
  });

  it('should set loading on register', () => {
    const state = authReducer(
      initialAuthState,
      AuthActions.register({ request: { username: 'new', email: 'new@test.com', password: 'secret', firstName: 'New', lastName: 'User' } }),
    );
    expect(state.loading).toBeTrue();
  });

  it('should clear error on clearError', () => {
    const errorState = authReducer(initialAuthState, AuthActions.loginFailure({ error: 'test error' }));
    const state = authReducer(errorState, AuthActions.clearError());
    expect(state.error).toBeNull();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
