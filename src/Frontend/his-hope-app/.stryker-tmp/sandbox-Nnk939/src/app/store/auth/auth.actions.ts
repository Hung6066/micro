// @ts-nocheck
import { createAction, props } from '@ngrx/store';
import { LoginRequest, RegisterRequest, User } from '@core/models/auth.model';

export const login = createAction(
  '[Auth] Login',
  props<{ request: LoginRequest }>(),
);

export const loginSuccess = createAction(
  '[Auth] Login Success',
  props<{ user: User }>(),
);

export const loginFailure = createAction(
  '[Auth] Login Failure',
  props<{ error: string }>(),
);

export const register = createAction(
  '[Auth] Register',
  props<{ request: RegisterRequest }>(),
);

export const registerSuccess = createAction(
  '[Auth] Register Success',
  props<{ user: User }>(),
);

export const registerFailure = createAction(
  '[Auth] Register Failure',
  props<{ error: string }>(),
);

export const logout = createAction('[Auth] Logout');

export const logoutSuccess = createAction('[Auth] Logout Success');

export const logoutFailure = createAction(
  '[Auth] Logout Failure',
  props<{ error: string }>(),
);

export const loadCurrentUser = createAction('[Auth] Load Current User');

export const loadCurrentUserSuccess = createAction(
  '[Auth] Load Current User Success',
  props<{ user: User }>(),
);

export const loadCurrentUserFailure = createAction(
  '[Auth] Load Current User Failure',
  props<{ error: string }>(),
);

export const clearError = createAction('[Auth] Clear Error');
