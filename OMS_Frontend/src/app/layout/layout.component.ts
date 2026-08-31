import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
  Router
} from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { PermissionService } from '../auth/permission.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet
  ],
  templateUrl: './layout.component.html',
})
export class LayoutComponent {
  sidebarOpen = signal(true);
  user: any;

  isAdminOrSuperAdmin = false;
  profileMenuOpen = false;

  constructor(
    private authService: AuthService,
    private router: Router,
    public perm: PermissionService
  ) {
    this.user = this.authService.getCurrentUser();
    this.perm.load().subscribe();
  }

  get isSuperAdmin(): boolean {
    return this.authService.currentRole() === 'Super Admin';
  }

  canView(key: string): boolean {
    return this.isSuperAdmin || this.perm.canView(key);
  }

  toggleSidebar(): void {
    this.sidebarOpen.update((v) => !v);
  }

  logout(): void {
    this.profileMenuOpen = false;
    this.perm.reset();
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  initials(): string {
    if (!this.user) return '?';

    return `${this.user.firstName?.[0] ?? ''}${this.user.lastName?.[0] ?? ''}`
      .toUpperCase();
  }
}

