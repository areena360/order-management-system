import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RolesService } from './roles.service';
import { environment } from '../../environments/environment';
import { ManageUsersComponent } from '../manage-user/manage-users.component';
import { ManageRolesComponent } from '../manage-roles/manage-roles.component';

describe('Database role catalog', () => {
  let http: HttpTestingController;
  let service: RolesService;
  const url = `${environment.apiUrl}/roles`;
  const catalog = [
    { id: 1, name: 'Super Admin', isActive: true },
    { id: 27, name: 'Auditor', isActive: true, canDelete: true },
    { id: 28, name: 'Inactive role', isActive: false }
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(RolesService);
  });
  afterEach(() => http.verify());

  it('allows custom active roles while excluding inactive and Super Admin roles from assignment', () => {
    service.getAssignableRoles().subscribe(roles => expect(roles).toEqual([catalog[1]]));
    http.expectOne(url).flush(catalog);
  });

  it('includes unassigned roles in the user filter and uses database names for form labels', () => {
    const component = TestBed.runInInjectionContext(() => TestBed.createComponent(ManageUsersComponent).componentInstance);
    component.loadRoles();
    http.expectOne(url).flush(catalog);
    expect(component.roleOptions).toEqual([catalog[1]]);
    expect(component.roles).toContain('Auditor');
    component.form.roleId = 27;
    expect(component.formRoleName()).toBe('Auditor');
  });

  it('shows a role-loading failure and recovers on retry without a fixed fallback', () => {
    const component = TestBed.createComponent(ManageUsersComponent).componentInstance;
    component.loadRoles();
    http.expectOne(url).flush({}, { status: 500, statusText: 'Failed' });
    expect(component.rolesError).toBeTruthy();
    expect(component.roleOptions).toEqual([]);
    component.loadRoles();
    http.expectOne(url).flush(catalog);
    expect(component.rolesError).toBe('');
    expect(component.roleOptions[0].name).toBe('Auditor');
  });

  it('refreshes the catalog after creation and loads the new role permissions', () => {
    const component = TestBed.createComponent(ManageRolesComponent).componentInstance;
    component.newRoleName = 'Auditor';
    component.createRole();
    const request = http.expectOne(url);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Auditor' });
    request.flush(catalog[1]);
    http.expectOne(url).flush(catalog);
    http.expectOne(`${environment.apiUrl}/rolepermissions/27`).flush([]);
    expect(component.selectedRoleName).toBe('Auditor');
    expect(component.roleOptions.some(r => r.name === 'Super Admin')).toBeFalse();
  });

  it('activates a previously inactive role and reloads the catalog', () => {
    const component = TestBed.createComponent(ManageRolesComponent).componentInstance;
    component.selectedRoleId = 28;
    component.roleOptions = catalog;
    expect(component.selectedRoleInactive).toBeTrue();
    component.activateRole();
    const request = http.expectOne(`${url}/28/activate`);
    expect(request.request.method).toBe('PATCH');
    request.flush(null);
    http.expectOne(url).flush(catalog.map(r => ({ ...r, isActive: true })));
    http.expectOne(`${environment.apiUrl}/rolepermissions/28`).flush([]);
    expect(component.selectedRoleInactive).toBeFalse();
  });

  it('renders the inactive-role recovery action and permission controls', () => {
    const fixture = TestBed.createComponent(ManageRolesComponent);
    fixture.detectChanges();
    http.expectOne(url).flush([catalog[2]]);
    http.expectOne(`${environment.apiUrl}/rolepermissions/28`).flush([
      { screenKey: 'Order Amount', canView: true, canAdd: false, canEdit: false, canDelete: false }
    ]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Activate role');
    expect(fixture.nativeElement.textContent).toContain('Order Amount');
    expect(fixture.nativeElement.querySelectorAll('tbody input[type="checkbox"]').length).toBe(2);
  });

  it('requires confirmation and reloads the catalog after deleting a custom role', () => {
    const component = TestBed.createComponent(ManageRolesComponent).componentInstance;
    component.roleOptions = catalog;
    component.selectedRoleId = 27;
    component.openDeleteRole();
    http.expectNone(`${url}/27`);
    expect(component.roleToDelete?.name).toBe('Auditor');
    component.confirmDeleteRole();
    component.confirmDeleteRole();
    component.closeDeleteRole();
    expect(component.roleToDelete).not.toBeNull();
    const request = http.expectOne(`${url}/27`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
    http.expectOne(url).flush([]);
    expect(component.roleToDelete).toBeNull();
    expect(component.roleOptions).toEqual([]);
    expect(component.selectedRoleId).toBeNull();
    expect(component.permissions).toEqual([]);
  });

  it('keeps the role and shows the API explanation when it is assigned to a user', () => {
    const component = TestBed.createComponent(ManageRolesComponent).componentInstance;
    component.roleOptions = catalog;
    component.selectedRoleId = 27;
    component.openDeleteRole();
    component.confirmDeleteRole();
    http.expectOne(`${url}/27`).flush({ message: 'Cannot delete a role that is assigned to users.' },
      { status: 409, statusText: 'Conflict' });
    expect(component.deleteRoleError).toContain('assigned to users');
    expect(component.roleOptions).toContain(catalog[1]);
    expect(component.deletingRole).toBeFalse();
    component.closeDeleteRole();
    expect(component.roleToDelete).toBeNull();
  });

  it('does not offer deletion for protected roles', () => {
    const component = TestBed.createComponent(ManageRolesComponent).componentInstance;
    component.roleOptions = [{ id: 2, name: 'Admin', isActive: true, canDelete: false }];
    component.selectedRoleId = 2;
    component.openDeleteRole();
    expect(component.canDeleteSelectedRole).toBeFalse();
    expect(component.roleToDelete).toBeNull();
  });

  it('keeps custom role colors stable across reloads and distinct for new role IDs', () => {
    const component = TestBed.createComponent(ManageUsersComponent).componentInstance;
    component.allRoles = catalog;
    const auditorColor = component.roleBadgeStyle('Auditor');
    component.allRoles = [...catalog, { id: 29, name: 'Warehouse', isActive: true }].reverse();
    expect(component.roleBadgeStyle('Auditor')).toEqual(auditorColor);
    expect(component.roleBadgeStyle('Warehouse')).not.toEqual(auditorColor);
    expect(auditorColor['background-color']).toContain('hsl');
    expect(component.roleBadgeClass('Admin')).toContain('bg-blue-100');
  });

  for (const modal of ['add', 'edit'] as const) {
    it(`closes the ${modal} user modal only on a backdrop click and not while saving`, () => {
      const fixture = TestBed.createComponent(ManageUsersComponent);
      fixture.detectChanges();
      http.expectOne(url).flush(catalog);
      http.expectOne(`${environment.apiUrl}/users`).flush([]);
      const component = fixture.componentInstance;
      if (modal === 'add') component.openAddModal();
      else component.openEditModal({ id: 20, roleId: 27, role: 'Auditor', firstName: 'Test', lastName: 'User' } as any);
      fixture.detectChanges();
      const backdrop = fixture.nativeElement.querySelector(`[data-user-modal="${modal}"]`) as HTMLElement;
      (backdrop.querySelector('input') as HTMLElement).click();
      expect(modal === 'add' ? component.showAddModal : component.showEditModal).toBeTrue();
      component.saving = true;
      backdrop.click();
      expect(modal === 'add' ? component.showAddModal : component.showEditModal).toBeTrue();
      component.saving = false;
      backdrop.click();
      expect(component.showAddModal || component.showEditModal).toBeFalse();
    });
  }
});
