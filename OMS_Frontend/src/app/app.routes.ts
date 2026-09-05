import { Routes } from '@angular/router';

import { LoginComponent } from './login/login.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { LayoutComponent } from './layout/layout.component';
import { DashboardComponent } from './dashboard/dashboard.component';

import {
  adminOrSuperAdminGuard,
  authGuard,
  ordersGuard,
  ordersAddGuard,
  ordersEditGuard
} from './guards/auth.guard';

import { ManageUsersComponent } from './manage-user/manage-users.component';
import { RegisterComponent } from './register/register.component';
import { ProfileComponent } from './profile/profile.component';
import { ManageRolesComponent } from './manage-roles/manage-roles.component';

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

    children: [

      // Dashboard
      {
        path: '',
        component: DashboardComponent
      },

      // Manage Users
      {
        path: 'manage-users',
        component: ManageUsersComponent,
        canActivate: [adminOrSuperAdminGuard]
      },

      // Profile
      {
        path: 'profile',
        component: ProfileComponent
      },

      // Manage Roles
      {
        path: 'manage-roles',
        component: ManageRolesComponent
      },

      // Orders
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

          // /dashboard/orders/:id/edit
          {
            path: ':id/edit',
            loadComponent: () =>
              import('./orders/order-form/order-form.component')
                .then(m => m.OrderFormComponent),
            canActivate: [ordersEditGuard]
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