export interface LookupItem {
  id: number;
  name: string;
}

export interface CustomerOption {
  id: number;
  name: string;
  email: string;
}

export interface OrderListItem {
  source?: string;
  id: number;

  manufacturerOrderNumber: string;
  customerOrderNumber: string | null;

  customerName: string;

  customerProductTitle: string;
  manufacturerProductTitle: string | null;

  amount: number | null;

  status: string;
  orderStatusId: number;

  priority: string | null;

  daysForMaking: number;
  deadline: string | null;

  trackingNumber: string | null;

  // ===== Assignment (NEW) =====
  isAssigned?: boolean;
  requiresCustomerAssignment?: boolean;
  assignedDate?: string | null;

  images?: OrderImageItem[];
}

export interface OrderStatusHistoryItem {
  id: number;
  statusId: number;
  status: string;
  createdDate: string;
  changedByName?: string | null;
}

export interface OrderImageItem {
  id: number;
  imageURL: string;
}

export interface InventoryBillItem {
  id: number;
  billNumber: number | null;
  billDetails: string;
  billImage: string | null;
  createdDate: string;
}

export interface OrderDetails {
  source?: string;
  id: number;

  manufacturerOrderNumber: string;
  customerOrderNumber: string | null;

  customerId: number;
  customerName: string;

  customerProductTitle: string;
  manufacturerProductTitle: string | null;

  genderId: number;
  gender: string | null;

  customerMaterialId: number | null;
  customerMaterial: string | null;

  manufacturerMaterialId: number | null;
  manufacturerMaterial: string | null;

  amount: number | null;

  priorityId: number | null;
  priority: string | null;

  isCustomSize: boolean;

  sizeId: number | null;
  size: string | null;

  sizeChartId: number | null;
  sizeChart: string | null;

  sizeDetails: string | null;

  daysForMaking: number;
  deadline: string | null;

  consigneeName: string;
  shippingEmail: string | null;
  shippingContact: string | null;
  consigneeAddress: string;
  courier: string | null;

  trackingNumber: string | null;

  notesByCustomer: string | null;
  notesByManufacturer: string | null;

  orderStatusId: number;
  status: string;

  // ===== Assignment (NEW) =====
  isAssigned?: boolean;
  requiresCustomerAssignment?: boolean;
  assignedDate?: string | null;

  images: OrderImageItem[];
  statusHistory: OrderStatusHistoryItem[];
  inventoryBills: InventoryBillItem[];
}

export interface OrderFormValue {
  customerProductTitle: string;

  manufacturerProductTitle: string | null;

  customerOrderNumber: string | null;

  customerId: number | null;

  amount: number | null;

  genderId: number | null;

  customerMaterialId: number | null;

  manufacturerMaterialId: number | null;

  isCustomSize: boolean;

  sizeId: number | null;

  sizeChartId: number | null;

  sizeDetails: string | null;

  daysForMaking?: number | null;
  deadline?: string | null;

  priorityId: number | null;

  consigneeName: string;

  shippingEmail: string | null;
  shippingContact: string | null;

  consigneeAddress: string;

  courier: string | null;

  trackingNumber: string | null;

  notesByCustomer: string | null;

  notesByManufacturer: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface OrderQuery {
  source?: string;
  pageNumber: number;
  pageSize: number;

  search?: string;

  sortBy?: string;

  sortDirection?: 'asc' | 'desc';

  statusId?: number | null;

  priorityId?: number | null;

  customerId?: number | null;

  genderId?: number | null;

  materialId?: number | null;

  dateFrom?: string | null;

  dateTo?: string | null;
}
