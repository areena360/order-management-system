import { TestBed } from '@angular/core/testing';
import { ChangeDetectorRef } from '@angular/core';
import { BehaviorSubject, of } from 'rxjs';
import { ChatModalComponent } from './chat-modal.component';
import { ChatService } from '../chat.service';
import { AuthService } from '../../../auth/auth.service';
import { PermissionService } from '../../../auth/permission.service';
import { ChatSignalrService } from '../../../core/signalr/chat-signalr.service';

describe('Chat role controls', () => {
  let component: ChatModalComponent;
  let views: Set<string>;
  let sends: Set<string>;
  let updates: BehaviorSubject<boolean>;
  let api: any;
  let live: any;
  beforeEach(() => {
    views = new Set(['Order Group Chat']); sends = new Set(); updates = new BehaviorSubject(true);
    api = { getOrderConversation: jasmine.createSpy().and.returnValue(of([])), getGroupConversation: jasmine.createSpy().and.returnValue(of([])), markRead: jasmine.createSpy().and.returnValue(of(undefined)) };
    live = { onMessage: jasmine.createSpy().and.returnValue(() => {}), onGroupMessage: jasmine.createSpy().and.returnValue(() => {}), joinOrderChat: jasmine.createSpy().and.resolveTo(), leaveOrderChat: jasmine.createSpy().and.resolveTo(), sendMessage: jasmine.createSpy().and.resolveTo(), sendGroupMessage: jasmine.createSpy().and.resolveTo() };
    TestBed.configureTestingModule({ providers: [
      { provide: ChatService, useValue: api }, { provide: ChatSignalrService, useValue: live },
      { provide: AuthService, useValue: { getToken: () => null } },
      { provide: PermissionService, useValue: { canView: (s: string) => views.has(s), canAdd: (s: string) => sends.has(s), permissionsLoaded$: updates } },
      { provide: ChangeDetectorRef, useValue: { markForCheck: () => {}, detectChanges: () => {} } }
    ] });
    component = TestBed.runInInjectionContext(() => new ChatModalComponent());
    component.orderId = 7;
  });
  afterEach(() => component.ngOnDestroy());
  it('opens group-only access without requesting customer history', () => {
    component.ngOnInit();
    expect(component.activeTab).toBe('group');
    expect(api.getGroupConversation).toHaveBeenCalledWith(7);
    expect(api.getOrderConversation).not.toHaveBeenCalled();
    component.switchTab('customer');
    expect(component.activeTab).toBe('group');
  });
  it('view-only roles cannot invoke either send action', async () => {
    views.add('Order Customer Chat');
    component.newMessage = 'customer'; component.groupNewMessage = 'group';
    await component.send(); await component.sendGroup();
    expect(live.sendMessage).not.toHaveBeenCalled();
    expect(live.sendGroupMessage).not.toHaveBeenCalled();
  });
  it('closes and clears the active chat when permission is revoked', () => {
    component.ngOnInit();
    const closed = spyOn(component.closed, 'emit');
    views.clear(); updates.next(true);
    expect(closed).toHaveBeenCalled();
    expect(component.groupMessages).toEqual([]);
    expect(component.canViewGroupChat).toBeFalse();
  });
  it('group send works independently of customer access', async () => {
    sends.add('Order Group Chat'); component.groupNewMessage = 'team';
    await component.sendGroup();
    expect(live.sendGroupMessage).toHaveBeenCalledWith(7, 'team');
    expect(component.canViewCustomerChat).toBeFalse();
  });
});
