import { createFeatureSelector, createSelector } from '@ngrx/store';
import { ErrorState } from './error.reducer';

export const selectErrorState = createFeatureSelector<ErrorState>('error');

export const selectError = createSelector(
  selectErrorState,
  (state) => state,
);

export const selectErrorMessage = createSelector(
  selectErrorState,
  (state) => state.message,
);

export const selectErrorCorrelationId = createSelector(
  selectErrorState,
  (state) => state.correlationId,
);
