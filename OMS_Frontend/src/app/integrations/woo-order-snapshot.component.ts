import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-woo-order-snapshot', standalone: true, imports: [CommonModule],
  styles: [':host { display: block; }'],
  template: `
    <section class="bg-white rounded-xl shadow-sm ring-1 ring-gray-200 p-4 sm:p-5">
      <h2 class="text-base font-semibold text-gray-900">{{provider}} Order Information</h2>
      <p *ngIf="error()" role="alert">{{ error() }}</p>
      <ng-container *ngIf="data() as d">
        <p class="text-sm text-gray-700 mt-2">{{d.storeName}} · #{{d.externalOrderId}} · {{d.externalStatus}} · {{d.total | number:'1.2-4'}} {{d.currency}}</p>
        <p class="text-sm text-gray-700 mt-2">Buyer: {{d.buyer.name}} · {{d.buyer.email}} · {{d.buyer.phone}}</p>
        <p class="text-sm text-gray-500">Payment method: {{d.paymentMethod}} · Store updated {{d.modifiedAt | date:'medium'}}</p>
        <p class="mt-3 text-xs font-medium uppercase tracking-wide text-gray-400">Production unit {{d.unitNumber}} · Product line {{d.externalLineId}}</p><p *ngIf="!d.isCurrentUnit" class="mt-2 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">Quantity changed in {{provider}}. This production row is retained for review.</p><div class="overflow-auto mt-4 rounded-lg ring-1 ring-gray-200"><table class="w-full text-left text-sm"><thead class="bg-gray-50 text-xs font-medium uppercase tracking-wide text-gray-400"><tr><th class="px-3 py-3">Product / SKU</th><th class="px-3 py-3">Quantity</th><th class="px-3 py-3">Line total</th><th class="px-3 py-3">Attributes</th></tr></thead>
          <tbody><tr *ngFor="let item of items()" class="border-t border-gray-100 text-gray-700">
            <td class="px-3 py-3">{{item.Name}}<br><span class="text-gray-500">{{item.Sku}} · Product {{item.ProductId}} / Variation {{item.VariationId}}</span>
            <div class="flex flex-wrap gap-2 mt-2"><a *ngFor="let url of imageUrls(item)" [href]="url" target="_blank" rel="noopener noreferrer"><img [src]="url" [alt]="item.Name" loading="lazy" referrerpolicy="no-referrer" class="h-20 w-20 rounded-lg object-cover ring-1 ring-gray-200"><span class="text-xs underline">View full image</span></a></div></td>
            <td class="px-3 py-3">{{item.Quantity}}</td><td class="px-3 py-3">{{item.Total | number:'1.2-4'}} {{d.currency}}</td><td class="px-3 py-3">{{item.Attributes}}</td>
          </tr></tbody></table></div>
        <details class="mt-3 text-sm"><summary>Billing and shipping snapshot</summary><div class="grid sm:grid-cols-2 gap-4 mt-2"><div><strong>Billing</strong><p *ngFor="let line of billing()">{{line}}</p></div><div><strong>Shipping</strong><p *ngFor="let line of shipping()">{{line}}</p></div></div></details>
        <p class="text-xs text-gray-500 mt-3">This row represents one production unit. Quantities and retail totals above refer to the complete {{provider}} order; do not add these totals across unit rows.</p>
      </ng-container>
    </section>`
})
export class WooOrderSnapshotComponent implements OnInit {
  @Input({required: true}) orderId!: number;
  @Input() provider = 'WooCommerce';
  private http = inject(HttpClient);
  data = signal<any>(null); items = signal<any[]>([]); billing = signal<string[]>([]); shipping = signal<string[]>([]); error = signal('');
  safeImage(url: string) { try { return new URL(url).protocol === 'https:'; } catch { return false; } }
  imageUrls(item: any): string[] { return [...new Set<string>([item.ImageUrl, ...(item.ImageUrls ?? [])].filter(url => this.safeImage(url)))]; }
  ngOnInit() {
    this.http.get<any>(`${environment.apiUrl}/integrations/${this.provider === 'Shopify' ? 'shopify' : 'woocommerce'}/orders/${this.orderId}`).subscribe({
      next: d => {
        this.data.set(d); this.items.set(JSON.parse(d.itemsJson));
        const format = (raw: string) => Object.values(JSON.parse(raw)).filter(v => typeof v === 'string' && v) as string[];
        this.billing.set(format(d.billingJson)); this.shipping.set(format(d.shippingJson));
      }, error: () => this.error.set('Unable to load store details for this account.')
    });
  }
}

