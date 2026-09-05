import {
  Component,
  HostListener,
  OnDestroy,
  OnInit,
  inject
} from '@angular/core';

import { CommonModule } from '@angular/common';

import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';

import {
  ActivatedRoute,
  Router
} from '@angular/router';

import {
  Subject,
  forkJoin,
  of
} from 'rxjs';

import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  finalize,
  switchMap,
  takeUntil
} from 'rxjs/operators';

import { FooterComponent } from '../../footer/footer.component';

import {
  LookupService,
  LOOKUP_TYPE
} from '../lookup.service';

import { OrdersService } from '../orders.service';

import {
  CustomerOption,
  LookupItem,
  OrderDetails,
  OrderFormValue
} from '../order.models';

import {
  priorityBadgeClass,
  statusBadgeClass
} from '../order-badge.util';

@Component({
  selector: 'app-order-form',

  standalone: true,

  imports: [
    CommonModule,
    ReactiveFormsModule,
    FooterComponent
  ],

  templateUrl: './order-form.component.html'
})
export class OrderFormComponent
  implements OnInit, OnDestroy {

  private readonly fb = inject(FormBuilder);

  form = this.fb.group({

    customerProductTitle: [
      '',
      [
        Validators.required,
        Validators.maxLength(200)
      ]
    ],

    manufacturerProductTitle: [
      '',
      [
        Validators.maxLength(200)
      ]
    ],

    customerOrderNumber: [
      '',
      [
        Validators.maxLength(100)
      ]
    ],

    customerId: [
      null as number | null,
      Validators.required
    ],

    amount: [
      null as number | null,
      [
        Validators.min(0)
      ]
    ],

    genderId: [
      null as number | null,
      Validators.required
    ],

    customerMaterialId: [
      null as number | null,
      Validators.required
    ],

    manufacturerMaterialId: [
      null as number | null,
      Validators.required
    ],

    isCustomSize: [
      false
    ],

    sizeId: [
      null as number | null
    ],

    sizeChartId: [
      null as number | null
    ],

    sizeDetails: [
      '',
      [
        Validators.maxLength(2000)
      ]
    ],

    daysForMaking: [
      null as number | null,
      [
        Validators.required,
        Validators.min(0),
        Validators.max(3650)
      ]
    ],

    priorityId: [
      null as number | null
    ],

    consigneeName: [
      '',
      [
        Validators.required,
        Validators.maxLength(200)
      ]
    ],

    consigneeAddress: [
      '',
      [
        Validators.required,
        Validators.maxLength(1000)
      ]
    ],

    trackingNumber: [
      '',
      [
        Validators.maxLength(200)
      ]
    ],

    notesByCustomer: [
      '',
      [
        Validators.maxLength(3000)
      ]
    ],

    notesByManufacturer: [
      '',
      [
        Validators.maxLength(3000)
      ]
    ]

  });

  isEditMode = false;

  orderId: number | null = null;

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

  customerOptions: CustomerOption[] = [];

  customerSearchTerm = '';

  selectedCustomer:
    CustomerOption | null = null;

  showCustomerDropdown = false;

  customerLoading = false;

  private customerSearch$ =
    new Subject<string>();

  private destroy$ =
    new Subject<void>();

  statusBadgeClass =
    statusBadgeClass;

  priorityBadgeClass =
    priorityBadgeClass;

  constructor(
    private route: ActivatedRoute,

    private router: Router,

    private ordersService: OrdersService,

    private lookupService: LookupService
  ) {}

  ngOnInit(): void {

    this.setupSizeValidation();

    this.setupCustomerSearch();

    const idParam =
      this.route.snapshot.paramMap.get(
        'id'
      );

    if (idParam) {

      const id =
        Number(idParam);

      if (
        Number.isNaN(id) ||
        id <= 0
      ) {

        this.errorMsg =
          'Invalid order ID.';

        this.loading = false;

        return;
      }

      this.isEditMode = true;

      this.orderId = id;

    } else {

      this.isEditMode = false;

      this.orderId = null;
    }

    this.loadLookups();
  }

  ngOnDestroy(): void {

    this.destroy$.next();

    this.destroy$.complete();
  }

  private setupSizeValidation(): void {

    this.updateSizeValidators();

    this.form.controls.isCustomSize
      .valueChanges
      .pipe(
        takeUntil(this.destroy$)
      )
      .subscribe(() => {

        this.updateSizeValidators();
      });
  }

  private updateSizeValidators(): void {

    const isCustom =
      this.form.controls
        .isCustomSize
        .value;

    const sizeControl =
      this.form.controls.sizeId;

    const chartControl =
      this.form.controls.sizeChartId;

    const detailsControl =
      this.form.controls.sizeDetails;

    if (isCustom) {

      sizeControl.clearValidators();

      chartControl.clearValidators();

      detailsControl.setValidators([
        Validators.required,
        Validators.maxLength(2000)
      ]);

    } else {

      sizeControl.setValidators([
        Validators.required
      ]);

      chartControl.setValidators([
        Validators.required
      ]);

      detailsControl.setValidators([
        Validators.maxLength(2000)
      ]);
    }

    sizeControl.updateValueAndValidity({
      emitEvent: false
    });

    chartControl.updateValueAndValidity({
      emitEvent: false
    });

    detailsControl.updateValueAndValidity({
      emitEvent: false
    });
  }

  private setupCustomerSearch(): void {

    this.customerSearch$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),

        switchMap(search => {

          this.customerLoading = true;

          return this.lookupService
            .getCustomers(search)
            .pipe(
              catchError(() =>
                of([])
              ),
              finalize(() => {
                this.customerLoading =
                  false;
              })
            );
        }),

        takeUntil(this.destroy$)
      )
      .subscribe(customers => {

        this.customerOptions =
          customers;
      });
  }

  private loadLookups(): void {

    this.loading = true;

    forkJoin({

      statuses:
        this.lookupService.getByType(
          LOOKUP_TYPE.OrderStatus
        ),

      priorities:
        this.lookupService.getByType(
          LOOKUP_TYPE.Priority
        ),

      genders:
        this.lookupService.getByType(
          LOOKUP_TYPE.Gender
        ),

      materials:
        this.lookupService.getByType(
          LOOKUP_TYPE.Material
        ),

      sizes:
        this.lookupService.getByType(
          LOOKUP_TYPE.Size
        ),

      sizeCharts:
        this.lookupService.getByType(
          LOOKUP_TYPE.SizeChart
        )

    })
    .pipe(
      takeUntil(this.destroy$)
    )
    .subscribe({

      next: result => {

        this.statuses =
          result.statuses;

        this.priorities =
          result.priorities;

        this.genders =
          result.genders;

        this.materials =
          result.materials;

        this.sizes =
          result.sizes;

        this.sizeCharts =
          result.sizeCharts;

        if (
          this.isEditMode &&
          this.orderId
        ) {

          this.loadOrder(
            this.orderId
          );

        } else {

          this.loading = false;

          this.customerSearch$.next('');
        }
      },

      error: () => {

        this.errorMsg =
          'Unable to load order form data. Please refresh and try again.';

        this.loading = false;
      }

    });
  }

  private loadOrder(
    id: number
  ): void {

    this.ordersService
      .getOrder(id)
      .pipe(
        takeUntil(this.destroy$)
      )
      .subscribe({

        next: order => {

          this.patchOrder(order);

          this.loading = false;
        },

        error: error => {

          this.errorMsg =
            error?.error?.message ??
            'Unable to load order. Please try again.';

          this.loading = false;
        }

      });
  }

  private patchOrder(
    order: OrderDetails
  ): void {

    this.orderNumber =
      order.manufacturerOrderNumber;

    this.currentStatus =
      order.status;

    this.form.patchValue({

      customerProductTitle:
        order.customerProductTitle,

      manufacturerProductTitle:
        order.manufacturerProductTitle,

      customerOrderNumber:
        order.customerOrderNumber,

      customerId:
        order.customerId,

      amount:
        order.amount,

      genderId:
        order.genderId,

      customerMaterialId:
        order.customerMaterialId,

      manufacturerMaterialId:
        order.manufacturerMaterialId,

      isCustomSize:
        order.isCustomSize,

      sizeId:
        order.sizeId,

      sizeChartId:
        order.sizeChartId,

      sizeDetails:
        order.sizeDetails,

      daysForMaking:
        order.daysForMaking,

      priorityId:
        order.priorityId,

      consigneeName:
        order.consigneeName,

      consigneeAddress:
        order.consigneeAddress,

      trackingNumber:
        order.trackingNumber,

      notesByCustomer:
        order.notesByCustomer,

      notesByManufacturer:
        order.notesByManufacturer

    });

    this.selectedCustomer = {

      id: order.customerId,

      name: order.customerName,

      email: ''

    };

    this.customerSearchTerm =
      order.customerName;

    this.updateSizeValidators();
  }

  onCustomerFocus(): void {

    this.showCustomerDropdown = true;

    if (
      this.customerOptions.length === 0
    ) {

      this.customerSearch$.next(
        this.customerSearchTerm.trim()
      );
    }
  }

  onCustomerInput(
    value: string
  ): void {

    this.customerSearchTerm =
      value;

    this.showCustomerDropdown = true;

    const normalizedValue =
      value.trim().toLowerCase();

    if (
      this.selectedCustomer &&
      normalizedValue !==
        this.selectedCustomer.name
          .trim()
          .toLowerCase()
    ) {

      this.selectedCustomer = null;

      this.form.controls.customerId
        .setValue(null);

      this.form.controls.customerId
        .markAsTouched();
    }

    this.customerSearch$.next(
      value.trim()
    );
  }

  selectCustomer(
    customer: CustomerOption
  ): void {

    this.selectedCustomer =
      customer;

    this.customerSearchTerm =
      customer.name;

    this.form.controls.customerId
      .setValue(customer.id);

    this.form.controls.customerId
      .markAsTouched();

    this.form.controls.customerId
      .markAsDirty();

    this.showCustomerDropdown =
      false;
  }

  clearCustomer(): void {

    this.selectedCustomer = null;

    this.customerSearchTerm = '';

    this.form.controls.customerId
      .setValue(null);

    this.form.controls.customerId
      .markAsTouched();

    this.showCustomerDropdown = true;

    this.customerSearch$.next('');
  }

  save(): void {

    if (this.saving) {
      return;
    }

    if (this.form.invalid) {

      this.form.markAllAsTouched();

      this.errorMsg =
        'Please complete all required fields before saving.';

      return;
    }

    const raw =
      this.form.getRawValue();

    if (!raw.customerId) {

      this.errorMsg =
        'Please select a customer.';

      this.form.controls.customerId
        .markAsTouched();

      return;
    }

    const payload:
      OrderFormValue = {

      customerProductTitle:
        raw.customerProductTitle?.trim() ?? '',

      manufacturerProductTitle:
        this.nullIfBlank(
          raw.manufacturerProductTitle
        ),

      customerOrderNumber:
        this.nullIfBlank(
          raw.customerOrderNumber
        ),

      customerId:
        raw.customerId,

      amount:
        raw.amount === null
          ? null
          : Number(raw.amount),

      genderId:
        raw.genderId,

      customerMaterialId:
        raw.customerMaterialId,

      manufacturerMaterialId:
        raw.manufacturerMaterialId,

      isCustomSize:
        raw.isCustomSize ?? false,

      sizeId:
        raw.isCustomSize
          ? null
          : raw.sizeId,

      sizeChartId:
        raw.isCustomSize
          ? null
          : raw.sizeChartId,

      sizeDetails:
        raw.isCustomSize
          ? this.nullIfBlank(
              raw.sizeDetails
            )
          : null,

      daysForMaking:
        raw.daysForMaking === null
          ? null
          : Number(raw.daysForMaking),

      priorityId:
        raw.priorityId,

      consigneeName:
        raw.consigneeName?.trim() ?? '',

      consigneeAddress:
        raw.consigneeAddress?.trim() ?? '',

      trackingNumber:
        this.nullIfBlank(
          raw.trackingNumber
        ),

      notesByCustomer:
        this.nullIfBlank(
          raw.notesByCustomer
        ),

      notesByManufacturer:
        this.nullIfBlank(
          raw.notesByManufacturer
        )
    };

    this.saving = true;

    this.errorMsg = '';

    const request$ =
      this.isEditMode &&
      this.orderId

        ? this.ordersService.updateOrder(
            this.orderId,
            payload
          )

        : this.ordersService.createOrder(
            payload
          );

    request$
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => {
          this.saving = false;
        })
      )
      .subscribe({

        next: () => {

          this.router.navigate([
            '/dashboard/orders'
          ]);
        },

        error: error => {

          this.errorMsg =
            error?.error?.message ??
            'Unable to save order. Please try again.';
        }

      });
  }

  cancel(): void {

    this.router.navigate([
      '/dashboard/orders'
    ]);
  }

  isInvalid(
    controlName: string
  ): boolean {

    const control =
      this.form.get(controlName);

    return !!control &&
      control.invalid &&
      (
        control.touched ||
        control.dirty
      );
  }

  getError(
    controlName: string
  ): string {

    const control =
      this.form.get(controlName);

    if (!control?.errors) {
      return '';
    }

    if (
      control.errors['required']
    ) {

      return 'This field is required.';
    }

    if (
      control.errors['maxlength']
    ) {

      return `Maximum ${control.errors['maxlength'].requiredLength} characters allowed.`;
    }

    if (
      control.errors['min']
    ) {

      return `Value must be at least ${control.errors['min'].min}.`;
    }

    if (
      control.errors['max']
    ) {

      return `Value cannot be greater than ${control.errors['max'].max}.`;
    }

    return 'Invalid value.';
  }

  private nullIfBlank(
    value: string | null
  ): string | null {

    if (
      value === null ||
      value === undefined
    ) {

      return null;
    }

    const trimmed =
      value.trim();

    return trimmed.length > 0
      ? trimmed
      : null;
  }

  get pageTitle(): string {

    return this.isEditMode
      ? 'Edit Order'
      : 'Add Order';
  }

  get pageSubtitle(): string {

    return this.isEditMode
      ? 'Update the order information below.'
      : 'Create a new customer order.';
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

    if (
      !target.closest(
        '[data-customer-select]'
      )
    ) {

      this.showCustomerDropdown =
        false;
    }
  }
}