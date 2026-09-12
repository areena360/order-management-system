import { Injectable } from '@angular/core';
import {
  HttpClient,
  HttpParams
} from '@angular/common/http';
import { Observable } from 'rxjs';

import {
  CustomerOption,
  InventoryBillItem,
  LookupItem,
  OrderDetails,
  OrderFormValue,
  OrderImageItem,
  OrderListItem,
  OrderQuery,
  PagedResult
} from './order.models';

import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class OrdersService {

  private readonly apiUrl =
    `${environment.apiUrl}/orders`;

  private readonly backendUrl =
    environment.apiUrl.replace(/\/api\/?$/, '');

  constructor(
    private http: HttpClient
  ) {}

  getFileUrl(
    fileUrl: string | null | undefined
  ): string {

    if (!fileUrl) {
      return '';
    }

    if (
      fileUrl.startsWith('http://') ||
      fileUrl.startsWith('https://')
    ) {
      return fileUrl;
    }

    return `${this.backendUrl}${fileUrl.startsWith('/') ? '' : '/'}${fileUrl}`;
  }

  getImageUrl(
    imageUrl: string | null | undefined
  ): string {

    return this.getFileUrl(imageUrl);
  }

  getOrders(
    query: OrderQuery
  ): Observable<PagedResult<OrderListItem>> {

    let params = new HttpParams()
      .set('pageNumber', query.pageNumber)
      .set('pageSize', query.pageSize);

    if (query.search) params = params.set('search', query.search);
    if (query.sortBy) params = params.set('sortBy', query.sortBy);
    if (query.sortDirection) params = params.set('sortDirection', query.sortDirection);
    if (query.statusId) params = params.set('statusId', query.statusId);
    if (query.priorityId) params = params.set('priorityId', query.priorityId);
    if (query.customerId) params = params.set('customerId', query.customerId);
    if (query.genderId) params = params.set('genderId', query.genderId);
    if (query.materialId) params = params.set('materialId', query.materialId);
    if (query.dateFrom) params = params.set('dateFrom', query.dateFrom);
    if (query.dateTo) params = params.set('dateTo', query.dateTo);

    return this.http.get<PagedResult<OrderListItem>>(
      this.apiUrl,
      { params }
    );
  }

  getOrder(
    id: number
  ): Observable<OrderDetails> {

    return this.http.get<OrderDetails>(
      `${this.apiUrl}/${id}`
    );
  }

  createOrder(
    dto: OrderFormValue
  ): Observable<OrderDetails> {

    return this.http.post<OrderDetails>(
      this.apiUrl,
      dto
    );
  }

  updateOrder(
    id: number,
    dto: OrderFormValue
  ): Observable<OrderDetails> {

    return this.http.put<OrderDetails>(
      `${this.apiUrl}/${id}`,
      dto
    );
  }

  deleteOrder(
    id: number
  ): Observable<void> {

    return this.http.delete<void>(
      `${this.apiUrl}/${id}`
    );
  }

  updateStatus(
    id: number,
    statusId: number
  ): Observable<OrderDetails> {

    return this.http.patch<OrderDetails>(
      `${this.apiUrl}/${id}/status`,
      { statusId }
    );
  }

  uploadImages(
    id: number,
    files: File[]
  ): Observable<OrderImageItem[]> {

    const formData = new FormData();

    files.forEach(file => {
      formData.append('files', file);
    });

    return this.http.post<OrderImageItem[]>(
      `${this.apiUrl}/${id}/images`,
      formData
    );
  }

  deleteImage(
    orderId: number,
    imageId: number
  ): Observable<void> {

    return this.http.delete<void>(
      `${this.apiUrl}/${orderId}/images/${imageId}`
    );
  }

  getInventoryBills(
    orderId: number
  ): Observable<InventoryBillItem[]> {

    return this.http.get<InventoryBillItem[]>(
      `${this.apiUrl}/${orderId}/inventory-bill`
    );
  }

  addInventoryBill(
    orderId: number,
    dto: {
      billNumber: number | null;
      billDetails: string;
      billImageFile?: File | null;
    }
  ): Observable<InventoryBillItem> {

    const formData = new FormData();

    if (
      dto.billNumber !== null &&
      dto.billNumber !== undefined
    ) {
      formData.append(
        'billNumber',
        String(dto.billNumber)
      );
    }

    formData.append('billDetails', dto.billDetails);

    if (dto.billImageFile) {
      formData.append('billImageFile', dto.billImageFile);
    }

    return this.http.post<InventoryBillItem>(
      `${this.apiUrl}/${orderId}/inventory-bill`,
      formData
    );
  }

  updateInventoryBill(
    orderId: number,
    billId: number,
    dto: {
      billNumber: number | null;
      billDetails: string;
      billImageFile?: File | null;
    }
  ): Observable<InventoryBillItem> {

    const formData = new FormData();

    if (
      dto.billNumber !== null &&
      dto.billNumber !== undefined
    ) {
      formData.append(
        'billNumber',
        String(dto.billNumber)
      );
    }

    formData.append('billDetails', dto.billDetails);

    if (dto.billImageFile) {
      formData.append('billImageFile', dto.billImageFile);
    }

    return this.http.put<InventoryBillItem>(
      `${this.apiUrl}/${orderId}/inventory-bill/${billId}`,
      formData
    );
  }

  deleteInventoryBill(
    orderId: number,
    billId: number
  ): Observable<void> {

    return this.http.delete<void>(
      `${this.apiUrl}/${orderId}/inventory-bill/${billId}`
    );
  }
}