import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, Router } from '@angular/router';
import { map, of, catchError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { PermissionService } from '../auth/permission.service';

// Blocks any route if there's no JWT in storage
export const authGuard: CanActivateFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.getToken()) {
    return true;
  }

  router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
  return false;
};

// Apply to every dashboard child, including navigation within an existing layout.
export const assignedRoleGuard: CanActivateChildFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.getToken()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  if (route.routeConfig?.path === 'profile') {
    return true;
  }

  const denied = router.parseUrl('/dashboard/profile');
  if (!authService.hasAssignedRole()) return denied;

  const permissions = inject(PermissionService);
  const paths = route.pathFromRoot.map(part => part.routeConfig?.path);
  const screen = paths.includes('orders') || paths.some(path => path?.startsWith('integrations/'))
    ? 'Orders'
    : route.routeConfig?.path === 'manage-users' ? 'Manage Users'
    : route.routeConfig?.path === 'manage-roles' ? 'Manage Roles'
    : route.routeConfig?.path === '' ? 'Dashboard' : null;
  return permissions.load().pipe(
    map(() => {
      if (paths.some(path => path?.startsWith('integrations/')) && permissions.adminAssignedOrdersOnly()) return denied;
      return screen && permissions.canView(screen) ? true : denied;
    }),
    catchError(() => of(denied))
  );
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
      return of(router.parseUrl('/dashboard/profile'));
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
      return of(router.parseUrl('/dashboard/profile'));
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
      return of(router.parseUrl('/dashboard/profile'));
    })
  );
};
