import {
  Component, OnInit, OnDestroy, HostListener, ViewChild, ElementRef,
  ChangeDetectorRef, inject
} from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { catchError, from, map, mergeMap, of, toArray } from 'rxjs';
import { FooterComponent } from "../footer/footer.component";
import { AuthService } from '../auth/auth.service';
import { PermissionService } from '../auth/permission.service';

type UserStatus = 'Pending' | 'Approved' | 'Rejected';

export interface AppUser {
  id: number;
  firstName: string;
  lastName: string;
  firstContact: string;
  secondContact: string;
  email: string;
  homeAddress: string;
  officeAddress: string;
  websiteUrl: string;
  roleId: number | null;
  role: string;
  isActive: boolean;
  isDeleted: boolean;
  status: UserStatus;
  createdDate: string;
  createdBy: string;
  updatedDate: string;
  updatedBy: string;
}

export interface UserForm {
  id?: number;
  firstName: string;
  lastName: string;
  email: string;
  firstContact: string;
  secondContact: string;
  homeAddress: string;
  officeAddress: string;
  websiteUrl: string;
  roleId: number | null;
  isActive?: boolean;
  password?: string;
}

interface ColumnOption { key: string; label: string; }

@Component({
  selector: 'app-manage-users',
  standalone: true,
  imports: [CommonModule, FormsModule, FooterComponent],
  templateUrl: './manage-users.component.html'
})
export class ManageUsersComponent implements OnInit, OnDestroy {

  private readonly cdr = inject(ChangeDetectorRef);

  @ViewChild('tableScroll')
  set tableScrollRef(ref: ElementRef<HTMLDivElement> | undefined) {
    this.tableScrollEl = ref;
    this.setupTableResizeObserver();
  }

  tableScrollEl?: ElementRef<HTMLDivElement>;
  private tableResizeObserver?: ResizeObserver;
  hasHorizontalScroll = false;

  users: AppUser[] = [];
  filteredUsers: AppUser[] = [];
  loading = true;
  errorMsg = '';

  sortBy = 'createdDate';
  sortDirection: 'asc' | 'desc' = 'desc';
  targetStatus: UserStatus = 'Pending';
  approvalRoleId: number | null = null;
  showApprovalRoleMenu = false;
  statusError = '';
  readonly userStatuses: UserStatus[] = ['Pending', 'Approved', 'Rejected'];

  searchTerm = '';
  roleFilter = 'All';
  statusFilter: 'active' | 'deleted' | 'all' = 'active';
  roles: string[] = ['All'];
  roleOptions: { id: number; name: string }[] = [
    { id: 2, name: 'Admin' },
    { id: 3, name: 'Finance' },
    { id: 4, name: 'Customer' },
    { id: 5, name: 'Staff' },
    { id: 6, name: 'Sales' }
  ];

  currentPage = 1;
  pageSize = 8;
  pageSizeOptions: number[] = [5, 8, 10, 25, 50];

  columnOptions: ColumnOption[] = [
    { key: 'firstContact', label: 'First Contact' },
    { key: 'secondContact', label: 'Second Contact' },
    { key: 'email', label: 'Email' },
    { key: 'homeAddress', label: 'Home Address' },
    { key: 'officeAddress', label: 'Office Address' },
    { key: 'websiteUrl', label: 'Website' },
    { key: 'createdDate', label: 'Created Date' },
    { key: 'createdBy', label: 'Created By' },
    { key: 'updatedDate', label: 'Updated Date' },
    { key: 'updatedBy', label: 'Updated By' },
  ];

  visibleColumns: Record<string, boolean> = {
    firstContact: true,
    secondContact: false,
    email: true,
    homeAddress: false,
    officeAddress: false,
    websiteUrl: false,
    createdDate: false,
    createdBy: false,
    updatedDate: false,
    updatedBy: false,
  };

  showColumnMenu = false;
  showRoleMenu = false;

  toggleRoleMenu(): void { this.showRoleMenu = !this.showRoleMenu; }

