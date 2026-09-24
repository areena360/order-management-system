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
        { provide: LookupService, useValue: { getByType: () => of([]), getCustomers: () => of([]) } },
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
    component.order = { amount: 250, trackingNumber: 'TRACK-42' } as OrderDetails;
    component.form.patchValue({ customerProductTitle: 'Shirt', customerMaterialId: 1, genderId: 1,
      isCustomSize: true, sizeDetails: 'Custom measurements', trackingNumber: 'TRACK-42', amount: 250 });
    fixture.detectChanges();
    for (const field of ['manufacturerProductTitle', 'notesByManufacturer']) {
      expect(fixture.nativeElement.querySelector(`[formControlName="${field}"]`).disabled).toBeTrue();
    }
    for (const field of ['manufacturerMaterialId']) {
      expect(fixture.nativeElement.querySelector(`[data-dd="${field}"] button`).disabled).toBeTrue();
    }
    expect(fixture.nativeElement.querySelector('[data-status-dd] button').disabled).toBeTrue();
    expect(fixture.nativeElement.querySelector('[formControlName="trackingNumber"]').value).toBe('TRACK-42');
    orders.updateOrder.and.returnValue(EMPTY);
    component.save();
    expect(orders.updateOrder).toHaveBeenCalledWith(42, jasmine.objectContaining({ amount: 250, trackingNumber: 'TRACK-42' }));
  });

  it('lets a customer select priority when creating an order and submits it', () => {
    const fixture = TestBed.createComponent(OrderFormComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    expect(fixture.nativeElement.querySelector('[data-customer-select] > div.relative button')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-dd="priorityId"] button').disabled).toBeFalse();
    component.selectDropdown('priorityId', 12);
    component.form.patchValue({ customerProductTitle: 'Shirt', genderId: 1, customerMaterialId: 1,
      isCustomSize: true, sizeDetails: 'Custom measurements' });
    orders.createOrder.and.returnValue(EMPTY);
    component.save();
    expect(orders.createOrder).toHaveBeenCalledWith(jasmine.objectContaining({ priorityId: 12 }));
  });

  it('lets Super Admin create with an amount and an automatically filled disabled customer product name', () => {
    role = 'Super Admin';
    const fixture = TestBed.createComponent(OrderFormComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    expect(fixture.nativeElement.querySelector('[formControlName="customerProductTitle"]').disabled).toBeTrue();
    component.form.patchValue({ manufacturerProductTitle: 'Production shirt', customerId: 4, genderId: 1,
      customerMaterialId: 1, manufacturerMaterialId: 1, amount: 150, isCustomSize: true,
      sizeDetails: 'Custom measurements', consigneeName: 'Recipient', consigneeAddress: 'Delivery address' });
    orders.createOrder.and.returnValue(EMPTY);
    component.save();
    expect(orders.createOrder).toHaveBeenCalledWith(jasmine.objectContaining({
      customerProductTitle: 'Production shirt', manufacturerProductTitle: 'Production shirt', amount: 150
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
    component.confirmAssign();
    expect(orders.assignOrders).toHaveBeenCalledWith([1]);
    component.openBulkDeleteModal();
    expect(component.showDeleteModal).toBeFalse();
    component.clearOrderSelection();
    expect(component.selectedOrderIds.size).toBe(2);
    result.next({});
    result.complete();
    expect(component.selectedOrderIds.size).toBe(0);
    expect(component.fetchOrders).toHaveBeenCalled();
  });

  it('prevents a customer inline tracking edit from sending any request', () => {
    const fixture = listFixture();
    expect(fixture.nativeElement.querySelector('input[placeholder="Add tracking"]').disabled).toBeTrue();
    fixture.componentInstance.onTrackingChange(fixture.componentInstance.orders[0], 'unauthorized');
    expect(orders.getOrder).not.toHaveBeenCalled();
    expect(orders.updateOrder).not.toHaveBeenCalled();
  });
});
