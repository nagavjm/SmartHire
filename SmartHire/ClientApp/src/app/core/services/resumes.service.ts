import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export type IndexingStatus = 'Pending' | 'Processing' | 'Indexed' | 'Failed';

export interface ResumeSummary {
  id: number;
  blobName: string;
  fileExtension: string;
  sizeInBytes: number;
  status: IndexingStatus;
  blobLastModifiedUtc: string;
  lastIndexedAtUtc?: string;
}

export interface ResumeDetail extends ResumeSummary {
  blobUrl: string;
  containerName: string;
  failureReason?: string;
  candidateName?: string;
}

@Injectable({ providedIn: 'root' })
export class ResumesService {
  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<ResumeSummary[]> {
    return this.http.get<ResumeSummary[]>('/api/resumes');
  }

  getById(id: number): Observable<ResumeDetail> {
    return this.http.get<ResumeDetail>(`/api/resumes/${id}`);
  }
}
