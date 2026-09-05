import { Injectable } from '@angular/core';

import {
  HttpClient,
  HttpParams
} from '@angular/common/http';

import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

import {
  CustomerOption,
  LookupItem
} from './order.models';

export const LOOKUP_TYPE = {

  OrderStatus: 1,

  Priority: 2,

  Gender: 3,

  Material: 4,

  Size: 5,

  SizeChart: 6

} as const;

@Injectable({
  providedIn: 'root'
})
export class LookupService {

  private readonly apiUrl =
    `${environment.apiUrl}/lookups`;

  constructor(
    private http: HttpClient
  ) {}

  getByType(
    typeId: number
  ): Observable<LookupItem[]> {

    return this.http.get<LookupItem[]>(
      `${this.apiUrl}/by-type/${typeId}`
    );
  }

  getCustomers(
    search?: string
  ): Observable<CustomerOption[]> {

    let params = new HttpParams();

    if (search) {
      params = params.set(
        'search',
        search
      );
    }

    return this.http.get<CustomerOption[]>(
      `${this.apiUrl}/customers`,
      { params }
    );
  }
}