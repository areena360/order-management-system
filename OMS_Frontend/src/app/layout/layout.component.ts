<<<<<<< Updated upstream
import { Component, HostListener, signal } from '@angular/core';
=======
import { Component, signal, computed } from '@angular/core';
>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
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
=======
  profileMenuOpen = false;

  user;
  isAdminOrSuperAdmin;
  initials;

  constructor(private authService: AuthService, private router: Router) {
    this.user = this.authService.currentUser;

    this.isAdminOrSuperAdmin = computed(() => {
      const role = this.authService.currentRole();
      return role === 'Super Admin' || role === 'Admin';
    });

    this.initials = computed(() => {
      const u = this.user();
      return u ? `${u.firstName?.[0] ?? ''}${u.lastName?.[0] ?? ''}`.toUpperCase() : '?';
    });
>>>>>>> Stashed changes
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
<<<<<<< Updated upstream

  @HostListener('document:click', ['$event'])
onDocumentClick(event: MouseEvent) {
  const target = event.target as HTMLElement;

  if (!target.closest('.profile-menu-container')) {
    this.profileMenuOpen = false;
  }
}

  initials(): string {
    if (!this.user) return '?';

    return `${this.user.firstName?.[0] ?? ''}${this.user.lastName?.[0] ?? ''}`
      .toUpperCase();
  }
}

=======
}
>>>>>>> Stashed changes
