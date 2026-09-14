export interface ChatMessage {
  id: number;
  orderId: number;
  customerId: number;
  senderUserId: number;
  senderRole: 'Customer' | 'Admin' | string;
  message: string;
  createdDate: string;
}