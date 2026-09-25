import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { EMPTY, of, Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { PermissionService } from '../auth/permission.service';
import { PollingService } from '../core/polling/polling.service';
import { ChatSignalrService } from '../core/signalr/chat-signalr.service';
import { LookupService } from './lookup.service';
import { OrdersService } from './orders.service';
import { OrderFormComponent } from './order-form/order-form.component';
import { ManageOrdersComponent } from './manage-orders/manage-orders.component';
import { OrderDetails, OrderListItem } from './order.models';

describe('Order field permissions and shared bulk selection', () => {
  let role: string;
  let orders: jasmine.SpyObj<OrdersService>;
  const requiredShipping = { customerOrderNumber: 'REF-42', sizeId: 5, sizeChartId: 6,
    consigneeName: 'Recipient', consigneeAddress: 'Delivery address',
    shippingEmail: 'recipient@example.test', shippingContact: '+92 300 1234567' };

  beforeEach(async () => {
    role = 'Customer';
    orders = jasmine.createSpyObj('OrdersService', ['assignOrders', 'deleteOrder', 'getOrder', 'getOrders', 'updateOrder', 'createOrder', 'getImageUrl']);
    await TestBed.configureTestingModule({
      imports: [OrderFormComponent, ManageOrdersComponent],
      providers: [
        { provide: AuthService, useValue: {
          currentRole: () => role, isCustomer: () => role === 'Customer', getToken: () => null,
          getProfile: () => of({ id: 4, firstName: 'Test', lastName: 'Customer', email: 'test@example.test' })
        } },
        { provide: OrdersService, useValue: orders },
        { provide: LookupService, useValue: { getByType: () => of([{ id: 10, name: 'new' }, { id: 11, name: 'assign' }]), getCustomers: () => of([]) } },
        { provide: PollingService, useValue: { poll: () => EMPTY } },
        { provide: ChatSignalrService, useValue: { onMessage: () => () => {} } },
        { provide: PermissionService, useValue: { canView: () => true, canAdd: () => true, canEdit: () => false, canDelete: () => false } },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } }
      ]
    }).compileComponents();
  });

  for (const [name, tracking, amount] of [
    ['Customer', false, false], ['Admin', true, true], ['Super Admin', true, true],
    ['Staff', true, false], ['Finance', false, true], ['Sales', false, false]
  ] as const) {
    it(`shows tracking and amount with the correct edit permissions for ${name}`, () => {
      role = name;
      const fixture = TestBed.createComponent(OrderFormComponent);
      fixture.detectChanges();
      for (const [field, enabled] of [['trackingNumber', tracking], ['amount', amount]] as const) {
        const input = fixture.nativeElement.querySelector(`[formControlName="${field}"]`) as HTMLInputElement;
        expect(input).withContext(field + ' remains visible').not.toBeNull();
        expect(input.disabled).withContext(field + ' permission').toBe(!enabled);
      }
    });
  }

  it('shows customer restricted fields disabled while preserving tracking and amount on save', () => {
    const fixture = TestBed.createComponent(OrderFormComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.isEditMode = true;
    component.orderId = 42;
    component.order = { amount: 250, trackingNumber: 'TRACK-42', courier: 'DHL' } as OrderDetails;
    component.form.patchValue({ ...requiredShipping, customerProductTitle: 'Shirt', customerMaterialId: 1, genderId: 1,
      isCustomSize: true, sizeDetails: 'Custom measurements', trackingNumber: 'TRACK-42', amount: 250 });
    fixture.detectChanges();
    for (const field of ['notesByManufacturer', 'courier']) {
      expect(fixture.nativeElement.querySelector(`[formControlName="${field}"]`).disabled).toBeTrue();
    }
    for (const field of ['manufacturerMaterialId']) {
      expect(fixture.nativeElement.querySelector(`[data-dd="${field}"]`)).toBeNull();
    }
    expect(fixture.nativeElement.querySelector('[data-status-dd] button').disabled).toBeTrue();
    expect(fixture.nativeElement.querySelector('[formControlName="trackingNumber"]').value).toBe('TRACK-42');
    orders.updateOrder.and.returnValue(EMPTY);
    component.save();
    expect(orders.updateOrder).toHaveBeenCalledWith(42, jasmine.objectContaining({ amount: 250, trackingNumber: 'TRACK-42', courier: 'DHL', sizeId: 5, sizeChartId: 6 }));
  });

  it('hides priority from customer creation and does not submit a customer-selected priority', () => {
    const fixture = TestBed.createComponent(OrderFormComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    expect(fixture.nativeElement.querySelector('[data-customer-select] > div.relative button')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-dd="priorityId"]')).toBeNull();
    expect(component.form.controls.priorityId.disabled).toBeTrue();
    component.form.controls.priorityId.setValue(12);
    component.form.patchValue({ ...requiredShipping, customerProductTitle: 'Shirt', genderId: 1, customerMaterialId: 1,
      isCustomSize: true, sizeDetails: 'Custom measurements' });
    orders.createOrder.and.returnValue(EMPTY);
    component.save();
    expect(orders.createOrder).toHaveBeenCalledWith(jasmine.objectContaining({ priorityId: null }));
  });

  it('lets Super Admin leave customer title and material blank while supplying manufacturer requirements', () => {
    role = 'Super Admin';
    const fixture = TestBed.createComponent(OrderFormComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    expect(fixture.nativeElement.querySelector('[formControlName="customerProductTitle"]').disabled).toBeFalse();
    component.form.patchValue({ ...requiredShipping, manufacturerProductTitle: 'Production shirt', customerId: 4, genderId: 1,
      manufacturerMaterialId: 1, priorityId: 2, courier: 'DHL', trackingNumber: 'TRACK-42', amount: 150, isCustomSize: true,
      sizeDetails: 'Custom measurements', consigneeName: 'Recipient', consigneeAddress: 'Delivery address' });
    orders.createOrder.and.returnValue(EMPTY);
    component.save();
    expect(orders.createOrder).toHaveBeenCalledWith(jasmine.objectContaining({
      customerProductTitle: '', customerMaterialId: null, manufacturerProductTitle: 'Production shirt', amount: 150
    }));
  });

  it('renders selected image thumbnails and releases their previews on removal and close', async () => {
    const fixture = TestBed.createComponent(OrderFormComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const revoke = spyOn(URL, 'revokeObjectURL').and.callThrough();
    const bytes = Uint8Array.from(atob('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII='), char => char.charCodeAt(0));
    const files = [new File([bytes], 'front.png', { type: 'image/png' }), new File([bytes], 'back.png', { type: 'image/png' })];
    component.onImagesSelected({ target: { files, value: '' } } as unknown as Event);
    fixture.detectChanges();
    const previews = [...component.selectedImagePreviews];
    const thumbnails = fixture.nativeElement.querySelectorAll('img[src^="blob:"]') as NodeListOf<HTMLImageElement>;
    expect(thumbnails.length).toBe(2);
    await Promise.all(Array.from(thumbnails).map(img => img.decode()));
    component.removeSelectedImage(0);
    fixture.detectChanges();
    expect(revoke).toHaveBeenCalledWith(previews[0]);
    expect(component.selectedImages[0].name).toBe('back.png');
    expect(fixture.nativeElement.querySelectorAll('img[src^="blob:"]').length).toBe(1);
    fixture.destroy();
    expect(revoke).toHaveBeenCalledWith(previews[1]);
  });

  for (const account of ['Customer', 'Admin', 'Super Admin']) {
    it(`enforces sheet requirements and disabled defaults for ${account}`, () => {
      role = account;
      const fixture = TestBed.createComponent(OrderFormComponent);
      fixture.detectChanges();
      const component = fixture.componentInstance;
      component.form.patchValue({ ...requiredShipping, customerId: 4, customerProductTitle: 'Shirt', genderId: 3,
        manufacturerProductTitle: 'Production shirt', manufacturerMaterialId: 4, priorityId: 2,
        courier: 'UPS', trackingNumber: 'TRACK-42' });
      expect(component.form.valid).toBeTrue();
      expect(component.form.controls.statusId.disabled).toBeTrue();
      expect(component.form.controls.statusId.value).toBe(account === 'Customer' ? 10 : 11);
      const fields = ['customerOrderNumber', 'genderId', 'sizeId', 'sizeChartId', 'consigneeName', 'shippingEmail', 'shippingContact', 'consigneeAddress',
        ...(account === 'Customer' ? ['customerProductTitle'] : ['customerId', 'manufacturerProductTitle', 'manufacturerMaterialId', 'priorityId', 'courier', 'trackingNumber'])];
      for (const field of fields) {
        const control = component.form.get(field)!;
        const previous = control.value;
        control.setValue(null);
        expect(component.form.invalid).withContext(field + ' must be required').toBeTrue();
        control.setValue(previous);
      }
      component.form.controls.shippingContact.setValue('   ');
      expect(component.form.invalid).toBeTrue();
      component.form.controls.shippingContact.setValue(requiredShipping.shippingContact);
      component.form.controls.shippingEmail.setValue('bad-email');
      expect(component.form.invalid).toBeTrue();
      expect(component.form.controls.notesByCustomer.disabled).toBe(account !== 'Customer');
      expect(component.form.controls.notesByManufacturer.disabled).toBe(account === 'Customer');
    });
  }

  it('accepts all inventory document types while keeping the size limit', () => {
    role = 'Admin';
    const component = TestBed.createComponent(OrderFormComponent).componentInstance;
    for (const name of ['bill.xlsx', 'bill.docx', 'bill.zip', 'bill.html', 'bill']) {
      const file = new File(['bill'], name);
      component.onBillImageSelected({ target: { files: [file], value: '' } } as unknown as Event);
      expect(component.billImageFile).toBe(file);
      expect(component.billError).toBe('');
    }
    const large = new File([new Uint8Array(10 * 1024 * 1024 + 1)], 'large.zip');
    component.onBillImageSelected({ target: { files: [large], value: '' } } as unknown as Event);
    expect(component.billError).toContain('10 MB');
  });

  function listFixture() {
    const fixture = TestBed.createComponent(ManageOrdersComponent);
    const component = fixture.componentInstance;
    const items = [
      { id: 1, manufacturerOrderNumber: 'AD1', isAssigned: false, images: [] },
      { id: 2, manufacturerOrderNumber: 'AD2', isAssigned: true, images: [] }
    ] as unknown as OrderListItem[];
    orders.getOrders.and.returnValue(of({ items, totalCount: 2 } as any));
    fixture.detectChanges();
    return fixture;
  }

  it('keeps priority hidden from the customer table after resetting columns', () => {
    const fixture = listFixture();
    const component = fixture.componentInstance;
    component.orders[0].priority = 'Most Urgent';
    component.resetColumns();
    fixture.detectChanges();
    expect(component.isColumnVisible('priority')).toBeFalse();
    expect(component.columnOptions.some(column => column.key === 'priority')).toBeFalse();
    expect(fixture.nativeElement.textContent).not.toContain('Most Urgent');
  });

  it('shows assignment dates and Days Passed instead of order audit dates', () => {
    const fixture = listFixture();
    fixture.componentInstance.orders[1].assignedDate = '2026-09-20T12:00:00Z';
    fixture.detectChanges();
    const content = fixture.nativeElement.textContent;
    expect(content).toContain('Assign Date');
    expect(content).toContain('Days Passed');
    expect(content).toContain('Sep 20, 2026');
    expect(content).not.toContain('Created Date');
    expect(content).not.toContain('Updated Date');
  });

  it('renders one checkbox per order and uses that selection for customer bulk delete', () => {
    const fixture = listFixture();
    const component = fixture.componentInstance;
    expect(fixture.nativeElement.querySelectorAll('thead input[type="checkbox"]').length).toBe(1);
    expect(fixture.nativeElement.querySelectorAll('tbody input[type="checkbox"]').length).toBe(2);
    component.togglePageSelection();
    fixture.detectChanges();
    const deleteButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.trim() === 'Delete');
    expect(deleteButton).toBeDefined();
    expect(deleteButton!.disabled).toBeFalse();
    deleteButton!.click();
    expect(component.bulkDeleteOrderIds).toEqual([1, 2]);
  });

  it('assigns only unassigned orders from the same selection and blocks overlapping actions', () => {
    const fixture = listFixture();
    const component = fixture.componentInstance;
    const result = new Subject<any>();
    orders.assignOrders.and.returnValue(result);
    spyOn(component, 'fetchOrders');
    component.togglePageSelection();
    component.openAssignModal();
    component.confirmAssign();
    expect(orders.assignOrders).toHaveBeenCalledWith([1]);
    component.openBulkDeleteModal();
    expect(component.showDeleteModal).toBeFalse();
    component.clearOrderSelection();
    expect(component.selectedOrderIds.size).toBe(2);
    result.next({});
    result.complete();
    expect([...component.selectedOrderIds]).toEqual([2]);
    expect(component.fetchOrders).toHaveBeenCalled();
  });

  it('assigns the clicked order without assigning other selected rows', () => {
    const fixture = listFixture();
    const component = fixture.componentInstance;
    component.selectedOrderIds = new Set([2]);
    orders.assignOrders.and.returnValue(of({} as any));
    spyOn(component, 'fetchOrders').and.callThrough();
    const button = Array.from(fixture.nativeElement.querySelectorAll('tbody button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.trim() === 'Assign Order')!;
    button.click();
    expect(component.detailsOrderId).toBeNull();
    expect(component.pendingAssignIds).toEqual([1]);
    component.confirmAssign();
    expect(orders.assignOrders).toHaveBeenCalledWith([1]);
    expect([...component.selectedOrderIds]).toEqual([2]);
    component.openAssignModal(component.orders[1]);
    expect(component.showAssignModal).toBeFalse();
  });

  it('opens order details in a modal and closes without navigating', () => {
    const fixture = listFixture();
    orders.getOrder.and.returnValue(EMPTY);
    fixture.componentInstance.viewOrder(fixture.componentInstance.orders[0]);
    fixture.detectChanges();
    const dialog = fixture.nativeElement.querySelector('app-order-details > div') as HTMLElement;
    expect(dialog.classList.contains('fixed')).toBeTrue();
    expect(orders.getOrder).toHaveBeenCalledWith(1);
    expect(TestBed.inject(Router).navigate).not.toHaveBeenCalled();
    dialog.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-order-details')).toBeNull();
  });

  it('prevents a customer inline tracking edit from sending any request', () => {
    const fixture = listFixture();
    expect(fixture.nativeElement.querySelector('input[placeholder="Add tracking"]').disabled).toBeTrue();
    fixture.componentInstance.onTrackingChange(fixture.componentInstance.orders[0], 'unauthorized');
    expect(orders.getOrder).not.toHaveBeenCalled();
    expect(orders.updateOrder).not.toHaveBeenCalled();
  });
});
