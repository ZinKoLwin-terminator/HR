import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { ApplicationStage, InterviewRound } from '../../core/models/recruitment.models';
import { API_BASE_URL } from '../api-base-url';
import { mapHttpError } from '../http-error.mapper';

/** One card of the pipeline board. */
export interface PipelineCard {
  applicationId: number;
  jobOpeningId: number;
  jobTitle: string;
  candidateId: number;
  candidateName: string;
  experienceYears: number;
  stage: ApplicationStage;
  appliedOn: string;
  rejectionReason: string | null;
  nextInterview: { id: number; round: InterviewRound; date: string; startTime: string } | null;
  completedInterviews: number;
  rowVersion: string;
}

export interface PipelineColumn {
  stage: ApplicationStage;
  count: number;
  cards: PipelineCard[];
}

export interface PipelineView {
  jobOpeningId: number | null;
  /** Always the five stages, in pipeline order. */
  columns: PipelineColumn[];
}

/** Applications per opening (and how many are selected) and per candidate. Ids not listed have 0. */
export interface RecruitmentCounts {
  jobOpenings: { jobOpeningId: number; applications: number; selected: number }[];
  candidates: { candidateId: number; applications: number }[];
}

/**
 * Read-only recruitment views the API computes on the server: the pipeline board and the
 * application counts. They are not part of an abstract data contract (the screens still build the
 * board and the counts from the four lists), so this service is available for a screen change that
 * wants one request instead of four lists. It needs no `rowVersion`, and a missing id means 0.
 */
@Injectable({ providedIn: 'root' })
export class RecruitmentViewsHttpService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** The board for every opening, or for one opening. */
  pipeline(jobOpeningId?: number | null): Observable<PipelineView> {
    let params = new HttpParams();
    if (jobOpeningId !== null && jobOpeningId !== undefined) {
      params = params.set('jobOpeningId', jobOpeningId);
    }
    return this.http
      .get<PipelineView>(`${this.baseUrl}/recruitment/pipeline`, { params })
      .pipe(catchError((e) => throwError(() => mapHttpError(e))));
  }

  counts(): Observable<RecruitmentCounts> {
    return this.http.get<RecruitmentCounts>(`${this.baseUrl}/recruitment/counts`).pipe(
      map((counts) => ({
        jobOpenings: counts.jobOpenings ?? [],
        candidates: counts.candidates ?? [],
      })),
      catchError((e) => throwError(() => mapHttpError(e))),
    );
  }
}
