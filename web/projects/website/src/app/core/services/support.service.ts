import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';

export interface GuestContactRequest {
  name: string;
  channel: string;
  subject: string;
  message: string;
}

export interface GuestContactMessage {
  id: number;
  name: string;
  channel: string;
  subject: string;
  message: string;
  createdAt: string;
}

@Injectable({
  providedIn: 'root',
})
export class SupportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/support';

  sendGuestContact(req: GuestContactRequest): Observable<ApiResponse<GuestContactMessage>> {
    return this.http.post<ApiResponse<GuestContactMessage>>(`${this.baseUrl}/contact-public`, req);
  }
}
