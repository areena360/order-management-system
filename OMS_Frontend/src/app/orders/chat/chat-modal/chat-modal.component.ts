import {
  Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy,
  OnInit, Output, SimpleChanges, ViewChild, inject, ChangeDetectorRef, HostListener,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, takeUntil } from 'rxjs';

import { AuthService } from '../../../auth/auth.service';
import { PermissionService } from '../../../auth/permission.service';
import { ChatService } from '../chat.service';
import { ChatMessage, IncomingGroupChatMessage } from '../chat.models';
import { ChatSignalrService } from '../../../core/signalr/chat-signalr.service';

type ChatTab = 'customer' | 'group';

@Component({
  selector: 'app-chat-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-modal.component.html',
  styleUrl: './chat-modal.component.css',
})
export class ChatModalComponent implements OnInit, OnChanges, OnDestroy {

  @Input() orderId!: number;
  @Input() orderNumber = '';
  @Input() customerName = '';
  @Input() isCustomer = false;
  @Input() initialTab: ChatTab = 'customer';
  @Input() customerUnread = 0;
  @Input() groupUnread = 0;
  @Output() channelRead = new EventEmitter<ChatTab>();

  @Output() closed = new EventEmitter<void>();

  @ViewChild('scrollAnchor') scrollAnchor?: ElementRef<HTMLDivElement>;
  @ViewChild('groupScrollAnchor') groupScrollAnchor?: ElementRef<HTMLDivElement>;
  @ViewChild('textarea') textarea?: ElementRef<HTMLTextAreaElement>;
  @ViewChild('groupTextarea') groupTextarea?: ElementRef<HTMLTextAreaElement>;

  private readonly chatService = inject(ChatService);
  private readonly chatSignalr = inject(ChatSignalrService);
  private readonly authService = inject(AuthService);
  private readonly permissionService = inject(PermissionService);
  private readonly cdr = inject(ChangeDetectorRef);

  private destroy$ = new Subject<void>();
  private unregisterListener: (() => void) | null = null;
  private unregisterGroupListener: (() => void) | null = null;
  private joinedOrderId: number | null = null;

  // Tabs
  activeTab: ChatTab = 'customer';

  // Customer chat
  messages: ChatMessage[] = [];
  newMessage = '';
  loading = true;
  private customerHistoryRequested = false;
  sending = false;
  errorMsg = '';

  // Group chat
  get canViewCustomerChat(): boolean { return this.permissionService.canView('Order Customer Chat'); }
  get canSendCustomerChat(): boolean { return this.canViewCustomerChat && this.permissionService.canAdd('Order Customer Chat'); }
  get canViewGroupChat(): boolean { return !this.isCustomer && this.permissionService.canView('Order Group Chat'); }
  get canSendGroupChat(): boolean { return this.canViewGroupChat && this.permissionService.canAdd('Order Group Chat'); }
  groupMessages: IncomingGroupChatMessage[] = [];
  groupNewMessage = '';
  groupLoading = false;
  groupSending = false;
  groupErrorMsg = '';
  private groupLoaded = false;

  currentUserId = 0;
  readonly enteringMessages = new Set<number>();
  private readonly entranceTimers = new Map<number, ReturnType<typeof setTimeout>>();

  trackMessage(_index: number, message: { id: number }): number { return message.id; }

  private animateMessage(id: number, tab: ChatTab): void {
    if (this.activeTab !== tab || document.visibilityState !== 'visible'
      || (tab === 'customer' ? this.loading : this.groupLoading)) return;
    this.enteringMessages.add(id);
    this.entranceTimers.set(id, setTimeout(() => {
      this.enteringMessages.delete(id);
      this.entranceTimers.delete(id);
      this.cdr.markForCheck();
    }, 360));
  }

  private clearEntranceAnimations(): void {
    this.entranceTimers.forEach(timer => clearTimeout(timer));
    this.entranceTimers.clear();
    this.enteringMessages.clear();
  }

