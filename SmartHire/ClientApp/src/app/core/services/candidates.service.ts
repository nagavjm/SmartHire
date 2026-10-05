import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface CandidateSummary {
  id: number;
  resumeDocumentId: number;
  candidateName: string;
  email?: string;
  phone?: string;
  location?: string;
  totalExperienceYears?: number;
}

export interface CandidateDetail extends CandidateSummary {
  skills?: string;
  resumeSummary?: string;
  blobUrl?: string;
}

@Injectable({ providedIn: 'root' })
export class CandidatesService {
  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<CandidateSummary[]> {
    return this.http.get<CandidateSummary[]>('/api/candidates');
  }

  getById(id: number): Observable<CandidateDetail> {
    return this.http.get<CandidateDetail>(`/api/candidates/${id}`);
  }
}
