import { SCORECARD_STATUSES } from '../models/kpi.models';
import {
  reviewVisibility,
  reviewVisibilityForOwner,
  showsHistoryComment,
} from './kpi-review-visibility';

describe('KPI review visibility (mirror of the API ReviewVisibility)', () => {
  it('shows everything to anyone who is not the scorecard’s employee', () => {
    for (const status of SCORECARD_STATUSES) {
      const all = { managerReview: true, hrComment: true, decisionComments: true };
      expect(reviewVisibility(status, 23, 5)).toEqual(all);
      expect(reviewVisibility(status, 23, null)).toEqual(all); // no linked employee
    }
  });

  it('shows the owner the review once Finalized or Rejected, the HR comment only when Finalized', () => {
    for (const status of SCORECARD_STATUSES) {
      const decided = status === 'Finalized' || status === 'Rejected';
      expect(reviewVisibility(status, 23, 23), status).toEqual({
        managerReview: decided,
        hrComment: status === 'Finalized',
        decisionComments: decided,
      });
    }
  });

  it('always shows Return and Reject reasons, other comments only once decided', () => {
    const approved = reviewVisibilityForOwner('Approved');
    expect(showsHistoryComment(approved, 'Return')).toBe(true);
    expect(showsHistoryComment(approved, 'Reject')).toBe(true);
    expect(showsHistoryComment(approved, 'Approve')).toBe(false);
    expect(showsHistoryComment(approved, 'Finalize')).toBe(false);
    expect(showsHistoryComment(reviewVisibilityForOwner('Finalized'), 'Approve')).toBe(true);
  });
});
