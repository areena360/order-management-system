import { Component, EventEmitter, Input, OnDestroy, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, forkJoin, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, finalize, switchMap, takeUntil } from 'rxjs/operators';

import { FooterComponent } from '../../footer/footer.component';
import { LookupService, LOOKUP_TYPE } from '../lookup.service';
import { OrdersService } from '../orders.service';
import { CustomerOption, LookupItem, OrderDetails, OrderFormValue } from '../order.models';
import { priorityBadgeClass, statusBadgeClass } from '../order-badge.util';
import { AuthService } from '../../auth/auth.service';
import { PollingService } from '../../core/polling/polling.service';

interface PendingBill {
  billNumber: number | null;
  billDetails: string;
  billImageFile: File | null;
}

@Component({
  selector: 'app-order-form',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, FooterComponent],
  templateUrl: './order-form.component.html',
  styles: [':host input:disabled, :host textarea:disabled, :host button:disabled { background-color: #f3f4f6; color: #6b7280; cursor: not-allowed; }']
})
export class OrderFormComponent implements OnInit, OnDestroy {

  @Input() asModal = false;
  @Input() orderId: number | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly polling = inject(PollingService);

  form = this.fb.group({
    customerProductTitle: ['', [Validators.maxLength(200)]],
    manufacturerProductTitle: ['', [Validators.maxLength(200)]],
    customerOrderNumber: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]],
    manufacturerOrderNumber: ['', [Validators.maxLength(100)]],
    customerId: [null as number | null, Validators.required],
    amount: [null as number | null, [Validators.min(0)]],
    genderId: [null as number | null, Validators.required],
    customerMaterialId: [null as number | null],
    manufacturerMaterialId: [null as number | null],
    isCustomSize: [false],
    sizeId: [null as number | null],
    sizeChartId: [null as number | null],
    sizeDetails: ['', [Validators.maxLength(2000)]],
    priorityId: [null as number | null],
    statusId: [null as number | null, Validators.required],
    consigneeName: ['', [Validators.required, Validators.maxLength(200)]],
    shippingEmail: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    shippingContact: ['', [Validators.required, Validators.maxLength(100)]],
    consigneeAddress: ['', [Validators.required, Validators.maxLength(1000)]],
    courier: [''],
    trackingNumber: ['', [Validators.maxLength(200)]],
    deadline: [null as string | null],
    notesByCustomer: ['', [Validators.maxLength(3000)]],
    notesByManufacturer: ['', [Validators.maxLength(3000)]]
  });

  isEditMode = false;
  loading = true;
  saving = false;
  errorMsg = '';
  orderNumber: string | null = null;
  currentStatus: string | null = null;

  statuses: LookupItem[] = [];
  priorities: LookupItem[] = [];
  genders: LookupItem[] = [];
  materials: LookupItem[] = [];
  sizes: LookupItem[] = [];
  sizeCharts: LookupItem[] = [];
  readonly couriers = ['DHL', 'SkyNet', 'FedEx', 'UPS'];

  customerOptions: CustomerOption[] = [];
  customerSearchTerm = '';
  selectedCustomer: CustomerOption | null = null;
  showCustomerDropdown = false;
  customerLoading = false;

  openDropdown: string | null = null;

  showStatusDropdown = false;
  changingStatus = false;

  order: OrderDetails | null = null;

  selectedImages: File[] = [];
  selectedImagePreviews: string[] = [];
  uploadingImages = false;
  imageUploadError = '';
  deletingImageId: number | null = null;

  selectedImagePreviewUrl: string | null = null;

  showImageDeleteModal = false;
  deletingImage = false;
  imageDeleteError = '';
  imageToDelete: any = null;

  showBillModal = false;
  savingBill = false;
  billError = '';
  billForm: { billNumber: number | null; billDetails: string } = { billNumber: null, billDetails: '' };
  billImageFile: File | null = null;
  editingBillId: number | null = null;

  pendingBills: PendingBill[] = [];

  existingImages: { id: number; url: string; fileName: string }[] = [];
  existingBills: { id: number; billNumber?: number | null; billDetails: string; fileUrl?: string }[] = [];

  showConfirmDeleteModal = false;
  confirmDeleteTitle = '';
  confirmDeleteMessage = '';
  confirmDeleteBusy = false;
  confirmDeleteError = '';
  private confirmDeleteAction: (() => void) | null = null;

  private customerSearch$ = new Subject<string>();
  private destroy$ = new Subject<void>();
  private statusPollBusy = false;

  statusBadgeClass = statusBadgeClass;
  priorityBadgeClass = priorityBadgeClass;

  isCustomer = false;

  get canEditTracking(): boolean {
    return ['Super Admin', 'Admin', 'Staff'].includes(this.authService.currentRole() ?? '');
  }

  get canEditAmount(): boolean {
    return ['Super Admin', 'Admin', 'Finance'].includes(this.authService.currentRole() ?? '');
  }

  get canEditDeadline(): boolean {
    return ['Super Admin', 'Admin'].includes(this.authService.currentRole() ?? '');
  }

  private applyFieldPermissions(): void {
    if (!this.canEditTracking) this.form.controls.trackingNumber.disable({ emitEvent: false });
    if (!this.canEditAmount) this.form.controls.amount.disable({ emitEvent: false });
    if (!this.canEditDeadline) this.form.controls.deadline.disable({ emitEvent: false });

    if (this.isCustomer) {
      for (const name of ['customerId', 'manufacturerOrderNumber', 'manufacturerProductTitle', 'manufacturerMaterialId', 'statusId', 'notesByManufacturer', 'courier']) {
        this.form.get(name)?.disable({ emitEvent: false });
      }
      this.form.controls.priorityId.disable({ emitEvent: false });
      this.form.controls.customerMaterialId.clearValidators();
      this.form.controls.customerProductTitle.setValidators([Validators.required, Validators.maxLength(200)]);
    } else {
      this.form.controls.notesByCustomer.disable({ emitEvent: false });
      this.form.controls.statusId.disable({ emitEvent: false });
      this.form.controls.manufacturerMaterialId.setValidators([Validators.required]);
      this.form.controls.manufacturerProductTitle.setValidators([Validators.required, Validators.maxLength(200)]);
      this.form.controls.customerOrderNumber.setValidators([Validators.required, Validators.maxLength(100)]);
      this.form.controls.priorityId.setValidators([Validators.required]);
      this.form.controls.courier.setValidators([Validators.required]);
      this.form.controls.trackingNumber.setValidators([Validators.required, Validators.maxLength(200)]);
    }
    this.form.controls.customerMaterialId.updateValueAndValidity({ emitEvent: false });
    this.form.controls.manufacturerMaterialId.updateValueAndValidity({ emitEvent: false });
    this.form.controls.customerProductTitle.updateValueAndValidity({ emitEvent: false });
    this.form.controls.customerOrderNumber.updateValueAndValidity({ emitEvent: false });
    this.form.controls.manufacturerProductTitle.updateValueAndValidity({ emitEvent: false });
    this.form.controls.priorityId.updateValueAndValidity({ emitEvent: false });
    this.form.controls.courier.updateValueAndValidity({ emitEvent: false });
    this.form.controls.trackingNumber.updateValueAndValidity({ emitEvent: false });

    for (const control of Object.values(this.form.controls)) {
      if (control.hasValidator(Validators.required)) {
        control.addValidators(Validators.pattern(/\S/));
        control.updateValueAndValidity({ emitEvent: false });
      }
    }
  }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private ordersService: OrdersService,
    private lookupService: LookupService
  ) {}

  ngOnInit(): void {
    this.isCustomer = this.authService.isCustomer();

    this.setupSizeValidation();
    this.setupCustomerSearch();

    if (this.isCustomer) {
      this.authService.getProfile()
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: profile => {
            this.form.controls.customerId.setValue(profile.id);
            this.selectedCustomer = {
              id: profile.id,
              name: `${profile.firstName} ${profile.lastName}`,
              email: profile.email
            };
            this.customerSearchTerm = this.selectedCustomer.name;
          },
          error: () => { this.errorMsg = 'Unable to load your customer profile. Please refresh and try again.'; }
        });
    }

    let idParam: string | null = null;
    if (this.asModal && this.orderId) {
      idParam = this.orderId.toString();
    } else if (!this.asModal) {
      idParam = this.route.snapshot.paramMap.get('id');
    }

    if (idParam) {
      const id = Number(idParam);
      if (Number.isNaN(id) || id <= 0) {
        this.errorMsg = 'Invalid order ID.';
        this.loading = false;
        return;
      }
      this.isEditMode = true;
      this.orderId = id;
    } else {
      this.isEditMode = false;
      this.orderId = null;
    }

    this.applyFieldPermissions();
    this.loadLookups();
    this.setupStatusPolling();

    document.addEventListener('click', this.documentClickListener, true);
  }

  ngOnDestroy(): void {
    document.removeEventListener('click', this.documentClickListener, true);
    this.clearSelectedImages();
    this.destroy$.next();
    this.destroy$.complete();
  }

  private readonly documentClickListener = (event: Event): void => {
    const target = event.target as HTMLElement;
    if (this.showImageDeleteModal || this.showBillModal || this.showConfirmDeleteModal || this.selectedImagePreviewUrl) return;
    if (!target.closest('[data-customer-select]')) this.showCustomerDropdown = false;
    if (!target.closest('[data-dd]')) this.openDropdown = null;
    if (!target.closest('[data-status-dd]')) this.showStatusDropdown = false;
  };

  private setupStatusPolling(): void {
    if (!this.isEditMode || !this.orderId) return;

    this.polling.poll(15000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.syncStatusOnly());
  }

  private syncStatusOnly(): void {
    if (!this.orderId) return;
    if (this.statusPollBusy) return;
    if (this.saving || this.changingStatus) return;
    if (this.showBillModal || this.showConfirmDeleteModal) return;
    if (this.loading) return;

    this.statusPollBusy = true;

    this.ordersService.getOrder(this.orderId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: order => {
          this.statusPollBusy = false;
          if (order.status !== this.currentStatus) {
            this.currentStatus = order.status;
          }
          const control = this.form.controls.statusId;
          const serverStatusId = (order as any).orderStatusId ?? null;
          if (!control.dirty && control.value !== serverStatusId) {
            control.setValue(serverStatusId, { emitEvent: false });
          }
        },
        error: () => { this.statusPollBusy = false; }
      });
  }

  private setupSizeValidation(): void {
    this.updateSizeValidators();
    this.form.controls.isCustomSize.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.updateSizeValidators());
  }

  private updateSizeValidators(): void {
    const isCustom = this.form.controls.isCustomSize.value;
    const sizeControl = this.form.controls.sizeId;
    const chartControl = this.form.controls.sizeChartId;
    const detailsControl = this.form.controls.sizeDetails;

    sizeControl.setValidators([Validators.required]);
    chartControl.setValidators([Validators.required]);
    if (isCustom) {
      detailsControl.setValidators([Validators.required, Validators.maxLength(2000)]);
    } else {
      sizeControl.setValidators([Validators.required]);
      chartControl.setValidators([Validators.required]);
      detailsControl.setValidators([Validators.maxLength(2000)]);
    }

    sizeControl.updateValueAndValidity({ emitEvent: false });
    chartControl.updateValueAndValidity({ emitEvent: false });
    detailsControl.updateValueAndValidity({ emitEvent: false });
  }

  private setupCustomerSearch(): void {
    this.customerSearch$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap(search => {
          this.customerLoading = true;
          return this.lookupService.getCustomers(search).pipe(
            catchError(() => of([])),
            finalize(() => (this.customerLoading = false))
          );
        }),
        takeUntil(this.destroy$)
      )
      .subscribe(customers => (this.customerOptions = customers));
  }

  private loadLookups(): void {
    this.loading = true;

    const lookups$ = this.isCustomer
      ? forkJoin({
          statuses: this.lookupService.getByType(LOOKUP_TYPE.OrderStatus),
          genders: this.lookupService.getByType(LOOKUP_TYPE.Gender),
          materials: this.lookupService.getByType(LOOKUP_TYPE.Material),
          sizes: this.lookupService.getByType(LOOKUP_TYPE.Size),
          sizeCharts: this.lookupService.getByType(LOOKUP_TYPE.SizeChart),
          priorities: of([] as LookupItem[])
        })
      : forkJoin({
          statuses: this.lookupService.getByType(LOOKUP_TYPE.OrderStatus),
          priorities: this.lookupService.getByType(LOOKUP_TYPE.Priority),
          genders: this.lookupService.getByType(LOOKUP_TYPE.Gender),
          materials: this.lookupService.getByType(LOOKUP_TYPE.Material),
          sizes: this.lookupService.getByType(LOOKUP_TYPE.Size),
          sizeCharts: this.lookupService.getByType(LOOKUP_TYPE.SizeChart)
        });

    lookups$
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          this.statuses = result.statuses;
          this.priorities = result.priorities;
          this.genders = result.genders;
          this.materials = [...result.materials, { id: -1, name: 'Other Material' } as LookupItem];
          this.sizes = result.sizes;
          this.sizeCharts = result.sizeCharts;

          if (!this.isEditMode) {
            const name = this.isCustomer ? 'new' : 'assign';
            this.form.controls.statusId.setValue(this.statuses.find(item => item.name.toLowerCase() === name)?.id ?? null, { emitEvent: false });
          }

          if (this.isEditMode && this.orderId) {
            this.loadOrder(this.orderId);
          } else {
            this.loading = false;
            if (!this.isCustomer) this.customerSearch$.next('');
          }
        },
        error: () => {
          this.errorMsg = 'Unable to load order form data. Please refresh and try again.';
          this.loading = false;
        }
      });
  }

  private loadOrder(id: number): void {
    this.ordersService.getOrder(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: order => { this.patchOrder(order); this.loading = false; },
        error: (error: HttpErrorResponse) => {
          this.errorMsg = error?.error?.message ?? 'Unable to load order. Please try again.';
          this.loading = false;
        }
      });
  }

  private patchOrder(order: OrderDetails): void {
    this.order = order;
    this.orderNumber = order.manufacturerOrderNumber;
    this.currentStatus = order.status;

    this.form.patchValue({
      customerProductTitle: order.customerProductTitle,
      manufacturerProductTitle: order.manufacturerProductTitle,
      customerOrderNumber: order.customerOrderNumber,
      manufacturerOrderNumber: order.manufacturerOrderNumber,
      customerId: order.customerId,
      amount: order.amount,
      genderId: order.genderId,
      customerMaterialId: order.customerMaterialId,
      manufacturerMaterialId: order.manufacturerMaterialId,
      isCustomSize: order.isCustomSize,
      sizeId: order.sizeId,
      sizeChartId: order.sizeChartId,
      sizeDetails: order.sizeDetails,
      priorityId: order.priorityId,
      statusId: (order as any).orderStatusId ?? null,
      consigneeName: order.consigneeName,
      shippingEmail: order.shippingEmail ?? '',
      shippingContact: order.shippingContact ?? '',
      consigneeAddress: order.consigneeAddress,
      courier: order.courier ?? '',
      trackingNumber: order.trackingNumber,
      deadline: order.deadline,
      notesByCustomer: order.notesByCustomer,
      notesByManufacturer: order.notesByManufacturer
    }, { emitEvent: false });

    this.selectedCustomer = { id: order.customerId, name: order.customerName, email: '' };
    this.customerSearchTerm = order.customerName;

    this.existingImages = (order.images ?? []).map((img: any) => ({
      id: img.id,
      url: this.ordersService.getImageUrl(img.imageURL ?? img.imageUrl),
      fileName: (img.imageURL ?? img.imageUrl ?? '').split('/').pop() ?? ''
    }));

    this.existingBills = (order.inventoryBills ?? []).map((bill: any) => ({
      id: bill.id,
      billNumber: bill.billNumber,
      billDetails: bill.billDetails,
      fileUrl: bill.billImage ? this.ordersService.getFileUrl(bill.billImage) : undefined
    }));

    this.updateSizeValidators();
  }

  onCustomerFocus(): void {
    if (this.form.controls.customerId.disabled) return;
    this.showCustomerDropdown = true;
    if (this.customerOptions.length === 0) {
      this.customerSearch$.next(this.customerSearchTerm.trim());
    }
  }

  onCustomerInput(value: string): void {
    if (this.form.controls.customerId.disabled) return;
    this.customerSearchTerm = value;
    this.showCustomerDropdown = true;
    const normalizedValue = value.trim().toLowerCase();
    if (this.selectedCustomer && normalizedValue !== this.selectedCustomer.name.trim().toLowerCase()) {
      this.selectedCustomer = null;
      this.form.controls.customerId.setValue(null);
      this.form.controls.customerId.markAsTouched();
    }
    this.customerSearch$.next(value.trim());
  }

  selectCustomer(customer: CustomerOption): void {
    if (this.form.controls.customerId.disabled) return;
    this.selectedCustomer = customer;
    this.customerSearchTerm = customer.name;
    this.form.controls.customerId.setValue(customer.id);
    this.form.controls.customerId.markAsTouched();
    this.form.controls.customerId.markAsDirty();
    this.showCustomerDropdown = false;
  }

  clearCustomer(): void {
    if (this.isCustomer) return;
    this.selectedCustomer = null;
    this.customerSearchTerm = '';
    this.form.controls.customerId.setValue(null);
    this.form.controls.customerId.markAsTouched();
    this.showCustomerDropdown = true;
    this.customerSearch$.next('');
  }

  toggleDropdown(key: string): void {
    if (this.form.get(key)?.disabled) return;
    this.openDropdown = this.openDropdown === key ? null : key;
  }

  selectDropdown(controlName: string, value: number | null): void {
    const control = this.form.get(controlName);
    if (!control || control.disabled) return;
    control.setValue(value);
    control.markAsTouched();
    control.markAsDirty();
    this.openDropdown = null;
  }

  optionName(options: LookupItem[], id: number | null | undefined): string {
    if (id === null || id === undefined) return '';
    return options.find(o => o.id === id)?.name ?? '';
  }

  onStatusSelected(statusId: number): void {
    if (this.form.controls.statusId.disabled) return;
    this.showStatusDropdown = false;

    const control = this.form.controls.statusId;
    if (control.value === statusId) return;

    const previousValue = control.value;
    control.setValue(statusId);
    control.markAsDirty();
    control.markAsTouched();

    if (!this.isEditMode || !this.orderId) return;

    this.changingStatus = true;
    this.ordersService.updateStatus(this.orderId, statusId)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => (this.changingStatus = false))
      )
      .subscribe({
        next: updatedOrder => {
          this.order = updatedOrder;
          this.currentStatus = updatedOrder.status;
        },
        error: (error: HttpErrorResponse) => {
          this.errorMsg = error?.error?.message ?? 'Unable to update status. Please try again.';
          control.setValue(previousValue, { emitEvent: false });
        }
      });
  }

  save(): void {
    if (this.saving) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errorMsg = 'Please complete all required fields before saving.';
      return;
    }

    const raw = this.form.getRawValue();

    if (!raw.customerId) {
      this.errorMsg = this.isCustomer ? 'Your customer profile is not loaded. Please refresh and try again.' : 'Please select a customer.';
      this.form.controls.customerId.markAsTouched();
      return;
    }

    const payload: any = {
      customerProductTitle: raw.customerProductTitle?.trim() ?? '',
      manufacturerProductTitle: this.isCustomer ? null : this.nullIfBlank(raw.manufacturerProductTitle),
      customerOrderNumber: this.nullIfBlank(raw.customerOrderNumber),
      manufacturerOrderNumber: this.isCustomer ? undefined : this.nullIfBlank(raw.manufacturerOrderNumber),
      customerId: raw.customerId,
      amount: this.canEditAmount ? (raw.amount === null ? null : Number(raw.amount)) : (this.order?.amount ?? null),
      genderId: raw.genderId,
      customerMaterialId: raw.customerMaterialId,
      manufacturerMaterialId: this.isCustomer ? (this.order?.manufacturerMaterialId ?? null) : raw.manufacturerMaterialId,
      isCustomSize: raw.isCustomSize ?? false,
      sizeId: raw.sizeId,
      sizeChartId: raw.sizeChartId,
      sizeDetails: raw.isCustomSize ? this.nullIfBlank(raw.sizeDetails) : null,
      priorityId: this.isCustomer ? (this.order?.priorityId ?? null) : raw.priorityId,
      consigneeName: raw.consigneeName?.trim() ?? '',
      shippingEmail: this.nullIfBlank(raw.shippingEmail),
      shippingContact: this.nullIfBlank(raw.shippingContact),
      consigneeAddress: raw.consigneeAddress?.trim() ?? '',
      courier: this.isCustomer ? (this.order?.courier ?? null) : this.nullIfBlank(raw.courier),
      trackingNumber: this.canEditTracking ? this.nullIfBlank(raw.trackingNumber) : (this.order?.trackingNumber ?? null),
      deadline: this.canEditDeadline ? this.nullIfBlank(raw.deadline) : (this.order?.deadline ?? null),
      notesByCustomer: this.isCustomer ? this.nullIfBlank(raw.notesByCustomer) : (this.order?.notesByCustomer ?? null),
      notesByManufacturer: this.isCustomer ? null : this.nullIfBlank(raw.notesByManufacturer)
    };

    if (!this.isEditMode && this.form.controls.statusId.enabled && raw.statusId) {
      payload.statusId = raw.statusId;
    }

    this.saving = true;
    this.errorMsg = '';

    if (this.isEditMode && this.orderId) {
      this.ordersService.updateOrder(this.orderId, payload)
        .pipe(takeUntil(this.destroy$), finalize(() => (this.saving = false)))
        .subscribe({
          next: () => {
            if (this.asModal) this.saved.emit();
            else this.router.navigate(['/dashboard/orders']);
          },
          error: (error: HttpErrorResponse) => {
            this.showSaveError(error);
          }
        });
      return;
    }

    this.ordersService.createOrder(payload)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: createdOrder => this.attachImagesAndBills(createdOrder.id),
        error: (error: HttpErrorResponse) => {
          this.saving = false;
          this.showSaveError(error);
        }
      });
  }

  private attachImagesAndBills(orderId: number): void {
    const calls: any[] = [];

    if (this.selectedImages.length) {
      calls.push(this.ordersService.uploadImages(orderId, this.selectedImages));
    }

    if (!this.isCustomer) {
      this.pendingBills.forEach(bill => {
        calls.push(this.ordersService.addInventoryBill(orderId, bill));
      });
    }

    if (!calls.length) {
      this.saving = false;
      if (this.asModal) this.saved.emit();
      else this.router.navigate(['/dashboard/orders']);
      return;
    }

    forkJoin(calls)
      .pipe(takeUntil(this.destroy$), finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          if (this.asModal) this.saved.emit();
          else this.router.navigate(['/dashboard/orders']);
        },
        error: (error: HttpErrorResponse) => {
          this.errorMsg = error?.error?.message
            ?? 'Order was created, but some images/bills failed to upload.';
          if (this.asModal) this.saved.emit();
          else this.router.navigate(['/dashboard/orders']);
        }
      });
  }

  cancel(): void {
    if (this.asModal) {
      this.closed.emit();
    } else {
      this.router.navigate(['/dashboard/orders']);
    }
  }

  isInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  getError(controlName: string): string {
    const control = this.form.get(controlName);
    if (!control?.errors) return '';
    if (control.errors['required']) return 'This field is required.';
    if (control.errors['pattern']) return 'Enter a value, not only spaces.';
    if (control.errors['email']) return 'Enter a valid email address.';
    if (control.errors['maxlength']) return `Maximum ${control.errors['maxlength'].requiredLength} characters allowed.`;
    if (control.errors['min']) return `Value must be at least ${control.errors['min'].min}.`;
    if (control.errors['max']) return `Value cannot be greater than ${control.errors['max'].max}.`;
    return 'Invalid value.';
  }

  private nullIfBlank(value: string | null): string | null {
    if (value === null || value === undefined) return null;
    const trimmed = value.trim();
    return trimmed.length > 0 ? trimmed : null;
  }

  private showSaveError(error: HttpErrorResponse): void {
    const errors = error.error?.errors as Record<string, string[]> | undefined;
    this.errorMsg = errors ? Object.values(errors).flat().join(' ') :
      (error.error?.message ?? error.error?.title ?? 'Unable to save order. Please try again.');
  }

  get pageTitle(): string { return this.isEditMode ? 'Edit Order' : 'Add Order'; }
  get pageSubtitle(): string {
    return this.isEditMode ? 'Update the order information below.' : 'Create a new customer order.';
  }

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

    if (this.isEditMode && this.orderId && validFiles.length) {
      this.uploadingImages = true;
      this.ordersService.uploadImages(this.orderId, validFiles)
        .pipe(takeUntil(this.destroy$), finalize(() => (this.uploadingImages = false)))
        .subscribe({
          next: (uploaded: any[]) => {
            const mapped = (uploaded ?? []).map(img => ({
              id: img.id,
              url: this.ordersService.getImageUrl(img.imageURL ?? img.imageUrl),
              fileName: (img.imageURL ?? img.imageUrl ?? '').split('/').pop() ?? ''
            }));
            this.existingImages = [...this.existingImages, ...mapped];
          },
          error: (error: HttpErrorResponse) => {
            this.imageUploadError = error?.error?.message ?? 'Unable to upload images.';
          }
        });
    } else {
      this.selectedImages = [...this.selectedImages, ...validFiles];
      this.selectedImagePreviews = [...this.selectedImagePreviews, ...validFiles.map(file => URL.createObjectURL(file))];
    }

    input.value = '';
  }

  removeSelectedImage(index: number): void {
    const preview = this.selectedImagePreviews[index];
    if (preview) URL.revokeObjectURL(preview);
    this.selectedImagePreviews.splice(index, 1);
    this.selectedImages.splice(index, 1);
  }

  clearSelectedImages(): void {
    this.selectedImagePreviews.forEach(preview => URL.revokeObjectURL(preview));
    this.selectedImagePreviews = [];
    this.selectedImages = [];
    this.imageUploadError = '';
  }

  openImagePreview(url: string): void {
    this.selectedImagePreviewUrl = url;
  }

  closeImagePreview(): void {
    this.selectedImagePreviewUrl = null;
  }

  deleteExistingImage(img: { id: number }): void {
    if (!this.orderId) return;
    this.confirmDeleteTitle = 'Delete Image';
    this.confirmDeleteMessage = 'Are you sure you want to delete this image? This action cannot be undone.';
    this.confirmDeleteError = '';
    this.confirmDeleteAction = () => {
      this.confirmDeleteBusy = true;
      this.deletingImageId = img.id;
      this.ordersService.deleteImage(this.orderId!, img.id)
        .pipe(takeUntil(this.destroy$), finalize(() => {
          this.deletingImageId = null;
          this.confirmDeleteBusy = false;
        }))
        .subscribe({
          next: () => {
            this.existingImages = this.existingImages.filter(x => x.id !== img.id);
            this.showConfirmDeleteModal = false;
          },
          error: (error: HttpErrorResponse) => {
            this.confirmDeleteError = error?.error?.message ?? 'Unable to delete image. Please try again.';
          }
        });
    };
    this.showConfirmDeleteModal = true;
  }

  formatFileSize(size: number): string {
    if (size < 1024) return `${size} B`;
    if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`;
    return `${(size / (1024 * 1024)).toFixed(1)} MB`;
  }

  openBillModal(): void {
    this.billForm = { billNumber: null, billDetails: '' };
    this.billImageFile = null;
    this.billError = '';
    this.editingBillId = null;
    this.showBillModal = true;
  }

  closeBillModal(): void {
    if (this.savingBill) return;
    this.showBillModal = false;
    this.editingBillId = null;
  }

  editExistingBill(bill: { id: number; billNumber?: number | null; billDetails: string }): void {
    this.billForm = { billNumber: bill.billNumber ?? null, billDetails: bill.billDetails };
    this.billImageFile = null;
    this.billError = '';
    this.editingBillId = bill.id;
    this.showBillModal = true;
  }

  onBillImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const file = input.files[0];
    if (file.size > 10 * 1024 * 1024) {
      this.billError = 'File must be smaller than 10 MB.';
      input.value = ''; return;
    }
    this.billError = '';
    this.billImageFile = file;
  }

  removeBillImage(): void { this.billImageFile = null; }

  saveBill(): void {
    if (this.savingBill) return;
    if (!this.billForm.billDetails?.trim()) {
      this.billError = 'Bill details are required.';
      return;
    }

    if (this.isEditMode && this.orderId && this.editingBillId) {
      const billId = this.editingBillId;
      this.savingBill = true;
      this.ordersService.updateInventoryBill(this.orderId, billId, {
        billNumber: this.billForm.billNumber,
        billDetails: this.billForm.billDetails.trim(),
        billImageFile: this.billImageFile
      })
        .pipe(takeUntil(this.destroy$), finalize(() => (this.savingBill = false)))
        .subscribe({
          next: (updated: any) => {
            this.existingBills = this.existingBills.map(b =>
              b.id === billId
                ? { id: updated.id, billNumber: updated.billNumber, billDetails: updated.billDetails,
                    fileUrl: updated.billImage ? this.ordersService.getFileUrl(updated.billImage) : undefined }
                : b
            );
            this.showBillModal = false;
            this.editingBillId = null;
          },
          error: (error: HttpErrorResponse) => {
            this.billError = error?.error?.message ?? 'Unable to update bill.';
          }
        });
      return;
    }

    if (this.isEditMode && this.orderId) {
      this.savingBill = true;
      this.ordersService.addInventoryBill(this.orderId, {
        billNumber: this.billForm.billNumber,
        billDetails: this.billForm.billDetails.trim(),
        billImageFile: this.billImageFile
      })
        .pipe(takeUntil(this.destroy$), finalize(() => (this.savingBill = false)))
        .subscribe({
          next: (created: any) => {
            this.existingBills = [...this.existingBills, {
              id: created.id, billNumber: created.billNumber, billDetails: created.billDetails,
              fileUrl: created.billImage ? this.ordersService.getFileUrl(created.billImage) : undefined
            }];
            this.showBillModal = false;
          },
          error: (error: HttpErrorResponse) => {
            this.billError = error?.error?.message ?? 'Unable to add bill.';
          }
        });
      return;
    }

    this.pendingBills.push({
      billNumber: this.billForm.billNumber,
      billDetails: this.billForm.billDetails.trim(),
      billImageFile: this.billImageFile
    });
    this.showBillModal = false;
  }

  removePendingBill(index: number): void { this.pendingBills.splice(index, 1); }

  deleteExistingBill(bill: { id: number }): void {
    if (!this.orderId) return;
    this.confirmDeleteTitle = 'Delete Inventory Bill';
    this.confirmDeleteMessage = 'Are you sure you want to delete this bill? This action cannot be undone.';
    this.confirmDeleteError = '';
    this.confirmDeleteAction = () => {
      this.confirmDeleteBusy = true;
      this.ordersService.deleteInventoryBill(this.orderId!, bill.id)
        .pipe(takeUntil(this.destroy$), finalize(() => (this.confirmDeleteBusy = false)))
        .subscribe({
          next: () => {
            this.existingBills = this.existingBills.filter(x => x.id !== bill.id);
            this.showConfirmDeleteModal = false;
          },
          error: (error: HttpErrorResponse) => {
            this.confirmDeleteError = error?.error?.message ?? 'Unable to delete bill. Please try again.';
          }
        });
    };
    this.showConfirmDeleteModal = true;
  }

  confirmDelete(): void {
    if (this.confirmDeleteBusy || !this.confirmDeleteAction) return;
    this.confirmDeleteAction();
  }

  closeConfirmDeleteModal(): void {
    if (this.confirmDeleteBusy) return;
    this.showConfirmDeleteModal = false;
    this.confirmDeleteAction = null;
    this.confirmDeleteError = '';
  }

  getImageUrl(imageUrl: string | null | undefined): string {
    return this.ordersService.getImageUrl(imageUrl);
  }

  getFileUrl(fileUrl: string | null | undefined): string {
    return this.ordersService.getFileUrl(fileUrl);
  }

  trackById(index: number, item: { id: number }): number { return item.id; }

  formatDate(date: string | null | undefined): string {
    if (!date) return '—';
    const parsedDate = new Date(date);
    if (Number.isNaN(parsedDate.getTime())) return '—';
    return parsedDate.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }
}