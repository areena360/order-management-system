import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  Output,
  SimpleChanges,
  ViewChild,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, takeUntil } from 'rxjs';

import { AuthService } from '../../../auth/auth.service';
import { ChatService } from '../chat.service';
import { ChatMessage } from '../chat.models';
import {
  ChatSignalrService,
} from '../../../core/signalr/chat-signalr.service';

@Component({
  selector: 'app-chat-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-modal.component.html',
})
export class ChatModalComponent implements OnInit, OnChanges, OnDestroy {

  @Input() orderId!: number;
  @Input() orderNumber = '';
  @Input() customerName = '';
  @Input() isCustomer = false;

  @Output() closed = new EventEmitter<void>();

  @ViewChild('scrollAnchor') scrollAnchor?: ElementRef<HTMLDivElement>;
  @ViewChild('textarea') textarea?: ElementRef<HTMLTextAreaElement>;

  private readonly chatService = inject(ChatService);
  private readonly chatSignalr = inject(ChatSignalrService);
  private readonly authService = inject(AuthService);

  private destroy$ = new Subject<void>();
  private unregisterListener: (() => void) | null = null;
  private joinedOrderId: number | null = null;

  messages: ChatMessage[] = [];
  newMessage = '';
  loading = true;
  sending = false;
  errorMsg = '';

  currentUserId = 0;

  ngOnInit(): void {
    this.currentUserId = this.readUserIdFromToken();

    this.unregisterListener = this.chatSignalr.onMessage(msg => {
      if (msg.orderId !== this.orderId) return;
      if (this.messages.some(m => m.id === msg.id)) return;

      this.messages.push(msg);
      this.scrollToBottom();
    });

    this.loadHistory();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['orderId'] && !changes['orderId'].firstChange) {
      this.loadHistory();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();

    if (this.unregisterListener) this.unregisterListener();

    if (this.joinedOrderId !== null) {
      this.chatSignalr.leaveOrderChat(this.joinedOrderId);
      this.joinedOrderId = null;
    }
  }

  private loadHistory(): void {
    if (!this.orderId) return;

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
          this.messages = msgs;
          this.loading = false;
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
    if (!text || this.sending || !this.orderId) return;

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

  isOwn(msg: ChatMessage): boolean {
    return msg.senderUserId === this.currentUserId;
  }

  formatTime(dateStr: string): string {
    const d = new Date(dateStr);
    if (Number.isNaN(d.getTime())) return '';
    return d.toLocaleTimeString('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
    });
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
    } catch {
      return 0;
    }
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.scrollAnchor?.nativeElement;
      if (el) el.scrollIntoView({ behavior: 'smooth', block: 'end' });
    }, 30);
  }
}