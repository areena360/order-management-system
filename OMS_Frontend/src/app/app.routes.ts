import { Routes } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { LayoutComponent } from './layout/layout.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { adminOrSuperAdminGuard, authGuard, superAdminGuard } from './guards/auth.guard';
import { ManageUsersComponent } from './manage-user/manage-users.component';
import { RegisterComponent } from './register/register.component';
import { AccountPendingComponent } from './account-pending/account-pending.component';
 
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
