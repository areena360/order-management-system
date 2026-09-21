import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { LookupService } from '../orders/lookup.service';
import { WooSelectComponent } from './woo-select.component';

@Component({standalone:true,imports:[CommonModule,FormsModule,WooSelectComponent],template:`
<div class="min-h-screen bg-gray-50 p-4 sm:p-6"><div class="max-w-7xl mx-auto space-y-4">
 <div class="flex items-center justify-between"><div><h1 class="text-2xl font-bold text-gray-900">Shopify</h1><p class="mt-1 text-sm text-gray-500">Connect your store and manage automatic order synchronization.</p></div><button (click)="load()" [disabled]="busy()">Refresh</button></div>
 <p *ngIf="error()" role="alert" class="rounded-lg bg-red-50 p-4 text-sm text-red-700">{{error()}}</p>
 <p *ngIf="message()" role="status" class="rounded-lg bg-green-50 p-4 text-sm text-green-700">{{message()}}</p>
 <section *ngIf="!ready"><h2>App setup required</h2><p class="text-sm text-gray-600">Configure your Shopify app credentials and public HTTPS addresses on the OMS server. No Shopify password or access token needs to be entered here.</p></section>
 <section><h2>Connect a Shopify store</h2><p *ngIf="publicUrl" class="text-sm mt-2 text-gray-600">For installation, open <a class="underline" [href]="publicUrl" rel="noopener noreferrer">the public HTTPS OMS address</a> and sign in there.</p><div class="grid sm:grid-cols-2 lg:grid-cols-3 gap-4 mt-4">
 <label>Store domain<input [(ngModel)]="shop" placeholder="your-store.myshopify.com" autocomplete="off"></label>
 <label *ngIf="admin">OMS customer<app-woo-select [(ngModel)]="owner" [options]="owners" [emptyValue]="null" label="OMS customer" placeholder="Select customer"></app-woo-select></label>
 <label>Default gender<app-woo-select [(ngModel)]="gender" [options]="genders" label="Default gender"></app-woo-select></label>
 <label>Default material<app-woo-select [(ngModel)]="material" [options]="materials" label="Default material"></app-woo-select></label>
 <label>Initial OMS status<app-woo-select [(ngModel)]="status" [options]="statuses" label="Initial OMS status"></app-woo-select></label></div>
 <p class="text-sm text-gray-500 my-4">Each product unit creates a separate OMS row. Review production sizing after import. Shopify will ask you to approve app permissions.</p>
 <button class="primary" (click)="connect()" [disabled]="busy()||!ready||!shop||!gender||!material||!status||(admin&&!owner)">Connect with Shopify</button></section>
 <section *ngFor="let s of stores()"><div class="flex justify-between gap-3"><div><h2>{{s.shop}}</h2><p class="text-sm text-gray-500">{{s.orderCount}} production rows · Last sync {{s.lastSyncAt ? (s.lastSyncAt|date:'medium') : 'Never'}}</p></div><span class="text-sm" [class.text-green-700]="s.isActive">{{s.isActive?'Connected':'Disconnected'}}</span></div>
 <p *ngIf="s.lastError" class="mt-3 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">{{s.lastError}}</p>
 <div class="flex flex-wrap gap-2 mt-4"><button (click)="sync(s)" [disabled]="busy()||!s.isActive">Import / sync last 30 days</button><button (click)="logsFor(s)">View logs</button><button (click)="disconnectId=s.id">Disconnect</button></div>
 <div *ngIf="disconnectId===s.id" class="mt-3 rounded-lg bg-gray-50 p-3 text-sm"><p>Stop sync for this store? Existing OMS orders remain. Uninstall the app in Shopify to revoke Shopify access.</p><button (click)="disconnect(s)">Confirm disconnect</button> <button (click)="disconnectId=null">Cancel</button></div>
 <details class="mt-5"><summary class="cursor-pointer text-sm font-semibold">Import defaults and fulfillment settings</summary><div class="grid sm:grid-cols-3 gap-4 mt-4">
 <label>Gender<app-woo-select [(ngModel)]="s.defaultGenderId" [options]="genders" label="Gender"></app-woo-select></label>
 <label>Material<app-woo-select [(ngModel)]="s.defaultMaterialId" [options]="materials" label="Material"></app-woo-select></label>
 <label>Initial status<app-woo-select [(ngModel)]="s.defaultStatusId" [options]="statuses" label="Status"></app-woo-select></label></div>
 <label class="flex items-center gap-2 mt-4"><input type="checkbox" [(ngModel)]="s.fulfillmentEnabled"> Enable Shopify fulfillment and tracking updates</label>
 <p class="text-sm text-gray-500 mt-2">When every current unit reaches the selected status, OMS fulfills the merchant-managed Shopify items. Customer notification is disabled. Payments, refunds and cancellations remain manual.</p>
 <label class="mt-3">OMS shipped status<app-woo-select [(ngModel)]="s.shippedStatusId" [options]="statuses" [emptyValue]="null" label="Shipped status"></app-woo-select></label><button class="mt-4" (click)="save(s)" [disabled]="busy()">Save settings</button></details>
 <div class="flex items-end gap-2 mt-4"><label>Shopify order ID<input type="number" min="1" [(ngModel)]="s.retryId"></label><button (click)="retry(s)" [disabled]="busy()||!s.retryId">Retry order</button></div></section>
 <section *ngIf="logs().length"><h2>Recent sync jobs</h2><div class="overflow-auto"><table class="w-full text-left text-sm"><thead class="bg-gray-50 text-xs text-gray-500 uppercase"><tr><th>Topic</th><th>Order / resource</th><th>State</th><th>Attempts</th><th>Message</th></tr></thead><tbody><tr *ngFor="let l of logs()" class="border-t border-gray-100"><td>{{l.topic}}</td><td>{{l.externalId}}</td><td>{{l.completedAt?'Complete':'Retry pending'}}</td><td>{{l.attempts}}</td><td>{{l.error||'—'}}<div *ngIf="l.topic==='customers/data_request' && !l.completedAt"><button (click)="exportPrivacy(l)">Download data</button><button (click)="privacyResolveId=l.id">Mark resolved</button><div *ngIf="privacyResolveId===l.id">Confirm data was delivered to the verified requester.<button (click)="resolvePrivacy(l)">Confirm resolution</button><button (click)="privacyResolveId=null">Cancel</button></div></div></td></tr></tbody></table></div></section>
</div></div>`,styles:[`section{background:white;border-radius:12px;box-shadow:0 1px 2px #0000000d;outline:1px solid #e5e7eb;padding:24px}h2{font-size:16px;font-weight:600;color:#111827}label{display:block;font-size:14px;font-weight:500;color:#374151}input:not([type=checkbox]){display:block;margin-top:6px;width:100%;padding:8px 12px;border:1px solid #d1d5db;border-radius:8px;font-size:14px}button{padding:8px 14px;border:1px solid #d1d5db;border-radius:8px;font-size:14px;cursor:pointer;background:white}button:hover{background:#f9fafb}button:disabled{opacity:.5;cursor:wait}.primary{background:#1f2937;color:white}.primary:hover{background:#111827}td,th{padding:10px}button:focus-visible,input:focus-visible{outline:2px solid #6b7280;outline-offset:2px}`]})
export class ShopifyComponent {
 private http=inject(HttpClient);private lookup=inject(LookupService);private auth=inject(AuthService);private base=environment.apiUrl+'/integrations/shopify';
 busy=signal(false);error=signal('');message=signal('');stores=signal<any[]>([]);logs=signal<any[]>([]);
 publicUrl='';logStoreId=0;privacyResolveId:number|null=null;ready=false;shop='';owner:number|null=null;gender=0;material=0;status=0;genders:any[]=[];materials:any[]=[];statuses:any[]=[];owners:any[]=[];disconnectId:number|null=null;
 get admin(){return ['Super Admin','Admin'].includes(this.auth.currentRole()??'');}
 constructor(){this.load();}
 async run(fn:()=>Promise<void>){if(this.busy())return;this.busy.set(true);this.error.set('');try{await fn();}catch(e:any){this.error.set(e.error?.message??e.error?.title??'Shopify integration unavailable. Check server setup and connection.');}finally{this.busy.set(false);}}
 load(){return this.run(async()=>{const [g,m,s,c,stores]=await Promise.all([firstValueFrom(this.lookup.getByType(3)),firstValueFrom(this.lookup.getByType(4)),firstValueFrom(this.lookup.getByType(1)),firstValueFrom(this.http.get<any>(this.base+'/configuration')),firstValueFrom(this.http.get<any[]>(this.base+'/stores'))]);this.genders=g;this.materials=m;this.statuses=s;this.ready=c.ready;this.publicUrl=c.frontendUrl?.startsWith('https://')?c.frontendUrl+'/dashboard/integrations/shopify':'';this.stores.set(stores);if(this.admin)this.owners=await firstValueFrom(this.http.get<any[]>(this.base+'/owners'));});}
 connect(){return this.run(async()=>{const r=await firstValueFrom(this.http.post<any>(this.base+'/connect',{shop:this.shop,ownerUserId:this.owner,genderId:this.gender,materialId:this.material,statusId:this.status}));if(r.url)window.location.assign(r.url);else {this.message.set('Shopify connected directly. Import can now run locally.');await this.load();}});}
 sync(s:any){return this.run(async()=>{await firstValueFrom(this.http.post(this.base+`/stores/${s.id}/sync`,{}));this.message.set('Import queued. Jobs run automatically while OMS is running.');});}
 retry(s:any){return this.run(async()=>{await firstValueFrom(this.http.post(this.base+`/stores/${s.id}/retry/${s.retryId}`,{}));this.message.set('Order queued for retry.');});}
 logsFor(s:any){return this.run(async()=>{this.logStoreId=s.id;this.logs.set(await firstValueFrom(this.http.get<any[]>(this.base+`/stores/${s.id}/logs`)));});}
 exportPrivacy(l:any){return this.run(async()=>{const blob=await firstValueFrom(this.http.get(this.base+`/stores/${this.logStoreId}/privacy/${l.id}`,{responseType:'blob'}));const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=`shopify-privacy-${l.id}.json`;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);});}
 resolvePrivacy(l:any){return this.run(async()=>{await firstValueFrom(this.http.post(this.base+`/stores/${this.logStoreId}/privacy/${l.id}/resolve`,{}));l.completedAt=new Date().toISOString();this.privacyResolveId=null;this.message.set('Privacy request marked resolved.');});}
 save(s:any){return this.run(async()=>{await firstValueFrom(this.http.put(this.base+`/stores/${s.id}/settings`,{genderId:s.defaultGenderId,materialId:s.defaultMaterialId,statusId:s.defaultStatusId,fulfillmentEnabled:s.fulfillmentEnabled,shippedStatusId:s.shippedStatusId}));this.message.set('Shopify settings saved.');});}
 disconnect(s:any){return this.run(async()=>{await firstValueFrom(this.http.post(this.base+`/stores/${s.id}/disconnect`,{}));s.isActive=false;this.disconnectId=null;});}
}