  selectRole(r: string): void {
    this.roleFilter = r;
    this.showRoleMenu = false;
    this.applyFilters();
  }

  showFormRoleMenu = false;
  showAddPassword = false;
  showPageSizeMenu = false;

  toggleFormRoleMenu(): void { this.showFormRoleMenu = !this.showFormRoleMenu; }
  toggleAddPassword(): void { this.showAddPassword = !this.showAddPassword; }

  selectFormRole(id: number): void {
    this.form.roleId = id;
    this.showFormRoleMenu = false;
  }

  formRoleName(): string {
    return this.roleOptions.find((r) => r.id === this.form.roleId)?.name || 'Select role';
  }

  toggleColumn(key: string): void {
    this.visibleColumns[key] = !this.visibleColumns[key];
    setTimeout(() => this.updateHorizontalScrollState(), 0);
  }

  isColumnVisible(key: string): boolean { return !!this.visibleColumns[key]; }
  toggleColumnMenu(): void { this.showColumnMenu = !this.showColumnMenu; }

  resetColumns(): void {
    this.columnOptions.forEach((c) => (this.visibleColumns[c.key] = true));
    setTimeout(() => this.updateHorizontalScrollState(), 0);
  }

  hideAllOptionalColumns(): void {
    this.columnOptions.forEach((c) => (this.visibleColumns[c.key] = false));
    setTimeout(() => this.updateHorizontalScrollState(), 0);
  }

  visibleColumnCount(): number {
    return Object.values(this.visibleColumns).filter(Boolean).length;
  }

  togglePageSizeMenu(): void { this.showPageSizeMenu = !this.showPageSizeMenu; }

  selectPageSize(size: number): void {
    this.pageSize = size;
    this.showPageSizeMenu = false;
    this.currentPage = 1;
    setTimeout(() => this.updateHorizontalScrollState(), 0);
  }

  // =================== Bulk selection ===================

  selectedUserIds = new Set<number>();
  showBulkRoleMenu = false;
  bulkRoleSaving = false;
  bulkDeleteUserIds: number[] = [];
  bulkStatusMessage = '';
  bulkStatusHasErrors = false;

  get bulkBusy(): boolean { return this.bulkRoleSaving || this.saving; }

  get allPageUsersSelected(): boolean {
    const selectable = this.paginatedUsers.filter(u => !this.isRowSuperAdmin(u));
    return selectable.length > 0 && selectable.every(u => this.selectedUserIds.has(u.id));
  }

  get somePageUsersSelected(): boolean {
    const selectable = this.paginatedUsers.filter(u => !this.isRowSuperAdmin(u));
    return selectable.some(u => this.selectedUserIds.has(u.id)) && !this.allPageUsersSelected;
  }

  isUserSelectable(user: AppUser): boolean {
    if (this.isRowSuperAdmin(user)) return false;
    if ((!this.canEdit && !this.canDelete) || this.bulkBusy) return false;
    return true;
  }

  toggleUserSelection(user: AppUser): void {
    if (!this.isUserSelectable(user)) return;
    if (this.selectedUserIds.has(user.id)) this.selectedUserIds.delete(user.id);
    else this.selectedUserIds.add(user.id);
    this.bulkStatusMessage = '';
  }

  togglePageSelection(): void {
    if ((!this.canEdit && !this.canDelete) || this.bulkBusy || this.loading) return;
    if (this.allPageUsersSelected) {
      this.selectedUserIds.clear();
    } else {
      this.paginatedUsers.forEach(u => {
        if (!this.isRowSuperAdmin(u)) this.selectedUserIds.add(u.id);
      });
      // trigger change detection for the Set
      this.selectedUserIds = new Set(this.selectedUserIds);
    }
    this.bulkStatusMessage = '';
  }

  clearUserSelection(): void {
    if (this.bulkBusy) return;
    this.selectedUserIds.clear();
    this.selectedUserIds = new Set();
    this.showBulkRoleMenu = false;
    this.bulkStatusMessage = '';
  }

