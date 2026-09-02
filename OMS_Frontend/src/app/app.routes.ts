import { Routes } from '@angular/router';

import { LoginComponent } from './login/login.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { LayoutComponent } from './layout/layout.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import {
  adminOrSuperAdminGuard,
  authGuard,
  superAdminGuard
} from './guards/auth.guard';
import { ManageUsersComponent } from './manage-user/manage-users.component';
import { RegisterComponent } from './register/register.component';
import { ProfileComponent } from './profile/profile.component';
import { ManageRolesComponent } from './manage-roles/manage-roles.component';

export const routes: Routes = [

  { path: '', redirectTo: 'login', pathMatch: 'full' },

  { path: 'login', component: LoginComponent },

  { path: 'register', component: RegisterComponent },

  { path: 'forgot-password', component: ForgotPasswordComponent },

  { path: 'reset-password', component: ResetPasswordComponent },

  // Everything under /dashboard shares the sidebar + topbar layout
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

      {
        path: 'manage-roles',
        component: ManageRolesComponent
      }

    ]
  },

  { path: '**', redirectTo: 'login' }

];