import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { LookupService } from '../orders/lookup.service';
import { WooSelectComponent } from './woo-select.component';

@Component({selector: 'app-shopify', standalone:true,imports:[CommonModule,FormsModule,WooSelectComponent],template:`
<div class="min-h-screen bg-gray-50 p-4 sm:p-6"><div class="max-w-2xl mx-auto space-y-4">
 <header><p class="text-sm text-gray-500">Settings / Integrations</p><h1 class="text-2xl font-bold text-gray-900">Shopify</h1><p class="mt-2 text-gray-600">Connect your store to bring orders into OMS automatically.</p></header>
 <p *ngIf="busy()" role="status">Please wait…</p>
 <p *ngIf="error()" role="alert" class="rounded-lg bg-red-50 p-4 text-sm text-red-700">{{error()}} <button (click)="load()" [disabled]="busy()">Try again</button></p>
 <p *ngIf="message()" role="status" class="rounded-lg bg-green-50 p-4 text-sm text-green-700">{{message()}}</p>
 <section *ngIf="!busy() && !ready && !error()"><h2>Shopify is not available yet</h2><p class="mt-2 text-sm text-gray-600">Ask your OMS administrator to enable Shopify, then return here to connect.</p></section>
 <section><h2>Connect your store</h2><p class="mt-2 text-sm text-gray-600">Enter your Shopify store address, then approve the connection in Shopify.</p>
 <p *ngIf="publicUrl && publicOriginMismatch" class="text-sm mt-3"><a class="underline" [href]="publicUrl">Continue to your secure OMS page to connect</a></p>
 <form (ngSubmit)="connect()" class="space-y-4 mt-4">
 <label>Store address<input name="shop" [(ngModel)]="shop" placeholder="your-store.myshopify.com" autocomplete="url" required></label>
 <label *ngIf="admin">Connect for customer<app-woo-select name="owner" [(ngModel)]="owner" [options]="owners" [emptyValue]="null" label="Customer" placeholder="Select customer"></app-woo-select></label>
 <button type="submit" class="primary" [disabled]="busy()||!ready||!shop.trim()||!gender||!material||!status||publicOriginMismatch||(admin&&!owner)">{{busy() ? 'Please wait…' : 'Connect Shopify'}}</button>
 <p class="text-xs text-gray-500">Import defaults are applied automatically. You can review product details in Orders.</p>
 </form></section>
 <section *ngFor="let s of stores()"><div class="flex flex-wrap justify-between gap-3"><div><h2 class="break-all">{{s.shop}}</h2><p class="mt-2 text-sm text-gray-500">Last synced {{s.lastSyncAt ? (s.lastSyncAt|date:'medium') : 'Not yet'}}</p></div><span class="text-sm" [class.text-green-700]="s.isActive">{{s.isActive?'Connected':'Disconnected'}}</span></div>
 <p class="mt-3 text-sm text-gray-600">{{s.orderCount}} imported production rows</p>
 <p class="mt-2 text-sm text-gray-600">Imported orders belong to the customer account selected when connecting. That customer must review and Assign them before they appear for administrators.</p>
 <button class="mt-4" (click)="load()" [disabled]="busy()">Refresh status</button>
 <p *ngIf="s.lastError" class="mt-3 text-sm text-amber-800">{{s.lastError}}</p>
 <button *ngIf="s.isActive" class="mt-4" (click)="disconnectId=s.id" [disabled]="busy()">Disconnect</button>
 <button *ngIf="!s.isActive" class="mt-4" (click)="shop=s.shop" [disabled]="busy()">Use this store address</button>
 <div *ngIf="disconnectId===s.id" class="mt-3 rounded-lg bg-gray-50 p-3 text-sm"><p class="mb-3">Disconnect this store? Existing orders will stay in OMS. Uninstall the app in Shopify to revoke access.</p><button (click)="disconnect(s)" [disabled]="busy()">Disconnect store</button> <button (click)="disconnectId=null">Cancel</button></div></section>
</div></div>`,styles:[`section{background:white;border-radius:12px;box-shadow:0 1px 2px #0000000d;outline:1px solid #e5e7eb;padding:24px}h2{font-size:16px;font-weight:600;color:#111827}label{display:block;font-size:14px;font-weight:500;color:#374151}input:not([type=checkbox]){display:block;margin-top:6px;width:100%;padding:8px 12px;border:1px solid #d1d5db;border-radius:8px;font-size:14px}button{padding:8px 14px;border:1px solid #d1d5db;border-radius:8px;font-size:14px;cursor:pointer;background:white}button:hover{background:#f9fafb}button:disabled{opacity:.5;cursor:wait}.primary{background:#1f2937;color:white}.primary:hover{background:#111827}td,th{padding:10px}button:focus-visible,input:focus-visible{outline:2px solid #6b7280;outline-offset:2px}`]})
export class ShopifyComponent {
 private http=inject(HttpClient);private lookup=inject(LookupService);private auth=inject(AuthService);private base=environment.apiUrl+'/integrations/shopify';
 busy=signal(false);error=signal('');message=signal('');stores=signal<any[]>([]);
 get publicOriginMismatch(){return !!this.publicUrl && new URL(this.publicUrl).origin !== window.location.origin;}
 publicUrl='';ready=false;shop='';owner:number|null=null;gender=0;material=0;status=0;genders:any[]=[];materials:any[]=[];statuses:any[]=[];owners:any[]=[];disconnectId:number|null=null;
 get admin(){return ['Super Admin','Admin'].includes(this.auth.currentRole()??'');}
 constructor(){this.load();}
 async run(fn:()=>Promise<void>){if(this.busy())return;this.busy.set(true);this.error.set('');try{await fn();}catch(e:any){this.error.set(e.error?.message??e.error?.title??'Shopify integration unavailable. Check server setup and connection.');}finally{this.busy.set(false);}}
 load(){return this.run(async()=>{const [g,m,s,c,stores]=await Promise.all([firstValueFrom(this.lookup.getByType(3)),firstValueFrom(this.lookup.getByType(4)),firstValueFrom(this.lookup.getByType(1)),firstValueFrom(this.http.get<any>(this.base+'/configuration')),firstValueFrom(this.http.get<any[]>(this.base+'/stores'))]);this.genders=g;this.materials=m;this.statuses=s;this.gender=g[0]?.id??0;this.material=m[0]?.id??0;this.status=s.find(x=>x.name.toLowerCase()==='new')?.id??s.find(x=>x.name.toLowerCase()==='assign')?.id??0;if(!this.gender||!this.material||!this.status)throw {error:{message:'Your administrator needs to configure order defaults before you can connect.'}};this.ready=c.ready;this.publicUrl=c.frontendUrl?.startsWith('https://')?c.frontendUrl+'/dashboard/integrations/shopify':'';this.stores.set(stores);if(this.admin)this.owners=await firstValueFrom(this.http.get<any[]>(this.base+'/owners'));});}
 connect(){return this.run(async()=>{const r=await firstValueFrom(this.http.post<any>(this.base+'/connect',{shop:this.shop.trim().replace(/^https?:\/\//i,'').replace(/\/$/,'').toLowerCase(),ownerUserId:this.owner,genderId:this.gender,materialId:this.material,statusId:this.status}));if(r.url)window.location.assign(r.url);else {this.message.set('Shopify connected. Your orders will sync automatically.');this.stores.set(await firstValueFrom(this.http.get<any[]>(this.base+'/stores')));}});}
 disconnect(s:any){return this.run(async()=>{await firstValueFrom(this.http.post(this.base+`/stores/${s.id}/disconnect`,{}));s.isActive=false;this.disconnectId=null;});}
}

