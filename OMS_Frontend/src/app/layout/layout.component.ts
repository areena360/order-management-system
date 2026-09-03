import { Component, HostListener, signal, computed } from '@angular/core';
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
  profileMenuOpen = false;

  // Mobile screen detection
  isMobile = signal(window.innerWidth < 640);

  user;
  isAdminOrSuperAdmin;
  initials;

  constructor(
    private authService: AuthService,
    private router: Router,
    public perm: PermissionService
  ) {
    this.user = this.authService.currentUser;
    this.perm.load().subscribe();

    this.isAdminOrSuperAdmin = computed(() => {
      const role = this.authService.currentRole();
      return role === 'Super Admin' || role === 'Admin';
    });

    this.initials = computed(() => {
      const u = this.user();
      return u
        ? `${u.firstName?.[0] ?? ''}${u.lastName?.[0] ?? ''}`.toUpperCase()
        : '?';
    });
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

  @HostListener('window:resize')
  onResize(): void {
    this.isMobile.set(window.innerWidth < 640);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;

    if (!target.closest('.profile-menu-container')) {
      this.profileMenuOpen = false;
    }
  }
}