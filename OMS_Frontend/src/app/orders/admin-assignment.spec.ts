import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { EMPTY, of, Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { PermissionService } from '../auth/permission.service';
import { PollingService } from '../core/polling/polling.service';
import { ChatSignalrService } from '../core/signalr/chat-signalr.service';
import { ManageOrdersComponent } from './manage-orders/manage-orders.component';
import { RolesAndPermissionsComponent } from '../roles-and-permissions/roles-and-permissions.component';
import { OrdersService } from './orders.service';
import { OrderDetailsComponent } from './order-details/order-details.component';
import { OrderDetails, OrderListItem } from './order.models';

describe('Admin assigned orders', () => {
  let role: string;
  let orders: jasmine.SpyObj<OrdersService>;
  let permissions: PermissionService;

  beforeEach(() => {
    role = 'Staff';
    orders = jasmine.createSpyObj('OrdersService', ['getImageUrl', 'getOrders', 'saveAdminAssignments', 'updateAssignmentStatus', 'getAssignmentOptions', 'getAssignmentMessages', 'readAssignmentMessage']);
    orders.getAssignmentMessages.and.returnValue(of({}));
    orders.getImageUrl.and.returnValue('');
    orders.getOrders.and.returnValue(of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 }));
    TestBed.configureTestingModule({
      imports: [ManageOrdersComponent, RolesAndPermissionsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
        { provide: AuthService, useValue: { currentRole: () => role, isCustomer: () => false, sessionChanged$: EMPTY,
          getToken: () => null, getProfile: () => of({ id: 7 }) } },
        { provide: OrdersService, useValue: orders },
        { provide: PollingService, useValue: { poll: () => EMPTY } },
        { provide: ChatSignalrService, useValue: { onMessage: () => () => {}, onGroupMessage: () => () => {} } }
      ]
    });
    permissions = TestBed.inject(PermissionService);
  });

  function loadRestricted(restricted = true): void {
    permissions.load(true).subscribe();
    TestBed.inject(HttpTestingController).expectOne(req => req.url.endsWith('/profile/permissions')).flush([
      { screenKey: 'Orders', adminAssignedOnly: restricted, canView: false, canAdd: true, canEdit: true, canDelete: true },
      { screenKey: 'Order Amount', canView: true, canEdit: true }
    ]);
  }

  it('grants assigned-order viewing while denying all order mutations, even with stale action flags', () => {
    loadRestricted();
    expect(permissions.canView('Orders')).toBeTrue();
    expect(permissions.canAdd('Orders')).toBeFalse();
    expect(permissions.canEdit('Orders')).toBeFalse();
    expect(permissions.canDelete('Orders')).toBeFalse();
    expect(permissions.canEdit('Order Amount')).toBeFalse();
  });

  it('hides sensitive columns even after resetting column preferences', () => {
    loadRestricted();
    const fixture = TestBed.createComponent(ManageOrdersComponent);
    spyOn(fixture.componentInstance, 'ngOnInit');
    fixture.componentInstance.resetColumns();
    for (const key of ['amount', 'customerOrderNumber', 'trackingNumber']) {
      expect(fixture.componentInstance.isColumnVisible(key)).toBeFalse();
      expect(fixture.componentInstance.columnOptions.some(c => c.key === key)).toBeFalse();
    }
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-dd="customerFilter"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-dd="sourceFilter"]')).toBeNull();
  });

  it('hides business details and normal history while preserving production information', () => {
    loadRestricted();
    const fixture = TestBed.createComponent(OrderDetailsComponent);
    fixture.detectChanges();
    fixture.componentInstance.errorMsg = '';
    spyOn(fixture.componentInstance, 'ngOnInit');
    fixture.componentInstance.loading = false;
    fixture.componentInstance.order = { id: 42, manufacturerOrderNumber: 'AD42',
      customerName: 'Private Buyer', customerOrderNumber: 'Private Reference', amount: 987654,
      consigneeName: 'Private Receiver', consigneeAddress: 'Private Address', trackingNumber: 'Private Tracking',
      status: 'Private Status', assignmentStatus: 'inprogress', customerProductTitle: 'Production Product',
      manufacturerMaterial: 'Cotton', sizeDetails: 'Production Measurements', isCustomSize: true,
      statusHistory: [], inventoryBills: [], images: [] } as unknown as OrderDetails;
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    for (const value of ['Private', '987,654', 'Amount', 'Status History', 'Inventory Bills', 'Shipping Address'])
      expect(text).not.toContain(value);
    for (const value of ['Production Product', 'Cotton', 'Production Measurements', 'In Progress'])
      expect(text).toContain(value);
    loadRestricted(false);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Private Buyer');
    expect(fixture.nativeElement.textContent).toContain('987,654');
    expect(fixture.nativeElement.textContent).toContain('Status History');
  });

  it('renders assignment progress instead of the main status dropdown', () => {
    loadRestricted();
    const fixture = TestBed.createComponent(ManageOrdersComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    component.loading = false;
    component.orders = [{ id: 42, manufacturerOrderNumber: 'AD42', customerName: 'Customer',
      customerProductTitle: 'Product', assignmentStatus: 'assigned', status: 'new' } as OrderListItem];
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-order-status-menu]')).toBeNull();
    expect(fixture.nativeElement.querySelector('select[aria-label="Assignment status"]')).toBeNull();
    const trigger: HTMLButtonElement = fixture.nativeElement.querySelector('button[aria-label="Assignment status"]');
    const bounds = spyOn(trigger, 'getBoundingClientRect').and.returnValue({ left: 100, right: 220, top: window.innerHeight - 160, bottom: window.innerHeight - 120 } as DOMRect);
    trigger.click(); fixture.detectChanges();
    expect(component.statusMenuTop).toBe(window.innerHeight - 112);
    expect(component.statusMenuMaxHeight).toBe(104);
    bounds.and.returnValue({ left: 60, right: 180, top: 60, bottom: 100 } as DOMRect);
    document.dispatchEvent(new Event('scroll')); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#manufacturing-status-menu').style.top).toBe('108px');
    expect(component.statusMenuLeft).toBe(60);
    bounds.and.returnValue({ left: 60, right: 180, top: -50, bottom: -10 } as DOMRect);
    document.dispatchEvent(new Event('scroll')); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#manufacturing-status-menu')).toBeNull();
    bounds.and.returnValue({ left: 60, right: 180, top: 60, bottom: 100 } as DOMRect);
    trigger.click(); fixture.detectChanges();
    const options = Array.from(fixture.nativeElement.querySelectorAll('#manufacturing-status-menu button')) as HTMLButtonElement[];
    expect(options.map(o => o.textContent?.trim().replace('✓', '').trim())).toEqual(['Assigned', 'In Progress', 'Done']);
    orders.updateAssignmentStatus.and.returnValue(of({ assignmentStatus: 'done' }));
    options[2].click();
    expect(orders.updateAssignmentStatus).toHaveBeenCalledWith(42, 'done');
    expect(component.orders[0].assignmentStatus).toBe('done');
  });

  it('loads saved messages beside assignees in the accordion', () => {
    role = 'Super Admin';
    loadRestricted(false);
    orders.getAssignmentOptions.and.returnValue(of([{ id: 7, name: 'Cutting', users: [{ id: 9, name: 'Team member', email: 'member@example.test' }] }]));
    orders.getAssignmentMessages.and.returnValue(of({ 9: 'Cut carefully' }));
    const fixture = TestBed.createComponent(ManageOrdersComponent);
    const component = fixture.componentInstance;
    spyOn(component, 'ngOnInit');
    fixture.detectChanges();
    component.loading = false;
    component.orders = [{ id: 42, assignedUserIds: [9] } as OrderListItem];
    component.openUserAssignments(component.orders[0], new MouseEvent('click'));
    fixture.detectChanges();
    const textarea: HTMLTextAreaElement = fixture.nativeElement.querySelector('textarea[aria-label="Message for Team member"]');
    expect(textarea).not.toBeNull();
    expect(component.assignmentMessages[9]).toBe('Cut carefully');
    expect(textarea.disabled).toBeFalse();
  });

  it('clears unread only when the recipient successfully opens the message', () => {
    loadRestricted();
    const component = TestBed.createComponent(ManageOrdersComponent).componentInstance;
    const response = new Subject<{ message: string }>();
    orders.readAssignmentMessage.and.returnValue(response);
    const order = { id: 42, hasAssignmentMessage: true, assignmentMessageUnread: true } as OrderListItem;
    component.openAssignmentMessage(order, new MouseEvent('click'));
    expect(order.assignmentMessageUnread).toBeTrue();
    response.next({ message: 'Cut carefully' }); response.complete();
    expect(component.messageText).toBe('Cut carefully');
    expect(order.assignmentMessageUnread).toBeFalse();
    component.closeAssignmentMessage();
    expect(component.messageOrder).toBeNull();
  });

  it('saves multiple selected users and can remove every assignee', () => {
    role = 'Admin';
    loadRestricted(false);
    const component = TestBed.createComponent(ManageOrdersComponent).componentInstance;
    component.assignmentOrderId = 42;
    component.assignmentOptionsLoaded = true;
    component.orders = [{ id: 42 } as OrderListItem];
    component.toggleAssignee(7); component.toggleAssignee(9);
    orders.saveAdminAssignments.and.returnValue(of({ assignedUserIds: [7, 9] }));
    component.saveUserAssignments();
    expect(orders.saveAdminAssignments).toHaveBeenCalledWith(42, [7, 9], { 7: '', 9: '' });
    expect(component.orders[0].assignedUserIds).toEqual([7, 9]);
    component.assignmentOrderId = 42;
    component.toggleAssignee(7); component.toggleAssignee(9);
    orders.saveAdminAssignments.and.returnValue(of({ assignedUserIds: [] }));
    component.saveUserAssignments();
    expect(orders.saveAdminAssignments).toHaveBeenCalledWith(42, [], {});
  });

  it('disables Visible, Add, Edit and Delete when assigned-only access is checked', () => {
    const fixture = TestBed.createComponent(RolesAndPermissionsComponent);
    const component = fixture.componentInstance;
    spyOn(component, 'ngOnInit');
    component.selectedRoleId = 7;
    component.roleOptions = [{ id: 7, name: 'Production', isActive: true }];
    const row = { screenKey: 'Orders', canView: true, canAdd: true, canEdit: true, canDelete: true, adminAssignedOnly: false };
    component.permissions = [row];
    component.toggleAdminAssigned(row, true);
    const save = TestBed.inject(HttpTestingController).expectOne(req => req.method === 'PUT' && req.url.endsWith('/rolepermissions'));
    expect(save.request.body).toEqual({ roleId: 7, permissions: [{ ...row }] });
    expect(save.request.body.permissions[0].adminAssignedOnly).toBeTrue();
    save.flush({});
    fixture.detectChanges();
    const checks: HTMLInputElement[] = Array.from(fixture.nativeElement.querySelectorAll('tbody input'));
    expect(checks.length).toBe(5);
    expect(checks.filter(c => c.disabled).length).toBe(4);
    expect([row.canView, row.canAdd, row.canEdit, row.canDelete]).toEqual([false, false, false, false]);
    expect(row.adminAssignedOnly).toBeTrue();
  });

  it('immediately replaces old orders when permissions change and ignores an older response', () => {
    loadRestricted(false);
    const oldRequest = new Subject<any>();
    const restrictedRequest = new Subject<any>();
    orders.getOrders.and.returnValues(oldRequest, restrictedRequest);
    const fixture = TestBed.createComponent(ManageOrdersComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.orders = [{ id: 99 } as OrderListItem];
    component.totalCount = 50;
    component.detailsOrderId = 99;
    loadRestricted(true);
    expect(component.orders).toEqual([]);
    expect(component.totalCount).toBe(0);
    expect(component.detailsOrderId).toBeNull();
    expect(orders.getOrders).toHaveBeenCalledTimes(2);
    restrictedRequest.next({ items: [{ id: 42, assignmentStatus: 'assigned' }], totalCount: 1 });
    oldRequest.next({ items: [{ id: 99 }], totalCount: 50 });
    expect(component.orders.map(order => order.id)).toEqual([42]);
    expect(component.totalCount).toBe(1);
  });

  it('restores the checkbox if immediate saving fails', () => {
    const component = TestBed.createComponent(RolesAndPermissionsComponent).componentInstance;
    component.selectedRoleId = 7;
    component.roleOptions = [{ id: 7, name: 'Production', isActive: true }];
    const row = { screenKey: 'Orders', canView: true, canAdd: true, canEdit: true, canDelete: true, adminAssignedOnly: false };
    component.toggleAdminAssigned(row, true);
    TestBed.inject(HttpTestingController).expectOne(req => req.method === 'PUT').flush({}, { status: 500, statusText: 'Failed' });
    expect(row.adminAssignedOnly).toBeFalse();
    expect(row.canView).toBeTrue();
    expect(component.assignmentPermissionError).toContain('not saved');
  });
});
