import { inject } from '@angular/core';
import { createHisHopeBearerTokenInterceptor } from '@his-hope/frontend-foundation';
import { AuthService } from '@core/services/auth.service';

/**
 * Auth interceptor for dual-mode authentication:
 * - OIDC tokens (via angular-auth-oidc-client): adds Bearer header
 * - BFF sessions (via HttpOnly cookies): handled automatically by withCredentials
 *
 * Both modes coexist during the transition from OIDC to BFF-only.
 *
 * Uses the shared foundation interceptor factory which sets
 * withCredentials: true by default, ensuring BFF session cookies
 * are sent on every /api/ request.
 */
export const authInterceptor = createHisHopeBearerTokenInterceptor(
  () => inject(AuthService).getAccessToken(),
  {
    matches: (url) => url.includes("/api/") && !url.includes("/localization"),
  },
);
