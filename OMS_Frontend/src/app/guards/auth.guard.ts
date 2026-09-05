import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
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
// Used for Create User Account + Manage Customers routes
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
// Used for Manage Users route
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

export const ordersGuard: CanActivateFn = () => {
  const permissionService = inject(PermissionService);
  const router = inject(Router);

  return permissionService.canView('Orders')
    ? true
    : router.parseUrl('/dashboard');
};

export const ordersAddGuard: CanActivateFn = () => {
  const permissionService = inject(PermissionService);
  const router = inject(Router);

  return permissionService.canAdd('Orders')
    ? true
    : router.parseUrl('/dashboard/orders');
};

export const ordersEditGuard: CanActivateFn = () => {
  const permissionService = inject(PermissionService);
  const router = inject(Router);

  return permissionService.canEdit('Orders')
    ? true
    : router.parseUrl('/dashboard/orders');
};