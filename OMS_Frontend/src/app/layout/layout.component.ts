import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './layout.component.html',
})
export class LayoutComponent {
  sidebarOpen = signal(true);
  user: any;
  isAdminOrSuperAdmin = false;
  profileMenuOpen = false;

  constructor(private authService: AuthService, private router: Router) {
    this.user = this.authService.getCurrentUser();
    const role = this.authService.currentRole();
    this.isAdminOrSuperAdmin = role === 'Super Admin' || role === 'Admin';
  }

  toggleSidebar(): void {
    this.sidebarOpen.update((v) => !v);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  initials(): string {
    if (!this.user) return '?';
    return `${this.user.firstName?.[0] ?? ''}${this.user.lastName?.[0] ?? ''}`.toUpperCase();
  }
}