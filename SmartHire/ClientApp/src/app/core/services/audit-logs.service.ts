import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AuditLog {
  id: number;
  userId: number | null;
  eventType: string;
  description: string | null;
  metadata: string | null;
  createdAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class AuditLogsService {
  constructor(private readonly http: HttpClient) {}

  getAll(take = 100): Observable<AuditLog[]> {
    return this.http.get<AuditLog[]>('/api/audit-logs', { params: { take } });
  }
}
