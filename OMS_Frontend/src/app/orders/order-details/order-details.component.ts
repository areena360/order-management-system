import { Component, HostListener, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { OrdersService } from '../orders.service';
import { LookupService, LOOKUP_TYPE } from '../lookup.service';
import {
  OrderDetails,
  LookupItem
} from '../order.models';
import {
  statusBadgeClass,
  priorityBadgeClass
} from '../order-badge.util';

import { PermissionService } from '../../auth/permission.service';

@Component({
  selector: 'app-order-details',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule
  ],
  templateUrl: './order-details.component.html'
})
export class OrderDetailsComponent implements OnInit {

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly ordersService = inject(OrdersService);
  private readonly lookupService = inject(LookupService);
  private readonly permissionService = inject(PermissionService);

  order: OrderDetails | null = null;

  statuses: LookupItem[] = [];

  loading = true;
  errorMsg = '';

  changingStatus = false;
  showStatusDropdown = false;

  // Delete Order Modal state
  showDeleteModal = false;
  deletingOrder = false;
  deleteError = '';

  // Image upload state
  selectedImages: File[] = [];
  uploadingImages = false;
  imageUploadError = '';

  // Image delete state
  deletingImageId: number | null = null;

  // Image Delete Modal state
  showImageDeleteModal = false;
  deletingImage = false;
  imageDeleteError = '';
  imageToDelete: any = null;

  // Inventory Bill modal state
  showBillModal = false;
  savingBill = false;
  billError = '';
  billForm: { billNumber: number | null; billDetails: string } = {
    billNumber: null,
    billDetails: ''
  };
  billImageFile: File | null = null;

  statusBadgeClass = statusBadgeClass;
  priorityBadgeClass = priorityBadgeClass;

  private orderId = 0;

  ngOnInit(): void {
    const id = Number(
      this.route.snapshot.paramMap.get('id')
    );

    if (!id || id <= 0) {
      this.errorMsg = 'Invalid order ID.';
      this.loading = false;
      return;
    }

    this.orderId = id;

    this.loadOrder();
    this.loadStatuses();
  }

