import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, of, catchError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { PermissionService } from '../auth/permission.service';

// Blocks any route if there's no JWT in storage
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.getToken()) {
    return true;
  }

  router.navigate(['/login']);
  return false;
};

// Blocks route unless logged-in user's role is "Super Admin"
export const superAdminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.getToken()) {
    router.navigate(['/login']);
    return false;
  }

  if (!authService.isSuperAdmin()) {
    router.navigate(['/dashboard']);
    return false;
  }

  return true;
};

// Blocks route unless role is "Super Admin" or "Admin"
export const adminOrSuperAdminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.getToken()) {
    router.navigate(['/login']);
    return false;
  }

  const role = authService.currentRole();
  if (role !== 'Super Admin' && role !== 'Admin') {
    router.navigate(['/dashboard']);
    return false;
  }

  return true;
};

// ============ ORDER GUARDS ============

export const ordersGuard: CanActivateFn = () => {
  const permissionService = inject(PermissionService);
  const router = inject(Router);
  const authService = inject(AuthService);

  // Pehle auth check
  if (!authService.getToken()) {
    router.navigate(['/login']);
    return false;
  }

  // Agar permissions already loaded hain toh direct check karo
  if (permissionService.isLoaded()) {
    const hasPermission = permissionService.canView('Orders');
    if (hasPermission) {
      return true;
    }
    return router.parseUrl('/dashboard');
  }

  // Warna permissions load hone ka wait karo
  return permissionService.load().pipe(
    map(() => {
      const hasPermission = permissionService.canView('Orders');
      if (hasPermission) {
        return true;
      }
      return router.parseUrl('/dashboard');
    }),
    catchError(() => {
      // Error ki surat mein access allow karo
      return of(true);
    })
  );
};

export const ordersAddGuard: CanActivateFn = () => {
  const permissionService = inject(PermissionService);
  const router = inject(Router);
  const authService = inject(AuthService);

  if (!authService.getToken()) {
    router.navigate(['/login']);
    return false;
  }

  if (permissionService.isLoaded()) {
    if (permissionService.canAdd('Orders')) {
      return true;
    }
    return router.parseUrl('/dashboard/orders');
  }

  return permissionService.load().pipe(
    map(() => {
      if (permissionService.canAdd('Orders')) {
        return true;
      }
      return router.parseUrl('/dashboard/orders');
    }),
    catchError(() => {
      return of(true);
    })
  );
};

export const ordersEditGuard: CanActivateFn = () => {
  const permissionService = inject(PermissionService);
  const router = inject(Router);
  const authService = inject(AuthService);

  if (!authService.getToken()) {
    router.navigate(['/login']);
    return false;
  }

  if (permissionService.isLoaded()) {
    if (permissionService.canEdit('Orders')) {
      return true;
    }
    return router.parseUrl('/dashboard/orders');
  }

  return permissionService.load().pipe(
    map(() => {
      if (permissionService.canEdit('Orders')) {
        return true;
      }
      return router.parseUrl('/dashboard/orders');
    }),
    catchError(() => {
      return of(true);
    })
  );
};