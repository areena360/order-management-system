import {
  Component,
  HostListener,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';

import { AuthService } from '../auth/auth.service';
import { PermissionService } from '../auth/permission.service';
import { ShopifyComponent } from '../integrations/shopify.component';
import { WooCommerceComponent } from '../integrations/woocommerce.component';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    ShopifyComponent,
    WooCommerceComponent,
  ],
  templateUrl: './layout.component.html',
})
export class LayoutComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  private permissions = inject(PermissionService);

  /* =========================================================
   * Sidebar
   * ======================================================= */
  sidebarOpen = signal(true);
  isMobile = signal(false);

  constructor() {
    this.updateIsMobile();
    this.permissions.load().subscribe({ error: () => {} });
  }

  @HostListener('window:resize')
  onResize() {
    this.updateIsMobile();
  }

  private updateIsMobile() {
    if (typeof window === 'undefined') return;
    const mobile = window.innerWidth < 768;
    this.isMobile.set(mobile);
    this.sidebarOpen.set(!mobile);
  }

  toggleSidebar() {
    this.sidebarOpen.update(v => !v);
  }

  /* =========================================================
   * Auth / user
   * ======================================================= */
  user = this.auth.currentUser;

  initials = computed(() => {
    const u = this.user();
    if (!u) return '?';
    const f = (u.firstName ?? '').trim().charAt(0);
    const l = (u.lastName ?? '').trim().charAt(0);
    return (f + l).toUpperCase() || u.email?.charAt(0).toUpperCase() || '?';
  });

  canView(permission: string): boolean {
    if (!this.auth.hasAssignedRole()) return false;
    if (permission === 'Settings' && this.permissions.adminAssignedOrdersOnly()) return false;
    return this.permissions.canView(permission === 'Settings' ? 'Orders' : permission);
  }

  logout() {
    this.auth.logout();
    this.router.navigate(['/login']);
  }

  /* =========================================================
   * Profile dropdown
   * ======================================================= */
  profileMenuOpen = false;

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    const target = event.target as HTMLElement;
    if (!target.closest('.profile-menu-container')) {
      this.profileMenuOpen = false;
    }
  }

  /* =========================================================
   * Settings drawer
   * ======================================================= */
  settingsMounted = signal(false);
  settingsOpen = signal(false);
  settingsTab = signal<'shopify' | 'woocommerce'>('shopify');

  private closeTimer?: ReturnType<typeof setTimeout>;

  openSettings(tab?: 'shopify' | 'woocommerce') {
    if (!this.canView('Settings')) return;
    if (tab) this.settingsTab.set(tab);
    if (this.closeTimer) {
      clearTimeout(this.closeTimer);
      this.closeTimer = undefined;
    }
    this.settingsMounted.set(true);
    requestAnimationFrame(() => this.settingsOpen.set(true));
    document.body.style.overflow = 'hidden';
  }

  closeSettings() {
    this.settingsOpen.set(false);
    document.body.style.overflow = '';
    if (this.closeTimer) clearTimeout(this.closeTimer);
    this.closeTimer = setTimeout(() => {
      this.settingsMounted.set(false);
      this.closeTimer = undefined;
    }, 300);
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    if (this.settingsOpen()) this.closeSettings();
  }
}
