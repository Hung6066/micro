import { createFeatureSelector, createSelector } from '@ngrx/store';
import { AuthState } from './auth.reducer';

export const selectAuthState = createFeatureSelector<AuthState>('auth');

export const selectUser = createSelector(
  selectAuthState,
  (state) => state.user,
);

export const selectIsLoggedIn = createSelector(
  selectAuthState,
  (state) => !!state.user,
);

export const selectAuthLoading = createSelector(
  selectAuthState,
  (state) => state.loading,
);

export const selectAuthError = createSelector(
  selectAuthState,
  (state) => state.error,
);

export const selectIsOidcAuthenticated = createSelector(
  selectAuthState,
  (state) => state.isOidcAuthenticated,
);
