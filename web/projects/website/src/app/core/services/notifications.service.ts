import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';

export interface AppNotification {
  id: number;
  type: string;
  title: string;
  body: string;
  isRead: boolean;
  dealId: number | null;
  companyDealId: number | null;
  installmentId: number | null;
  disputeId: number | null;
  createdAt: string;
}

export interface UnreadCount {
  count: number;
}

export interface ReadAllResult {
  markedCount: number;
}

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/notifications';

  getNotifications(page = 1, pageSize = 20): Observable<ApiResponse<AppNotification[]>> {
    return this.http.get<ApiResponse<AppNotification[]>>(this.baseUrl, {
      params: { page, pageSize },
    });
  }

  getUnreadCount(): Observable<ApiResponse<UnreadCount>> {
    return this.http.get<ApiResponse<UnreadCount>>(`${this.baseUrl}/unread-count`);
  }

  markRead(id: number): Observable<ApiResponse<AppNotification>> {
    return this.http.post<ApiResponse<AppNotification>>(`${this.baseUrl}/${id}/read`, null);
  }

  markAllRead(): Observable<ApiResponse<ReadAllResult>> {
    return this.http.post<ApiResponse<ReadAllResult>>(`${this.baseUrl}/read-all`, null);
  }
}
