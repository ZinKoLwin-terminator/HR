import { Bilingual } from './module-guide';

export interface MapNode {
  id: string;
  label: string;
  icon: string;
  /** Top-left corner in the 960 × 520 diagram. */
  x: number;
  y: number;
  /** Screen opened by the "Open" link. */
  route?: string;
  summary: Bilingual;
}

export interface MapEdge {
  from: string;
  to: string;
  /** What flows along the arrow. */
  label: Bilingual;
  /** A step that people must do by hand (no automatic link). */
  manual?: boolean;
}

export interface WorkflowStep {
  /** Module name shown as a tag. */
  module: string;
  title: Bilingual;
  text: Bilingual;
  route?: string;
}

export interface Workflow {
  id: string;
  title: Bilingual;
  summary: Bilingual;
  steps: readonly WorkflowStep[];
}

export const MAP_NODES: readonly MapNode[] = [
  {
    id: 'settings',
    label: 'Settings',
    icon: 'settings',
    x: 20,
    y: 30,
    route: '/settings/users',
    summary: {
      en: 'User accounts, roles and permissions.',
      my: 'User Account၊ Role နှင့် Permission များ။',
    },
  },
  {
    id: 'recruitment',
    label: 'Recruitment',
    icon: 'person_search',
    x: 250,
    y: 30,
    route: '/recruitment/pipeline',
    summary: {
      en: 'Finds and selects the person to hire.',
      my: 'ခန့်မည့်သူကို ရှာဖွေရွေးချယ်သည်။',
    },
  },
  {
    id: 'organization',
    label: 'Organization',
    icon: 'corporate_fare',
    x: 20,
    y: 230,
    route: '/organization/companies',
    summary: {
      en: 'Company structure and job grades. The foundation of everything else.',
      my: 'ကုမ္ပဏီဖွဲ့စည်းပုံနှင့် Job Grade။ အခြားအရာအားလုံး၏ အခြေခံ။',
    },
  },
  {
    id: 'employees',
    label: 'Employees',
    icon: 'groups',
    x: 250,
    y: 230,
    route: '/employees/list',
    summary: {
      en: 'The employee profile that the other modules refer to.',
      my: 'အခြား Module များ ကိုးကားသော ဝန်ထမ်း Profile။',
    },
  },
  {
    id: 'dataexchange',
    label: 'Data Exchange',
    icon: 'swap_vert',
    x: 20,
    y: 430,
    route: '/data-exchange/import',
    summary: {
      en: 'Bulk-load employees from Excel.',
      my: 'Excel မှ ဝန်ထမ်းများကို အစုလိုက်ထည့်သည်။',
    },
  },
  {
    id: 'attendance',
    label: 'Attendance',
    icon: 'schedule',
    x: 520,
    y: 30,
    route: '/attendance/overview',
    summary: {
      en: 'Shifts, daily records and overtime requests.',
      my: 'Shift၊ နေ့စဉ်မှတ်တမ်းနှင့် OT တောင်းခံမှု။',
    },
  },
  {
    id: 'leave',
    label: 'Leave',
    icon: 'beach_access',
    x: 520,
    y: 230,
    route: '/leave/applications',
    summary: {
      en: 'Entitlement, applications and approvals.',
      my: 'ခွင့်ရက်၊ ခွင့်တင်ခြင်းနှင့် ခွင့်ပြုချက်။',
    },
  },
  {
    id: 'holidays',
    label: 'Holiday calendar',
    icon: 'event',
    x: 760,
    y: 130,
    summary: {
      en: 'Public and company holidays used when counting working days.',
      my: 'အလုပ်ရက်ရေတွက်ရာတွင် သုံးသော အများပြည်သူနှင့် ကုမ္ပဏီအားလပ်ရက်များ။',
    },
  },
  {
    id: 'salary',
    label: 'Salary setup',
    icon: 'request_quote',
    x: 250,
    y: 430,
    route: '/payroll/employee-salaries',
    summary: {
      en: 'Components, structures and each employee’s basic salary.',
      my: 'Component၊ Structure နှင့် ဝန်ထမ်းတစ်ဦးစီ၏ Basic လစာ။',
    },
  },
  {
    id: 'payroll',
    label: 'Payroll',
    icon: 'payments',
    x: 760,
    y: 360,
    route: '/payroll/processing',
    summary: {
      en: 'Monthly processing, payslips and history.',
      my: 'လစဉ်တွက်ချက်ခြင်း၊ Payslip နှင့် History။',
    },
  },
];

