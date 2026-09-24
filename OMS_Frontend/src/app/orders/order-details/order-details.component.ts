import { Component, EventEmitter, Input, Output, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';

import { OrdersService } from '../orders.service';
import { OrderDetails } from '../order.models';
import {
  statusBadgeClass,
  priorityBadgeClass
} from '../order-badge.util';

import { PermissionService } from '../../auth/permission.service';
import { AuthService } from '../../auth/auth.service';
import { PollingService } from '../../core/polling/polling.service';

@Component({
  selector: 'app-order-details',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule
  ],
  templateUrl: './order-details.component.html'
})
export class OrderDetailsComponent implements OnInit, OnDestroy {
  @Input() asModal = false;
  @Input() selectedOrderId: number | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() deleted = new EventEmitter<void>();

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly ordersService = inject(OrdersService);
  private readonly permissionService = inject(PermissionService);
  private readonly authService = inject(AuthService);
  get isCustomer(): boolean { return this.authService.isCustomer(); }
  private readonly polling = inject(PollingService);

  private readonly destroy$ = new Subject<void>();

  order: OrderDetails | null = null;

  loading = true;
  errorMsg = '';

  showDeleteModal = false;
  deletingOrder = false;
  deleteError = '';

  selectedImagePreviewUrl: string | null = null;

  statusBadgeClass = statusBadgeClass;
  priorityBadgeClass = priorityBadgeClass;

  private orderId = 0;
  private silentRefreshBusy = false;

  ngOnInit(): void {
    const id = Number(
      this.selectedOrderId ?? this.route.snapshot.paramMap.get('id')
    );

    if (!id || id <= 0) {
      this.errorMsg = 'Invalid order ID.';
      this.loading = false;
      return;
    }

    this.orderId = id;

    this.loadOrder();
    this.setupPolling();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // =================== Backdrop click (modal only) ===================
  // Called when clicking directly on the outer fullscreen overlay.
  // The inner card uses (click)="$event.stopPropagation()" so clicks inside won't reach here.
  onBackdropClick(event: MouseEvent): void {
    if (!this.asModal) return;
    if (this.deletingOrder || this.showDeleteModal) return;
    if (event.target === event.currentTarget) {
      this.closed.emit();
    }
  }

  private setupPolling(): void {
    this.polling.poll(10000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.silentRefresh());
  }

  private silentRefresh(): void {
    if (this.silentRefreshBusy) return;
    if (this.showDeleteModal) return;
    if (this.loading) return;

    this.silentRefreshBusy = true;

    this.ordersService.getOrder(this.orderId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: order => {
          this.order = order;
          this.silentRefreshBusy = false;
        },
        error: error => {
          this.silentRefreshBusy = false;

          if (error?.status === 404) {
            if (this.asModal) this.deleted.emit();
            else this.goBack();
          }
        }
      });
  }

  loadOrder(): void {
    this.loading = true;
    this.errorMsg = '';

    this.ordersService.getOrder(this.orderId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (order) => {
          this.order = order;
          this.loading = false;
        },
        error: (error) => {
          console.error('Failed to load order:', error);
          this.errorMsg =
            error?.error?.message ||
            'Unable to load order details. Please try again.';
          this.loading = false;
        }
      });
  }

  canEditOrder(): boolean {
    return this.permissionService.canEdit('Orders');
  }

  editOrder(): void {
    if (!this.order) return;
    this.router.navigate(['/dashboard/orders', this.order.id, 'edit']);
  }

  goBack(): void {
    if (this.asModal) {
      if (!this.deletingOrder) this.closed.emit();
      return;
    }
    this.router.navigate(['/dashboard/orders']);
  }

  getFileUrl(fileUrl: string | null | undefined): string {
    return this.ordersService.getFileUrl(fileUrl);
  }

  formatDate(date: string | null | undefined): string {
    if (!date) return '—';
    const parsedDate = new Date(date);
    if (Number.isNaN(parsedDate.getTime())) return '—';
    return parsedDate.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    });
  }

  formatDateTime(date: string | null | undefined): string {
    if (!date) return '—';
    const parsedDate = new Date(date);
    if (Number.isNaN(parsedDate.getTime())) return '—';
    return parsedDate.toLocaleString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  isOverdue(): boolean {
    if (!this.order?.deadline) return false;
    const dl = Date.parse(this.order.deadline);
    return !isNaN(dl) && Date.now() > dl;
  }

  getImageUrl(imageUrl: string | null | undefined): string {
    return this.ordersService.getImageUrl(imageUrl);
  }

  hasValue(value: unknown): boolean {
    return value !== null && value !== undefined && value !== '';
  }

  trackById(index: number, item: { id: number }): number {
    return item.id;
  }

  canDeleteOrder(): boolean {
    return this.permissionService.canDelete('Orders');
  }

  openDeleteModal(): void {
    if (!this.order || !this.canDeleteOrder()) return;
    this.deleteError = '';
    this.showDeleteModal = true;
  }

  closeDeleteModal(): void {
    if (this.deletingOrder) return;
    this.showDeleteModal = false;
    this.deleteError = '';
  }

  deleteOrder(): void {
    if (!this.order || this.deletingOrder || !this.canDeleteOrder()) return;

    this.deletingOrder = true;
    this.deleteError = '';

    this.ordersService.deleteOrder(this.order.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.deletingOrder = false;
          this.showDeleteModal = false;
          if (this.asModal) this.deleted.emit();
          else this.goBack();
        },
        error: error => {
          console.error('Failed to delete order:', error);
          this.deletingOrder = false;
          this.deleteError =
            error?.error?.message ||
            'Unable to delete order. Please try again.';
        }
      });
  }

  openImagePreview(url: string): void {
    this.selectedImagePreviewUrl = url;
  }

  closeImagePreview(): void {
    this.selectedImagePreviewUrl = null;
  }
}