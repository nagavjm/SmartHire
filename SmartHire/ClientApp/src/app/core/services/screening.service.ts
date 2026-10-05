import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface ScreeningSearchRequest {
  jobTitle: string;
  jobDescription: string;
  requiredSkills: string;
  minimumExperienceYears: number;
  preferredExperienceYears?: number;
  location?: string;
}

export interface CandidateMatch {
  candidateProfileId?: number;
  resumeDocumentId: number;
  candidateName: string;
  matchScore: number;
  skillMatchScore?: number;
  experienceMatchScore?: number;
  strengths?: string[];
  missingSkills?: string[];
  recommendation?: string;
  resumeSummary?: string;
  rank: number;
}

export interface ScreeningOutcome {
  screeningRequestId: number;
  candidates: CandidateMatch[];
}

@Injectable({ providedIn: 'root' })
export class ScreeningService {
  constructor(private readonly http: HttpClient) {}

  search(request: ScreeningSearchRequest): Observable<ScreeningOutcome> {
    return this.http.post<ScreeningOutcome>('/api/screening/search', request);
  }

  rank(request: ScreeningSearchRequest): Observable<ScreeningOutcome> {
    return this.http.post<ScreeningOutcome>('/api/screening/rank', request);
  }
}