export const MAP_EDGES: readonly MapEdge[] = [
  {
    from: 'organization',
    to: 'employees',
    label: {
      en: 'Company, branch, department, position and grade chosen on the profile',
      my: 'Profile တွင် ရွေးရသော ကုမ္ပဏီ၊ ဘဏ်ခွဲ၊ ဌာန၊ ရာထူးနှင့် Grade',
    },
  },
  {
    from: 'organization',
    to: 'leave',
    label: {
      en: 'The job grade decides which leave policy applies',
      my: 'Job Grade အလိုက် ခွင့် Policy ကို ရွေးသည်',
    },
  },
  {
    from: 'organization',
    to: 'salary',
    label: {
      en: 'The grade’s salary band limits the basic salary',
      my: 'Grade ၏ လစာအကွာအဝေးသည် Basic လစာကို ကန့်သတ်သည်',
    },
  },
  {
    from: 'recruitment',
    to: 'employees',
    manual: true,
    label: {
      en: 'Manual step: after “Selected”, HR registers the person as an employee',
      my: 'လက်ဖြင့်လုပ်ရသည် — “Selected” ဖြစ်ပြီးနောက် HR က ဝန်ထမ်းအဖြစ် စာရင်းသွင်းရသည်',
    },
  },
  {
    from: 'dataexchange',
    to: 'employees',
    label: {
      en: 'Employees imported from Excel after validation',
      my: 'စစ်ဆေးပြီး Excel မှ ကူးထည့်သော ဝန်ထမ်းများ',
    },
  },
  {
    from: 'employees',
    to: 'settings',
    label: {
      en: 'A user account is linked to one employee',
      my: 'User Account တစ်ခုသည် ဝန်ထမ်းတစ်ဦးနှင့် ချိတ်ထားသည်',
    },
  },
  {
    from: 'employees',
    to: 'attendance',
    label: {
      en: 'Shift assignment and daily records belong to the employee',
      my: 'Shift ခွဲပေးမှုနှင့် နေ့စဉ်မှတ်တမ်းများသည် ဝန်ထမ်းနှင့် ပိုင်ဆိုင်သည်',
    },
  },
  {
    from: 'employees',
    to: 'leave',
    label: {
      en: 'Balances and applications belong to the employee',
      my: 'ခွင့်လက်ကျန်နှင့် ခွင့်လျှောက်လွှာများသည် ဝန်ထမ်းနှင့် ပိုင်ဆိုင်သည်',
    },
  },
  {
    from: 'employees',
    to: 'salary',
    label: {
      en: 'Each employee has a salary assignment (set separately)',
      my: 'ဝန်ထမ်းတစ်ဦးစီတွင် လစာသတ်မှတ်ချက်ရှိသည် (သီးခြားသတ်မှတ်ရသည်)',
    },
  },
  {
    from: 'employees',
    to: 'payroll',
    label: {
      en: 'Joining and exit dates decide proration for the month',
      my: 'ဝင်/ထွက်ရက်များက လအတွက် အချိုးကျတွက်ချက်မှုကို ဆုံးဖြတ်သည်',
    },
  },
  {
    from: 'leave',
    to: 'attendance',
    label: {
      en: 'Approved leave turns past attendance records into “On Leave”',
      my: 'ခွင့်ပြုပြီးခွင့်သည် ယခင်တက်ရောက်မှုမှတ်တမ်းများကို “On Leave” ပြောင်းသည်',
    },
  },
  {
    from: 'leave',
    to: 'payroll',
    label: {
      en: 'Approved unpaid leave becomes a deduction',
      my: 'ခွင့်ပြုပြီး လစာမဲ့ခွင့်သည် နုတ်ငွေဖြစ်လာသည်',
    },
  },
  {
    from: 'attendance',
    to: 'payroll',
    label: {
      en: 'Only approved overtime requests are paid (1.5×). Daily records are not read.',
      my: 'ခွင့်ပြုပြီး OT တောင်းခံမှုများကိုသာ ပေးသည် (၁.၅ ဆ)။ နေ့စဉ်မှတ်တမ်းကို မဖတ်ပါ။',
    },
  },
  {
    from: 'salary',
    to: 'payroll',
    label: {
      en: 'Basic salary, structure and components',
      my: 'Basic လစာ၊ Structure နှင့် Component များ',
    },
  },
  {
    from: 'holidays',
    to: 'leave',
    label: {
      en: 'Holidays are not counted as leave days',
      my: 'အားလပ်ရက်များကို ခွင့်ရက်အဖြစ် မရေတွက်ပါ',
    },
  },
  {
    from: 'holidays',
    to: 'attendance',
    label: {
      en: 'Holiday punches are shown as Present with a remark',
      my: 'အားလပ်ရက်တွင် Punch လုပ်လျှင် Present နှင့် မှတ်ချက်ပြသည်',
    },
  },
  {
    from: 'holidays',
    to: 'payroll',
    label: {
      en: 'Working days in the month exclude holidays',
      my: 'လအတွင်း အလုပ်ရက်များတွင် အားလပ်ရက်မပါ',
    },
  },
];

