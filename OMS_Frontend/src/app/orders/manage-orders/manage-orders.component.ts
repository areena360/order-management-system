import {
  Component,
  HostListener,
  OnDestroy,
  OnInit
} from '@angular/core';

import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import {
  debounceTime,
  distinctUntilChanged,
  Subject,
  takeUntil
} from 'rxjs';

import { FooterComponent } from '../../footer/footer.component';

import { PermissionService } from '../../auth/permission.service';

import { OrdersService } from '../orders.service';

import {
  LookupService,
  LOOKUP_TYPE
} from '../lookup.service';

import {
  CustomerOption,
  LookupItem,
  OrderListItem,
  OrderQuery
} from '../order.models';

import {
  statusBadgeClass,
  priorityBadgeClass
} from '../order-badge.util';

interface ColumnOption {
  key: string;
  label: string;
}

@Component({
  selector: 'app-manage-orders',

  standalone: true,

  imports: [
    CommonModule,
    FormsModule,
    FooterComponent
  ],

  templateUrl: './manage-orders.component.html'
})
export class ManageOrdersComponent
  implements OnInit, OnDestroy {

  orders: OrderListItem[] = [];

  totalCount = 0;

  loading = true;

  errorMsg = '';

  searchTerm = '';

  // Delete Modal state
  deleteOrderItem: OrderListItem | null = null;
  showDeleteModal = false;
  deletingOrder = false;
  deleteError = '';

  private searchInput$ =
    new Subject<string>();

  private destroy$ =
    new Subject<void>();

  statuses: LookupItem[] = [];

  priorities: LookupItem[] = [];

  genders: LookupItem[] = [];

  materials: LookupItem[] = [];

  customerOptions: CustomerOption[] = [];

  private customerFilterSearch$ =
    new Subject<string>();

  statusFilter: number | null = null;

  priorityFilter: number | null = null;

  genderFilter: number | null = null;

  materialFilter: number | null = null;

  customerFilter: number | null = null;

  dateFrom: string | null = null;

  dateTo: string | null = null;

  showFilters = false;

  /** Key of the currently open filter dropdown, or null when none is open. */
  openFilterDropdown: string | null = null;

  sortBy = 'CreatedDate';

  sortDirection: 'asc' | 'desc' = 'desc';

  currentPage = 1;

  pageSize = 25;

  pageSizeOptions = [
    10,
    25,
    50,
    100
  ];

  showPageSizeMenu = false;

  showColumnMenu = false;

  columnOptions: ColumnOption[] = [

    {
      key: 'customerOrderNumber',
      label: 'Customer Order #'
    },

    {
      key: 'manufacturerProductTitle',
      label: 'Manufacturer Product'
    },

    {
      key: 'priority',
      label: 'Priority'
    },

    {
      key: 'daysForMaking',
      label: 'Days for Making'
    },

    {
      key: 'trackingNumber',
      label: 'Tracking Number'
    },

    {
      key: 'createdDate',
      label: 'Created Date'
    }

  ];

  hiddenColumns =
    new Set<string>([
      'manufacturerProductTitle'
    ]);

  canView = false;

  canAdd = false;

  canEdit = false;

  canDelete = false;

  // Delete state (keeping for backward compatibility)
  deletingOrderId: number | null = null;

  statusBadgeClass =
    statusBadgeClass;

  priorityBadgeClass =
    priorityBadgeClass;

  constructor(
    private ordersService: OrdersService,

    private lookupService: LookupService,

    private permissionService: PermissionService,

    private router: Router
  ) { }

  ngOnInit(): void {

    this.canView =
      this.permissionService.canView(
        'Orders'
      );

    this.canAdd =
      this.permissionService.canAdd(
        'Orders'
      );

    this.canEdit =
      this.permissionService.canEdit(
        'Orders'
      );

    this.canDelete =
      this.permissionService.canDelete(
        'Orders'
      );

    this.searchInput$
      .pipe(
        debounceTime(350),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {

        this.currentPage = 1;

        this.fetchOrders();

      });

    this.customerFilterSearch$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      )
      .subscribe(term => {

        this.lookupService
          .getCustomers(term)
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: result => {
              this.customerOptions = result;
            }
          });

      });

    this.loadLookups();

    this.fetchOrders();
  }

  ngOnDestroy(): void {

    this.destroy$.next();

    this.destroy$.complete();
  }

  private loadLookups(): void {

    this.lookupService
      .getByType(
        LOOKUP_TYPE.OrderStatus
      )
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          this.statuses = result;
        }
      });

    this.lookupService
      .getByType(
        LOOKUP_TYPE.Priority
      )
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          this.priorities = result;
        }
      });

    this.lookupService
      .getByType(
        LOOKUP_TYPE.Gender
      )
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          this.genders = result;
        }
      });

    this.lookupService
      .getByType(
        LOOKUP_TYPE.Material
      )
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          this.materials = result;
        }
      });

    this.lookupService
      .getCustomers()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          this.customerOptions = result;
        }
      });
  }

  onSearchChange(): void {

    this.searchInput$.next(
      this.searchTerm
    );
  }

  onCustomerFilterSearch(
    term: string
  ): void {

    this.customerFilterSearch$.next(
      term
    );
  }

  fetchOrders(): void {

    this.loading = true;

    this.errorMsg = '';

    const query: OrderQuery = {

      pageNumber:
        this.currentPage,

      pageSize:
        this.pageSize,

      search:
        this.searchTerm || undefined,

      sortBy:
        this.sortBy,

      sortDirection:
        this.sortDirection,

      statusId:
        this.statusFilter,

      priorityId:
        this.priorityFilter,

      genderId:
        this.genderFilter,

      materialId:
        this.materialFilter,

      customerId:
        this.customerFilter,

      dateFrom:
        this.dateFrom,

      dateTo:
        this.dateTo
    };

    this.ordersService
      .getOrders(query)
      .pipe(takeUntil(this.destroy$))
      .subscribe({

        next: response => {

          this.orders =
            response.items;

          this.totalCount =
            response.totalCount;

          this.loading = false;
        },

        error: error => {

          this.errorMsg =
            error?.error?.message ??
            'Unable to load orders. Please try again.';

          this.loading = false;
        }

      });
  }

  applyFilters(): void {

    this.currentPage = 1;

    this.showFilters = false;
    this.openFilterDropdown = null;

    this.fetchOrders();
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

    this.showFilters = false;

    this.fetchOrders();
  }

  get activeFilterCount(): number {

    return [

      this.statusFilter,

      this.priorityFilter,

      this.genderFilter,

      this.materialFilter,

      this.customerFilter,

      this.dateFrom,

      this.dateTo

    ].filter(
      value =>
        value !== null &&
        value !== undefined &&
        value !== ''
    ).length;
  }

  toggleFilterDropdown(key: string): void {
    this.openFilterDropdown =
      this.openFilterDropdown === key
        ? null
        : key;
  }

  selectFilterDropdown(
    filterName: string,
    value: number | null
  ): void {
    switch (filterName) {
      case 'statusFilter':
        this.statusFilter = value;
        break;

      case 'priorityFilter':
        this.priorityFilter = value;
        break;

      case 'customerFilter':
        this.customerFilter = value;
        break;

      case 'genderFilter':
        this.genderFilter = value;
        break;

      case 'materialFilter':
        this.materialFilter = value;
        break;

      default:
        return;
    }

    this.openFilterDropdown = null;
  }

  optionName(
    options: LookupItem[],
    id: number | null | undefined
  ): string {
    if (id === null || id === undefined) {
      return '';
    }

    return (
      options.find(
        option => option.id === id
      )?.name ?? ''
    );
  }

  sort(column: string): void {

    if (this.sortBy === column) {

      this.sortDirection =
        this.sortDirection === 'asc'
          ? 'desc'
          : 'asc';

    } else {

      this.sortBy = column;

      this.sortDirection = 'asc';
    }

    this.fetchOrders();
  }

  sortIcon(column: string): string {

    if (this.sortBy !== column) {
      return '';
    }

    return this.sortDirection === 'asc' ? '▲' : '▼';
  }

  get totalPages(): number {

    return Math.max(
      1,
      Math.ceil(
        this.totalCount /
        this.pageSize
      )
    );
  }

  get pageNumbers(): number[] {

    const total =
      this.totalPages;

    const current =
      this.currentPage;

    const pages: number[] = [];

    const start =
      Math.max(
        1,
        current - 2
      );

    const end =
      Math.min(
        total,
        current + 2
      );

    for (
      let page = start;
      page <= end;
      page++
    ) {
      pages.push(page);
    }

    return pages;
  }

  goToPage(page: number): void {

    if (
      page >= 1 &&
      page <= this.totalPages &&
      page !== this.currentPage
    ) {

      this.currentPage = page;

      this.fetchOrders();
    }
  }

  togglePageSizeMenu(): void {

    this.showPageSizeMenu =
      !this.showPageSizeMenu;
  }

  selectPageSize(size: number): void {

    this.pageSize = size;

    this.currentPage = 1;

    this.showPageSizeMenu = false;

    this.fetchOrders();
  }

  toggleColumnMenu(): void {

    this.showColumnMenu =
      !this.showColumnMenu;
  }

  isColumnVisible(
    key: string
  ): boolean {

    return !this.hiddenColumns.has(
      key
    );
  }

  toggleColumn(key: string): void {

    if (
      this.hiddenColumns.has(key)
    ) {

      this.hiddenColumns.delete(key);

    } else {

      this.hiddenColumns.add(key);
    }
  }

  resetColumns(): void {

    this.hiddenColumns.clear();
  }

  hideAllOptionalColumns(): void {

    this.hiddenColumns =
      new Set(
        this.columnOptions.map(
          column => column.key
        )
      );
  }

  visibleColumnCount(): number {

    return (
      this.columnOptions.length -
      this.hiddenColumns.size
    );
  }

  addOrder(): void {

    this.router.navigate([
      '/dashboard/orders/add'
    ]);
  }

  viewOrder(order: OrderListItem): void {

    this.router.navigate([
      '/dashboard/orders',
      order.id
    ]);
  }

  editOrder(
    order: OrderListItem,
    event: Event
  ): void {

    event.stopPropagation();

    this.router.navigate([
      '/dashboard/orders',
      order.id,
      'edit'
    ]);
  }

  // ============================================
  // DELETE MODAL METHODS
  // ============================================

  /**
   * Open delete confirmation modal
   */
  openDeleteModal(
    order: OrderListItem,
    event: Event
  ): void {
    event.stopPropagation();

    if (this.deletingOrder) {
      return;
    }

    this.deleteOrderItem = order;
    this.deleteError = '';
    this.showDeleteModal = true;
  }

  /**
   * Close delete confirmation modal
   */
  closeDeleteModal(): void {
    if (this.deletingOrder) {
      return;
    }

    this.showDeleteModal = false;
    this.deleteOrderItem = null;
    this.deleteError = '';
  }

  /**
   * Confirm and execute order deletion
   */
  confirmDeleteOrder(): void {
    if (!this.deleteOrderItem || this.deletingOrder) {
      return;
    }

    this.deletingOrder = true;
    this.deletingOrderId = this.deleteOrderItem.id;
    this.deleteError = '';

    this.ordersService.deleteOrder(this.deleteOrderItem.id).subscribe({
      next: () => {
        this.deletingOrder = false;
        this.deletingOrderId = null;
        this.showDeleteModal = false;
        this.deleteOrderItem = null;

        // Refetch so pagination/totals stay correct
        this.fetchOrders();
      },

      error: (error) => {
        console.error('Failed to delete order:', error);
        this.deletingOrder = false;
        this.deletingOrderId = null;

        this.deleteError =
          error?.error?.message ??
          'Unable to delete order. Please try again.';
      }
    });
  }

  get showingFrom(): number {

    if (this.totalCount === 0) {
      return 0;
    }

    return (
      (this.currentPage - 1) *
      this.pageSize
    ) + 1;
  }

  get showingTo(): number {

    return Math.min(
      this.currentPage *
      this.pageSize,
      this.totalCount
    );
  }

  @HostListener(
    'document:click',
    ['$event']
  )
  onDocumentClick(
    event: Event
  ): void {

    const target =
      event.target as HTMLElement;

    // Don't close anything if delete modal is open
    if (this.showDeleteModal) {
      return;
    }

    if (
      !target.closest(
        '[data-column-menu]'
      )
    ) {

      this.showColumnMenu = false;
    }

    if (
      !target.closest(
        '[data-pagesize-menu]'
      )
    ) {

      this.showPageSizeMenu = false;
    }

    if (
      !target.closest(
        '[data-filters-menu]'
      )
    ) {

      this.showFilters = false;
      this.openFilterDropdown = null;
    }

    if (!target.closest('[data-dd]')) {
      this.openFilterDropdown = null;
    }
  }
}