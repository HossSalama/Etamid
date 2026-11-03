export interface ApiError {
  code: string;
  field?: string;
  message: string;
  // The server marks error extras [JsonExtensionData], so they arrive flattened
  // into the error object itself — e.g. the product-import row errors land here.
  errors?: Array<{ row: number; field: string; code: string }>;
}

export interface ApiMeta {
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  errors?: ApiError[];
  meta?: ApiMeta;
}
