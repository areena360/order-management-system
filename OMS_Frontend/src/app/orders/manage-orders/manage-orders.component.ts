import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';

import { FooterComponent } from '../../footer/footer.component';
import { PermissionService } from '../../auth/permission.service';
import { AuthService } from '../../auth/auth.service';
import { OrdersService } from '../orders.service';
import { LookupService, LOOKUP_TYPE } from '../lookup.service';
import { PollingService } from '../../core/polling/polling.service';

import { CustomerOption, LookupItem, OrderImageItem, OrderListItem, OrderQuery } from '../order.models';
import { statusBadgeClass, priorityBadgeClass } from '../order-badge.util';

interface ColumnOption { key: string; label: string; }

@Component({
  selector: 'app-manage-orders',
  standalone: true,
  imports: [CommonModule, FormsModule, FooterComponent],
  templateUrl: './manage-orders.component.html'
})
export class ManageOrdersComponent implements OnInit, OnDestroy {

  private readonly polling = inject(PollingService);

  orders: OrderListItem[] = [];
  totalCount = 0;
  loading = true;
  errorMsg = '';
  searchTerm = '';

  deleteOrderItem: OrderListItem | null = null;
  showDeleteModal = false;
  deletingOrder = false;
  deleteError = '';

  showImageModal = false;
  selectedOrderImages: OrderImageItem[] = [];
  activeImageIndex = 0;

  private searchInput$ = new Subject<string>();
  private destroy$ = new Subject<void>();

  statuses: LookupItem[] = [];
  priorities: LookupItem[] = [];
  genders: LookupItem[] = [];
  materials: LookupItem[] = [];
  customerOptions: CustomerOption[] = [];
  private customerFilterSearch$ = new Subject<string>();

  statusFilter: number | null = null;
  priorityFilter: number | null = null;
  genderFilter: number | null = null;
  materialFilter: number | null = null;
  customerFilter: number | null = null;
  dateFrom: string | null = null;
  dateTo: string | null = null;

  openFilterDropdown: string | null = null;

  sortBy = 'CreatedDate';
  sortDirection: 'asc' | 'desc' = 'desc';
  currentPage = 1;
  pageSize = 25;
  pageSizeOptions = [10, 25, 50, 100];
  showPageSizeMenu = false;
  showColumnMenu = false;

  columnOptions: ColumnOption[] = [
    { key: 'customerOrderNumber', label: 'Customer Order #' },
    { key: 'manufacturerProductTitle', label: 'Manufacturer Product' },
    { key: 'priority', label: 'Priority' },
    { key: 'daysForMaking', label: 'Days for Making' },
    { key: 'trackingNumber', label: 'Tracking Number' },
    { key: 'createdDate', label: 'Created Date' }
  ];

  hiddenColumns = new Set<string>(['manufacturerProductTitle']);

  canView = false;
  canAdd = false;
  canEdit = false;
  canDelete = false;

  isCustomer = false;

  deletingOrderId: number | null = null;

  statusBadgeClass = statusBadgeClass;
  priorityBadgeClass = priorityBadgeClass;

  private silentRefreshBusy = false;

  constructor(
    private ordersService: OrdersService,
    private lookupService: LookupService,
    private permissionService: PermissionService,
    private authService: AuthService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.canView = this.permissionService.canView('Orders');
    this.canAdd = this.permissionService.canAdd('Orders');
    this.canEdit = this.permissionService.canEdit('Orders');
    this.canDelete = this.permissionService.canDelete('Orders');

    this.isCustomer = this.authService.isCustomer();

    if (this.isCustomer) {
      this.columnOptions = this.columnOptions.filter(c =>
        c.key !== 'manufacturerProductTitle' &&
        c.key !== 'priority' &&
        c.key !== 'daysForMaking'
      );
      this.hiddenColumns = new Set<string>(['manufacturerProductTitle']);
    }

    this.searchInput$.pipe(
      debounceTime(350),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.currentPage = 1;
      this.fetchOrders();
    });

    this.customerFilterSearch$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe(term => {
      this.lookupService.getCustomers(term)
        .pipe(takeUntil(this.destroy$))
        .subscribe({ next: r => this.customerOptions = r });
    });

