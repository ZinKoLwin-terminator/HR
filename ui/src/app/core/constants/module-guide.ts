import { ATTENDANCE_GUIDE } from './attendance-guide';
import { EMPLOYEES_GUIDE } from './employees-guide';
import { LEAVE_GUIDE } from './leave-guide';
import { ORGANIZATION_GUIDE } from './organization-guide';
import { PAYROLL_GUIDE } from './payroll-guide';
import { RECRUITMENT_GUIDE } from './recruitment-guide';

/** A string in both supported guide languages. */
export interface Bilingual {
  en: string;
  my: string;
}

export interface GuideScreen {
  label: string;
  route: string;
  text: Bilingual;
}

/** A block of a long-form guide article shown under the rules of a module. */
export type GuideBlock =
  | { type: 'heading'; text: Bilingual }
  | { type: 'paragraph'; text: Bilingual }
  | { type: 'flow'; steps: readonly Bilingual[] }
  | { type: 'list'; items: readonly Bilingual[] }
  | { type: 'steps'; items: readonly Bilingual[] }
  | { type: 'code'; lines: readonly string[] }
  | { type: 'table'; head: readonly Bilingual[]; rows: readonly (readonly Bilingual[])[] }
  | { type: 'note'; text: Bilingual };

export interface GuideModule {
  id: string;
  name: string;
  icon: string;
  summary: Bilingual;
  screens: readonly GuideScreen[];
  rules: readonly Bilingual[];
  /** Optional long-form walkthrough (setup, calculation, examples). */
  article?: readonly GuideBlock[];
  /** Honest limitation shown to the reader, when there is one. */
  note?: Bilingual;
}