  ngOnInit(): void {
    this.currentUserId = this.readUserIdFromToken();

    // Customer chat listener (existing)
    this.unregisterListener = this.chatSignalr.onMessage(msg => {
      if (!this.canViewCustomerChat || msg.orderId !== this.orderId) return;
      if (this.messages.some(m => m.id === msg.id)) return;
      this.messages.push(msg);
      this.animateMessage(msg.id, 'customer');
      this.cdr.markForCheck();
      if (this.activeTab === 'customer' && !this.loading) this.acknowledgeRead('customer');
      this.scrollToBottom();
    });

    // Group chat permissions & listener
    if (!this.isCustomer) {
        this.unregisterGroupListener = this.chatSignalr.onGroupMessage(msg => {
          if (!this.canViewGroupChat || msg.orderId !== this.orderId) return;
          if (this.groupMessages.some(m => m.id === msg.id)) return;
          this.groupMessages.push(msg);
          this.animateMessage(msg.id, 'group');
          if (this.activeTab === 'group' && !this.groupLoading) this.acknowledgeRead('group');
          this.cdr.detectChanges();
          this.scrollGroupToBottom();
        });
    }

    this.activeTab = this.initialTab === 'group' && this.canViewGroupChat ? 'group' : 'customer';
    if (!this.canViewCustomerChat && this.canViewGroupChat) this.activeTab = 'group';
    if (this.canViewCustomerChat) this.loadHistory();
    if (this.activeTab === 'group') this.loadGroupHistory();
    this.permissionService.permissionsLoaded$.pipe(takeUntil(this.destroy$)).subscribe(loaded => {
      if (!loaded) return;
      if (!this.canViewCustomerChat) { this.messages = []; this.customerHistoryRequested = false; this.loading = true; }
      if (!this.canViewGroupChat) { this.groupMessages = []; this.groupLoaded = false; }
      if (this.activeTab === 'customer' ? !this.canViewCustomerChat : !this.canViewGroupChat) this.close();
      this.cdr.markForCheck();
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['orderId'] && !changes['orderId'].firstChange) {
      this.clearEntranceAnimations();
      this.groupLoaded = false;
      this.groupMessages = [];
      this.activeTab = 'customer';
      this.loadHistory();
    }
  }

  ngOnDestroy(): void {
    this.clearEntranceAnimations();
    this.destroy$.next();
    this.destroy$.complete();

    this.unregisterListener?.();
    this.unregisterGroupListener?.();

    if (this.joinedOrderId !== null) {
      this.chatSignalr.leaveOrderChat(this.joinedOrderId);
      this.joinedOrderId = null;
    }
  }

  // =================== Tabs ===================
  switchTab(tab: ChatTab): void {
    if (tab === 'group' ? !this.canViewGroupChat : !this.canViewCustomerChat) return;
    if (tab !== this.activeTab) this.clearEntranceAnimations();
    this.activeTab = tab;
    if (tab === 'customer' && !this.customerHistoryRequested) { this.loadHistory(); return; }
    if (tab === 'group' && this.canViewGroupChat && !this.groupLoaded) {
      this.loadGroupHistory();
    } else if (tab === 'group' || (!this.loading && !this.errorMsg)) {
      this.acknowledgeRead(tab);
    }
  }

  // =================== Customer chat ===================
  private loadHistory(): void {
    if (!this.orderId || !this.canViewCustomerChat) return;
    this.customerHistoryRequested = true;

    this.loading = true;
    this.errorMsg = '';
    this.messages = [];

    if (this.joinedOrderId !== null && this.joinedOrderId !== this.orderId) {
      this.chatSignalr.leaveOrderChat(this.joinedOrderId);
      this.joinedOrderId = null;
    }

    this.chatService.getOrderConversation(this.orderId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: msgs => {
          if (!this.canViewCustomerChat) return;
          this.messages = msgs;
          this.loading = false;
          if (this.activeTab === 'customer') this.acknowledgeRead('customer');
          this.scrollToBottom();

          this.chatSignalr.joinOrderChat(this.orderId)
            .then(() => { this.joinedOrderId = this.orderId; });

          setTimeout(() => this.textarea?.nativeElement.focus(), 50);
        },
        error: (err: HttpErrorResponse) => {
          this.errorMsg = err?.error?.message ?? 'Unable to load chat.';
          this.loading = false;
        }
      });
  }

