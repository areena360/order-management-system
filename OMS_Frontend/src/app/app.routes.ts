import { Routes } from '@angular/router';

import { LoginComponent } from './login/login.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { LayoutComponent } from './layout/layout.component';
import { DashboardComponent } from './dashboard/dashboard.component';

import {
  authGuard,
  assignedRoleGuard,
  ordersGuard,
  ordersAddGuard
} from './guards/auth.guard';

import { ManageUsersComponent } from './manage-user/manage-users.component';
import { RegisterComponent } from './register/register.component';
import { ProfileComponent } from './profile/profile.component';
import { RolesAndPermissionsComponent } from './roles-and-permissions/roles-and-permissions.component';

import { ManageOrdersComponent } from './orders/manage-orders/manage-orders.component';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full'
  },

  {
    path: 'login',
    component: LoginComponent
  },

  {
    path: 'register',
    component: RegisterComponent
  },

  {
    path: 'forgot-password',
    component: ForgotPasswordComponent
  },

  {
    path: 'reset-password',
    component: ResetPasswordComponent
  },

  {
    path: 'dashboard',
    component: LayoutComponent,
    canActivate: [authGuard],
    canActivateChild: [assignedRoleGuard],

    children: [
      { path: 'integrations/shopify', loadComponent: () => import('./integrations/shopify.component').then(m => m.ShopifyComponent) },
      {
        path: 'integrations/woocommerce',
        loadComponent: () => import('./integrations/woocommerce.component').then(m => m.WooCommerceComponent)
      },
      // Dashboard
      {
        path: '',
        component: DashboardComponent
      },

      // Manage Users
      {
        path: 'manage-users',
        component: ManageUsersComponent,
      },

      // Profile
      {
        path: 'profile',
        component: ProfileComponent
      },

      // Roles and Permissions
      { path: 'manage-roles', redirectTo: 'roles-and-permissions', pathMatch: 'full' },
      {
        path: 'roles-and-permissions',
        component: RolesAndPermissionsComponent
      },

      // Orders - IMPORTANT: Path order matters!
      {
        path: 'orders',
        canActivate: [ordersGuard],

        children: [
          // /dashboard/orders
          {
            path: '',
            component: ManageOrdersComponent
          },

          // /dashboard/orders/add
          {
            path: 'add',
            loadComponent: () =>
              import('./orders/order-form/order-form.component')
                .then(m => m.OrderFormComponent),
            canActivate: [ordersAddGuard]
          },

          // Keep old edit links on the details page; editing uses its modal.
          {
            path: ':id/edit',
            redirectTo: ':id',
            pathMatch: 'full'
          },

          // /dashboard/orders/:id
          {
            path: ':id',
            loadComponent: () =>
              import('./orders/order-details/order-details.component')
                .then(m => m.OrderDetailsComponent)
          }
        ]
      }
    ]
  },

  {
    path: '**',
    redirectTo: 'login'
  }
];
