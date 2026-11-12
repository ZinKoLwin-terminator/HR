import { Observable } from 'rxjs';
import {
  Application,
  ApplicationFilter,
  ApplicationStage,
  Candidate,
  Interview,
  InterviewFilter,
  JobOpening,
  JobOpeningStatus,
} from '../../models/recruitment.models';
import { CrudDataService } from './crud-data.service';

export abstract class JobOpeningDataService extends CrudDataService<JobOpening> {
  /** Draft/On Hold → Open, Open → On Hold, anything but Filled → Closed, Closed → Open. */
  abstract changeStatus(id: number, status: JobOpeningStatus): Observable<JobOpening>;
}

export abstract class CandidateDataService extends CrudDataService<Candidate> {}

export abstract class ApplicationDataService extends CrudDataService<
  Application,
  Application,
  ApplicationFilter
> {
  /**
   * Moves an application along the pipeline; `reason` is required when rejecting.
   *
   * Selecting a candidate who matches the do-not-rehire list is a Conflict until an HR
   * Administrator approves it: resend with `blacklistOverrideReason` (at most 300 characters).
   * A caller who is not an HR Administrator gets Forbidden for an override.
   */
  abstract moveStage(
    id: number,
    stage: ApplicationStage,
    reason?: string,
    blacklistOverrideReason?: string,
  ): Observable<Application>;
}

export abstract class InterviewDataService extends CrudDataService<
  Interview,
  Interview,
  InterviewFilter
> {}