  async send(): Promise<void> {
    const text = (this.newMessage ?? '').trim();
    if (!text || this.sending || !this.orderId || !this.canSendCustomerChat) return;

    this.sending = true;
    this.errorMsg = '';

    try {
      await this.chatSignalr.sendMessage(this.orderId, text);
      this.newMessage = '';
      setTimeout(() => this.textarea?.nativeElement.focus(), 10);
    } catch (err: any) {
      this.errorMsg = err?.message ?? 'Unable to send message.';
    } finally {
      this.sending = false;
    }
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  // =================== Group chat ===================
  private loadGroupHistory(): void {
    if (!this.orderId || !this.canViewGroupChat) return;

    this.groupLoading = true;
    this.groupErrorMsg = '';
    this.groupMessages = [];

    this.chatService.getGroupConversation(this.orderId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: msgs => {
          if (!this.canViewGroupChat) return;
          this.groupMessages = msgs;
          this.groupLoading = false;
          this.groupLoaded = true;
          if (this.activeTab === 'group') this.acknowledgeRead('group');
          this.scrollGroupToBottom();
        },
        error: (err: HttpErrorResponse) => {
          this.groupErrorMsg = err?.error?.message ?? 'Unable to load group chat.';
          this.groupLoading = false;
        }
      });
  }

  async sendGroup(): Promise<void> {
    const text = (this.groupNewMessage ?? '').trim();
    if (!text || this.groupSending || !this.orderId || !this.canSendGroupChat) return;

    this.groupSending = true;
    this.groupErrorMsg = '';

    try {
      await this.chatSignalr.sendGroupMessage(this.orderId, text);
      this.groupNewMessage = '';
      setTimeout(() => this.groupTextarea?.nativeElement.focus(), 10);
    } catch (err: any) {
      this.groupErrorMsg = err?.message ?? 'Unable to send group message.';
    } finally {
      this.groupSending = false;
    }
  }

  onGroupKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendGroup();
    }
  }

  @HostListener('document:visibilitychange')
  onVisibilityChange(): void { this.acknowledgeRead(this.activeTab); }

  private acknowledgeRead(tab: ChatTab): void {
    if (document.visibilityState !== 'visible' || this.activeTab !== tab) return;
    if (tab === 'group' ? !this.canViewGroupChat : !this.canViewCustomerChat) return;
    if (tab === 'customer' ? (this.loading || !!this.errorMsg) : (!this.groupLoaded || this.groupLoading || !!this.groupErrorMsg)) return;
    const messages = tab === 'group' ? this.groupMessages : this.messages;
    const lastId = messages.reduce((max, message) => Math.max(max, message.id), 0);
    if (!lastId) return;
    this.chatService.markRead(this.orderId, tab, lastId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => this.channelRead.emit(tab),
      error: () => { /* Keep unread badges until the server confirms read progress. */ }
    });
  }

  // =================== Helpers ===================
  isOwn(msg: ChatMessage): boolean {
    return msg.senderUserId === this.currentUserId;
  }

  isOwnGroup(msg: IncomingGroupChatMessage): boolean {
    return msg.senderUserId === this.currentUserId;
  }

  formatTime(dateStr: string): string {
    const d = new Date(dateStr);
    if (Number.isNaN(d.getTime())) return '';
    return d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  }

  close(): void {
    this.closed.emit();
  }

  private readUserIdFromToken(): number {
    const token = this.authService.getToken();
    if (!token) return 0;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return Number(payload.userId ?? 0);
    } catch { return 0; }
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.scrollAnchor?.nativeElement;
      if (el) el.scrollIntoView({ behavior: 'smooth', block: 'end' });
    }, 30);
  }

  private scrollGroupToBottom(): void {
    setTimeout(() => {
      const el = this.groupScrollAnchor?.nativeElement;
      if (el) el.scrollIntoView({ behavior: 'smooth', block: 'end' });
    }, 30);
  }
}
