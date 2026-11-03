import {
  ActivityCategory,
  ActivityEntry,
  AppNotification,
} from '../../core/models/activity.models';

function minutesAgo(minutes: number): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

type ActivitySeed = [
  category: ActivityCategory,
  message: string,
  actor: string,
  minutesAgo: number,
];

const ACTIVITY_SEED: ActivitySeed[] = [
  [
    'leave',
    'Leave request submitted by Phyu Sin Thant (Annual Leave, 3 days)',
    'Phyu Sin Thant',
    18,
  ],
  ['attendance', '42 employees checked in at Yangon Head Office', 'System', 45],
  ['employee', 'New employee Ingyin Thwe onboarded as Engineering Intern', 'Hnin Ei Phyu', 140],
  ['leave', 'Annual leave approved for Yamin Aye', 'Ei Mon Kyaw', 260],
  ['payroll', 'Payroll draft created for the current period', 'Khin Sandar', 380],
  [
    'recruitment',
    'Interview scheduled for Senior Backend Engineer candidate',
    'Nandar Hlaing',
    600,
  ],
  ['organization', 'Position "Engineering Intern" headcount updated to 3', 'Su Myat Noe', 1_100],
  ['employee', 'Employment status of Ko Ko Gyi changed to Suspended', 'Zaw Lin Oo', 1_900],
  ['system', 'Sample data environment initialised', 'System', 2_880],
];

/** SAMPLE DATA */
export const MOCK_ACTIVITIES: ActivityEntry[] = ACTIVITY_SEED.map(
  ([category, message, actor, ago], index) => ({
    id: index + 1,
    category,
    message,
    actor,
    timestamp: minutesAgo(ago),
  }),
);

/** SAMPLE DATA */
export const MOCK_NOTIFICATIONS: AppNotification[] = [
  {
    id: 1,
    title: 'Leave approvals pending',
    message: 'Several leave requests are waiting for your approval.',
    icon: 'event_busy',
    link: '/leave/approvals',
    timestamp: minutesAgo(20),
    read: false,
  },
  {
    id: 2,
    title: 'Payroll draft ready',
    message: 'The current period payroll draft is ready for review.',
    icon: 'payments',
    link: '/payroll/processing',
    timestamp: minutesAgo(380),
    read: false,
  },
  {
    id: 3,
    title: 'Probation reviews due',
    message: 'Probation periods end soon for recently joined employees.',
    icon: 'badge',
    link: '/employees',
    timestamp: minutesAgo(1_500),
    read: false,
  },
  {
    id: 4,
    title: 'Document expiry',
    message: 'Two employee work permits expire within 30 days.',
    icon: 'description',
    link: '/employees/documents',
    timestamp: minutesAgo(4_300),
    read: true,
  },
];
