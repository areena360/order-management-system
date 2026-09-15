import {
  Component, HostListener, OnDestroy, OnInit, ViewChild, ElementRef,
  ChangeDetectorRef, inject
} from '@angular/core';
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
import { ChatModalComponent } from '../chat/chat-modal/chat-modal.component';
import {
  ChatSignalrService,
  IncomingChatMessage,
} from '../../core/signalr/chat-signalr.service';

import { CustomerOption, LookupItem, OrderImageItem, OrderListItem, OrderQuery } from '../order.models';
import { statusBadgeClass, priorityBadgeClass } from '../order-badge.util';

interface ColumnOption { key: string; label: string; }

@Component({
  selector: 'app-manage-orders',
  standalone: true,
  imports: [CommonModule, FormsModule, FooterComponent, ChatModalComponent],
  templateUrl: './manage-orders.component.html'
})
export class ManageOrdersComponent implements OnInit, OnDestroy {

  private readonly polling = inject(PollingService);
  private readonly chatSignalr = inject(ChatSignalrService);
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

  // Chat modal state
  showChatModal = false;
  chatOrderId: number | null = null;
  chatOrderNumber = '';
  chatCustomerName = '';

  // Unread message counters, keyed by orderId
  unreadMessages = new Map<number, number>();
  currentUserId = 0;

  private searchInput$ = new Subject<string>();
  private destroy$ = new Subject<void>();
  private unregisterChatListener: (() => void) | null = null;

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

  // Inline status dropdown state
  openStatusId: number | null = null;
  statusSavingId: number | null = null;

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

    // Excel-style alternating rows: light blue / white
  private readonly rowColors = [
    'bg-blue-50 hover:bg-blue-100',
    'bg-white hover:bg-gray-100'
  ];

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
    this.currentUserId = this.readUserIdFromToken();

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

