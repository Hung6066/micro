import { createReducer, on } from '@ngrx/store';
import { captureError, clearError } from './error.actions';

export interface ErrorState {
  message: string | null;
  code: string | null;
  correlationId: string | null;
  timestamp: string | null;
}

export const initialErrorState: ErrorState = {
  message: null,
  code: null,
  correlationId: null,
  timestamp: null,
};

export const errorReducer = createReducer(
  initialErrorState,

  on(captureError, (state, { message, code, correlationId }) => ({
    message,
    code,
    correlationId,
    timestamp: new Date().toISOString(),
  })),

  on(clearError, () => initialErrorState),
);
