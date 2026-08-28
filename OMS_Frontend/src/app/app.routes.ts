import { Routes } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { LayoutComponent } from './layout/layout.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { adminOrSuperAdminGuard, authGuard, superAdminGuard } from './guards/auth.guard';
import { ManageUsersComponent } from './manage-user/manage-users.component';
import { RegisterComponent } from './register/register.component';
<<<<<<< Updated upstream
import { AccountPendingComponent } from './account-pending/account-pending.component';
 
=======
import { ProfileComponent } from './profile/profile.component';
import { ManageRolesComponent } from './manage-roles/manage-roles.component';
import { permissionGuard } from './guards/permission.guard';

>>>>>>> Stashed changes
export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'account-pending', component: AccountPendingComponent },
  { path: 'forgot-password', component: ForgotPasswordComponent },
  { path: 'reset-password', component: ResetPasswordComponent },
  { path: 'manage-users', component: ManageUsersComponent, canActivate: [adminOrSuperAdminGuard] },
 
  // Everything under /dashboard shares the sidebar layout and requires login
  {
<<<<<<< Updated upstream
  path: 'dashboard',
  component: LayoutComponent,
  canActivate: [authGuard],
  children: [
    { path: '', component: DashboardComponent },
    { path: 'manage-users', component: ManageUsersComponent, canActivate: [adminOrSuperAdminGuard] },
  ],
},
  { path: '**', redirectTo: 'login' },
];
=======
    path: 'dashboard',
    component: LayoutComponent,
    children: [
      {
        path: '',
        component: DashboardComponent,
        canActivate: [permissionGuard],
        data: { screenKey: 'Dashboard' }
      },
      {
        path: 'manage-users',
        component: ManageUsersComponent,
        canActivate: [permissionGuard],
        data: { screenKey: 'Manage Users' }
      },
      {
        path: 'manage-roles',
        component: ManageRolesComponent,
        canActivate: [permissionGuard],
        data: { screenKey: 'Manage Roles' }
      },
      { path: 'profile', component: ProfileComponent }
    ]
  },

  { path: '**', redirectTo: 'login' }

];
>>>>>>> Stashed changes
