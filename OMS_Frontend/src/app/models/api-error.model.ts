export interface ApiErrorResponse {
  status: number;
  title: string;
  type?: string;
  instance?: string;
  traceId?: string;
  errorCode?: string;
  errors?: Record<string, string[]> | null;
  detail?: string | null;
}