export const WORKFLOWS: readonly Workflow[] = [
  {
    id: 'setup',
    title: { en: '1. First-time setup', my: '၁။ ပထမဆုံး Setup' },
    summary: {
      en: 'Do this once, in this order, before adding people.',
      my: 'ဝန်ထမ်းမထည့်မီ ဤအစဉ်အတိုင်း တစ်ကြိမ်လုပ်ပါ။',
    },
    steps: [
      {
        module: 'Organization',
        title: { en: 'Build the structure', my: 'ဖွဲ့စည်းပုံ တည်ဆောက်ပါ' },
        text: {
          en: 'Company → branch → department → section → positions.',
          my: 'Company → Branch → Department → Section → Position။',
        },
        route: '/organization/companies',
      },
      {
        module: 'Organization',
        title: { en: 'Define job grades', my: 'Job Grade သတ်မှတ်ပါ' },
        text: {
          en: 'Each grade has a salary band. Leave policies and salaries use it later.',
          my: 'Grade တိုင်းတွင် လစာအကွာအဝေးရှိသည်။ နောက်ပိုင်း ခွင့် Policy နှင့် လစာတွင် သုံးသည်။',
        },
        route: '/organization/job-grades',
      },
      {
        module: 'Leave',
        title: {
          en: 'Create leave types and policies',
          my: 'ခွင့်အမျိုးအစားနှင့် Policy ဖန်တီးပါ',
        },
        text: {
          en: 'Policies are chosen by job grade, minimum service and monthly accrual.',
          my: 'Policy ကို Job Grade၊ အလုပ်သက်နှင့် လစဉ်စုဆောင်းမှုအလိုက် ရွေးသည်။',
        },
        route: '/leave/policies',
      },
      {
        module: 'Attendance',
        title: { en: 'Define shifts', my: 'Shift များ သတ်မှတ်ပါ' },
        text: {
          en: 'Working hours and rules that decide Present, Late and overtime.',
          my: 'Present၊ Late နှင့် OT ကို ဆုံးဖြတ်သော အလုပ်ချိန်နှင့် စည်းမျဉ်းများ။',
        },
        route: '/attendance/shifts',
      },
      {
        module: 'Payroll',
        title: {
          en: 'Create salary components and structures',
          my: 'Salary Component နှင့် Structure ဖန်တီးပါ',
        },
        text: {
          en: 'Earnings and deductions, grouped into reusable structures.',
          my: 'ဝင်ငွေ/နုတ်ငွေများကို ပြန်သုံးနိုင်သော Structure အဖြစ် စုစည်းပါ။',
        },
        route: '/payroll/salary-components',
      },
      {
        module: 'Settings',
        title: { en: 'Create users and roles', my: 'User နှင့် Role ဖန်တီးပါ' },
        text: {
          en: 'Give people accounts. In the demo build permissions are simulated; with the API the server enforces them.',
          my: 'လူများအတွက် Account ပေးပါ။ Demo Build တွင် Permission သည် နမူနာသာ၊ API ဖြင့်သုံးလျှင် Server က ကန့်သတ်သည်။',
        },
        route: '/settings/users',
      },
    ],
  },
  {
    id: 'hire',
    title: { en: '2. Hire to first payday', my: '၂။ ခန့်အပ်ခြင်းမှ ပထမလစာအထိ' },
    summary: {
      en: 'From an open vacancy to a person who can be paid. Steps 6–8 are done by hand.',
      my: 'လစ်လပ်ချက်ဖွင့်ခြင်းမှ လစာပေးနိုင်သည့်အထိ။ အဆင့် ၆–၈ ကို လက်ဖြင့်လုပ်ရသည်။',
    },
    steps: [
      {
        module: 'Recruitment',
        title: { en: 'Create and publish a job opening', my: 'Job Opening ဖန်တီး၍ Publish လုပ်ပါ' },
        text: {
          en: 'Starts as Draft. Only Open openings accept applications.',
          my: 'Draft ဖြင့်စသည်။ Open ဖြစ်သော Opening တွင်သာ လျှောက်လွှာလက်ခံသည်။',
        },
        route: '/recruitment/job-openings',
      },
      {
        module: 'Recruitment',
        title: { en: 'Add candidates and screen them', my: 'Candidate ထည့်၍ Screening လုပ်ပါ' },
        text: {
          en: 'One application per candidate per opening. Rejecting needs a reason.',
          my: 'Candidate တစ်ဦးလျှင် Opening တစ်ခုအတွက် လျှောက်လွှာတစ်ခု။ ပယ်ချလျှင် အကြောင်းပြချက်လို။',
        },
        route: '/recruitment/candidates',
      },
      {
        module: 'Recruitment',
        title: {
          en: 'Schedule and complete the interview',
          my: 'အင်တာဗျူးချိန်း၍ ပြီးဆုံးအောင်လုပ်ပါ',
        },
        text: {
          en: 'An interviewer cannot be double-booked. Completing needs a score (1–5) and a recommendation.',
          my: 'Interviewer ကို နှစ်နေရာမချိန်းနိုင်။ ပြီးဆုံးရန် ရမှတ် (၁–၅) နှင့် အကြံပြုချက်လို။',
        },
        route: '/recruitment/interviews',
      },
      {
        module: 'Recruitment',
        title: { en: 'Select the candidate', my: 'Candidate ကို ရွေးချယ်ပါ' },
        text: {
          en: 'Needs a completed interview with “Hire” or “Strong hire” and a free vacancy.',
          my: '“Hire” သို့ “Strong hire” ပါသော အင်တာဗျူးပြီးခြင်းနှင့် လစ်လပ်နေသောနေရာ လို။',
        },
        route: '/recruitment/pipeline',
      },
      {
        module: 'Employees',
        title: {
          en: 'Register the employee (manual)',
          my: 'ဝန်ထမ်းအဖြစ် စာရင်းသွင်းပါ (လက်ဖြင့်)',
        },
        text: {
          en: 'Recruitment does not create the employee automatically. HR adds the profile (status Probation).',
          my: 'Recruitment က ဝန်ထမ်းကို အလိုအလျောက်မဖန်တီးပါ။ HR က Profile ထည့်ရသည် (Status Probation)။',
        },
        route: '/employees/new',
      },
      {
        module: 'Payroll',
        title: { en: 'Assign a salary (manual)', my: 'လစာသတ်မှတ်ပါ (လက်ဖြင့်)' },
        text: {
          en: 'Basic salary must stay inside the job grade’s band. Without it the employee is skipped in payroll.',
          my: 'Basic လစာသည် Job Grade ၏ အကွာအဝေးအတွင်း ရှိရမည်။ မသတ်မှတ်လျှင် Payroll တွင် ကျော်သွားမည်။',
        },
        route: '/payroll/employee-salaries',
      },
      {
        module: 'Attendance',
        title: { en: 'Assign a shift (manual)', my: 'Shift ခွဲပေးပါ (လက်ဖြင့်)' },
        text: {
          en: 'So daily records and the weekly schedule work for the new employee.',
          my: 'ဝန်ထမ်းအသစ်အတွက် နေ့စဉ်မှတ်တမ်းနှင့် အပတ်စဉ်ဇယား အလုပ်လုပ်စေရန်။',
        },
        route: '/attendance/shift-assignment',
      },
      {
        module: 'Payroll',
        title: { en: 'Included in the next payroll', my: 'နောက်လစာတွက်ချက်မှုတွင် ပါဝင်မည်' },
        text: {
          en: 'The first month is prorated by calendar days from the joining date.',
          my: 'ပထမလကို ဝင်ရက်မှစ၍ ပြက္ခဒိန်ရက်အလိုက် အချိုးကျတွက်ချက်သည်။',
        },
        route: '/payroll/processing',
      },
    ],
  },
  {
    id: 'leave',
    title: { en: '3. A leave request', my: '၃။ ခွင့်တောင်းဆိုမှု' },
    summary: {
      en: 'What happens from applying to the effect on attendance and pay.',
      my: 'ခွင့်တင်ခြင်းမှ တက်ရောက်မှုနှင့် လစာအပေါ် သက်ရောက်မှုအထိ။',
    },
    steps: [
      {
        module: 'Leave',
        title: { en: 'Apply for leave', my: 'ခွင့်လျှောက်ပါ' },
        text: {
          en: 'Days count working days only (no weekends or holidays), or 0.5 for a half day. Overlaps and ineligible leave types are refused.',
          my: 'အလုပ်ရက်သာ ရေတွက်သည် (စနေ၊ တနင်္ဂနွေ၊ အားလပ်ရက်မပါ)၊ တစ်ဝက်ခွင့်ဆိုလျှင် ၀.၅။ ထပ်နေခြင်းနှင့် အရည်အချင်းမပြည့်သော ခွင့်ကို ပယ်သည်။',
        },
        route: '/leave/applications',
      },
      {
        module: 'Leave',
        title: { en: 'Submit', my: 'Submit လုပ်ပါ' },
        text: {
          en: 'Checked against the balance; a required document must be attached.',
          my: 'လက်ကျန်နှင့် တိုက်စစ်သည်၊ လိုအပ်သော စာရွက်တွဲရမည်။',
        },
        route: '/leave/applications',
      },
      {
        module: 'Leave',
        title: { en: 'Approve or reject', my: 'ခွင့်ပြု သို့ ပယ်ချပါ' },
        text: {
          en: 'Handled in the approvals queue by someone other than the applicant.',
          my: 'လျှောက်ထားသူမဟုတ်သူက Approvals Queue တွင် ဆုံးဖြတ်သည်။',
        },
        route: '/leave/approvals',
      },
      {
        module: 'Attendance',
        title: { en: 'Attendance is updated', my: 'တက်ရောက်မှု ပြောင်းလဲသည်' },
        text: {
          en: 'Past working days in the range become “On Leave”. Future days get no record yet.',
          my: 'အပိုင်းအခြားအတွင်း ကျော်လွန်ပြီးသော အလုပ်ရက်များသည် “On Leave” ဖြစ်သွားသည်။ အနာဂတ်ရက်များတွင် မှတ်တမ်းမရှိသေးပါ။',
        },
        route: '/attendance/records',
      },
      {
        module: 'Payroll',
        title: { en: 'Unpaid leave reduces pay', my: 'လစာမဲ့ခွင့်က လစာကို လျှော့စေသည်' },
        text: {
          en: 'At processing, approved unpaid days are deducted at the daily rate.',
          my: 'Process လုပ်ချိန်တွင် ခွင့်ပြုပြီး လစာမဲ့ရက်များကို နေ့ပျမ်းမျှလစာဖြင့် နုတ်သည်။',
        },
        route: '/payroll/processing',
      },
    ],
  },
  {
    id: 'month',
    title: { en: '4. The monthly cycle', my: '၄။ လစဉ်လုပ်ငန်းစဉ်' },
    summary: {
      en: 'What HR does every month, from daily records to the payslip.',
      my: 'နေ့စဉ်မှတ်တမ်းမှ Payslip အထိ HR လစဉ်လုပ်ရသည်များ။',
    },
    steps: [
      {
        module: 'Attendance',
        title: { en: 'Daily records build up', my: 'နေ့စဉ်မှတ်တမ်းများ စုဆောင်းသည်' },
        text: {
          en: 'Check in/out creates the day’s record from the shift rules. Fix mistakes through an approved adjustment.',
          my: 'Check in/out က Shift စည်းမျဉ်းအလိုက် မှတ်တမ်းဖန်တီးသည်။ အမှားများကို ခွင့်ပြုချက်ပါ Adjustment ဖြင့် ပြင်ပါ။',
        },
        route: '/attendance/records',
      },
      {
        module: 'Attendance',
        title: { en: 'Approve overtime', my: 'OT ကို ခွင့်ပြုပါ' },
        text: {
          en: 'Extra hours are paid only if an overtime request is approved.',
          my: 'အချိန်ပို ခွင့်ပြုထားမှသာ ပိုက်ဆံပေးသည်။',
        },
        route: '/attendance/overtime',
      },
      {
        module: 'Leave',
        title: { en: 'Approve pending leave', my: 'စောင့်ဆိုင်းနေသော ခွင့်များကို ဆုံးဖြတ်ပါ' },
        text: {
          en: 'Do this before processing: later approvals do not change payslips already made.',
          my: 'Process မလုပ်မီ လုပ်ပါ — နောက်မှ ခွင့်ပြုလျှင် ထုတ်ပြီး Payslip မပြောင်းပါ။',
        },
        route: '/leave/approvals',
      },
      {
        module: 'Payroll',
        title: { en: 'Create the month’s period', my: 'လအတွက် Period ဖန်တီးပါ' },
        text: {
          en: 'One per month, not in the future, and earlier months must be finished first.',
          my: 'တစ်လလျှင် တစ်ခု၊ အနာဂတ်မဖြစ်ရ၊ အရင်လများ မပြီးမချင်း မလုပ်နိုင်။',
        },
        route: '/payroll/processing',
      },
      {
        module: 'Payroll',
        title: { en: 'Process', my: 'Process လုပ်ပါ' },
        text: {
          en: 'Uses salary, joining/exit dates, unpaid leave, approved overtime and holidays. Employees without a salary are skipped with a warning.',
          my: 'လစာ၊ ဝင်/ထွက်ရက်၊ လစာမဲ့ခွင့်၊ ခွင့်ပြုပြီး OT နှင့် အားလပ်ရက်ကို သုံးသည်။ လစာမရှိသူကို သတိပေးချက်နှင့် ကျော်သည်။',
        },
        route: '/payroll/processing',
      },
      {
        module: 'Payroll',
        title: { en: 'Review and approve', my: 'စစ်ဆေး၍ Approve လုပ်ပါ' },
        text: {
          en: 'Check each employee’s gross, deductions and net. Something wrong? Return the period to draft and process again.',
          my: 'ဝန်ထမ်းတစ်ဦးစီ၏ Gross၊ Deduction၊ Net ကို စစ်ပါ။ မှားလျှင် Draft သို့ပြန်ပြီး ပြန်တွက်ပါ။',
        },
        route: '/payroll/processing',
      },
      {
        module: 'Payroll',
        title: { en: 'Mark as paid', my: 'Mark as paid လုပ်ပါ' },
        text: {
          en: 'Records the status only; no money moves. After this the period is final.',
          my: 'Status ကိုသာ မှတ်သည်၊ ငွေမလွှဲပါ။ ထို့နောက် Period သည် အပြီးအပိုင်။',
        },
        route: '/payroll/processing',
      },
      {
        module: 'Payroll',
        title: { en: 'Payslips and history', my: 'Payslip နှင့် History' },
        text: {
          en: 'Payslips are snapshots that can be viewed and printed. History compares gross and net by month.',
          my: 'Payslip သည် Snapshot ဖြစ်ပြီး ကြည့်/ပရင့်ထုတ်နိုင်သည်။ History တွင် လအလိုက် Gross/Net နှိုင်းယှဉ်နိုင်သည်။',
        },
        route: '/payroll/payslips',
      },
    ],
  },
];
