import { Injectable } from '@angular/core';
import {
  HttpClient,
  HttpParams
} from '@angular/common/http';

import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

import {
  OrderDetails,
  OrderFormValue,
  OrderListItem,
  OrderQuery,
  OrderImageItem,
  PagedResult,
  InventoryBillItem
} from './order.models';

@Injectable({
  providedIn: 'root'
})
export class OrdersService {

  private readonly apiUrl =
    `${environment.apiUrl}/orders`;

  constructor(
    private http: HttpClient
  ) {}

  getOrders(
    query: OrderQuery
  ): Observable<PagedResult<OrderListItem>> {

    let params = new HttpParams()
      .set('pageNumber', query.pageNumber)
      .set('pageSize', query.pageSize);

    if (query.search) {
      params = params.set('search', query.search);
    }

    if (query.sortBy) {
      params = params.set('sortBy', query.sortBy);
    }

    if (query.sortDirection) {
      params = params.set(
        'sortDirection',
        query.sortDirection
      );
    }

    if (query.statusId) {
      params = params.set(
        'statusId',
        query.statusId
      );
    }

    if (query.priorityId) {
      params = params.set(
        'priorityId',
        query.priorityId
      );
    }

    if (query.customerId) {
      params = params.set(
        'customerId',
        query.customerId
      );
    }

    if (query.genderId) {
      params = params.set(
        'genderId',
        query.genderId
      );
    }

    if (query.materialId) {
      params = params.set(
        'materialId',
        query.materialId
      );
    }

    if (query.dateFrom) {
      params = params.set(
        'dateFrom',
        query.dateFrom
      );
    }

    if (query.dateTo) {
      params = params.set(
        'dateTo',
        query.dateTo
      );
    }

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
      billImage?: string | null;
    }
  ): Observable<InventoryBillItem> {

    return this.http.post<InventoryBillItem>(
      `${this.apiUrl}/${orderId}/inventory-bill`,
      dto
    );
  }
}