/** Content of the in-app Module Guide page. Routes must exist in the route config. */
export const MODULE_GUIDE: readonly GuideModule[] = [
  {
    id: 'dashboard',
    name: 'Dashboard',
    icon: 'space_dashboard',
    summary: {
      en: 'One-page view of the organization today: headcount, attendance, leave, payroll and recent activity.',
      my: 'ကုမ္ပဏီ၏ ယနေ့အခြေအနေကို တစ်မျက်နှာတည်းတွင် မြင်ရသည် — ဝန်ထမ်းအရေအတွက်၊ တက်ရောက်မှု၊ ခွင့်၊ လစာနှင့် လတ်တလောလှုပ်ရှားမှုများ။',
    },
    screens: [
      {
        label: 'Dashboard',
        route: '/dashboard',
        text: {
          en: 'KPI cards, attendance / headcount / department charts, pending leave requests, upcoming events, latest payroll summary and quick-access shortcuts.',
          my: 'KPI Card များ၊ တက်ရောက်မှု / ဝန်ထမ်းတိုးတက်မှု / ဌာနအလိုက် Chart များ၊ ခွင့်ခွင့်ပြုရန်စောင့်နေသောတောင်းဆိုမှုများ၊ လာမည့်ဖြစ်ရပ်များ၊ နောက်ဆုံးလစာအကျဉ်းချုပ်နှင့် Shortcut များ။',
        },
      },
    ],
    rules: [
      {
        en: 'Use “Download report” to export the current figures.',
        my: '“Download report” ဖြင့် လက်ရှိကိန်းဂဏန်းများကို ထုတ်ယူနိုင်သည်။',
      },
      {
        en: 'On a non-working day the attendance figures show the last working day.',
        my: 'အလုပ်မလုပ်သောနေ့တွင် နောက်ဆုံးအလုပ်လုပ်သည့်နေ့၏ တက်ရောက်မှုကို ပြသည်။',
      },
    ],
  },
  {
    id: 'organization',
    name: 'Organization',
    icon: 'corporate_fare',
    summary: {
      en: 'The foundation of the system. Define the company structure first; every other module builds on it.',
      my: 'စနစ်၏ အခြေခံ။ ကုမ္ပဏီဖွဲ့စည်းပုံကို အရင်ဆုံးသတ်မှတ်ပါ — အခြား Module အားလုံး ဤအချက်အလက်ကို အခြေခံသည်။',
    },
    screens: [
      {
        label: 'Companies',
        route: '/organization/companies',
        text: { en: 'Legal entities / companies.', my: 'ကုမ္ပဏီများ။' },
      },
      {
        label: 'Branches',
        route: '/organization/branches',
        text: { en: 'Offices under a company.', my: 'ကုမ္ပဏီအောက်ရှိ ရုံးခွဲများ။' },
      },
      {
        label: 'Departments',
        route: '/organization/departments',
        text: { en: 'Departments under a branch.', my: 'ဘဏ်ခွဲအောက်ရှိ ဌာနများ။' },
      },
      {
        label: 'Sections',
        route: '/organization/sections',
        text: { en: 'Teams inside a department.', my: 'ဌာနအတွင်းရှိ အဖွဲ့ငယ်များ။' },
      },
      {
        label: 'Positions',
        route: '/organization/positions',
        text: { en: 'Job titles.', my: 'ရာထူးအမည်များ။' },
      },
      {
        label: 'Job Grades',
        route: '/organization/job-grades',
        text: {
          en: 'Seniority levels with a salary band (minimum to maximum).',
          my: 'ရာထူးအဆင့်များနှင့် လစာအကွာအဝေး (အနည်းဆုံး–အများဆုံး)။',
        },
      },
      {
        label: 'Reporting Structure',
        route: '/organization/reporting-structure',
        text: { en: 'Who reports to whom.', my: 'ဘယ်သူက ဘယ်သူ့ကို Report လုပ်သည်ကို ကြည့်ရန်။' },
      },
    ],
    rules: [
      {
        en: 'Hierarchy is validated: a department must belong to a branch of its own company.',
        my: 'ဖွဲ့စည်းပုံကို စစ်ဆေးသည် — ဌာနသည် ကိုယ့်ကုမ္ပဏီ၏ ဘဏ်ခွဲအောက်တွင်သာ ရှိနိုင်သည်။',
      },
      {
        en: 'Records that are in use cannot be deleted. Deactivate them instead.',
        my: 'အသုံးပြုနေသော Record ကို ဖျက်၍မရပါ — Deactivate လုပ်ပါ။',
      },
      {
        en: 'A job grade’s salary band limits the basic salary in Payroll.',
        my: 'Job Grade ၏ လစာအကွာအဝေးသည် Payroll ရှိ Basic လစာကို ကန့်သတ်သည်။',
      },
    ],
    article: ORGANIZATION_GUIDE,
  },
  {
    id: 'employees',
    name: 'Employees',
    icon: 'groups',
    summary: {
      en: 'One profile per employee: personal, employment, documents, salary, attendance and leave in one place.',
      my: 'ဝန်ထမ်းတစ်ဦးလျှင် Profile တစ်ခု — ကိုယ်ရေး၊ အလုပ်အကိုင်၊ စာရွက်စာတမ်း၊ လစာ၊ တက်ရောက်မှုနှင့် ခွင့်ကို တစ်နေရာတည်းတွင် ကြည့်နိုင်သည်။',
    },
    screens: [
      {
        label: 'Employee List',
        route: '/employees/list',
        text: {
          en: 'Search, filter, sort and page through employees; export to Excel.',
          my: 'ရှာဖွေ၊ Filter၊ စီ၊ စာမျက်နှာခွဲကြည့်ရန်၊ Excel ထုတ်ရန်။',
        },
      },
      {
        label: 'Add Employee',
        route: '/employees/new',
        text: {
          en: 'Form with validation (phone, age, dates, duplicates).',
          my: 'ဖုန်း၊ အသက်၊ ရက်စွဲ၊ ထပ်နေခြင်း စစ်ဆေးမှုပါသော Form။',
        },
      },
      {
        label: 'Employee Documents',
        route: '/employees/documents',
        text: {
          en: 'Document library per employee.',
          my: 'ဝန်ထမ်းအလိုက် စာရွက်စာတမ်း စာကြည့်တိုက်။',
        },
      },
    ],
    rules: [
      {
        en: 'Open a profile to see tabs: overview, employment, history, documents, salary, attendance, leave.',
        my: 'Profile ကိုဖွင့်လျှင် Tab များ — Overview, Employment, History, Documents, Salary, Attendance, Leave။',
      },
      {
        en: 'Email and employee code must be unique.',
        my: 'Email နှင့် ဝန်ထမ်းကုဒ် မထပ်ရ။',
      },
      {
        en: 'Print a list, or print a profile or a letter (employment certificate, experience letter, leave approval letter) from the browser.',
        my: 'စာရင်း၊ Profile သို့မဟုတ် စာ (အလုပ်အကိုင်လက်မှတ်၊ အတွေ့အကြုံစာ၊ ခွင့်ခွင့်ပြုစာ) ကို Browser မှ ပရင့်ထုတ်နိုင်သည်။',
      },
    ],
    article: EMPLOYEES_GUIDE,
    note: {
      en: 'Only the file name of a document is stored in this version.',
      my: 'ဤ Version တွင် စာရွက်စာတမ်း၏ ဖိုင်အမည်သာ သိမ်းသည်။',
    },
  },
  {
    id: 'attendance',
    name: 'Attendance',
    icon: 'schedule',
    summary: {
      en: 'Track working time, shifts and overtime. Approved overtime feeds payroll.',
      my: 'အလုပ်ချိန်၊ Shift နှင့် အချိန်ပိုကို ခြေရာခံသည်။ ခွင့်ပြုပြီး OT သည် လစာတွက်ချက်မှုသို့ ဒေတာပေးသည်။',
    },
    screens: [
      {
        label: 'Dashboard',
        route: '/attendance/overview',
        text: {
          en: 'Attendance status today and by department.',
          my: 'ယနေ့ / ဌာနအလိုက် တက်ရောက်မှုအခြေအနေ။',
        },
      },
      {
        label: 'Check In / Out',
        route: '/attendance/check-in',
        text: { en: 'Clock in and out (simulated).', my: 'အလုပ်ဝင်/ထွက် မှတ်တမ်း (နမူနာ)။' },
      },
      {
        label: 'Attendance Records',
        route: '/attendance/records',
        text: { en: 'Daily records with status.', my: 'နေ့စဉ်မှတ်တမ်းနှင့် Status။' },
      },
      {
        label: 'Late & Early',
        route: '/attendance/exceptions',
        text: { en: 'Late arrivals and early departures.', my: 'နောက်ကျ / စောစောပြန် စာရင်း။' },
      },
      {
        label: 'Shift Management',
        route: '/attendance/shifts',
        text: { en: 'Define shifts and their rules.', my: 'Shift နှင့် စည်းမျဉ်းများ သတ်မှတ်ရန်။' },
      },
      {
        label: 'Holiday Calendar',
        route: '/attendance/holidays',
        text: {
          en: 'Public and company holidays that affect leave days and working days.',
          my: 'ခွင့်ရက်နှင့် အလုပ်ရက်ရေကို သက်ရောက်သော အများပြည်သူနှင့် ကုမ္ပဏီအားလပ်ရက်များ။',
        },
      },
      {
        label: 'Shift Assignment',
        route: '/attendance/shift-assignment',
        text: { en: 'Assign shifts to employees.', my: 'ဝန်ထမ်းများကို Shift ခွဲပေးရန်။' },
      },
      {
        label: 'Work Schedules',
        route: '/attendance/schedules',
        text: { en: 'Weekly schedule grid.', my: 'အပတ်စဉ် အလုပ်ချိန်ဇယား။' },
      },
      {
        label: 'Overtime',
        route: '/attendance/overtime',
        text: { en: 'Overtime requests and approval.', my: 'OT တောင်းခံမှုနှင့် ခွင့်ပြုချက်။' },
      },
      {
        label: 'Adjustments',
        route: '/attendance/adjustments',
        text: {
          en: 'Request a correction to a wrong record (approval required).',
          my: 'မှားယွင်းသောမှတ်တမ်းကို ပြင်ရန်တောင်းဆိုခြင်း (ခွင့်ပြုချက်လို)။',
        },
      },
    ],
    rules: [
      {
        en: 'Daily status is derived from the shift rules, holidays and approved leave.',
        my: 'နေ့စဉ် Status ကို Shift စည်းမျဉ်း၊ အားလပ်ရက်နှင့် ခွင့်ပြုပြီးခွင့်တို့မှ အလိုအလျောက်သတ်မှတ်သည်။',
      },
    ],
    article: ATTENDANCE_GUIDE,
  },
  {
    id: 'leave',
    name: 'Leave Management',
    icon: 'beach_access',
    summary: {
      en: 'Leave types, entitlement policies, balances, requests and approvals.',
      my: 'ခွင့်အမျိုးအစား၊ ခွင့်ရက် Policy၊ လက်ကျန်၊ တောင်းဆိုမှုနှင့် ခွင့်ပြုချက်များ။',
    },
    screens: [
      {
        label: 'Leave Types',
        route: '/leave/types',
        text: {
          en: 'Annual, sick, unpaid and other leave types.',
          my: 'နှစ်ပတ်လည်၊ နာမကျန်း၊ လစာမဲ့ စသည့်ခွင့်အမျိုးအစားများ။',
        },
      },
      {
        label: 'Leave Policies',
        route: '/leave/policies',
        text: {
          en: 'Entitlement by job grade, minimum service and monthly accrual.',
          my: 'Job Grade၊ အလုပ်သက်နှင့် လစဉ်စုဆောင်းမှုအလိုက် ခွင့်ရက်။',
        },
      },
      {
        label: 'Leave Balances',
        route: '/leave/balances',
        text: { en: 'Remaining days per employee.', my: 'ဝန်ထမ်းအလိုက် လက်ကျန်ခွင့်ရက်။' },
      },
      {
        label: 'Leave Applications',
        route: '/leave/applications',
        text: { en: 'Submit and track leave requests.', my: 'ခွင့်တင်ရန်နှင့် အခြေအနေကြည့်ရန်။' },
      },
      {
        label: 'Leave Approvals',
        route: '/leave/approvals',
        text: {
          en: 'Approve or reject pending requests.',
          my: 'စောင့်ဆိုင်းနေသောတောင်းဆိုမှုများကို ခွင့်ပြု/ပယ်ချရန်။',
        },
      },
      {
        label: 'Leave Calendar',
        route: '/leave/calendar',
        text: {
          en: 'Team calendar of who is away.',
          my: 'ဘယ်သူ ခွင့်ယူထားသည်ကို အဖွဲ့လိုက်ကြည့်ရန်။',
        },
      },
    ],
    rules: [
      {
        en: 'Balance = policy entitlement + accrual − approved leave.',
        my: 'လက်ကျန် = Policy ရက် + စုဆောင်းမှု − ခွင့်ပြုပြီးခွင့်။',
      },
      {
        en: 'Working days exclude weekends and public holidays.',
        my: 'အလုပ်ရက်ရေတွင် စနေ၊ တနင်္ဂနွေနှင့် အားလပ်ရက်များ မပါ။',
      },
    ],
    article: LEAVE_GUIDE,
  },
  {
    id: 'payroll',
    name: 'Payroll',
    icon: 'payments',
    summary: {
      en: 'Monthly pay calculated from salary setup, joining/exit dates, unpaid leave, approved overtime and holidays.',
      my: 'လစာ Setup၊ ဝင်/ထွက်ရက်၊ လစာမဲ့ခွင့်၊ ခွင့်ပြုပြီး OT နှင့် အားလပ်ရက်တို့မှ လစဉ်လစာ တွက်ချက်သည်။',
    },
    screens: [
      {
        label: 'Salary Components',
        route: '/payroll/salary-components',
        text: {
          en: 'Earnings and deductions: fixed, % of basic or % of gross.',
          my: 'ဝင်ငွေ/နုတ်ငွေ — ပုံသေ၊ Basic ၏ %၊ Gross ၏ %။',
        },
      },
      {
        label: 'Salary Structures',
        route: '/payroll/salary-structures',
        text: { en: 'Reusable sets of components.', my: 'Component များ၏ အစုအဝေး Template။' },
      },
      {
        label: 'Employee Salaries',
        route: '/payroll/employee-salaries',
        text: {
          en: 'Basic salary and structure from an effective date.',
          my: 'Effective Date အလိုက် Basic လစာနှင့် Structure။',
        },
      },
      {
        label: 'Payroll Processing',
        route: '/payroll/processing',
        text: {
          en: 'Create a month, then Process → Approve → Mark as paid.',
          my: 'လအလိုက် Period ဖန်တီး၍ Process → Approve → Mark as paid လုပ်ရန်။',
        },
      },
      {
        label: 'Payslips',
        route: '/payroll/payslips',
        text: {
          en: 'View and print payslips; print all payslips of a period from Payroll Review.',
          my: 'စလစ်ကြည့်ရန်၊ ပရင့်ထုတ်ရန်၊ Payroll Review မှ Period တစ်ခုလုံး၏ စလစ်အားလုံး ပရင့်ထုတ်ရန်။',
        },
      },
      {
        label: 'Payroll History',
        route: '/payroll/history',
        text: { en: 'Gross vs net per month.', my: 'လအလိုက် Gross နှင့် Net နှိုင်းယှဉ်။' },
      },
    ],
    rules: [
      {
        en: 'Set up components, structures and salaries once; then run Payroll Processing each month.',
        my: 'Component၊ Structure၊ လစာကို တစ်ကြိမ်သတ်မှတ်ပြီး လစဉ် Payroll Processing ကိုသာ လုပ်ပါ။',
      },
      {
        en: 'Earlier months must be finished first. Paid months are final; processed or approved months can be returned to draft.',
        my: 'အရင်လများ မပြီးမချင်း နောက်လမလုပ်နိုင်။ Paid ပြီးလျှင် အပြီးအပိုင်၊ Process/Approve အဆင့်ကို Draft သို့ပြန်နိုင်သည်။',
      },
      {
        en: 'Net pay = gross − deductions. Overtime is paid at 1.5× the hourly rate.',
        my: 'Net လစာ = Gross − Deductions။ OT ကို နာရီစားနှုန်း ၁.၅ ဆ ပေးသည်။',
      },
      {
        en: 'Payslips are snapshots: later setup changes do not alter them.',
        my: 'Payslip သည် Snapshot ဖြစ်၍ နောက်ပိုင်း Setup ပြောင်းလည်း မပြောင်းပါ။',
      },
    ],
    article: PAYROLL_GUIDE,
    note: {
      en: 'Figures are demonstration rules only. Tax, social security and bank payment are not included.',
      my: 'ကိန်းဂဏန်းများသည် Demo စည်းမျဉ်းသာဖြစ်သည်။ အခွန်၊ လူမှုဖူလုံရေးနှင့် ဘဏ်ငွေပေးချေမှု မပါဝင်ပါ။',
    },
  },
  {
    id: 'recruitment',
    name: 'Recruitment',
    icon: 'person_search',
    summary: {
      en: 'Track hiring from opening a vacancy to selecting a candidate.',
      my: 'ရာထူးလစ်လပ်ချက်ဖွင့်ခြင်းမှ ကိုယ်စားလှယ်လောင်းရွေးချယ်ခြင်းအထိ ခြေရာခံသည်။',
    },
    screens: [
      {
        label: 'Job Openings',
        route: '/recruitment/job-openings',
        text: {
          en: 'Draft, publish, hold, close and reopen vacancies.',
          my: 'Draft၊ Publish၊ Hold၊ Close၊ Reopen။',
        },
      },
      {
        label: 'Candidates',
        route: '/recruitment/candidates',
        text: { en: 'Applicant database (unique email).', my: 'လျှောက်ထားသူ ဒေတာ (Email မထပ်ရ)။' },
      },
      {
        label: 'Interviews',
        route: '/recruitment/interviews',
        text: {
          en: 'Schedule interviews; record score (1–5) and recommendation.',
          my: 'အင်တာဗျူးချိန်းရန်၊ ရမှတ် (၁–၅) နှင့် အကြံပြုချက်မှတ်ရန်။',
        },
      },
      {
        label: 'Pipeline',
        route: '/recruitment/pipeline',
        text: {
          en: 'Board: Applied → Screening → Interview → Selected / Rejected.',
          my: 'Board — Applied → Screening → Interview → Selected / Rejected။',
        },
      },
    ],
    rules: [
      {
        en: 'Rejecting needs a reason. Selecting needs a completed interview with a Hire recommendation and a free vacancy.',
        my: 'ပယ်ချရန် အကြောင်းပြချက်လို။ Selected ရန် Hire အကြံပြုချက်ပါသော အင်တာဗျူးပြီးခြင်းနှင့် လစ်လပ်နေသောနေရာ လို။',
      },
      {
        en: 'An opening becomes Filled automatically when enough candidates are selected.',
        my: 'လူလုံလောက်စွာ ရွေးပြီးလျှင် Opening သည် Filled ဖြစ်သွားသည်။',
      },
    ],
    article: RECRUITMENT_GUIDE,
  },
  {
    id: 'data-exchange',
    name: 'Data Exchange',
    icon: 'swap_vert',
    summary: {
      en: 'Import existing Excel data and export lists to Excel.',
      my: 'ရှိပြီးသား Excel ဒေတာကို ထည့်သွင်းရန်နှင့် စာရင်းများကို Excel ထုတ်ရန်။',
    },
    screens: [
      {
        label: 'Import Data',
        route: '/data-exchange/import',
        text: {
          en: 'Wizard: template → upload → columns → review → confirm → result.',
          my: 'အဆင့်ဆင့် — Template → Upload → Column → Review → Confirm → Result။',
        },
      },
      {
        label: 'Import History',
        route: '/data-exchange/history',
        text: {
          en: 'Past imports with problem details and a downloadable report.',
          my: 'ယခင် Import များ၊ အမှားအသေးစိတ်နှင့် Report ဒေါင်းလုဒ်။',
        },
      },
    ],
    rules: [
      {
        en: 'Files are validated before anything is saved; wrong headers and unsafe formulas are rejected.',
        my: 'မသိမ်းမီ ဖိုင်ကို စစ်ဆေးသည် — Header မှားခြင်း၊ အန္တရာယ်ရှိသော Formula များကို ပယ်သည်။',
      },
    ],
    note: {
      en: 'Import is available for Employees only so far.',
      my: 'လက်ရှိ Employees အတွက်သာ Import ရနိုင်သည်။',
    },
  },
  {
    id: 'settings',
    name: 'Settings',
    icon: 'settings',
    summary: {
      en: 'Configure the system and decide who can do what.',
      my: 'စနစ်ကို ပြင်ဆင်ပြီး ဘယ်သူ ဘာလုပ်ခွင့်ရှိသည်ကို သတ်မှတ်သည်။',
    },
    screens: [
      {
        label: 'General Settings',
        route: '/settings/general',
        text: {
          en: 'Organization name, time zone, date format, currency, fiscal year, notifications.',
          my: 'ကုမ္ပဏီအမည်၊ Time Zone၊ ရက်စွဲပုံစံ၊ ငွေကြေး၊ ဘဏ္ဍာနှစ်၊ Notification။',
        },
      },
      {
        label: 'Users',
        route: '/settings/users',
        text: {
          en: 'Accounts linked to an employee: Active, Inactive or Locked.',
          my: 'ဝန်ထမ်းနှင့်ချိတ်ထားသော Account — Active / Inactive / Locked။',
        },
      },
      {
        label: 'Roles',
        route: '/settings/roles',
        text: { en: 'Named sets of responsibilities.', my: 'အခန်းကဏ္ဍများ။' },
      },
      {
        label: 'Permissions',
        route: '/settings/permissions',
        text: {
          en: 'Module × action matrix for each role.',
          my: 'Role အလိုက် Module × Action Matrix။',
        },
      },
    ],
    rules: [
      {
        en: 'You cannot delete or demote your own account. Roles that have users cannot be deleted.',
        my: 'ကိုယ့် Account ကိုယ် ဖျက်/အဆင့်လျှော့၍မရ။ အသုံးပြုသူရှိသော Role ကို ဖျက်၍မရ။',
      },
    ],
    note: {
      en: 'In the demo build roles and permissions are simulated. With the API the server enforces them, and the screens hide what your role cannot use.',
      my: 'Demo Build တွင် Role နှင့် Permission သည် နမူနာသာဖြစ်သည်။ API ဖြင့်သုံးလျှင် Server က အမှန်တကယ်ကန့်သတ်ပြီး သင့် Role မသုံးနိုင်သည့်အရာများကို Screen များက ဖျောက်ထားသည်။',
    },
  },
];