    this.loadLookups();
    this.fetchOrders();
    this.setupPolling();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // Poll every 10 seconds. Silent refresh, no spinner flicker.
  private setupPolling(): void {
    this.polling.poll(10000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.silentRefresh());
  }

  private silentRefresh(): void {
    if (this.silentRefreshBusy) return;
    if (this.showDeleteModal || this.showImageModal) return;
    if (this.loading) return;

    this.silentRefreshBusy = true;

    this.ordersService.getOrders(this.buildQuery())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.orders = response.items;
          this.totalCount = response.totalCount;
          this.silentRefreshBusy = false;
        },
        error: () => {
          this.silentRefreshBusy = false;
        }
      });
  }

  private buildQuery(): OrderQuery {
    return {
      pageNumber: this.currentPage,
      pageSize: this.pageSize,
      search: this.searchTerm || undefined,
      sortBy: this.sortBy,
      sortDirection: this.sortDirection,
      statusId: this.statusFilter,
      priorityId: this.isCustomer ? null : this.priorityFilter,
      genderId: this.isCustomer ? null : this.genderFilter,
      materialId: this.isCustomer ? null : this.materialFilter,
      customerId: this.isCustomer ? null : this.customerFilter,
      dateFrom: this.dateFrom,
      dateTo: this.dateTo
    };
  }

  private loadLookups(): void {
    this.lookupService.getByType(LOOKUP_TYPE.OrderStatus)
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => this.statuses = r });

    if (this.isCustomer) return;

    this.lookupService.getByType(LOOKUP_TYPE.Priority)
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => this.priorities = r });

    this.lookupService.getByType(LOOKUP_TYPE.Gender)
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => this.genders = r });

    this.lookupService.getByType(LOOKUP_TYPE.Material)
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => this.materials = r });

    this.lookupService.getCustomers()
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => this.customerOptions = r });
  }

  onSearchChange(): void { this.searchInput$.next(this.searchTerm); }

  onCustomerFilterSearch(term: string): void { this.customerFilterSearch$.next(term); }

  onDateFilterChange(): void {
    this.currentPage = 1;
    this.fetchOrders();
  }

  fetchOrders(): void {
    this.loading = true;
    this.errorMsg = '';

    this.ordersService.getOrders(this.buildQuery())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.orders = response.items;
          this.totalCount = response.totalCount;
          this.loading = false;
        },
        error: error => {
          this.errorMsg = error?.error?.message ?? 'Unable to load orders. Please try again.';
          this.loading = false;
        }
      });
  }

  clearFilters(): void {
    this.statusFilter = null;
    this.priorityFilter = null;
    this.genderFilter = null;
    this.materialFilter = null;
    this.customerFilter = null;
    this.dateFrom = null;
    this.dateTo = null;
    this.openFilterDropdown = null;
    this.currentPage = 1;
    this.fetchOrders();
  }

  get activeFilterCount(): number {
    const vals = this.isCustomer
      ? [this.statusFilter, this.dateFrom, this.dateTo]
      : [this.statusFilter, this.priorityFilter, this.genderFilter, this.materialFilter, this.customerFilter, this.dateFrom, this.dateTo];

    return vals.filter(v => v !== null && v !== undefined && v !== '').length;
  }

  toggleFilterDropdown(key: string): void {
    this.openFilterDropdown = this.openFilterDropdown === key ? null : key;
  }

  selectFilterDropdown(filterName: string, value: number | null): void {
    switch (filterName) {
      case 'statusFilter': this.statusFilter = value; break;
      case 'priorityFilter': this.priorityFilter = value; break;
      case 'customerFilter': this.customerFilter = value; break;
      case 'genderFilter': this.genderFilter = value; break;
      case 'materialFilter': this.materialFilter = value; break;
      default: return;
    }
    this.openFilterDropdown = null;
    this.currentPage = 1;
    this.fetchOrders();
  }

  optionName(options: LookupItem[], id: number | null | undefined): string {
    if (id === null || id === undefined) return '';
    return options.find(o => o.id === id)?.name ?? '';
  }

  sort(column: string): void {
    if (this.sortBy === column) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = column;
      this.sortDirection = 'asc';
    }
    this.fetchOrders();
  }

  sortIcon(column: string): string {
    if (this.sortBy !== column) return '';
    return this.sortDirection === 'asc' ? '▲' : '▼';
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get pageNumbers(): number[] {
    const total = this.totalPages;
    const current = this.currentPage;
    const pages: number[] = [];
    const start = Math.max(1, current - 2);
    const end = Math.min(total, current + 2);
    for (let p = start; p <= end; p++) pages.push(p);
    return pages;
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages && page !== this.currentPage) {
      this.currentPage = page;
      this.fetchOrders();
    }
  }

  togglePageSizeMenu(): void { this.showPageSizeMenu = !this.showPageSizeMenu; }

  selectPageSize(size: number): void {
    this.pageSize = size;
    this.currentPage = 1;
    this.showPageSizeMenu = false;
    this.fetchOrders();
  }

  toggleColumnMenu(): void { this.showColumnMenu = !this.showColumnMenu; }

  isColumnVisible(key: string): boolean { return !this.hiddenColumns.has(key); }

  toggleColumn(key: string): void {
    if (this.hiddenColumns.has(key)) this.hiddenColumns.delete(key);
    else this.hiddenColumns.add(key);
  }

  resetColumns(): void { this.hiddenColumns.clear(); }

  hideAllOptionalColumns(): void {
    this.hiddenColumns = new Set(this.columnOptions.map(c => c.key));
  }

  visibleColumnCount(): number {
    return this.columnOptions.length - this.hiddenColumns.size;
  }

  addOrder(): void { this.router.navigate(['/dashboard/orders/add']); }

  viewOrder(order: OrderListItem): void {
    this.router.navigate(['/dashboard/orders', order.id]);
  }

  editOrder(order: OrderListItem, event: Event): void {
    event.stopPropagation();
    this.router.navigate(['/dashboard/orders', order.id, 'edit']);
  }

  openDeleteModal(order: OrderListItem, event: Event): void {
    event.stopPropagation();
    if (this.deletingOrder) return;
    this.deleteOrderItem = order;
    this.deleteError = '';
    this.showDeleteModal = true;
  }

  closeDeleteModal(): void {
    if (this.deletingOrder) return;
    this.showDeleteModal = false;
    this.deleteOrderItem = null;
    this.deleteError = '';
  }

  confirmDeleteOrder(): void {
    if (!this.deleteOrderItem || this.deletingOrder) return;

    this.deletingOrder = true;
    this.deletingOrderId = this.deleteOrderItem.id;
    this.deleteError = '';

    this.ordersService.deleteOrder(this.deleteOrderItem.id).subscribe({
      next: () => {
        this.deletingOrder = false;
        this.deletingOrderId = null;
        this.showDeleteModal = false;
        this.deleteOrderItem = null;
        this.fetchOrders();
      },
      error: (error) => {
        console.error('Failed to delete order:', error);
        this.deletingOrder = false;
        this.deletingOrderId = null;
        this.deleteError = error?.error?.message ?? 'Unable to delete order. Please try again.';
      }
    });
  }

  openImageModal(order: OrderListItem, event: Event): void {
    event.stopPropagation();
    if (!order.images || order.images.length === 0) return;
    this.selectedOrderImages = order.images ?? [];
    this.activeImageIndex = 0;
    this.showImageModal = true;
  }

  closeImageModal(): void {
    this.showImageModal = false;
    this.selectedOrderImages = [];
    this.activeImageIndex = 0;
  }

  setActiveImage(index: number): void { this.activeImageIndex = index; }

  prevImage(): void {
    this.activeImageIndex = this.activeImageIndex === 0
      ? this.selectedOrderImages.length - 1
      : this.activeImageIndex - 1;
  }

  nextImage(): void {
    this.activeImageIndex = (this.activeImageIndex + 1) % this.selectedOrderImages.length;
  }

  getImageUrl(imageUrl: string | null | undefined): string {
    return this.ordersService.getImageUrl(imageUrl);
  }

  get showingFrom(): number {
    if (this.totalCount === 0) return 0;
    return (this.currentPage - 1) * this.pageSize + 1;
  }

  get showingTo(): number {
    return Math.min(this.currentPage * this.pageSize, this.totalCount);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (this.showDeleteModal || this.showImageModal) return;

    if (!target.closest('[data-column-menu]')) this.showColumnMenu = false;
    if (!target.closest('[data-pagesize-menu]')) this.showPageSizeMenu = false;
    if (!target.closest('[data-dd]')) this.openFilterDropdown = null;
  }
}