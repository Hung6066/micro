import { createAction, props } from '@ngrx/store';

export interface ErrorPayload {
  message: string;
  code: string;
  correlationId: string;
}

export const captureError = createAction(
  '[Error] Capture',
  props<ErrorPayload>(),
);

export const clearError = createAction('[Error] Clear');
