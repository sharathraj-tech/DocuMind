import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = async (_route, state) => {
  const router = inject(Router);
  const auth = inject(AuthService);
  await auth.init(); // restores the session after a page reload before we decide

  return auth.isAuthenticated()
    ? true
    : router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
};