import { TestBed } from '@angular/core/testing';
import { EMPTY, of, throwError } from 'rxjs';
import { ManufacturingProgressComponent } from './manufacturing-progress.component';
import { ManufacturingEvent, OrdersService } from '../orders.service';
import { PollingService } from '../../core/polling/polling.service';

describe('Manufacturing progress', () => {
  let api: jasmine.SpyObj<OrdersService>;
  const event = (id: number, userId: number, roleId: number, status: string, isSnapshot = false): ManufacturingEvent => ({
    id, userId, roleId, userName: `Member ${userId}`, roleName: `Role ${roleId}`, status,
    occurredAt: `2026-09-29T08:0${id}:00Z`, isSnapshot
  });
  beforeEach(() => {
    api = jasmine.createSpyObj('OrdersService', ['getManufacturing']);
    TestBed.configureTestingModule({ imports: [ManufacturingProgressComponent], providers: [
      { provide: OrdersService, useValue: api }, { provide: PollingService, useValue: { poll: () => EMPTY } }
    ] });
  });
  it('groups actual history by role and member and excludes removed members from completion', () => {
    api.getManufacturing.and.returnValue(of({ events: [event(1, 1, 7, 'assigned'), event(2, 2, 8, 'assigned'),
      event(3, 1, 7, 'done', true), event(4, 2, 8, 'unassigned')] }));
    const fixture = TestBed.createComponent(ManufacturingProgressComponent);
    fixture.componentRef.setInput('orderId', 42);
    fixture.componentRef.setInput('orderNumber', 'AD42');
    fixture.detectChanges();
    expect(api.getManufacturing).toHaveBeenCalledWith(42);
    expect(fixture.componentInstance.groups.length).toBe(2);
    expect(fixture.componentInstance.percent).toBe(100);
    expect(fixture.componentInstance.members.length).toBe(1);
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Role 7'); expect(text).toContain('Role 8');
    expect(text).toContain('Removed'); expect(text).toContain('existing status recorded');
    expect(fixture.nativeElement.querySelectorAll('ol li').length).toBe(4);
    api.getManufacturing.and.returnValue(throwError(() => new Error('Offline')));
    fixture.componentInstance.refresh(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelectorAll('ol li').length).toBe(4);
  });
  it('shows an empty state and closes using Escape', () => {
    api.getManufacturing.and.returnValue(of({ events: [] }));
    const fixture = TestBed.createComponent(ManufacturingProgressComponent);
    fixture.componentRef.setInput('orderId', 42);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No manufacturing team assigned yet');
    const close = spyOn(fixture.componentInstance.closed, 'emit');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(close).toHaveBeenCalled();
  });
});
