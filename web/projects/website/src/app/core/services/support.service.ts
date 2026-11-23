import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
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

// One FAQ from GET /api/v1/support/faqs (localized by Accept-Language).
export interface Faq {
  id: number;
  question: string;
  answer: string;
}

// One item from GET /api/v1/lookups/ticket-subjects.
export interface TicketSubject {
  id: number;
  code: string;
  name: string;
  iconUrl?: string | null;
}

// The ticket created by POST /api/v1/support/contact (only what the UI shows).
export interface SupportTicket {
  id: number;
  number: string;
  subject: string;
  status: string;
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

  // The FAQ page: ?search= filters question + answer in both languages.
  getFaqs(search?: string): Observable<ApiResponse<Faq[]>> {
    const trimmed = (search ?? '').trim();
    const params = trimmed ? new HttpParams().set('search', trimmed) : undefined;
    return this.http.get<ApiResponse<Faq[]>>(`${this.baseUrl}/faqs`, { params });
  }

  // The subject dropdown on the registered contact page.
  getTicketSubjects(): Observable<ApiResponse<TicketSubject[]>> {
    return this.http.get<ApiResponse<TicketSubject[]>>('/api/v1/lookups/ticket-subjects');
  }

  // The WhatsApp number for the contact page (AppSettings support_whatsapp).
  getSupportWhatsapp(): Observable<ApiResponse<{ supportWhatsapp: string }>> {
    return this.http.get<ApiResponse<{ supportWhatsapp: string }>>('/api/v1/config');
  }

  // The registered contact form creates a support ticket (login required).
  sendContact(req: { subjectId: number; message: string }): Observable<ApiResponse<SupportTicket>> {
    return this.http.post<ApiResponse<SupportTicket>>(`${this.baseUrl}/contact`, req);
  }

  // ---------- Support Requests page (GET /api/v1/support/tickets + /api/v1/me/disputes) ----------

  // The user's own tickets, newest first. status: 'all' | 'open' | 'closed'
  // (the API maps 'open' to Open + InReview). Page 1, 20 items: the design
  // has no "load more", so the list shows the newest 20.
  getMyTickets(status?: string): Observable<ApiResponse<SupportTicketSummary[]>> {
    let params = new HttpParams().set('page', 1).set('pageSize', 20);
    if (status && status !== 'all') params = params.set('status', status);
    return this.http.get<ApiResponse<SupportTicketSummary[]>>(`${this.baseUrl}/tickets`, { params });
  }

  // One ticket thread: messages oldest-first, senderType 'user' | 'admin'.
  getTicketDetail(id: number): Observable<ApiResponse<SupportTicketDetail>> {
    return this.http.get<ApiResponse<SupportTicketDetail>>(`${this.baseUrl}/tickets/${id}`);
  }

  // Reply to a ticket (message ≤ 1000 chars; the API refuses closed tickets).
  postTicketMessage(id: number, message: string): Observable<ApiResponse<SupportTicketMessage>> {
    return this.http.post<ApiResponse<SupportTicketMessage>>(`${this.baseUrl}/tickets/${id}/messages`, { message });
  }

  // Close my own ticket. The API refuses an already-closed ticket.
  closeTicket(id: number): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/tickets/${id}/close`, {});
  }

  // The user's own disputes (opened by me OR against me), newest first.
  // No status filter here: the API takes a single enum value and the "open"
  // tab spans two (UnderReview + AwaitingInfo), so the page filters
  // client-side. Page 1, 20 items, like the tickets above.
  getMyDisputes(): Observable<ApiResponse<MyDisputeSummary[]>> {
    return this.http.get<ApiResponse<MyDisputeSummary[]>>('/api/v1/me/disputes', {
      params: new HttpParams().set('page', 1).set('pageSize', 20),
    });
  }
}

// One row of GET /api/v1/support/tickets. status is snake_case from the API:
// 'open' | 'in_review' | 'closed'.
export interface SupportTicketSummary {
  id: number;
  number: string;
  subject: string;
  status: string;
  createdAt: string;
  closedAt: string | null;
  lastMessageAt: string | null;
}

// One message of GET /api/v1/support/tickets/{id}. senderType: 'user' | 'admin'.
export interface SupportTicketMessage {
  id: number;
  senderType: string;
  senderName: string;
  message: string;
  fileUrl?: string | null;
  createdAt: string;
}

export interface SupportTicketDetail {
  id: number;
  number: string;
  subject: string;
  status: string;
  dealId?: number | null;
  dealTitle?: string | null;
  userName: string;
  assignedAdminName?: string | null;
  createdAt: string;
  closedAt: string | null;
  messages: SupportTicketMessage[];
}

// One row of GET /api/v1/me/disputes. status is snake_case from the API:
// 'under_review' | 'awaiting_info' | 'resolved_buyer' | 'resolved_seller' | 'closed'.
export interface MyDisputeSummary {
  id: number;
  number: string;
  dealId: number;
  dealTitle: string;
  dealAmount: number;
  status: string;
  resolution: string;
  openedByName?: string | null;
  createdAt: string;
  resolvedAt: string | null;
}