    this.setupChatNotifications();
    this.loadLookups();
    this.fetchOrders();
    this.setupPolling();
  }

  ngOnDestroy(): void {
    if (this.unregisterChatListener) this.unregisterChatListener();
    this.tableResizeObserver?.disconnect();
    this.destroy$.next();
    this.destroy$.complete();
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

  /**
   * Single click jumps the table all the way to the left or right end.
   * Uses smooth behavior for a nice animation. Works reliably on all devices
   * (mobile, tablet, 1x/2x DPR) since it goes straight to the boundary.
   */
  scrollTable(direction: 'left' | 'right'): void {
    const el = this.tableScrollEl?.nativeElement;
    if (!el) return;

    const maxScroll = Math.max(0, el.scrollWidth - el.clientWidth);
    if (maxScroll === 0) return;

    const target = direction === 'left' ? 0 : maxScroll;

    el.scrollTo({ left: target, behavior: 'smooth' });
  }

  // =================== Multi-color rows ===================

  rowColorClass(index: number): string {
    return this.rowColors[index % this.rowColors.length];
  }

  // =================== Inline Status Dropdown ===================

  toggleStatusMenu(orderId: number): void {
    if (this.statusSavingId === orderId) return;
    this.openStatusId = this.openStatusId === orderId ? null : orderId;
  }

  isCurrentStatus(order: OrderListItem, status: LookupItem): boolean {
    const id = (order as any).statusId;
    if (id !== undefined && id !== null) return id === status.id;
    return order.status === status.name;
  }

  changeOrderStatus(order: OrderListItem, statusId: number): void {
    if (!this.canEdit) return;

    const targetStatus = this.statuses.find(s => s.id === statusId);
    if (!targetStatus) return;

    if (this.isCurrentStatus(order, targetStatus)) {
      this.openStatusId = null;
      return;
    }

    this.statusSavingId = order.id;
    this.openStatusId = null;

    this.ordersService.updateStatus(order.id, statusId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          order.status = targetStatus.name;
          (order as any).statusId = statusId;
          this.statusSavingId = null;
        },
        error: (error) => {
          this.statusSavingId = null;
          this.errorMsg = error?.error?.message ?? 'Unable to update status. Please try again.';
        }
      });
  }

  // =================== Inline Tracking Number Edit ===================

  onTrackingChange(order: OrderListItem, value: string): void {
    const newVal = (value ?? '').trim();
    const oldVal = (order.trackingNumber ?? '').trim();
    if (newVal === oldVal) return;

    this.ordersService.getOrder(order.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (full: any) => {
          const payload: any = {
            customerProductTitle: full.customerProductTitle,
            manufacturerProductTitle: full.manufacturerProductTitle,
            customerOrderNumber: full.customerOrderNumber,
            customerId: full.customerId,
            amount: full.amount,
            genderId: full.genderId,
            customerMaterialId: full.customerMaterialId,
            manufacturerMaterialId: full.manufacturerMaterialId,
            isCustomSize: full.isCustomSize,
            sizeId: full.sizeId,
            sizeChartId: full.sizeChartId,
            sizeDetails: full.sizeDetails,
            daysForMaking: full.daysForMaking,
            priorityId: full.priorityId,
            consigneeName: full.consigneeName,
            consigneeAddress: full.consigneeAddress,
            trackingNumber: newVal || null,
            notesByCustomer: full.notesByCustomer,
            notesByManufacturer: full.notesByManufacturer
          };

          this.ordersService.updateOrder(order.id, payload)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
              next: () => {
                order.trackingNumber = newVal || null;
              },
              error: (error) => {
                this.errorMsg = error?.error?.message ?? 'Unable to update tracking number.';
              }
            });
        },
        error: () => {
          this.errorMsg = 'Unable to load order for tracking update.';
        }
      });
  }

  // =================== Chat notifications ===================

  private setupChatNotifications(): void {
    this.unregisterChatListener = this.chatSignalr.onMessage(
      (msg: IncomingChatMessage) => this.onIncomingChatMessage(msg)
    );
  }

  private onIncomingChatMessage(msg: IncomingChatMessage): void {
    if (msg.senderUserId === this.currentUserId) return;
    if (this.showChatModal && this.chatOrderId === msg.orderId) return;

    const current = this.unreadMessages.get(msg.orderId) ?? 0;
    this.unreadMessages.set(msg.orderId, current + 1);
    this.unreadMessages = new Map(this.unreadMessages);
  }

  private readUserIdFromToken(): number {
    const token = this.authService.getToken();
    if (!token) return 0;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return Number(payload.userId ?? 0);
    } catch {
      return 0;
    }
  }

  getUnreadCount(orderId: number): number {
    return this.unreadMessages.get(orderId) ?? 0;
  }

  hasUnread(orderId: number): boolean {
    return this.getUnreadCount(orderId) > 0;
  }

  get totalUnread(): number {
    let total = 0;
    this.unreadMessages.forEach(v => total += v);
    return total;
  }

  // =================== Polling / fetching ===================

  private setupPolling(): void {
    this.polling.poll(10000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.silentRefresh());
  }

  private silentRefresh(): void {
    if (this.silentRefreshBusy) return;
    if (this.showDeleteModal || this.showImageModal || this.showChatModal) return;
    if (this.loading) return;
    if (this.openStatusId !== null) return;
    if (this.statusSavingId !== null) return;

    this.silentRefreshBusy = true;

    this.ordersService.getOrders(this.buildQuery())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.orders = response.items;
          this.totalCount = response.totalCount;
          this.silentRefreshBusy = false;
          setTimeout(() => this.updateHorizontalScrollState(), 0);
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
          setTimeout(() => this.updateHorizontalScrollState(), 0);
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
    setTimeout(() => this.updateHorizontalScrollState(), 0);
  }

  resetColumns(): void {
    this.hiddenColumns.clear();
    setTimeout(() => this.updateHorizontalScrollState(), 0);
  }

  hideAllOptionalColumns(): void {
    this.hiddenColumns = new Set(this.columnOptions.map(c => c.key));
    setTimeout(() => this.updateHorizontalScrollState(), 0);
  }

  visibleColumnCount(): number {
    return this.columnOptions.length - this.hiddenColumns.size;
  }

  // =================== Navigation / Actions ===================

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
        this.deletingOrder = false;
        this.deletingOrderId = null;
        this.deleteError = error?.error?.message ?? 'Unable to delete order. Please try again.';
      }
    });
  }

  // =================== Image modal ===================

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

  // =================== CHAT ===================

  openChatForOrder(order: OrderListItem, event: Event): void {
    event.stopPropagation();

    if (!order.id) return;

    if (this.unreadMessages.has(order.id)) {
      this.unreadMessages.delete(order.id);
      this.unreadMessages = new Map(this.unreadMessages);
    }

    this.chatOrderId = order.id;
    this.chatOrderNumber = order.manufacturerOrderNumber ?? '';
    this.chatCustomerName = order.customerName ?? '';
    this.showChatModal = true;
  }

  closeChatModal(): void {
    this.showChatModal = false;
    this.chatOrderId = null;
    this.chatOrderNumber = '';
    this.chatCustomerName = '';
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (this.showDeleteModal || this.showImageModal || this.showChatModal) return;

    if (!target.closest('[data-column-menu]')) this.showColumnMenu = false;
    if (!target.closest('[data-pagesize-menu]')) this.showPageSizeMenu = false;
    if (!target.closest('[data-dd]')) this.openFilterDropdown = null;
    if (!target.closest('[data-order-status-menu]')) this.openStatusId = null;
  }
}