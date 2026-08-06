import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../services/auth.service';

/**
 * Keeps the admin report out of the router for unauthenticated visitors.
 *
 * This is a usability measure, not the security boundary — a guard runs in the browser and can be
 * bypassed. The actual protection is `[Authorize]` on the API, so a bypassed guard yields an empty
 * page rather than leaked data.
 */
export const adminGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/admin/login'], {
    queryParams: { returnUrl: state.url }
  });
};
