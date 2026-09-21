import {
  Component, OnInit, OnDestroy, HostListener, ViewChild, ElementRef,
  ChangeDetectorRef, inject
} from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
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

interface ColumnOption {
  key: string;
  label: string;
}

@Component({
  selector: 'app-manage-users',
  standalone: true,
  imports: [CommonModule, FormsModule, FooterComponent],
  templateUrl: './manage-users.component.html'
})
export class ManageUsersComponent implements OnInit, OnDestroy {

  private readonly cdr = inject(ChangeDetectorRef);

  // ---- Scroll container reference (setter reattaches ResizeObserver) ----
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

  toggleRoleMenu(): void {
    this.showRoleMenu = !this.showRoleMenu;
  }

  selectRole(r: string): void {
    this.roleFilter = r;
    this.showRoleMenu = false;
    this.applyFilters();
  }

  showFormRoleMenu = false;
  showAddPassword = false;
  showPageSizeMenu = false;

  toggleFormRoleMenu(): void {
    this.showFormRoleMenu = !this.showFormRoleMenu;
  }

  toggleAddPassword(): void {
    this.showAddPassword = !this.showAddPassword;
  }

  selectFormRole(id: number): void {
    this.form.roleId = id;
    this.showFormRoleMenu = false;
  }

  formRoleName(): string {
    return this.roleOptions.find((r) => r.id === this.form.roleId)?.name || 'Select role';
  }

  toggleColumn(key: string): void {
    this.visibleColumns[key] = !this.visibleColumns[key];
    // Column visibility changed → overflow may have changed
    setTimeout(() => this.updateHorizontalScrollState(), 0);
  }

  isColumnVisible(key: string): boolean {
    return !!this.visibleColumns[key];
  }

  toggleColumnMenu(): void {
    this.showColumnMenu = !this.showColumnMenu;
  }

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

  togglePageSizeMenu(): void {
    this.showPageSizeMenu = !this.showPageSizeMenu;
  }

  selectPageSize(size: number): void {
    this.pageSize = size;
    this.showPageSizeMenu = false;
    this.currentPage = 1;
    setTimeout(() => this.updateHorizontalScrollState(), 0);
  }

  // =================== Horizontal overflow detection ===================

  private setupTableResizeObserver(): void {
    this.tableResizeObserver?.disconnect();
    const el = this.tableScrollEl?.nativeElement;

    if (!el) {
      this.hasHorizontalScroll = false;
      return;
    }

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
  onWindowResize(): void {
    this.updateHorizontalScrollState();
  }

  // =================== Horizontal scroll — one click = full end ===================

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

  ngOnInit(): void {
    this.fetchUsers();
  }

  ngOnDestroy(): void {
    this.tableResizeObserver?.disconnect();
  }

  get canAdd(): boolean {
    return this.auth.isSuperAdmin() || this.perm.canAdd('Manage Users');
  }

  get canEdit(): boolean {
    return this.auth.isSuperAdmin() || this.perm.canEdit('Manage Users');
  }

  get canDelete(): boolean {
    return this.auth.isSuperAdmin() || this.perm.canDelete('Manage Users');
  }

  emptyForm(): UserForm {
    return {
      firstName: '',
      lastName: '',
      email: '',
      firstContact: '',
      secondContact: '',
      homeAddress: '',
      officeAddress: '',
      websiteUrl: '',
      roleId: 4,
      isActive: true,
      password: ''
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
        // After table renders, check if horizontal scroll appeared
        setTimeout(() => this.updateHorizontalScrollState(), 0);
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  applyFilters(): void {
    let list = [...this.users];

    // Super Admin is never shown in this table
    list = list.filter(u => !this.isSuperAdmin(u));

    if (this.statusFilter === 'active') {
      list = list.filter(u => !u.isDeleted);
    } else if (this.statusFilter === 'deleted') {
      list = list.filter(u => u.isDeleted);
    }

    if (this.roleFilter !== 'All') {
      list = list.filter(u => u.role === this.roleFilter);
    }

    // Global search: searches across all user fields, including fields
    // that are currently hidden from the table.
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.trim().toLowerCase();

      list = list.filter(u => {
        const searchableValues = [
          u.id,
          u.firstName,
          u.lastName,
          this.fullName(u),
          u.firstContact,
          u.secondContact,
          u.email,
          u.homeAddress,
          u.officeAddress,
          u.websiteUrl,
          u.roleId,
          u.role,
          u.status,
          u.isDeleted ? 'deleted' : 'not deleted',
          u.createdDate,
          u.createdBy,
          u.updatedDate,
          u.updatedBy
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

  onPageSizeChange(): void {
    this.currentPage = 1;
  }

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
    if (user.roleId === roleId) {
      this.openUserRoleId = null;
      return;
    }

    const role = this.roleOptions.find(r => r.id === roleId);
    if (!role) return;

    this.roleSavingUserId = user.id;
    this.openUserRoleId = null;

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

    this.http.put(`${this.apiUrl}/${user.id}`, payload).subscribe({
      next: () => {
        user.roleId = roleId;
        user.role = role.name;
        this.roleSavingUserId = null;
        this.applyFilters();
      },
      error: () => {
        this.roleSavingUserId = null;
      }
    });
  }

  // ===== Status (Active/Inactive) dropdown =====
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
      next: () => {
        this.saving = false;
        this.showAddModal = false;
        this.fetchUsers();
      },
      error: () => {
        this.saving = false;
      }
    });
  }

  openEditModal(user: AppUser): void {
    this.selectedUser = user;
    this.form = {
      id: user.id,
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email,
      firstContact: user.firstContact,
      secondContact: user.secondContact,
      homeAddress: user.homeAddress,
      officeAddress: user.officeAddress,
      websiteUrl: user.websiteUrl,
      roleId: user.roleId,
      isActive: user.isActive
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
      next: () => {
        this.saving = false;
        this.showEditModal = false;
        this.fetchUsers();
      },
      error: () => {
        this.saving = false;
      }
    });
  }

  openDeleteModal(user: AppUser): void {
    this.selectedUser = user;
    this.showDeleteModal = true;
  }

  confirmDelete(): void {
    if (!this.selectedUser) return;
    this.saving = true;
    this.http.delete(`${this.apiUrl}/${this.selectedUser.id}`).subscribe({
      next: () => {
        this.saving = false;
        this.showDeleteModal = false;
        this.fetchUsers();
      },
      error: () => {
        this.saving = false;
      }
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
      next: () => {
        this.saving = false;
        this.closeModals();
        this.fetchUsers();
      },
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
  }

  isSuperAdmin(user: AppUser): boolean {
    return user.roleId === 1;
  }

  isRowSuperAdmin(user: AppUser): boolean {
    return user.role === 'Super Admin';
  }
}