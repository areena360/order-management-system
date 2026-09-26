import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../auth/auth.service';
import { PermissionService } from '../../auth/permission.service';
import { environment } from '../../../environments/environment';

export interface IncomingChatMessage {
  id: number;
  orderId: number;
  customerId: number;
  senderUserId: number;
  senderRole: 'Customer' | 'Admin' | string;
  message: string;
  createdDate: string;
}

// NEW: Group chat message shape
export interface IncomingGroupChatMessage {
  id: number;
  orderId: number;
  customerId: number;
  senderUserId: number;
  senderRole: string;
  senderName: string;
  message: string;
  channel: string;
  createdDate: string;
}

@Injectable({ providedIn: 'root' })
export class ChatSignalrService {

  private connection: signalR.HubConnection | null = null;
  private starting = false;

  private messageListeners = new Set<(msg: IncomingChatMessage) => void>();
  private groupMessageListeners = new Set<(msg: IncomingGroupChatMessage) => void>();

  constructor(private authService: AuthService, private permissions: PermissionService) {
    this.authService.sessionChanged$.subscribe(loggedIn => {
      if (loggedIn) this.start();
      else this.stop();
    });

    if (this.authService.getToken()) this.start();
  }

  async start(): Promise<void> {
    if (this.connection || this.starting) return;
    if (!this.authService.getToken()) return;

    this.starting = true;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.chatHubUrl, {
        accessTokenFactory: async () => (await firstValueFrom(this.authService.getValidToken())) ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('PermissionsChanged', () => {
      this.permissions.load(true).subscribe({ error: () => {} });
    });
    this.connection.onreconnected(() => {
      this.permissions.load(true).subscribe({ error: () => {} });
    });
    // Customer <-> Admin messages
    this.connection.on('MessageReceived', (msg: IncomingChatMessage) => {
      this.messageListeners.forEach(fn => fn(msg));
    });

    // NEW: Group (staff-only) messages
    this.connection.on('GroupMessageReceived', (msg: IncomingGroupChatMessage) => {
      this.groupMessageListeners.forEach(fn => fn(msg));
    });

    try {
      await this.connection.start();
      console.log('[Chat] Connected');
    } catch (err) {
      console.error('[Chat] Connection failed', err);
      this.connection = null;
    } finally {
      this.starting = false;
    }
  }

  async stop(): Promise<void> {
    if (!this.connection) return;
    try { await this.connection.stop(); } catch { /* ignore */ }
    this.connection = null;
  }

  // ==================== Customer chat ====================

  onMessage(fn: (msg: IncomingChatMessage) => void): () => void {
    this.messageListeners.add(fn);
    return () => this.messageListeners.delete(fn);
  }

  async joinOrderChat(orderId: number): Promise<void> {
    await this.start();
    if (!this.connection) return;
    try {
      await this.connection.invoke('JoinOrderChat', orderId);
    } catch (err) {
      console.error('[Chat] Join failed', err);
    }
  }

  async leaveOrderChat(orderId: number): Promise<void> {
    if (!this.connection) return;
    try {
      await this.connection.invoke('LeaveOrderChat', orderId);
    } catch { /* ignore */ }
  }

  async sendMessage(orderId: number, message: string): Promise<void> {
    await this.start();
    if (!this.connection) throw new Error('Chat not connected.');
    await this.connection.invoke('SendMessage', orderId, message);
  }

  // ==================== NEW: Group chat ====================

  onGroupMessage(fn: (msg: IncomingGroupChatMessage) => void): () => void {
    this.groupMessageListeners.add(fn);
    return () => this.groupMessageListeners.delete(fn);
  }

  async sendGroupMessage(orderId: number, message: string): Promise<void> {
    await this.start();
    if (!this.connection) throw new Error('Chat not connected.');
    await this.connection.invoke('SendGroupMessage', orderId, message);
  }
}