  updateSelectedRoles(roleId: number): void {
    this.showBulkRoleMenu = false;
    if (!this.canEdit || this.bulkBusy || this.loading) return;

    const role = this.roleOptions.find(r => r.id === roleId);
    if (!role) return;

    const selected = this.filteredUsers.filter(u =>
      this.selectedUserIds.has(u.id) && !this.isRowSuperAdmin(u) && !u.isDeleted
    );

    if (!selected.length) return;

    const changed = selected.filter(u => u.roleId !== roleId);

    this.bulkStatusMessage = '';
    this.bulkStatusHasErrors = false;

    if (!changed.length) {
      this.bulkStatusMessage = `All ${selected.length} selected users already have role ${role.name}.`;
      this.clearUserSelection();
      return;
    }

    this.bulkRoleSaving = true;

    from(changed).pipe(
      mergeMap(user => {
        const payload: UserForm = {
          id: user.id,
          firstName: user.firstName,
          lastName: user.lastName,
          email: user.email,
          firstContact: user.firstContact,
          secondContact: user.secondContact,
          homeAddress: user.homeAddress,
          officeAddress: user.officeAddress,
          websiteUrl: user.websiteUrl,
          roleId,
          isActive: user.isActive
        };
        return this.http.put(`${this.apiUrl}/${user.id}`, payload).pipe(
          map(() => ({ id: user.id, success: true })),
          catchError(() => of({ id: user.id, success: false }))
        );
      }, 4),
      toArray()
    ).subscribe(results => {
      const failed = results.filter(r => !r.success);
      const updated = results.length - failed.length;

      this.selectedUserIds = new Set(failed.map(r => r.id));
      this.bulkRoleSaving = false;
      this.bulkStatusHasErrors = failed.length > 0;
      this.bulkStatusMessage = `${updated} user(s) updated to role ${role.name}.`;

      const unchanged = selected.length - changed.length;
      if (unchanged) this.bulkStatusMessage += ` ${unchanged} already had this role.`;
      if (failed.length) this.bulkStatusMessage += ` ${failed.length} failed; remaining failed users are selected for retry.`;

      this.fetchUsers();
    });
  }

  openBulkDeleteModal(): void {
    if (!this.canDelete || this.bulkBusy || this.loading) return;

    this.bulkDeleteUserIds = this.filteredUsers
      .filter(u => this.selectedUserIds.has(u.id) && !this.isRowSuperAdmin(u) && !u.isDeleted)
      .map(u => u.id);

    if (!this.bulkDeleteUserIds.length) return;

    this.selectedUser = null;
    this.showBulkRoleMenu = false;
    this.showDeleteModal = true;
  }

  private confirmBulkDeleteUsers(): void {
    if (!this.canDelete || this.bulkBusy || !this.bulkDeleteUserIds.length) return;

    this.saving = true;

    from(this.bulkDeleteUserIds).pipe(
      mergeMap(id => this.http.delete(`${this.apiUrl}/${id}`).pipe(
        map(() => ({ id, success: true })),
        catchError(() => of({ id, success: false }))
      ), 4),
      toArray()
    ).subscribe(results => {
      const failed = results.filter(r => !r.success);
      const deleted = results.length - failed.length;

      this.selectedUserIds = new Set(failed.map(r => r.id));
      this.bulkStatusHasErrors = failed.length > 0;
      this.bulkStatusMessage = `${deleted} user(s) moved to Deleted.`;
      if (failed.length) this.bulkStatusMessage += ` ${failed.length} failed; remaining failed users are selected for retry.`;

      this.saving = false;
      this.showDeleteModal = false;
      this.bulkDeleteUserIds = [];
      this.fetchUsers();
    });
  }

  // =================== Horizontal overflow detection ===================

  private setupTableResizeObserver(): void {
    this.tableResizeObserver?.disconnect();
    const el = this.tableScrollEl?.nativeElement;
    if (!el) { this.hasHorizontalScroll = false; return; }
    this.tableResizeObserver = new ResizeObserver(() => this.updateHorizontalScrollState());
    this.tableResizeObserver.observe(el);
    setTimeout(() => this.updateHorizontalScrollState(), 0);
  }