  loadOrder(): void {
    this.loading = true;
    this.errorMsg = '';

    this.ordersService.getOrder(this.orderId).subscribe({
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

  private loadStatuses(): void {
    this.lookupService
      .getByType(LOOKUP_TYPE.OrderStatus)
      .subscribe({
        next: (statuses) => {
          this.statuses = statuses;
        },
        error: (error) => {
          console.error('Failed to load order statuses:', error);
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
    this.router.navigate(['/dashboard/orders']);
  }

  getFileUrl(fileUrl: string | null | undefined): string {
    return this.ordersService.getFileUrl(fileUrl);
  }

  changeStatus(statusId: number | string): void {
    const id = Number(statusId);

    if (!id || !this.order || this.changingStatus) return;
    if (id === this.order.orderStatusId) return;

    this.changingStatus = true;
    this.errorMsg = '';

    this.ordersService.updateStatus(this.order.id, id).subscribe({
      next: (updatedOrder) => {
        this.order = updatedOrder;
        this.changingStatus = false;
      },
      error: (error) => {
        console.error('Failed to update order status:', error);
        this.changingStatus = false;

        this.errorMsg =
          error?.error?.message ||
          'Unable to update order status. Please try again.';
      }
    });
  }

  /** Helper for custom dropdown display (used by Order Status dropdown). */
  optionName(options: LookupItem[], id: number | null | undefined): string {
    if (id === null || id === undefined) {
      return '';
    }
    return options.find(option => option.id === id)?.name ?? '';
  }

  /*
   * IMAGE HANDLING
   */

  onImagesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;

    if (!input.files?.length) return;

    this.imageUploadError = '';

    const files = Array.from(input.files);
    const validFiles: File[] = [];

    for (const file of files) {
      if (!file.type.startsWith('image/')) {
        this.imageUploadError = 'Only image files are allowed.';
        continue;
      }

      if (file.size > 10 * 1024 * 1024) {
        this.imageUploadError = 'Each image must be smaller than 10 MB.';
        continue;
      }

      validFiles.push(file);
    }

    this.selectedImages = validFiles;
    input.value = '';
  }

  removeSelectedImage(index: number): void {
    this.selectedImages.splice(index, 1);
  }

  clearSelectedImages(): void {
    this.selectedImages = [];
    this.imageUploadError = '';
  }

  uploadImages(): void {
    if (!this.order || !this.selectedImages.length || this.uploadingImages) return;

    this.uploadingImages = true;
    this.imageUploadError = '';

    this.ordersService.uploadImages(this.order.id, this.selectedImages).subscribe({
      next: () => {
        this.selectedImages = [];
        this.uploadingImages = false;
        this.loadOrder();
      },
      error: (error) => {
        console.error('Failed to upload images:', error);
        this.uploadingImages = false;

        this.imageUploadError =
          error?.error?.message ||
          'Unable to upload images. Please try again.';
      }
    });
  }

  // ============================================
  // IMAGE DELETE WITH CONFIRMATION MODAL
  // ============================================

  /**
   * Open image delete confirmation modal
   */
  openImageDeleteModal(image: any, event: Event): void {
    event.stopPropagation();

    if (this.deletingImage || this.deletingImageId !== null) {
      return;
    }

    this.imageToDelete = image;
    this.imageDeleteError = '';
    this.showImageDeleteModal = true;
  }

  /**
   * Close image delete confirmation modal
   */
  closeImageDeleteModal(): void {
    if (this.deletingImage) {
      return;
    }

    this.showImageDeleteModal = false;
    this.imageToDelete = null;
    this.imageDeleteError = '';
  }

  /**
   * Confirm and execute image deletion
   */
  confirmImageDelete(): void {
    if (!this.order || !this.imageToDelete || this.deletingImage) {
      return;
    }

    const imageId = this.imageToDelete.id;
    this.deletingImage = true;
    this.deletingImageId = imageId;
    this.imageDeleteError = '';

    this.ordersService.deleteImage(this.order.id, imageId).subscribe({
      next: () => {
        if (this.order) {
          this.order.images = this.order.images.filter(
            img => img.id !== imageId
          );
        }
        this.deletingImage = false;
        this.deletingImageId = null;
        this.showImageDeleteModal = false;
        this.imageToDelete = null;
      },
      error: (error) => {
        console.error('Failed to delete image:', error);
        this.deletingImage = false;
        this.deletingImageId = null;

        this.imageDeleteError =
          error?.error?.message ||
          'Unable to delete image. Please try again.';
      }
    });
  }

  formatFileSize(size: number): string {
    if (size < 1024) return `${size} B`;
    if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
    return `${(size / (1024 * 1024)).toFixed(1)} MB`;
  }

  /*
   * INVENTORY BILL
   */

  openBillModal(): void {
    this.billForm = { billNumber: null, billDetails: '' };
    this.billImageFile = null;
    this.billError = '';
    this.showBillModal = true;
  }

  closeBillModal(): void {
    if (this.savingBill) return;
    this.showBillModal = false;
  }

  onBillImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;

    const file = input.files[0];
    const allowedExt = ['.jpg', '.jpeg', '.png', '.webp', '.pdf'];
    const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();

    if (!allowedExt.includes(ext)) {
      this.billError = 'Unsupported file format. Use JPG, PNG, WEBP, or PDF.';
      input.value = '';
      return;
    }

    if (file.size > 10 * 1024 * 1024) {
      this.billError = 'File must be smaller than 10 MB.';
      input.value = '';
      return;
    }

    this.billError = '';
    this.billImageFile = file;
  }

  removeBillImage(): void {
    this.billImageFile = null;
  }

  saveBill(): void {
    if (!this.order || this.savingBill) return;

    if (!this.billForm.billDetails?.trim()) {
      this.billError = 'Bill details are required.';
      return;
    }

    this.savingBill = true;
    this.billError = '';

    this.ordersService.addInventoryBill(this.order.id, {
      billNumber: this.billForm.billNumber,
      billDetails: this.billForm.billDetails.trim(),
      billImageFile: this.billImageFile
    }).subscribe({
      next: (bill) => {
        if (this.order) {
          this.order.inventoryBills = [bill, ...this.order.inventoryBills];
        }
        this.savingBill = false;
        this.showBillModal = false;
      },
      error: (error) => {
        console.error('Failed to save inventory bill:', error);
        this.savingBill = false;

        this.billError =
          error?.error?.message ||
          'Unable to save inventory bill. Please try again.';
      }
    });
  }

  /*
   * GENERAL HELPERS
   */

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

  /*
   * DELETE ORDER
   */

  openDeleteModal(): void {
    if (!this.order || !this.canDeleteOrder()) {
      return;
    }

    this.deleteError = '';
    this.showDeleteModal = true;
  }

  closeDeleteModal(): void {
    if (this.deletingOrder) {
      return;
    }

    this.showDeleteModal = false;
    this.deleteError = '';
  }

  deleteOrder(): void {
    if (
      !this.order ||
      this.deletingOrder ||
      !this.canDeleteOrder()
    ) {
      return;
    }

    this.deletingOrder = true;
    this.deleteError = '';

    this.ordersService
      .deleteOrder(this.order.id)
      .subscribe({
        next: () => {
          this.deletingOrder = false;
          this.showDeleteModal = false;

          this.router.navigate([
            '/dashboard/orders'
          ]);
        },

        error: error => {
          console.error(
            'Failed to delete order:',
            error
          );

          this.deletingOrder = false;

          this.deleteError =
            error?.error?.message ||
            'Unable to delete order. Please try again.';
        }
      });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement;

    // Agar koi bhi modal open hai toh close mat karo
    if (this.showDeleteModal || this.showImageDeleteModal || this.showBillModal) {
      return;
    }

    if (!target.closest('[data-status-dd]')) {
      this.showStatusDropdown = false;
    }
  }
}