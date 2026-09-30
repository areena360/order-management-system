import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../auth/auth.service';
import { LayoutComponent } from '../layout/layout.component';
import { assignedRoleGuard } from './auth.guard';
import { PermissionService } from '../auth/permission.service';
import { of, Subject, throwError } from 'rxjs';

@Component({ standalone: true, template: 'Page' })
class TestPage {}

describe('Permission cache between sessions', () => {
  it('clears existing grants and ignores a previous session response', () => {
    const sessions = new Subject<boolean>();
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(), provideHttpClientTesting(),
      { provide: AuthService, useValue: { sessionChanged$: sessions } }
    ] });
    const permissions = TestBed.inject(PermissionService);
    const http = TestBed.inject(HttpTestingController);
    const grant = [{ screenKey: 'Dashboard', canView: true, canAdd: false, canEdit: false, canDelete: false }];
    permissions.load().subscribe();
    http.expectOne(req => req.url.endsWith('/profile/permissions')).flush(grant);
    expect(permissions.canView('Dashboard')).toBeTrue();
    permissions.load(true).subscribe();
    const oldRequest = http.expectOne(req => req.url.endsWith('/profile/permissions'));
    sessions.next(false);
    expect(permissions.canView('Dashboard')).toBeFalse();
    sessions.next(true);
    const newRequest = http.expectOne(req => req.url.endsWith('/profile/permissions'));
    oldRequest.flush(grant);
    expect(permissions.canView('Dashboard')).toBeFalse();
    newRequest.flush([]);
    expect(permissions.canView('Dashboard')).toBeFalse();
    http.verify();
  });
});

describe('Profile-only access for users without a role', () => {
  let auth: AuthService;
  let allowed: Set<string>;
  let permissions: jasmine.SpyObj<PermissionService>;

  beforeEach(() => {
    allowed = new Set<string>();
    permissions = jasmine.createSpyObj('PermissionService', ['load', 'canView', 'adminAssignedOrdersOnly']);
    permissions.adminAssignedOrdersOnly.and.returnValue(false);
    permissions.load.and.returnValue(of([]));
    permissions.canView.and.callFake(screen => allowed.has(screen));
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(),
        { provide: PermissionService, useValue: permissions }, provideRouter([
        { path: 'login', component: TestPage },
        { path: 'dashboard', canActivateChild: [assignedRoleGuard], children: [
          { path: '', component: TestPage },
          { path: 'profile', component: TestPage },
          { path: 'manage-users', component: TestPage },
          { path: 'roles-and-permissions', component: TestPage },
          { path: 'integrations/shopify', component: TestPage },
          { path: 'integrations/woocommerce', component: TestPage },
          { path: 'orders', children: [
            { path: '', component: TestPage },
            { path: 'add', component: TestPage },
            { path: ':id', component: TestPage }
          ] }
        ] }
      ])]
    });
    auth = TestBed.inject(AuthService);
    spyOn(auth, 'getToken').and.returnValue('test-token');
    auth.currentRole.set('No Role');
  });

  it('redirects direct URLs and subsequent navigation to profile', async () => {
    const harness = await RouterTestingHarness.create();
    for (const path of ['', '/orders', '/orders/add', '/orders/42', '/manage-users',
      '/roles-and-permissions', '/integrations/shopify', '/integrations/woocommerce']) {
      await harness.navigateByUrl('/dashboard' + path);
      expect(TestBed.inject(Router).url).withContext(path).toBe('/dashboard/profile');
    }
  });

  it('allows profile access but hides navigation and prevents opening settings', async () => {
    const harness = await RouterTestingHarness.create('/dashboard/profile');
    expect(TestBed.inject(Router).url).toBe('/dashboard/profile');
    const layout = TestBed.runInInjectionContext(() => new LayoutComponent());
    for (const screen of ['Dashboard', 'Manage Users', 'Manage Roles', 'Orders', 'Settings']) {
      expect(layout.canView(screen)).withContext(screen).toBeFalse();
    }
    layout.openSettings();
    expect(layout.settingsMounted()).toBeFalse();
    expect(harness.routeNativeElement?.textContent).toContain('Page');
  });

  it('treats missing, empty and normalized No Role values as unassigned', () => {
    for (const role of [null, '', ' ', 'No Role', ' no role ']) {
      auth.currentRole.set(role);
      expect(auth.hasAssignedRole()).toBeFalse();
    }
  });

  it('preserves explicitly granted access for assigned roles, including custom roles', async () => {
    allowed.add('Dashboard');
    const harness = await RouterTestingHarness.create();
    for (const role of ['Admin', 'Super Admin', 'Customer', 'Auditor']) {
      auth.currentRole.set(role);
      await harness.navigateByUrl('/dashboard');
      expect(TestBed.inject(Router).url).toBe('/dashboard');
      await harness.navigateByUrl('/dashboard/profile');
    }
  });

  it('keeps an approved user with an unconfigured role on profile', async () => {
    auth.currentRole.set('New Role');
    const harness = await RouterTestingHarness.create();
    for (const path of ['', '/orders', '/orders/add', '/orders/42', '/manage-users',
      '/roles-and-permissions', '/integrations/shopify', '/integrations/woocommerce']) {
      await harness.navigateByUrl('/dashboard' + path);
      expect(TestBed.inject(Router).url).withContext(path).toBe('/dashboard/profile');
    }
    const layout = TestBed.runInInjectionContext(() => new LayoutComponent());
    expect(layout.canView('Dashboard')).toBeFalse();
    expect(layout.canView('Orders')).toBeFalse();
    layout.openSettings();
    expect(layout.settingsMounted()).toBeFalse();
  });

  it('allows only the screens selected by the admin', async () => {
    auth.currentRole.set('New Role');
    allowed.add('Orders');
    const harness = await RouterTestingHarness.create('/dashboard/orders/42');
    expect(TestBed.inject(Router).url).toBe('/dashboard/orders/42');
    await harness.navigateByUrl('/dashboard/manage-users');
    expect(TestBed.inject(Router).url).toBe('/dashboard/profile');
    const layout = TestBed.runInInjectionContext(() => new LayoutComponent());
    expect(layout.canView('Orders')).toBeTrue();
    expect(layout.canView('Dashboard')).toBeFalse();
  });

  it('denies page access when permissions cannot be loaded', async () => {
    auth.currentRole.set('New Role');
    permissions.load.and.returnValue(throwError(() => new Error('Unavailable')));
    await RouterTestingHarness.create('/dashboard');
    expect(TestBed.inject(Router).url).toBe('/dashboard/profile');
  });

  it('requires authentication even for profile', async () => {
    (auth.getToken as jasmine.Spy).and.returnValue(null);
    await RouterTestingHarness.create('/dashboard/profile');
    expect(TestBed.inject(Router).url).toContain('/login?returnUrl=');
  });
});
