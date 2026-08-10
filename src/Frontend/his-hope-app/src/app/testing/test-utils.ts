import { Provider } from '@angular/core';
import { provideMockStore } from '@ngrx/store/testing';
import { AuthState, initialAuthState } from '@store/auth/auth.reducer';
import { PatientsState, initialPatientsState } from '@store/patients/patients.reducer';
import { ErrorState, initialErrorState } from '@store/error/error.reducer';

export interface AppState {
  auth: AuthState;
  patients: PatientsState;
  error: ErrorState;
}

export const initialAppState: AppState = {
  auth: initialAuthState,
  patients: initialPatientsState,
  error: initialErrorState,
};

export function createMockAuthState(overrides?: Partial<AuthState>): AuthState {
  return { ...initialAuthState, ...overrides };
}

export function createMockPatientsState(overrides?: Partial<PatientsState>): PatientsState {
  return { ...initialPatientsState, ...overrides };
}

export function createMockErrorState(overrides?: Partial<ErrorState>): ErrorState {
  return { ...initialErrorState, ...overrides };
}

export function provideMockAuthStore(authOverrides?: Partial<AuthState>): Provider[] {
  return [
    provideMockStore({
      initialState: {
        ...initialAppState,
        auth: createMockAuthState(authOverrides),
      },
    }),
  ];
}

export function provideMockPatientsStore(patientsOverrides?: Partial<PatientsState>): Provider[] {
  return [
    provideMockStore({
      initialState: {
        ...initialAppState,
        patients: createMockPatientsState(patientsOverrides),
      },
    }),
  ];
}

export function provideMockAllStores(overrides?: Partial<AppState>): Provider[] {
  return [
    provideMockStore({
      initialState: { ...initialAppState, ...overrides },
    }),
  ];
}
