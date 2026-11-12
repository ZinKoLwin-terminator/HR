import { Bilingual } from './module-guide';

const b = (en: string, my: string): Bilingual => ({ en, my });

/**
 * Detailed “what can I do here” bullets for every sidebar screen, keyed by route.
 * Shown under each screen in the Module Guide. Rules mirror what the mock services enforce.
 */
export const SCREEN_DETAILS: Readonly<Record<string, readonly Bilingual[]>> = {
  '/dashboard': [
    b(
      'KPI cards: total employees, present / late today, on leave today, absent today and new joiners in the last 30 days.',
      'KPI Card များ — ဝန်ထမ်းစုစုပေါင်း၊ ယနေ့ရောက်သူ/နောက်ကျသူ၊ ခွင့်ယူသူ၊ ပျက်ကွက်သူ၊ ၃၀ ရက်အတွင်း အသစ်ဝင်သူ။',
    ),
    b(
      'Charts: attendance today, employee growth over 12 months, department split; lists of pending leave and upcoming events.',
      'Chart များ — ယနေ့တက်ရောက်မှု၊ ၁၂ လ ဝန်ထမ်းတိုးတက်မှု၊ ဌာနအလိုက်ခွဲခြမ်း၊ ခွင့်စောင့်ဆိုင်းမှုနှင့် လာမည့်ဖြစ်ရပ်များ။',
    ),
    b(
      'Buttons: Refresh, Download report, Add employee and quick-access shortcuts to common tasks.',
      'ခလုတ်များ — Refresh၊ Download report၊ Add employee နှင့် မကြာခဏလုပ်သော အလုပ်များသို့ Shortcut။',
    ),
  ],

  '/organization/companies': [
    b(
      'Add, edit and deactivate the companies (legal entities) in the group. Company name and code must be unique.',
      'အုပ်စုအတွင်းရှိ ကုမ္ပဏီများ (Legal Entity) ကို ထည့်/ပြင်/Deactivate လုပ်သည်။ ကုမ္ပဏီအမည်နှင့် ကုဒ် မထပ်ရ။',
    ),
    b(
      'A company that branches or employees refer to cannot be deleted; deactivate it instead.',
      'ဘဏ်ခွဲ သို့ ဝန်ထမ်းများ ကိုးကားထားသော ကုမ္ပဏီကို ဖျက်၍မရ၊ Deactivate လုပ်ပါ။',
    ),
  ],
  '/organization/branches': [
    b(
      'Manage offices under a company. Every branch belongs to exactly one company.',
      'ကုမ္ပဏီအောက်ရှိ ရုံးခွဲများကို စီမံသည်။ ဘဏ်ခွဲတိုင်းသည် ကုမ္ပဏီတစ်ခုတည်းနှင့်သာ သက်ဆိုင်သည်။',
    ),
    b(
      'Departments, employees and payslips show the branch they sit in.',
      'ဌာန၊ ဝန်ထမ်းနှင့် Payslip များတွင် ၎င်းတို့ရှိသော ဘဏ်ခွဲကို ပြသည်။',
    ),
  ],
  '/organization/departments': [
    b(
      'Manage departments under a branch. The branch you choose must belong to the department’s company.',
      'ဘဏ်ခွဲအောက်ရှိ ဌာနများကို စီမံသည်။ ရွေးသော ဘဏ်ခွဲသည် ဌာန၏ ကုမ္ပဏီနှင့် သက်ဆိုင်ရမည်။',
    ),
    b(
      'Used as a filter in attendance and leave lists and in the dashboard department chart.',
      'တက်ရောက်မှုနှင့် ခွင့်စာရင်းများ၏ Filter နှင့် Dashboard ဌာနအလိုက် Chart တွင် သုံးသည်။',
    ),
  ],
  '/organization/sections': [
    b(
      'Manage teams inside a department (the closest thing to a “team” in this system).',
      'ဌာနအတွင်းရှိ အဖွဲ့ငယ်များကို စီမံသည် (ဤစနစ်ရှိ “Team” နှင့် အနီးဆုံး)။',
    ),
    b(
      'A section must belong to a department of the same branch.',
      'ဌာနခွဲသည် ဌာနနှင့် ဘဏ်ခွဲ ကိုက်ညီရမည်။',
    ),
  ],
  '/organization/positions': [
    b(
      'Define job titles. A position belongs to a department and can report to another position (but not to itself).',
      'ရာထူးအမည်များကို သတ်မှတ်သည်။ ရာထူးသည် ဌာနတစ်ခုနှင့်သက်ဆိုင်ပြီး အခြားရာထူးသို့ Report လုပ်နိုင်သည် (ကိုယ်တိုင်ကို မရ)။',
    ),
    b(
      'Because a position belongs to one department, moving an employee to another department also means choosing a new position.',
      'ရာထူးသည် ဌာနတစ်ခုနှင့်သာဆိုင်သဖြင့် ဝန်ထမ်းကို ဌာနပြောင်းလျှင် ရာထူးအသစ်လည်း ရွေးရမည်။',
    ),
  ],
  '/organization/job-grades': [
    b(
      'Define seniority levels with a minimum and maximum salary (the salary band). Maximum must not be lower than minimum.',
      'ရာထူးအဆင့်များကို အနည်းဆုံး/အများဆုံး လစာ (Salary Band) ဖြင့် သတ်မှတ်သည်။ အများဆုံးသည် အနည်းဆုံးထက် မနိမ့်ရ။',
    ),
    b(
      'The band limits an employee’s basic salary in Payroll, and the grade selects the leave policy (for example managers get more annual leave).',
      'Band သည် Payroll တွင် Basic လစာကို ကန့်သတ်ပြီး Grade က ခွင့် Policy ကို ရွေးသည် (ဥပမာ Manager များ နှစ်ပတ်လည်ခွင့်ပိုရသည်)။',
    ),
  ],
  '/organization/reporting-structure': [
    b(
      'A read-only view of who reports to whom, built from each employee’s manager.',
      'ဝန်ထမ်းတစ်ဦးစီ၏ Manager အပေါ်အခြေခံသော “ဘယ်သူက ဘယ်သူ့ကို Report လုပ်သည်” ကြည့်ရန် စာမျက်နှာ (ကြည့်ရုံသာ)။',
    ),
    b(
      'To change it, edit the employee’s manager on the employee form.',
      'ပြောင်းလိုလျှင် ဝန်ထမ်း Form တွင် Manager ကို ပြင်ပါ။',
    ),
  ],

  '/employees/list': [
    b(
      'Search by name or code, filter, sort and page through employees. Export the current list to Excel.',
      'အမည်/ကုဒ်ဖြင့် ရှာ၊ Filter၊ စီ၊ စာမျက်နှာခွဲကြည့်နိုင်သည်။ လက်ရှိစာရင်းကို Excel ထုတ်နိုင်သည်။',
    ),
    b(
      'Print the current view or all matching rows (up to 1,000) from the browser. On a profile: Print profile, and Letters for an employment certificate, experience letter or leave approval letter.',
      'လက်ရှိမြင်ကွင်း သို့မဟုတ် ကိုက်ညီသည့်အတန်းအားလုံး (အများဆုံး ၁,၀၀၀) ကို Browser မှ ပရင့်ထုတ်နိုင်သည်။ Profile တွင် Print profile နှင့် Letters (အလုပ်အကိုင်လက်မှတ်၊ အတွေ့အကြုံစာ၊ ခွင့်ခွင့်ပြုစာ) ရှိသည်။',
    ),
    b(
      'Open a profile for tabs: overview, employment, history, documents, salary, attendance and leave.',
      'Profile ကိုဖွင့်လျှင် Overview, Employment, History, Documents, Salary, Attendance, Leave Tab များကြည့်နိုင်သည်။',
    ),
    b(
      'Status values: Active, Probation, On Leave, Suspended, Resigned, Terminated. A resigned or terminated employee needs an exit date.',
      'Status များ — Active, Probation, On Leave, Suspended, Resigned, Terminated။ Resigned/Terminated ဖြစ်လျှင် ထွက်ရက်လို။',
    ),
  ],
  '/employees/new': [
    b(
      'Register an employee: personal details, national ID, contact, emergency contact, bank, employment type, joining date and the full chain company → branch → department → section → position and grade.',
      'ဝန်ထမ်းစာရင်းသွင်းသည် — ကိုယ်ရေး၊ မှတ်ပုံတင်၊ ဆက်သွယ်ရန်၊ အရေးပေါ်ဆက်သွယ်ရန်၊ ဘဏ်၊ အလုပ်အမျိုးအစား၊ ဝင်ရက်၊ ကုမ္ပဏီ→ဘဏ်ခွဲ→ဌာန→ဌာနခွဲ→ရာထူးနှင့် Grade။',
    ),
    b(
      'The employee code is generated (EMP + next number). Work email and code must be unique; age must be 18 or more.',
      'ဝန်ထမ်းကုဒ်ကို အလိုအလျောက်ပေးသည် (EMP + နောက်နံပါတ်)။ Email နှင့် ကုဒ် မထပ်ရ၊ အသက် ၁၈ နှစ်နှင့်အထက်။',
    ),
    b(
      'New employees start as Probation. Saving does not create a salary, shift or user account: add those separately.',
      'ဝန်ထမ်းအသစ်သည် Probation ဖြင့်စသည်။ သိမ်းလိုက်ခြင်းက လစာ၊ Shift၊ User Account မဖန်တီးပါ — သီးခြားထည့်ရမည်။',
    ),
  ],
  '/employees/documents': [
    b(
      'A library of documents per employee (contract, ID copy, certificates…). You can upload, filter and delete entries.',
      'ဝန်ထမ်းအလိုက် စာရွက်စာတမ်းစာကြည့်တိုက် (စာချုပ်၊ မှတ်ပုံတင်မိတ္တူ၊ လက်မှတ်များ…)။ Upload လုပ်၊ Filter စစ်၊ ဖျက်နိုင်သည်။',
    ),
    b(
      'In this version only the file name and details are stored, not the file content.',
      'ဤ Version တွင် ဖိုင်အမည်နှင့် အချက်အလက်သာ သိမ်းပြီး ဖိုင်အကြောင်းအရာကို မသိမ်းပါ။',
    ),
  ],

  '/attendance/overview': [
    b(
      'Today’s status at a glance: present, late, on leave and absent, with a department breakdown chart.',
      'ယနေ့အခြေအနေ အကျဉ်းချုပ် — ရောက်သူ၊ နောက်ကျသူ၊ ခွင့်ယူသူ၊ ပျက်ကွက်သူနှင့် ဌာနအလိုက် Chart။',
    ),
    b(
      'On a weekend or holiday it shows the last working day instead.',
      'စနေ၊ တနင်္ဂနွေ သို့ အားလပ်ရက်ဖြစ်လျှင် နောက်ဆုံးအလုပ်လုပ်သည့်နေ့ကို ပြသည်။',
    ),
  ],
  '/attendance/check-in': [
    b(
      'Clock in and out (simulated). The system creates the day’s record from the employee’s shift.',
      'အလုပ်ဝင်/ထွက် မှတ်တမ်း (နမူနာ)။ ဝန်ထမ်း၏ Shift အလိုက် ထိုနေ့ မှတ်တမ်းကို စနစ်က ဖန်တီးသည်။',
    ),
    b(
      'Blocked on a day of approved leave; no double check-in; check-out needs a check-in. A punch on a weekend or holiday counts as Present with a remark.',
      'ခွင့်ပြုပြီးနေ့တွင် မရ၊ Check-in နှစ်ကြိမ်မရ၊ Check-out ရန် Check-in လို။ စနေ/တနင်္ဂနွေ/အားလပ်ရက် Punch သည် မှတ်ချက်နှင့် Present ဖြစ်သည်။',
    ),
  ],
  '/attendance/records': [
    b(
      'Daily records with check-in/out, worked time, late and early minutes and status (Present, Late, Half Day, Absent, On Leave).',
      'နေ့စဉ်မှတ်တမ်း — ဝင်/ထွက်အချိန်၊ အလုပ်လုပ်ချိန်၊ နောက်ကျ/စောပြန်မိနစ်နှင့် Status။',
    ),
    b(
      'Late = check-in after shift start plus grace time. Half Day = under 4 hours worked after the break.',
      'Late = Shift စချိန် + Grace ကျော်၍ ဝင်ခြင်း။ Half Day = အနားယူချိန်နုတ်ပြီး ၄ နာရီမပြည့်ခြင်း။',
    ),
    b(
      'Records are not an input to payroll; wrong ones are fixed through Adjustments.',
      'မှတ်တမ်းများသည် Payroll ၏ input မဟုတ်ပါ။ အမှားများကို Adjustments ဖြင့် ပြင်ရသည်။',
    ),
  ],
  '/attendance/exceptions': [
    b(
      'A filtered list of late arrivals and early departures so supervisors can follow up.',
      'နောက်ကျ/စောစောပြန် ချွင်းချက်များကိုသာ စစ်ထုတ်ပြသော စာရင်း — Supervisor များ ဆက်လက်ဆောင်ရွက်ရန်။',
    ),
    b(
      'Late and absent days do not change pay by themselves; only approved unpaid leave does.',
      'နောက်ကျ/ပျက်ကွက်ခြင်းက လစာကို မပြောင်းပါ — ခွင့်ပြုပြီး လစာမဲ့ခွင့်သာ ပြောင်းသည်။',
    ),
  ],
  '/attendance/shifts': [
    b(
      'Define shifts: start and end time, break, grace minutes, work days and night shift.',
      'Shift သတ်မှတ်သည် — စ/ဆုံးချိန်၊ အနားယူချိန်၊ Grace မိနစ်၊ အလုပ်ရက်များနှင့် ညဆိုင်း။',
    ),
    b(
      'These settings drive Late, Half Day and overtime calculations. An inactive shift cannot be newly assigned.',
      'ဤ Setting များက Late၊ Half Day နှင့် OT တွက်ချက်မှုကို ဆုံးဖြတ်သည်။ Inactive Shift ကို အသစ်မခွဲပေးနိုင်။',
    ),
  ],
  '/attendance/holidays': [
    b(
      'Add, edit or delete public and company holidays. There can be one holiday per date.',
      'အများပြည်သူနှင့် ကုမ္ပဏီအားလပ်ရက်များကို ထည့်၊ ပြင်၊ ဖျက်နိုင်သည် — တစ်ရက်လျှင် အားလပ်ရက်တစ်ခုသာ ရှိနိုင်သည်။',
    ),
    b(
      'Holidays affect leave day counts and working days: they are not counted as leave days, and payroll excludes them from working days. Punches on a holiday count as Present.',
      'အားလပ်ရက်များသည် ခွင့်ရက်ရေနှင့် အလုပ်ရက်ရေကို သက်ရောက်သည် — ခွင့်ရက်အဖြစ် မရေတွက်ပါ၊ လစာတွက်ချက်ရာတွင်လည်း အလုပ်ရက်မှ နုတ်သည်။ အားလပ်ရက်တွင် Punch လုပ်လျှင် Present ဟု သတ်မှတ်သည်။',
    ),
  ],
  '/attendance/shift-assignment': [
    b(
      'Assign a shift to employees (one at a time or in bulk).',
      'ဝန်ထမ်းများကို Shift ခွဲပေးသည် (တစ်ဦးချင်း သို့ အစုလိုက်)။',
    ),
    b(
      'A change applies to future punches; existing records keep their old shift.',
      'ပြောင်းလဲမှုသည် အနာဂတ် Punch များအတွက်သာ — ရှိပြီးသားမှတ်တမ်းများသည် Shift အဟောင်းအတိုင်းကျန်သည်။',
    ),
  ],
  '/attendance/schedules': [
    b(
      'A weekly grid showing, for each employee, working days, holidays and approved leave.',
      'ဝန်ထမ်းတစ်ဦးစီ၏ အလုပ်ရက်၊ အားလပ်ရက်နှင့် ခွင့်ပြုပြီးခွင့်ကို အပတ်စဉ် Grid ဖြင့်ပြသည်။',
    ),
    b(
      'It is a read-only view built from shifts, holidays and leave: change those to change the schedule.',
      'Shift၊ အားလပ်ရက်နှင့် ခွင့်မှ တည်ဆောက်သော ကြည့်ရုံသာစာမျက်နှာ — ပြောင်းလိုလျှင် ထိုအရာများကို ပြင်ပါ။',
    ),
  ],
  '/attendance/overtime': [
    b(
      'Request overtime for a date with start and end time (15 minutes to 12 hours, not in the future), then approve or reject.',
      'ရက်စွဲတစ်ခုအတွက် စ/ဆုံးအချိန်ဖြင့် OT တောင်းခံပြီး ခွင့်ပြု/ပယ်ချသည် (၁၅ မိနစ်–၁၂ နာရီ၊ အနာဂတ်မရ)။',
    ),
    b(
      'Only approved requests are paid in Payroll, at 1.5× the hourly rate. One request per employee per date.',
      'ခွင့်ပြုပြီးတောင်းခံမှုများကိုသာ Payroll တွင် နာရီစားနှုန်း ၁.၅ ဆဖြင့် ပေးသည်။ ဝန်ထမ်း၊ ရက်စွဲတစ်ခုလျှင် တစ်ခုသာ။',
    ),
    b(
      'Approve before processing payroll; approving later needs the period returned to draft.',
      'Payroll Process မလုပ်မီ ခွင့်ပြုပါ — နောက်မှခွင့်ပြုလျှင် Period ကို Draft သို့ပြန်ရမည်။',
    ),
  ],
  '/attendance/adjustments': [
    b(
      'Ask to correct a wrong record (new check-in/out times or status) with a reason; the record must already exist and the date cannot be in the future.',
      'မှားယွင်းသောမှတ်တမ်းကို ပြင်ရန် အကြောင်းပြချက်ဖြင့် တောင်းဆိုသည် (အချိန်အသစ် သို့ Status)။ မှတ်တမ်းရှိပြီးသားဖြစ်ရမည်၊ အနာဂတ်ရက်မရ။',
    ),
    b(
      'One Pending request per employee per date. When approved, the record is recalculated and marked Manual.',
      'ဝန်ထမ်း၊ ရက်စွဲတစ်ခုလျှင် Pending တစ်ခုသာ။ ခွင့်ပြုလျှင် မှတ်တမ်းကို ပြန်တွက်ပြီး Manual အဖြစ်မှတ်သည်။',
    ),
  ],

  '/leave/types': [
    b(
      'Define leave types (annual, casual, sick, maternity, unpaid…): default days, whether paid, maximum consecutive days, whether a document is required and any gender restriction.',
      'ခွင့်အမျိုးအစားများ သတ်မှတ်သည် (နှစ်ပတ်လည်၊ ရံဖန်ရံခါ၊ နာမကျန်း၊ မီးဖွား၊ လစာမဲ့…) — ပုံသေရက်၊ လစာရ/မရ၊ ဆက်တိုက်ယူနိုင်ရက်၊ စာရွက်လို/မလို၊ ကျား/မ ကန့်သတ်ချက်။',
    ),
    b(
      'Maternity leave is for female employees and paternity leave for male employees.',
      'မီးဖွားခွင့်သည် အမျိုးသမီးများအတွက်၊ Paternity ခွင့်သည် အမျိုးသားများအတွက်။',
    ),
  ],
  '/leave/policies': [
    b(
      'Decide how many days an employee gets, by leave type, job grade, employment type and minimum months of service.',
      'ခွင့်အမျိုးအစား၊ Job Grade၊ အလုပ်အမျိုးအစားနှင့် အနည်းဆုံးအလုပ်သက်လအလိုက် ရက်အရေအတွက် သတ်မှတ်သည်။',
    ),
    b(
      'The most specific eligible policy wins (a grade match beats a general one). A policy can accrue monthly (days × month ÷ 12).',
      'အတိကျဆုံး သင့်တော်သော Policy ကို အသုံးပြုသည် (Grade ကိုက်လျှင် ယေဘုယျထက်သာ)။ လစဉ်စုဆောင်းနိုင်သည် (ရက် × လ ÷ ၁၂)။',
    ),
  ],
  '/leave/balances': [
    b(
      'Shows each employee’s entitled, used, pending and available days per leave type for the year.',
      'ခွင့်အမျိုးအစားအလိုက် ဝန်ထမ်းတစ်ဦးစီ၏ ရထိုက်သည့်ရက်၊ သုံးပြီးရက်၊ စောင့်ဆိုင်းရက်နှင့် လက်ကျန်ရက်ကို ပြသည်။',
    ),
    b(
      'Balances are calculated, not typed in: available = entitled − approved − submitted days.',
      'လက်ကျန်ကို လက်ဖြင့်မထည့်ရ၊ တွက်ချက်ထားသည် — ရနိုင်ရက် = ရထိုက်ရက် − ခွင့်ပြုပြီး − Submit လုပ်ပြီးရက်။',
    ),
  ],
  '/leave/applications': [
    b(
      'Create a leave request with dates (or a half day), reason and a document if required, save as Draft, then Submit.',
      'ရက်စွဲ (သို့ တစ်ဝက်ခွင့်)၊ အကြောင်းပြချက်နှင့် လိုအပ်လျှင် စာရွက်ဖြင့် ခွင့်လျှောက်လွှာဖန်တီးပြီး Draft သိမ်း၊ ထို့နောက် Submit လုပ်သည်။',
    ),
    b(
      'Days count working days only. It is refused if it overlaps another request, exceeds the balance or the maximum consecutive days, or starts before the joining date.',
      'အလုပ်ရက်သာ ရေတွက်သည်။ အခြားလျှောက်လွှာနှင့်ထပ်ခြင်း၊ လက်ကျန်/ဆက်တိုက်ရက်ကျော်ခြင်း၊ ဝင်ရက်မတိုင်မီစခြင်းတို့ကို ပယ်သည်။',
    ),
    b('You can cancel a request until it starts.', 'မစမီအထိ လျှောက်လွှာကို Cancel လုပ်နိုင်သည်။'),
  ],
  '/leave/approvals': [
    b(
      'Queue of submitted requests waiting for a decision: approve, or reject with a reason.',
      'ဆုံးဖြတ်ချက်စောင့်နေသော Submit လုပ်ပြီး လျှောက်လွှာများ — ခွင့်ပြု သို့ အကြောင်းပြချက်ဖြင့် ပယ်ချသည်။',
    ),
    b(
      'You cannot decide your own request. Approval turns past attendance days into On Leave and, for unpaid leave, creates a payroll deduction.',
      'ကိုယ့်လျှောက်လွှာကို ကိုယ်မဆုံးဖြတ်နိုင်။ ခွင့်ပြုလျှင် ယခင်တက်ရောက်မှုရက်များ On Leave ဖြစ်ပြီး လစာမဲ့ခွင့်ဆိုလျှင် Payroll တွင် နုတ်ငွေဖြစ်သည်။',
    ),
  ],
  '/leave/calendar': [
    b(
      'A monthly team calendar showing who is on leave, with holidays marked.',
      'ဘယ်သူ ခွင့်ယူထားသည်ကို အားလပ်ရက်များနှင့်တကွ ပြသော လစဉ်အဖွဲ့ပြက္ခဒိန်။',
    ),
    b(
      'Useful for planning cover before approving requests.',
      'ခွင့်မပြုမီ အစားထိုးစီစဉ်ရန် အသုံးဝင်သည်။',
    ),
  ],

  '/payroll/salary-components': [
    b(
      'Define earnings (housing, transport…) and deductions (tax, social…). Each has a unique code and a method: fixed amount, % of basic or % of gross.',
      'ဝင်ငွေ (အိမ်ငှား၊ ယာဉ်…) နှင့် နုတ်ငွေ (အခွန်၊ လူမှု…) သတ်မှတ်သည်။ ကုဒ်မထပ်ရ၊ နည်းလမ်း — ပုံသေ၊ Basic ၏ %၊ Gross ၏ %။',
    ),
    b(
      'Value must be above 0 and a percentage cannot exceed 100. A component used by a structure cannot be deleted.',
      'တန်ဖိုး ၀ ထက်ကြီးရမည်၊ % သည် ၁၀၀ မကျော်ရ။ Structure တွင်သုံးထားသော Component ကို ဖျက်၍မရ။',
    ),
  ],
  '/payroll/salary-structures': [
    b(
      'Group components into a reusable package (for example “Officer package”).',
      'Component များကို ပြန်သုံးနိုင်သော အစုအဝေး (ဥပမာ “Officer package”) အဖြစ် စုစည်းသည်။',
    ),
    b(
      'A structure needs at least one component and at least one earning.',
      'Structure တစ်ခုတွင် Component အနည်းဆုံးတစ်ခုနှင့် ဝင်ငွေ (Earning) အနည်းဆုံးတစ်ခု ပါရမည်။',
    ),
  ],
  '/payroll/employee-salaries': [
    b(
      'Give each employee a basic salary and a structure from an effective date. A raise is a new row; old rows stay as history.',
      'ဝန်ထမ်းတစ်ဦးစီအား Effective Date မှစ၍ Basic လစာနှင့် Structure ပေးသည်။ လစာတိုးလျှင် အတန်းအသစ်ထည့်ပြီး အဟောင်းများ မှတ်တမ်းအဖြစ်ကျန်သည်။',
    ),
    b(
      'Basic must be inside the job grade’s band and the date cannot be before joining. Employees with no salary are skipped in payroll with a warning.',
      'Basic သည် Job Grade Band အတွင်းဖြစ်ရမည်၊ ဝင်ရက်မတိုင်မီ မရ။ လစာမသတ်မှတ်သူကို Payroll တွင် သတိပေးချက်နှင့်ကျော်သည်။',
    ),
  ],
  '/payroll/processing': [
    b(
      'Create a monthly period (not in the future, one per month), then Process → Approve → Mark as paid.',
      'လစဉ် Period ဖန်တီးပြီး (အနာဂတ်မရ၊ တစ်လတစ်ခု) Process → Approve → Mark as paid လုပ်သည်။',
    ),
    b(
      'Process builds a payslip for each employee from salary, joining/exit dates, unpaid leave, approved overtime and holidays. Earlier draft or pending periods block it.',
      'Process သည် လစာ၊ ဝင်/ထွက်ရက်၊ လစာမဲ့ခွင့်၊ ခွင့်ပြုပြီး OT နှင့် အားလပ်ရက်မှ ဝန်ထမ်းတစ်ဦးစီအတွက် Payslip ဖန်တီးသည်။ အရင် Draft/Pending Period ရှိလျှင် မရ။',
    ),
    b(
      'A processed or approved period can be returned to draft (payslips are deleted). Paid is final. Mark as paid records status only.',
      'Process/Approve ပြီး Period ကို Draft သို့ပြန်နိုင်သည် (Payslip ဖျက်)။ Paid ပြီးလျှင် အပြီးအပိုင်။ Mark as paid သည် Status မှတ်ရုံသာ။',
    ),
  ],
  '/payroll/payslips': [
    b(
      'List payslips by period; open one to see basic, each earning, overtime, each deduction, unpaid leave, gross and net, then print it.',
      'Period အလိုက် Payslip စာရင်း — ဖွင့်လျှင် Basic၊ ဝင်ငွေတစ်ခုချင်း၊ OT၊ နုတ်ငွေတစ်ခုချင်း၊ လစာမဲ့ခွင့်၊ Gross၊ Net ကိုမြင်ရပြီး ပရင့်ထုတ်နိုင်သည်။',
    ),
    b(
      'Print payslip prints one payslip on an A4 page; on Payroll Review, Print all payslips prints the whole period, one payslip per page. Only the masked account is printed.',
      'Print payslip သည် စလစ်တစ်စောင်ကို A4 တစ်မျက်နှာထုတ်သည်၊ Payroll Review ရှိ Print all payslips သည် Period တစ်ခုလုံးကို တစ်မျက်နှာလျှင်တစ်စောင်ထုတ်သည်။ ဖုံးထားသော အကောင့်နံပါတ်သာ ပါသည်။',
    ),
    b(
      'A payslip is a snapshot: later changes to salary setup do not alter it.',
      'Payslip သည် Snapshot — နောက်ပိုင်း လစာ Setup ပြောင်းလည်း မပြောင်းပါ။',
    ),
  ],
  '/payroll/history': [
    b(
      'Compares gross and net pay month by month, as a chart with a table alternative.',
      'လအလိုက် Gross နှင့် Net လစာကို Chart (နှင့် Table) ဖြင့် နှိုင်းယှဉ်ပြသည်။',
    ),
    b('Use it to spot trends in payroll cost.', 'လစာကုန်ကျစရိတ် Trend ကိုကြည့်ရန် သုံးပါ။'),
  ],

  '/recruitment/job-openings': [
    b(
      'Create a vacancy as Draft, then Publish, Put on hold, Resume, Close or Reopen it.',
      'လစ်လပ်ချက်ကို Draft ဖြင့်ဖန်တီးပြီး Publish၊ Hold၊ Resume၊ Close၊ Reopen လုပ်သည်။',
    ),
    b(
      'Closed and Filled openings cannot be edited; one with applications cannot be deleted. It becomes Filled automatically when enough candidates are selected.',
      'Closed/Filled Opening ကို မပြင်နိုင်၊ လျှောက်လွှာပါလျှင် မဖျက်နိုင်။ လူလုံလောက်စွာရွေးပြီးလျှင် Filled ဖြစ်သွားသည်။',
    ),
  ],
  '/recruitment/candidates': [
    b(
      'Applicant database: add people, attach a resume (file name only) and apply them to an opening.',
      'လျှောက်ထားသူ ဒေတာ — လူထည့်၊ Resume (ဖိုင်အမည်သာ) တွဲ၊ Opening တစ်ခုသို့ လျှောက်ထားပေးသည်။',
    ),
    b(
      'Email must be unique. Applications only open for Open openings, one per candidate per opening. A candidate with applications cannot be deleted.',
      'Email မထပ်ရ။ Open ဖြစ်သော Opening တွင်သာ လျှောက်နိုင်၊ Candidate တစ်ဦးလျှင် Opening တစ်ခုအတွက် တစ်ခု။ လျှောက်လွှာပါသော Candidate ကို မဖျက်နိုင်။',
    ),
  ],
  '/recruitment/interviews': [
    b(
      'Schedule an interview (date, time, interviewer). Scheduling moves a screened candidate to the Interview stage.',
      'အင်တာဗျူးချိန်းသည် (ရက်၊ အချိန်၊ Interviewer)။ ချိန်းလိုက်သည်နှင့် Screening ပြီး Candidate သည် Interview အဆင့်သို့ရောက်သည်။',
    ),
    b(
      'An interviewer cannot be double-booked. Complete an interview with a score (1–5) and a recommendation; completed ones cannot be deleted.',
      'Interviewer ကို နှစ်နေရာမချိန်းနိုင်။ ရမှတ် (၁–၅) နှင့် အကြံပြုချက်ဖြင့် ပြီးဆုံးစေသည်၊ ပြီးသည်ကို မဖျက်နိုင်။',
    ),
  ],
  '/recruitment/pipeline': [
    b(
      'A board of columns: Applied → Screening → Interview → Selected / Rejected. Move candidates between stages.',
      'Column ပုံစံ Board — Applied → Screening → Interview → Selected / Rejected။ Candidate များကို အဆင့်ပြောင်းနိုင်သည်။',
    ),
    b(
      'Rejecting needs a reason and cancels scheduled interviews. Selected needs a completed interview with “Hire” or “Strong hire” and a free vacancy.',
      'ပယ်ချလျှင် အကြောင်းပြချက်လိုပြီး ချိန်းထားသော အင်တာဗျူးများကို ပယ်ဖျက်သည်။ Selected ရန် “Hire/Strong hire” ပါသောအင်တာဗျူးပြီးခြင်းနှင့် လစ်လပ်နေသောနေရာလို။',
    ),
    b(
      'Selecting does not create the employee: HR registers them in Employees by hand.',
      'Selected ဖြစ်ခြင်းက ဝန်ထမ်းကို မဖန်တီးပါ — HR က Employees တွင် လက်ဖြင့်စာရင်းသွင်းရသည်။',
    ),
  ],

  '/data-exchange/import': [
    b(
      'Wizard: download the template → upload your Excel → map columns → review problems → confirm → see the result.',
      'Wizard — Template ဒေါင်းလုဒ် → Excel Upload → Column ချိတ် → ပြဿနာစစ် → Confirm → ရလဒ်ကြည့်။',
    ),
    b(
      'You can create new employees, update existing ones, or both. Wrong headers, wrong file types and unsafe formulas are rejected before saving.',
      'ဝန်ထမ်းအသစ်ဖန်တီး၊ ရှိပြီးသားကို Update၊ သို့ နှစ်မျိုးလုံးလုပ်နိုင်သည်။ Header မှား၊ ဖိုင်အမျိုးအစားမှားနှင့် အန္တရာယ်ရှိ Formula များကို မသိမ်းမီ ပယ်သည်။',
    ),
    b('Only Employees can be imported so far.', 'လက်ရှိ Employees ကိုသာ Import လုပ်နိုင်သည်။'),
  ],
  '/data-exchange/history': [
    b(
      'A log of every import: who, when, how many rows succeeded or failed.',
      'Import တိုင်း၏ မှတ်တမ်း — ဘယ်သူ၊ ဘယ်အချိန်၊ အတန်းဘယ်လောက် အောင်မြင်/မအောင်မြင်။',
    ),
    b(
      'Open one to see each problem and download a validation report to fix the Excel file.',
      'ဖွင့်လျှင် ပြဿနာတစ်ခုချင်းကိုကြည့်ပြီး Validation Report ဒေါင်းလုဒ်ဆွဲကာ Excel ကိုပြင်နိုင်သည်။',
    ),
  ],

  '/settings/general': [
    b(
      'Organization name, contact email, time zone, date format, currency, fiscal year start and notification switches.',
      'ကုမ္ပဏီအမည်၊ ဆက်သွယ်ရန် Email၊ Time Zone၊ ရက်စွဲပုံစံ၊ ငွေကြေး၊ ဘဏ္ဍာနှစ်စတင်ရက်နှင့် Notification ခလုတ်များ။',
    ),
    b(
      'Saved in memory; other screens do not read these values yet.',
      'Memory ထဲတွင်သာသိမ်းသည် — အခြားစာမျက်နှာများက ဤတန်ဖိုးများကို မဖတ်သေးပါ။',
    ),
  ],
  '/settings/users': [
    b(
      'Create user accounts, each linked to one current employee (name and email are copied from the employee). Statuses: Active, Inactive, Locked (use Unlock account).',
      'ဝန်ထမ်းတစ်ဦးနှင့် ချိတ်ထားသော User Account ဖန်တီးသည် (အမည်/Email ကို ဝန်ထမ်းမှကူးသည်)။ Status — Active၊ Inactive၊ Locked (Unlock account ဖြင့်ဖွင့်)။',
    ),
    b(
      'You cannot delete, deactivate or change the role of your own account, and there must always be one active administrator.',
      'ကိုယ့် Account ကို ဖျက်/Deactivate/Role ပြောင်း၍မရ၊ Active Administrator တစ်ဦးအမြဲရှိရမည်။',
    ),
  ],
  '/settings/roles': [
    b(
      'Create and manage roles (HR Administrator, HR Officer, Department Manager, Payroll Officer, Recruiter, Employee, Auditor…).',
      'Role များ (HR Administrator, HR Officer, Department Manager, Payroll Officer, Recruiter, Employee, Auditor…) ကို ဖန်တီးစီမံသည်။',
    ),
    b(
      'Built-in roles cannot be renamed or deleted; a role with users cannot be deleted, and one with active users cannot be deactivated.',
      'Built-in Role ကို အမည်ပြောင်း/ဖျက်မရ၊ အသုံးပြုသူရှိသော Role ကို မဖျက်နိုင်၊ Active User ရှိသော Role ကို Deactivate မလုပ်နိုင်။',
    ),
  ],
  '/settings/permissions': [
    b(
      'A matrix of modules × actions (View, Create, Edit, Delete, Approve…) for each role.',
      'Role တစ်ခုစီအတွက် Module × Action (View, Create, Edit, Delete, Approve…) Matrix။',
    ),
    b(
      'Granting any action also grants View; removing View clears the module. HR Administrator is read-only.',
      'Action တစ်ခုပေးလျှင် View ပါပေးသည်၊ View ဖယ်လျှင် Module တစ်ခုလုံးရှင်းသည်။ HR Administrator ကို ပြင်၍မရ။',
    ),
    b(
      'In the demo build permissions are simulated. With the API the server enforces them, and the screens hide what your role cannot use.',
      'Demo Build တွင် Permission သည် နမူနာသာ။ API ဖြင့်သုံးလျှင် Server က အမှန်တကယ်ကန့်သတ်ပြီး သင့် Role မသုံးနိုင်သည့်အရာများကို Screen များက ဖျောက်ထားသည်။',
    ),
  ],
};