  private updateHorizontalScrollState(): void {
    const el = this.tableScrollEl?.nativeElement;
    const hasOverflow = !!el && el.scrollWidth > el.clientWidth + 2;
    if (hasOverflow !== this.hasHorizontalScroll) {
      this.hasHorizontalScroll = hasOverflow;
      this.cdr.detectChanges();
    }
  }

  @HostListener('window:resize')
  onWindowResize(): void { this.updateHorizontalScrollState(); }

  scrollTable(direction: 'left' | 'right'): void {
    const el = this.tableScrollEl?.nativeElement;
    if (!el) return;
    const maxScroll = Math.max(0, el.scrollWidth - el.clientWidth);
    if (maxScroll === 0) return;
    const target = direction === 'left' ? 0 : maxScroll;
    el.scrollTo({ left: target, behavior: 'smooth' });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('[data-approval-role-menu]')) this.showApprovalRoleMenu = false;
    if (!target.closest('[data-column-menu]')) this.showColumnMenu = false;
    if (!target.closest('[data-role-menu]')) this.showRoleMenu = false;
    if (!target.closest('[data-bulk-role-menu]')) this.showBulkRoleMenu = false;
    if (!target.closest('[data-form-role-menu]') && !target.closest('[data-form-role-menu-edit]')) this.showFormRoleMenu = false;
    if (!target.closest('[data-user-role-menu]')) this.openUserRoleId = null;
    if (!target.closest('[data-user-status-menu]')) this.openUserStatusId = null;
    if (!target.closest('[data-pagesize-menu]')) this.showPageSizeMenu = false;
  }

  showAddModal = false;
  showEditModal = false;
  showDeleteModal = false;
  showToggleActiveModal = false;
  selectedUser: AppUser | null = null;
  form: UserForm = this.emptyForm();
  saving = false;

  private apiUrl = 'https://localhost:44370/api/users';

  constructor(
    private http: HttpClient,
    public auth: AuthService,
    public perm: PermissionService
  ) {}

  ngOnInit(): void { this.fetchUsers(); }

  ngOnDestroy(): void { this.tableResizeObserver?.disconnect(); }

  get canAdd(): boolean { return this.auth.isSuperAdmin() || this.perm.canAdd('Manage Users'); }
  get canEdit(): boolean { return this.auth.isSuperAdmin() || this.perm.canEdit('Manage Users'); }
  get canDelete(): boolean { return this.auth.isSuperAdmin() || this.perm.canDelete('Manage Users'); }

  emptyForm(): UserForm {
    return {
      firstName: '', lastName: '', email: '',
      firstContact: '', secondContact: '',
      homeAddress: '', officeAddress: '', websiteUrl: '',
      roleId: 4, isActive: true, password: ''
    };
  }

  fetchUsers(): void {
    this.loading = true;
    this.errorMsg = '';
    this.http.get<AppUser[]>(this.apiUrl).subscribe({
      next: (data) => {
        this.users = data;
        this.roles = ['All', ...Array.from(new Set(data.map(u => u.role)))];
        this.applyFilters();
        this.loading = false;
        setTimeout(() => this.updateHorizontalScrollState(), 0);
      },
      error: () => { this.loading = false; }
    });
  }

  applyFilters(): void {
    let list = [...this.users];
    list = list.filter(u => !this.isSuperAdmin(u));

    if (this.statusFilter === 'active') list = list.filter(u => !u.isDeleted);
    else if (this.statusFilter === 'deleted') list = list.filter(u => u.isDeleted);

    if (this.roleFilter !== 'All') list = list.filter(u => u.role === this.roleFilter);

    if (this.searchTerm.trim()) {
      const term = this.searchTerm.trim().toLowerCase();
      list = list.filter(u => {
        const searchableValues = [
          u.id, u.firstName, u.lastName, this.fullName(u),
          u.firstContact, u.secondContact, u.email,
          u.homeAddress, u.officeAddress, u.websiteUrl,
          u.roleId, u.role, u.status,
          u.isDeleted ? 'deleted' : 'not deleted',
          u.createdDate, u.createdBy, u.updatedDate, u.updatedBy
        ];
        return searchableValues.some(value =>
          String(value ?? '').toLowerCase().includes(term)
        );
      });
    }

    this.filteredUsers = list.sort((a, b) => {
      const value = (u: AppUser): string | number => {
        if (this.sortBy === 'fullName') return this.fullName(u);
        if (this.sortBy === 'createdDate' || this.sortBy === 'updatedDate')
          return Date.parse(u[this.sortBy]) || 0;
        const v = u[this.sortBy as keyof AppUser];
        return typeof v === 'boolean' ? Number(v) : String(v ?? '');
      };
      const av = value(a), bv = value(b);
      const result = typeof av === 'number' && typeof bv === 'number'
        ? av - bv : String(av).localeCompare(String(bv), undefined, { numeric: true, sensitivity: 'base' });
      return (this.sortDirection === 'asc' ? result : -result) || b.id - a.id;
    });
    this.currentPage = 1;
    // Keep only selections still visible
    const visibleIds = new Set(this.filteredUsers.map(u => u.id));
    this.selectedUserIds = new Set(
      Array.from(this.selectedUserIds).filter(id => visibleIds.has(id))
    );
    setTimeout(() => this.updateHorizontalScrollState(), 0);
  }

  sort(column: string): void {
    this.sortDirection = this.sortBy === column && this.sortDirection === 'asc' ? 'desc' : 'asc';
    this.sortBy = column;
    this.applyFilters();
  }

  sortIcon(column: string): string {
    return this.sortBy === column ? (this.sortDirection === 'asc' ? '▲' : '▼') : '';
  }

  get paginatedUsers(): AppUser[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredUsers.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filteredUsers.length / this.pageSize));
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages) this.currentPage = page;
  }

  onPageSizeChange(): void { this.currentPage = 1; }

  roleBadgeClass(role: string): string {
    const map: Record<string, string> = {
      Admin: 'bg-blue-100 text-blue-700 ring-blue-600/20',
      Finance: 'bg-emerald-100 text-emerald-700 ring-emerald-600/20',
      Customer: 'bg-amber-100 text-amber-700 ring-amber-600/20',
      Staff: 'bg-cyan-100 text-cyan-700 ring-cyan-600/20',
      Sales: 'bg-pink-100 text-pink-700 ring-pink-600/20'
    };
    return map[role] || 'bg-gray-100 text-gray-700 ring-gray-600/20';
  }

  roleName(roleId: number | null): string {
    return this.roleOptions.find(r => r.id === roleId)?.name || 'Unknown';
  }

  openUserRoleId: number | null = null;
  roleSavingUserId: number | null = null;

  toggleUserRoleMenu(userId: number): void {
    if (this.roleSavingUserId === userId) return;
    this.openUserRoleId = this.openUserRoleId === userId ? null : userId;
  }

  changeUserRole(user: AppUser, roleId: number): void {
    if (!this.canEdit || user.isDeleted || user.roleId == null || this.isRowSuperAdmin(user)) return;
    if (user.roleId === roleId) { this.openUserRoleId = null; return; }

    const role = this.roleOptions.find(r => r.id === roleId);
    if (!role) return;

    this.roleSavingUserId = user.id;
    this.openUserRoleId = null;

    const payload: UserForm = {
      id: user.id,
      firstName: user.firstName, lastName: user.lastName, email: user.email,
      firstContact: user.firstContact, secondContact: user.secondContact,
      homeAddress: user.homeAddress, officeAddress: user.officeAddress,
      websiteUrl: user.websiteUrl, roleId, isActive: user.isActive
    };

    this.http.put(`${this.apiUrl}/${user.id}`, payload).subscribe({
      next: () => {
        user.roleId = roleId;
        user.role = role.name;
        this.roleSavingUserId = null;
        this.applyFilters();
      },
      error: () => { this.roleSavingUserId = null; }
    });
  }

  openUserStatusId: number | null = null;

  toggleUserStatusMenu(userId: number): void {
    this.openUserStatusId = this.openUserStatusId === userId ? null : userId;
  }

  selectUserStatus(user: AppUser, status: UserStatus): void {
    this.openUserStatusId = null;
    if (!this.canEdit || user.isDeleted || this.isSuperAdmin(user) || user.status === status) return;
    this.selectedUser = user;
    this.targetStatus = status;
    this.approvalRoleId = null;
    this.showApprovalRoleMenu = false;
    this.statusError = '';
    this.showToggleActiveModal = true;
  }

  fullName(user: AppUser): string {
    return `${user.firstName} ${user.lastName}`.trim();
  }

  openAddModal(): void {
    this.form = this.emptyForm();
    this.showAddModal = true;
  }

  submitAdd(addForm?: NgForm): void {
    if (addForm && addForm.invalid) {
      Object.values(addForm.controls).forEach(c => c.markAsTouched());
      return;
    }
    this.saving = true;
    this.http.post(this.apiUrl, this.form).subscribe({
      next: () => { this.saving = false; this.showAddModal = false; this.fetchUsers(); },
      error: () => { this.saving = false; }
    });
  }

  openEditModal(user: AppUser): void {
    this.selectedUser = user;
    this.form = {
      id: user.id,
      firstName: user.firstName, lastName: user.lastName, email: user.email,
      firstContact: user.firstContact, secondContact: user.secondContact,
      homeAddress: user.homeAddress, officeAddress: user.officeAddress,
      websiteUrl: user.websiteUrl, roleId: user.roleId, isActive: user.isActive
    };
    this.showEditModal = true;
  }

  submitEdit(editForm?: NgForm): void {
    if (!this.selectedUser) return;
    if (editForm && editForm.invalid) {
      Object.values(editForm.controls).forEach(c => c.markAsTouched());
      return;
    }
    this.saving = true;
    this.http.put(`${this.apiUrl}/${this.selectedUser.id}`, this.form).subscribe({
      next: () => { this.saving = false; this.showEditModal = false; this.fetchUsers(); },
      error: () => { this.saving = false; }
    });
  }

  openDeleteModal(user: AppUser): void {
    this.bulkDeleteUserIds = [];
    this.selectedUser = user;
    this.showDeleteModal = true;
  }

  confirmDelete(): void {
    if (this.bulkDeleteUserIds.length) { this.confirmBulkDeleteUsers(); return; }
    if (!this.selectedUser) return;
    this.saving = true;
    this.http.delete(`${this.apiUrl}/${this.selectedUser.id}`).subscribe({
      next: () => { this.saving = false; this.showDeleteModal = false; this.fetchUsers(); },
      error: () => { this.saving = false; }
    });
  }

  confirmToggleActive(): void {
    if (!this.selectedUser || this.saving) return;
    if (this.targetStatus === 'Approved' && !this.approvalRoleId) {
      this.statusError = 'Select a role before approving this user.';
      return;
    }
    this.saving = true;
    this.statusError = '';
    this.http.patch(`${this.apiUrl}/${this.selectedUser.id}/status`, {
      status: this.targetStatus,
      roleId: this.targetStatus === 'Approved' ? this.approvalRoleId : null
    }).subscribe({
      next: () => { this.saving = false; this.closeModals(); this.fetchUsers(); },
      error: (err) => {
        this.saving = false;
        this.statusError = err.error?.message || 'Could not update status. Please try again.';
      }
    });
  }

  closeModals(): void {
    if (this.saving) return;
    this.showAddModal = false;
    this.showEditModal = false;
    this.showDeleteModal = false;
    this.showToggleActiveModal = false;
    this.showApprovalRoleMenu = false;
    this.selectedUser = null;
    this.bulkDeleteUserIds = [];
  }

  isSuperAdmin(user: AppUser): boolean { return user.roleId === 1; }
  isRowSuperAdmin(user: AppUser): boolean { return user.role === 'Super Admin'; }
}