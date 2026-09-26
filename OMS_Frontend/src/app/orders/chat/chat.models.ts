export interface ChatMessage {
  id: number;
  orderId: number;
  customerId: number;
  senderUserId: number;
  senderRole: 'Customer' | 'Admin' | string;
  senderName?: string;   // NEW
  message: string;
  channel?: 'Customer' | 'Group' | string;  // NEW
  createdDate: string;
}

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