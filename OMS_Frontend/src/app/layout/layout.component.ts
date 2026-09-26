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

  /* =========================================================
   * Sidebar
   * ======================================================= */
  sidebarOpen = signal(true);
  isMobile = signal(false);

  constructor() {
    this.updateIsMobile();
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

  /**
   * Universal permission check — tries multiple AuthService APIs
   * and falls back gracefully.
   */
  canView(permission: string): boolean {
    const auth = this.auth as any;

    if (typeof auth.hasPermission === 'function')      return !!auth.hasPermission(permission);
    if (typeof auth.can === 'function')                return !!auth.can(permission);
    if (typeof auth.hasViewPermission === 'function')  return !!auth.hasViewPermission(permission);
    if (typeof auth.canView === 'function')            return !!auth.canView(permission);

    const permsSignal = auth.permissions;
    const perms =
      (typeof permsSignal === 'function' ? permsSignal() : permsSignal) ??
      auth.currentUser?.()?.permissions ??
      auth.currentUser?.()?.viewPermissions ??
      null;

    if (Array.isArray(perms)) return perms.includes(permission);

    const role = auth.currentRole?.() ?? auth.currentUser?.()?.role;
    if (role === 'Admin' || role === 'Super Admin') return true;

    return true;
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