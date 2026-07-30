import { inject } from '@angular/core';
import { createHisHopeBearerTokenInterceptor } from '@his-hope/frontend-foundation';
import { AuthService } from './auth.service';

export const authInterceptor = createHisHopeBearerTokenInterceptor(
  () => inject(AuthService).getAccessToken(),
);
