import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ChatMessage, IncomingGroupChatMessage } from './chat.models';

@Injectable({ providedIn: 'root' })
export class ChatService {

  private readonly apiUrl = `${environment.apiUrl}/chat`;

  constructor(private http: HttpClient) {}
  getUnread(): Observable<{ orderId: number; channel: string; count: number }[]> {
    return this.http.get<{ orderId: number; channel: string; count: number }[]>(`${this.apiUrl}/unread`);
  }
  markRead(orderId: number, channel: 'customer' | 'group', lastReadMessageId: number): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/order/${orderId}/read`, {
      channel: channel === 'group' ? 'Group' : 'Customer', lastReadMessageId
    });
  }

  getOrderConversation(orderId: number): Observable<ChatMessage[]> {
    return this.http.get<ChatMessage[]>(`${this.apiUrl}/order/${orderId}`);
  }

  // NEW
  getGroupConversation(orderId: number): Observable<IncomingGroupChatMessage[]> {
    return this.http.get<IncomingGroupChatMessage[]>(
      `${this.apiUrl}/order/${orderId}/group`
    );
  }
}
