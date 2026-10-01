import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { LookupService } from '../orders/lookup.service';
import { CustomerOption, LookupItem } from '../orders/order.models';
import { WooSelectComponent } from './woo-select.component';

interface Store {
  id: number; ownerUserId: number; storeName: string; storeUrl: string; isActive: boolean;
  lastSyncAt: string | null; pluginVersion: string; orderCount: number;
  defaultGenderId: number; defaultMaterialId: number; defaultStatusId: number;
  statusMappingsJson: string; mappings: Record<string, string>;
}

@Component({
  selector: 'app-woocommerce',
  standalone: true, imports: [CommonModule, FormsModule, WooSelectComponent],
  templateUrl: './woocommerce.component.html',
  styles: [`:host{display:block;background:#f9fafb;min-height:100%;padding:24px;color:#111827}button{cursor:pointer;border:1px solid #d1d5db;border-radius:8px;padding:8px 14px;font-size:14px;font-weight:500;transition:background .15s}button:hover{filter:brightness(.97)}button:focus-visible{outline:2px solid #6b7280;outline-offset:2px}button:disabled{opacity:.5;cursor:wait}label{display:block;font-size:14px;font-weight:500;color:#374151}input[type=checkbox]{accent-color:#1f2937;margin-right:6px}section{background:white;border-radius:12px;box-shadow:0 1px 2px #0000000d;outline:1px solid #e5e7eb;padding:24px;margin:16px 0}h2{font-size:16px;font-weight:600;margin-bottom:8px}th{font-size:12px;text-transform:uppercase;letter-spacing:.03em;color:#6b7280;background:#f9fafb}td{color:#4b5563}@media(max-width:640px){:host{padding:16px}section{padding:16px}}`]
})
export class WooCommerceComponent implements OnInit {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private lookup = inject(LookupService);
  auth = inject(AuthService);
  private base = `${environment.apiUrl}/integrations/woocommerce`;
  busy = signal(false); error = signal(''); message = signal('');
  stores = signal<Store[]>([]);
  review = signal<{storeName: string; storeUrl: string; expiresAt: string} | null>(null);
  code = this.route.snapshot.queryParamMap.get('code') ?? '';
  genders: LookupItem[] = []; materials: LookupItem[] = []; statuses: LookupItem[] = []; owners: CustomerOption[] = [];
  ownerUserId: number | null = null; defaultGenderId = 0; defaultMaterialId = 0; defaultStatusId = 0;
  storeUrl = '';
  openStore() {
    try {
      const url = new URL(this.storeUrl.includes('://') ? this.storeUrl.trim() : 'https://' + this.storeUrl.trim());
      if (!['https:', 'http:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) throw new Error();
      url.pathname = url.pathname.replace(/\/$/, '') + '/wp-admin/admin.php';
      url.search = 'page=oms-woocommerce';
      window.location.assign(url.toString());
    } catch { this.error.set('Enter a valid store address, such as https://your-store.com.'); }
  }
   disconnecting = signal<Store | null>(null);
  get ownerOptions() { return this.owners.map(o => ({id: o.id, name: `${o.name} — ${o.email}`})); }
  get admin() { return ['Admin', 'Super Admin'].includes(this.auth.currentRole() ?? ''); }
  async ngOnInit() {
    await this.run(async () => {
      const [g, m, s] = await Promise.all([firstValueFrom(this.lookup.getByType(3)), firstValueFrom(this.lookup.getByType(4)), firstValueFrom(this.lookup.getByType(1))]);
      this.genders = g; this.materials = m; this.statuses = s;
      this.defaultGenderId = g[0]?.id ?? 0; this.defaultMaterialId = m[0]?.id ?? 0;
      this.defaultStatusId = s.find(x => x.name.toLowerCase() === 'new')?.id ?? s.find(x => x.name.toLowerCase() === 'assign')?.id ?? 0;
      if (!this.defaultGenderId || !this.defaultMaterialId || !this.defaultStatusId) throw {error: {message: 'Your administrator needs to configure order defaults before you can connect.'}};
      if (this.admin) this.owners = await firstValueFrom(this.http.get<CustomerOption[]>(`${this.base}/owners`));
      if (this.code) this.review.set(await firstValueFrom(this.http.get<{storeName: string; storeUrl: string; expiresAt: string}>(`${this.base}/authorize/review`, {params: {code: this.code}})));
      await this.load();
    });
  }
  async run(action: () => Promise<void>) {
    if (this.busy()) return;
    this.busy.set(true); this.error.set(''); this.message.set('');
    try { await action(); }
    catch (e: any) { this.error.set(e.status === 404 ? 'Integration unavailable or authorization expired. Ask the administrator to enable WooCommerce after applying its migration.' : e.error?.message ?? e.error?.detail ?? 'Request failed. Check your connection and try again.'); }
    finally { this.busy.set(false); }
  }
  private async load() {
    const stores = await firstValueFrom(this.http.get<Store[]>(`${this.base}/connections`));
    this.stores.set(stores);
  }
  refresh() { return this.run(() => this.load()); }
  approve() { return this.run(async () => {
    await firstValueFrom(this.http.post(`${this.base}/authorize/approve`, {
      code: this.code, ownerUserId: this.ownerUserId, defaultGenderId: this.defaultGenderId,
      defaultMaterialId: this.defaultMaterialId, defaultStatusId: this.defaultStatusId
    }));
    this.review.set(null); this.code = '';
    await this.router.navigate([], {relativeTo: this.route, queryParams: {}, replaceUrl: true});
    await this.load(); this.message.set('Store authorized. Return to WordPress and click Finish connection.');
  }); }
  disconnect(s: Store) { return this.run(async () => {
    await firstValueFrom(this.http.post(`${this.base}/connections/${s.id}/disconnect`, {}));
    this.disconnecting.set(null); await this.load(); this.message.set('Disconnected. Existing orders retained.');
  }); }

}
