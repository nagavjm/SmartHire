import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export type IndexingJobStatus = 'Queued' | 'Running' | 'Completed' | 'CompletedWithErrors' | 'Failed';

export interface IndexingJob {
  id: number;
  status: IndexingJobStatus;
  documentsDiscovered: number;
  documentsIndexed: number;
  documentsFailed: number;
  startedAtUtc: string;
  completedAtUtc?: string;
  errorSummary?: string;
}

@Injectable({ providedIn: 'root' })
export class SyncService {
  constructor(private readonly http: HttpClient) {}

  start(): Observable<IndexingJob> {
    return this.http.post<IndexingJob>('/api/sync/start', {});
  }

  reindex(): Observable<IndexingJob> {
    return this.http.post<IndexingJob>('/api/sync/reindex', {});
  }

  getStatus(): Observable<IndexingJob> {
    return this.http.get<IndexingJob>('/api/sync/status');
  }

  getJobs(): Observable<IndexingJob[]> {
    return this.http.get<IndexingJob[]>('/api/sync/jobs');
  }
}
