import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissionService } from '../auth/permission.service';
import { AuthService } from '../auth/auth.service';

export const permissionGuard: CanActivateFn = (route) => {
  const perm = inject(PermissionService);
  const auth = inject(AuthService);
  const router = inject(Router);
  const screenKey = route.data['screenKey'] as string;

  if (auth.currentRole() === 'Super Admin') return true;

  const check = () => {
    if (perm.canView(screenKey)) return true;
    router.navigate(['/dashboard']);
    return false;
  };

  if (perm.isLoaded()) return check();

  // Agar service abhi load ho rahi hai (direct URL / refresh case)
  return new Promise<boolean>((resolve) => {
    perm.load().subscribe({
      next: () => resolve(check()),
      error: () => { router.navigate(['/login']); resolve(false); }
    });
  });
};