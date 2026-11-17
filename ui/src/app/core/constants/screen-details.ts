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
    b(
      'Export to Excel and import from Excel (Data Exchange → Import) when your role allows it. Rows are matched by the code, parents are given by their codes and Status is Active or Inactive.',
      'သင့် Role ခွင့်ပြုပါက Excel ထုတ်ခြင်းနှင့် Excel မှ ထည့်သွင်းခြင်း (Data Exchange → Import) ပြုလုပ်နိုင်သည်။ အတန်းများကို ကုဒ်ဖြင့်ချိတ်ပြီး မိခင် Record များကို ကုဒ်ဖြင့်ပေးရသည်။ Status သည် Active သို့ Inactive ဖြစ်သည်။',
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
    b(
      'Export to Excel and import from Excel (Data Exchange → Import) when your role allows it. Rows are matched by the code, parents are given by their codes and Status is Active or Inactive.',
      'သင့် Role ခွင့်ပြုပါက Excel ထုတ်ခြင်းနှင့် Excel မှ ထည့်သွင်းခြင်း (Data Exchange → Import) ပြုလုပ်နိုင်သည်။ အတန်းများကို ကုဒ်ဖြင့်ချိတ်ပြီး မိခင် Record များကို ကုဒ်ဖြင့်ပေးရသည်။ Status သည် Active သို့ Inactive ဖြစ်သည်။',
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
    b(
      'Export to Excel and import from Excel (Data Exchange → Import) when your role allows it. Rows are matched by the code, parents are given by their codes and Status is Active or Inactive.',
      'သင့် Role ခွင့်ပြုပါက Excel ထုတ်ခြင်းနှင့် Excel မှ ထည့်သွင်းခြင်း (Data Exchange → Import) ပြုလုပ်နိုင်သည်။ အတန်းများကို ကုဒ်ဖြင့်ချိတ်ပြီး မိခင် Record များကို ကုဒ်ဖြင့်ပေးရသည်။ Status သည် Active သို့ Inactive ဖြစ်သည်။',
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
    b(
      'Export to Excel and import from Excel (Data Exchange → Import) when your role allows it. Rows are matched by the code, parents are given by their codes and Status is Active or Inactive.',
      'သင့် Role ခွင့်ပြုပါက Excel ထုတ်ခြင်းနှင့် Excel မှ ထည့်သွင်းခြင်း (Data Exchange → Import) ပြုလုပ်နိုင်သည်။ အတန်းများကို ကုဒ်ဖြင့်ချိတ်ပြီး မိခင် Record များကို ကုဒ်ဖြင့်ပေးရသည်။ Status သည် Active သို့ Inactive ဖြစ်သည်။',
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
    b(
      'Export to Excel and import from Excel (Data Exchange → Import) when your role allows it. Rows are matched by the code, parents are given by their codes and Status is Active or Inactive.',
      'သင့် Role ခွင့်ပြုပါက Excel ထုတ်ခြင်းနှင့် Excel မှ ထည့်သွင်းခြင်း (Data Exchange → Import) ပြုလုပ်နိုင်သည်။ အတန်းများကို ကုဒ်ဖြင့်ချိတ်ပြီး မိခင် Record များကို ကုဒ်ဖြင့်ပေးရသည်။ Status သည် Active သို့ Inactive ဖြစ်သည်။',
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
    b(
      'Export to Excel and import from Excel (Data Exchange → Import) when your role allows it. Rows are matched by the code, parents are given by their codes and Status is Active or Inactive.',
      'သင့် Role ခွင့်ပြုပါက Excel ထုတ်ခြင်းနှင့် Excel မှ ထည့်သွင်းခြင်း (Data Exchange → Import) ပြုလုပ်နိုင်သည်။ အတန်းများကို ကုဒ်ဖြင့်ချိတ်ပြီး မိခင် Record များကို ကုဒ်ဖြင့်ပေးရသည်။ Status သည် Active သို့ Inactive ဖြစ်သည်။',
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
  '/employees/do-not-rehire': [
    b(
      'Former employees who must not be hired again. Filter Active (in force) or Lifted, and search by employee code or name. Open an entry to read its reason.',
      'ပြန်လည်မခန့်အပ်ရန် ဝန်ထမ်းဟောင်းများ။ Active (အသက်ဝင်) သို့မဟုတ် Lifted ဖြင့် Filter စစ်၍ ဝန်ထမ်းကုဒ်/အမည်ဖြင့်ရှာနိုင်သည်။ မှတ်တမ်းကိုဖွင့်၍ အကြောင်းပြချက်ဖတ်နိုင်သည်။',
    ),
    b(
      'Add entry: choose a former employee (one with an exit date) and give a reason (500 characters at most). One entry in force per person. Lift entry needs a reason; the entry stays as history.',
      'Add entry — ထွက်ရက်ရှိသော ဝန်ထမ်းဟောင်းကိုရွေး၍ အကြောင်းပြချက် (စာလုံး ၅၀၀ အထိ) ပေးပါ။ လူတစ်ဦးလျှင် အသက်ဝင်မှတ်တမ်း တစ်ခုသာ။ Lift entry တွင် အကြောင်းပြချက်လို၊ မှတ်တမ်းသည် သမိုင်းအဖြစ်ကျန်သည်။',
    ),
    b(
      'Needs the “view do-not-rehire” permission to open; adding or lifting needs the “edit” permission. The reason is shown here and on the former employee’s profile only.',
      'ဖွင့်ရန် “view do-not-rehire” ခွင့်ပြုချက်လိုပြီး ထည့်/ရုပ်သိမ်းရန် “edit” ခွင့်ပြုချက်လို။ အကြောင်းပြချက်ကို ဤနေရာနှင့် ဝန်ထမ်းဟောင်း Profile တွင်သာ ပြသည်။',
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
    b(
      'Export the records to Excel (export only: records come from punches and approved adjustments, so there is no import). The filters and date range apply to the file.',
      'မှတ်တမ်းများကို Excel ထုတ်နိုင်သည် (ထုတ်ရန်သာ — မှတ်တမ်းများသည် Punch နှင့် ခွင့်ပြုပြီး Adjustment မှလာသဖြင့် Import မရှိ)။ Filter နှင့် ရက်စွဲအပိုင်းအခြားက ဖိုင်ကို သက်ရောက်သည်။',
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
    b(
      'Export every employee with the current shift to Excel, or import a file with Employee Code and Shift Code to assign shifts in bulk (Data Exchange → Import).',
      'ဝန်ထမ်းတိုင်း၏ လက်ရှိ Shift ကို Excel ထုတ်နိုင်သည်။ Employee Code နှင့် Shift Code ပါသော ဖိုင်ကို Import လုပ်၍ Shift များကို အစုလိုက်ခွဲပေးနိုင်သည် (Data Exchange → Import)။',
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
    b(
      'Export the balances to Excel for the selected year: one row per employee and leave type (export only: balances are worked out, not stored).',
      'ရွေးထားသောနှစ်အတွက် လက်ကျန်များကို Excel ထုတ်နိုင်သည် — ဝန်ထမ်းနှင့် ခွင့်အမျိုးအစားတစ်ခုလျှင် တစ်တန်း (ထုတ်ရန်သာ — လက်ကျန်များကို တွက်ထုတ်ထားခြင်းဖြစ်ပြီး သိမ်းမထားပါ)။',
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
    b(
      'Export the applications to Excel (export only: importing would bypass the submit and approve steps). The filters and the leave date range apply to the file.',
      'ခွင့်လျှောက်လွှာများကို Excel ထုတ်နိုင်သည် (ထုတ်ရန်သာ — Import လုပ်လျှင် တင်သွင်း/ခွင့်ပြုအဆင့်များကို ကျော်သွားမည်)။ Filter နှင့် ခွင့်ရက်အပိုင်းအခြားက ဖိုင်ကို သက်ရောက်သည်။',
    ),
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

  '/performance/dashboard': [
    b(
      'Choose an evaluation period; the dashboard opens on the default one (the most recent Active period, else the most recent). The tabs are the views your role may open: My performance, My team (reviewers) and HR overview (HR). With one view there are no tabs.',
      'Evaluation Period ကိုရွေးပါ — Dashboard သည် ပုံသေ Period (နောက်ဆုံး Active Period၊ မရှိလျှင် နောက်ဆုံး Period) ဖြင့် ဖွင့်သည်။ Tab များသည် သင့် Role ကြည့်ခွင့်ရှိသော မြင်ကွင်းများ ဖြစ်သည် — My performance၊ My team (Reviewer များ) နှင့် HR overview (HR)။ မြင်ကွင်း တစ်ခုတည်းဆိုလျှင် Tab မပြပါ။',
    ),
    b(
      'My performance: a banner while your results are still to be submitted (Overdue after the due date), your overall score and rating, your KPIs with target, actual, achievement and weight, your manager’s feedback, and your earlier periods as a chart and a list.',
      'My performance — ရလဒ် Submit မလုပ်ရသေးလျှင် သတိပေးစာ (Due date ကျော်လျှင် Overdue)၊ သင့်စုစုပေါင်းရမှတ်နှင့် Rating၊ Target၊ Actual၊ Achievement၊ Weight ပါသော KPI များ၊ Manager ၏ Feedback နှင့် ယခင် Period များကို Chart နှင့် စာရင်းအဖြစ် ပြသည်။',
    ),
    b(
      'My team: team size, reviews waiting for you, approved or finalized scorecards, the team average, the status breakdown and a comparison of your team’s scores. HR overview: employees with KPIs, assignments, completed evaluations, pending submissions and approvals, the average score, the completion rate, department performance, top performers, employees requiring attention (with the threshold used) and the rating distribution.',
      'My team — အဖွဲ့အရွယ်အစား၊ သင့်ကိုစောင့်နေသော Review များ၊ Approved သို့ Finalized Scorecard များ၊ အဖွဲ့ပျမ်းမျှ၊ Status အလိုက်ခွဲခြမ်းမှုနှင့် အဖွဲ့ဝင်ရမှတ် နှိုင်းယှဉ်ချက်။ HR overview — KPI ရှိသော ဝန်ထမ်း၊ Assignment၊ ပြီးဆုံးသော Evaluation၊ Submit/Approve စောင့်ဆိုင်းမှု၊ ပျမ်းမျှရမှတ်၊ Completion rate၊ ဌာနအလိုက်စွမ်းဆောင်ရည်၊ ထိပ်တန်းဝန်ထမ်း၊ အာရုံစိုက်ရန်လိုသော ဝန်ထမ်း (သုံးထားသော Threshold နှင့်) နှင့် Rating ဖြန့်ဝေမှု။',
    ),
    b(
      'Every chart has a Table view with the exact values. All figures come from the server and count only the scorecards you may see; averages count Finalized scorecards only.',
      'Chart တိုင်းတွင် တန်ဖိုးအတိအကျပါသော Table view ရှိသည်။ ကိန်းဂဏန်းအားလုံး Server မှလာပြီး သင်ကြည့်ခွင့်ရှိသော Scorecard များကိုသာ ရေတွက်သည် — ပျမ်းမျှများသည် Finalized Scorecard များကိုသာ ရေတွက်သည်။',
    ),
  ],
  '/performance/reports': [
    b(
      'Choose a report: Summary by period (also Monthly, Quarterly and Annual), Employee performance history, Department performance, Ranking, KPI achievement or Target vs actual. You see your own rows, your team’s, or everyone’s for HR.',
      'Report ကိုရွေးပါ — Summary by period (Monthly၊ Quarterly နှင့် Annual လည်းပါ)၊ Employee performance history၊ Department performance၊ Ranking၊ KPI achievement သို့ Target vs actual။ ကိုယ့်အတန်း၊ အဖွဲ့၏အတန်း သို့ HR ဆိုလျှင် အားလုံးကို မြင်ရသည်။',
    ),
    b(
      'Filter by company, branch, department, position, employee, category, period, status, period type and year where the report uses them; search by employee code or name; page and sort on the server. The ranking keeps its fixed order: equal scores share a rank (1, 1, 3).',
      'Report က သုံးသည့်နေရာတွင် Company၊ Branch၊ ဌာန၊ Position၊ ဝန်ထမ်း၊ Category၊ Period၊ Status၊ Period type နှင့် နှစ်ဖြင့် စစ်ထုတ်ပါ — ဝန်ထမ်း Code သို့ အမည်ဖြင့် ရှာပြီး Server တွင် Page ခွဲ၍ စီပါ။ Ranking သည် ပုံသေအစီအစဉ်ဖြစ်ပြီး ရမှတ်တူလျှင် အဆင့်တူရသည် (1, 1, 3)။',
    ),
    b(
      'Export to Excel (Ranking, Target vs actual, and the scorecard list from the summary, employee and department reports) writes the rows of the current filters; it needs the Excel export permission. Comments and evidence files are never exported.',
      'Export to Excel (Ranking၊ Target vs actual နှင့် Summary၊ Employee၊ Department Report များမှ Scorecard စာရင်း) သည် လက်ရှိ Filter ၏ အတန်းများကို ရေးသည် — Excel export ခွင့်လိုသည်။ Comment နှင့် Evidence ဖိုင်များကို ဘယ်တော့မှ Export မလုပ်ပါ။',
    ),
    b(
      'Print lays out the page shown for paper; choose “Save as PDF” in the browser’s print dialog for a PDF.',
      'Print သည် ပြထားသော စာမျက်နှာကို စက္ကူအတွက် စီစဉ်ပေးသည် — PDF လိုလျှင် Browser ၏ Print dialog တွင် “Save as PDF” ကို ရွေးပါ။',
    ),
  ],
  '/performance/my-kpis': [
    b(
      'Your scorecards, newest period first: status, number of KPIs, the overall score and rating once the server has scored it, and the submission date. A Late chip marks a submission after the period’s due date. Without a linked employee record the page says so.',
      'သင့် Scorecard များ (နောက်ဆုံး Period အရင်) — Status၊ KPI အရေအတွက်၊ Server က တွက်ပြီးနောက် စုစုပေါင်းရမှတ်နှင့် Rating၊ Submit လုပ်သည့်ရက်။ Period ၏ Due date ကျော်ပြီး Submit လုပ်လျှင် Late ဟု ပြသည်။ ဝန်ထမ်းမှတ်တမ်းနှင့် မချိတ်ထားလျှင် ထိုသို့ ပြောပြသည်။',
    ),
    b(
      'Open a scorecard to work on it: each KPI shows its target, weight and scoring. Enter the actual (Yes/No for a yes-or-no KPI, a rating on its scale, otherwise a number) and a comment, then Save draft. The achievement and weighted score shown are the server’s; the line under an input while typing is only a preview.',
      'Scorecard ကိုဖွင့်ပြီး လုပ်ဆောင်ပါ — KPI တစ်ခုစီတွင် Target၊ Weight နှင့် တွက်နည်း ပြသည်။ Actual ကို ထည့်ပါ (Yes/No KPI အတွက် Yes/No၊ Rating KPI အတွက် ၎င်း၏ Scale အတွင်း Rating၊ ကျန်သည် ဂဏန်း) နှင့် Comment ရေးပြီး Save draft လုပ်ပါ။ ပြသော Achievement နှင့် Weighted score သည် Server ၏ ရလဒ်ဖြစ်ပြီး ရိုက်နေစဉ် Input အောက်ရှိ စာကြောင်းသည် Preview သာ ဖြစ်သည်။',
    ),
    b(
      'Evidence: add up to 5 files per KPI (PDF, PNG, JPEG, Word .docx or Excel .xlsx, up to 10 MB each), download or remove them. Files are always downloaded, never opened in the page.',
      'Evidence — KPI တစ်ခုလျှင် ဖိုင် ၅ ခုအထိ (PDF၊ PNG၊ JPEG၊ Word .docx သို့ Excel .xlsx၊ တစ်ခုလျှင် 10 MB အထိ) ထည့်၊ Download သို့ ဖယ်ရှားနိုင်သည်။ ဖိုင်များကို အမြဲ Download လုပ်ပြီး စာမျက်နှာထဲတွင် မဖွင့်ပါ။',
    ),
    b(
      'Submit when every KPI has a result (a missing one is pointed out on its KPI). After submitting the results are read-only. A returned scorecard shows the reviewer’s reason at the top; correct it and submit again. Once finalized or rejected the page shows the reviewer’s and HR’s feedback.',
      'KPI အားလုံးတွင် ရလဒ်ရှိမှ Submit လုပ်ပါ (မထည့်ရသေးသော KPI ကို ၎င်းနေရာတွင် ပြသည်)။ Submit ပြီးနောက် ရလဒ်များ ဖတ်ရန်သာ ဖြစ်သည်။ ပြန်ပို့ခံရသော Scorecard တွင် Reviewer ၏ အကြောင်းပြချက်ကို အပေါ်ဆုံးတွင် ပြသည် — ပြင်ပြီး ထပ် Submit လုပ်ပါ။ Finalized သို့ Rejected ဖြစ်ပြီးနောက် Reviewer နှင့် HR ၏ Feedback ကို ပြသည်။',
    ),
  ],
  '/performance/reviews': [
    b(
      'Scorecards waiting for you, oldest submission first: as a manager the ones that name you reviewer; as HR every manager-stage scorecard and the Approved ones waiting for the final review (not those you approved). Never your own. Filter by period, department and status, and search by employee.',
      'သင့်ကို စောင့်နေသော Scorecard များ (အစောဆုံး Submit အရင်) — Manager အဖြစ် သင့်ကို Reviewer အဖြစ် သတ်မှတ်ထားသည်များ၊ HR အဖြစ် Manager အဆင့်ရှိ Scorecard အားလုံးနှင့် Final review စောင့်နေသော Approved များ (ကိုယ်တိုင် Approve လုပ်ထားသည်များ မပါ)။ ကိုယ့် Scorecard ကို ဘယ်တော့မှ မပြ။ Period၊ ဌာန၊ Status ဖြင့် စစ်ထုတ်ပြီး ဝန်ထမ်းဖြင့် ရှာနိုင်သည်။',
    ),
    b(
      'Open one to review it: the employee’s results, comments and evidence are read-only. Start review, rate each KPI and the whole scorecard from 1 to 5 with feedback, and Save review notes (no status change).',
      'ဖွင့်ပြီး Review လုပ်ပါ — ဝန်ထမ်း၏ ရလဒ်၊ Comment နှင့် Evidence များ ဖတ်ရန်သာ။ Start review လုပ်၊ KPI တစ်ခုချင်းနှင့် Scorecard တစ်ခုလုံးကို 1 မှ 5 အထိ Rating နှင့် Feedback ပေးပြီး Save review notes လုပ်ပါ (Status မပြောင်း)။',
    ),
    b(
      'Approve (the confirmation says whether it finalizes at once or goes to HR), Return for revision or Reject; return and reject need a reason. In the HR final review HR can Finalize, Return or Reject, but not the person who approved it. Nobody reviews their own scorecard.',
      'Approve (အတည်ပြုချက်တွင် ချက်ချင်း Finalize ဖြစ်မလား HR ထံ သွားမလား ပြောပြသည်)၊ Return for revision သို့ Reject — Return နှင့် Reject တွင် အကြောင်းပြချက် လိုသည်။ HR Final review တွင် HR က Finalize၊ Return သို့ Reject လုပ်နိုင်သော်လည်း Approve လုပ်ခဲ့သူ မလုပ်ရ။ ကိုယ့် Scorecard ကို ကိုယ်တိုင် Review မလုပ်ရ။',
    ),
    b(
      'If someone else changed the scorecard meanwhile, you see the server’s message and a Reload button. The buttons are a convenience: the server decides who may act.',
      'အခြားသူက ကြားထဲတွင် ပြောင်းထားလျှင် Server ၏ စာနှင့် Reload ခလုတ်ကို ပြသည်။ ခလုတ်များသည် အဆင်ပြေစေရန်သာ ဖြစ်ပြီး မည်သူ လုပ်ဆောင်ခွင့်ရှိသည်ကို Server က ဆုံးဖြတ်သည်။',
    ),
  ],
  '/performance/assignments': [
    b(
      'Every scorecard with its period, department, status, number of KPIs, total weight, overall score and reviewer. Filter by period, department or status and search by employee code or name. A total weight that is not 100% is highlighted.',
      'Period၊ ဌာန၊ Status၊ KPI အရေအတွက်၊ Weight စုစုပေါင်း၊ စုစုပေါင်းရမှတ်နှင့် Reviewer ပါသော Scorecard စာရင်း။ Period၊ ဌာန သို့ Status ဖြင့် စစ်ထုတ်ပြီး ဝန်ထမ်း Code သို့ အမည်ဖြင့် ရှာနိုင်သည်။ Weight စုစုပေါင်း ၁၀၀% မဟုတ်လျှင် အရောင်ဖြင့် ပြထားသည်။',
    ),
    b(
      'Assign KPIs: choose a Draft or Active period and an employee, add KPIs from the active templates (department-specific ones only for that department) and adjust each target and weight. The running total cannot exceed 100%. The message says whether a scorecard was created or KPIs were added to the one in Planning.',
      'Assign KPIs — Draft သို့ Active Period နှင့် ဝန်ထမ်းကိုရွေးပြီး Active Template များမှ KPI ထည့်ကာ Target နှင့် Weight ကို တစ်ခုချင်းပြင်နိုင်သည် (ဌာနအလိုက် Template များသည် ထိုဌာနအတွက်သာ)။ Weight စုစုပေါင်း ၁၀၀% မကျော်ရ။ Scorecard အသစ် ဖန်တီးသလား၊ Planning ရှိ Scorecard ထဲ KPI ထပ်ထည့်သလား စာက ပြောပြသည်။',
    ),
    b(
      'Bulk assign: the same KPIs for a whole department, branch, position or job grade, or for hand-picked employees. The result lists who was assigned (with the number of KPIs) and who was skipped, with the reason.',
      'Bulk assign — ဌာန၊ Branch၊ Position၊ Job Grade တစ်ခုလုံး သို့ ရွေးထားသော ဝန်ထမ်းများကို KPI တူများ ပေးသည်။ ရလဒ်တွင် Assign ပြီးသူများ (KPI အရေအတွက်နှင့်) နှင့် ကျော်သွားသူများကို အကြောင်းပြချက်နှင့်တကွ ပြသည်။',
    ),
    b(
      'Open a row to see the plan: edit or remove a KPI, add more, delete the scorecard or Finalize plan (enabled only at exactly 100%; the confirmation names the reviewer, the current manager). Past Planning the plan is read-only. If someone else changed it meanwhile, you see the server’s message and a Reload button.',
      'အတန်းတစ်ခုကိုဖွင့်ပြီး Plan ကိုကြည့်ပါ — KPI ပြင်/ဖယ်ရှား၊ ထပ်ထည့်၊ Scorecard ဖျက် သို့ Finalize plan (၁၀၀% အတိအကျတွင်သာ နှိပ်နိုင်ပြီး အတည်ပြုချက်တွင် Reviewer ဖြစ်မည့် လက်ရှိ Manager အမည်ပါသည်)။ Planning ကျော်လျှင် Plan သည် ဖတ်ရန်သာ။ အခြားသူက ကြားထဲတွင် ပြောင်းထားလျှင် Server ၏ စာနှင့် Reload ခလုတ်ကို ပြသည်။',
    ),
  ],
  '/performance/templates': [
    b(
      'Define a reusable KPI: code, name, category, an optional department, scoring method, measurement type, direction, unit, frequency, default target and weight.',
      'ပြန်သုံးနိုင်သော KPI ကို သတ်မှတ်သည် — Code၊ အမည်၊ Category၊ ရွေးချယ်နိုင်သော ဌာန၊ Scoring Method၊ Measurement type၊ Direction၊ ယူနစ်၊ Frequency၊ ပုံသေ Target နှင့် Weight။',
    ),
    b(
      'The form adapts: Rating Based asks for the maximum rating and fixes the type to Rating; Boolean fixes the type to Boolean and the target to 1; Range Based asks for the range and fixes the direction; Capped Percentage asks for the maximum achievement; Lower Is Better needs one too, because a result of 0 scores the cap.',
      'Form သည် ရွေးချယ်မှုအလိုက် ပြောင်းသည် — Rating Based တွင် Maximum rating တောင်းပြီး Type ကို Rating သတ်မှတ်၊ Boolean တွင် Type ကို Boolean နှင့် Target ကို 1 သတ်မှတ်၊ Range Based တွင် Range တောင်းပြီး Direction ကို သတ်မှတ်၊ Capped Percentage တွင် Maximum achievement တောင်းသည်။ Lower Is Better တွင်လည်း လိုသည် — ရလဒ် 0 သည် Cap ရမှတ်ကို ရသောကြောင့်။',
    ),
    b(
      'The rules are checked as you type; the server has the final say. A template with assigned KPIs cannot be deleted: deactivate it instead. Editing a template never changes KPIs already assigned.',
      'ရိုက်နေစဉ် စည်းကမ်းများကို စစ်ပေးပြီး နောက်ဆုံးဆုံးဖြတ်သူမှာ Server ဖြစ်သည်။ Assign ပြီးသော KPI ရှိသည့် Template ကို ဖျက်၍မရ — Deactivate လုပ်ပါ။ Template ကိုပြင်လည်း Assign ပြီးသော KPI များ မပြောင်းပါ။',
    ),
  ],
  '/performance/categories': [
    b(
      'Add, edit, activate and deactivate KPI categories. The code and the name must be unique.',
      'KPI Category များကို ထည့်/ပြင်/Activate/Deactivate လုပ်သည်။ Code နှင့် အမည် မထပ်ရ။',
    ),
    b(
      'A category that templates use cannot be deleted; the message says how many use it. Deactivate it instead.',
      'Template များ သုံးနေသော Category ကို ဖျက်၍မရ — ဘယ်နှခု သုံးနေသည်ကို စာက ပြောပြသည်။ Deactivate လုပ်ပါ။',
    ),
  ],
  '/performance/periods': [
    b(
      'Create an evaluation period for a company: name, type, start and end date, an optional submission due date, and whether HR gives a final review.',
      'ကုမ္ပဏီတစ်ခုအတွက် Evaluation Period ဖန်တီးသည် — အမည်၊ အမျိုးအစား၊ စတင်/ပြီးဆုံးရက်၊ ရွေးချယ်နိုင်သော Submission due date နှင့် HR final review လို/မလို။',
    ),
    b(
      'A period moves Draft → Active → Completed → Closed, one confirmed step at a time. Closing makes it final. Draft: every field can change and it can be deleted. Active: only the name and due date. Completed and Closed: read-only.',
      'Period သည် Draft → Active → Completed → Closed အဆင့်ဆင့် ပြောင်းပြီး တစ်ဆင့်ချင်း အတည်ပြုရသည်။ Close လုပ်လျှင် အပြီးသတ်ဖြစ်သည်။ Draft — အကွက်အားလုံးပြင်နိုင်ပြီး ဖျက်နိုင်သည်။ Active — အမည်နှင့် Due date သာ။ Completed နှင့် Closed — ဖတ်ရန်သာ။',
    ),
    b(
      'Periods of the same type cannot overlap in a company; the message names the clashing period. Different types (a Monthly period inside a Quarterly one) are allowed.',
      'ကုမ္ပဏီတစ်ခုတွင် အမျိုးအစားတူ Period များ ရက်မထပ်ရ — ထပ်နေသော Period ကို စာတွင် ဖော်ပြသည်။ အမျိုးအစားမတူလျှင် (Quarterly အတွင်း Monthly) ရသည်။',
    ),
  ],
  '/performance/rating-bands': [
    b(
      'Bands turn an overall score into a label: a score gets the band with the highest minimum score it reaches. Labels and minimum scores must be unique.',
      'Band များသည် စုစုပေါင်းရမှတ်ကို Label အဖြစ် ပြောင်းပေးသည် — ရမှတ်သည် ၎င်းရောက်သော အနည်းဆုံးရမှတ် အမြင့်ဆုံး Band ကို ရသည်။ Label နှင့် အနည်းဆုံးရမှတ် မထပ်ရ။',
    ),
    b(
      'The list is sorted by minimum score, highest first. Deactivate a band to stop using it.',
      'စာရင်းကို အနည်းဆုံးရမှတ် မြင့်ရာမှ နိမ့်ရာသို့ စီထားသည်။ မသုံးလိုတော့သော Band ကို Deactivate လုပ်ပါ။',
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
    b(
      'Export the salary assignments to Excel, or import a file with Employee Code, Effective From, Salary Structure Code and Basic Salary (Data Exchange → Import). Basic salary is hidden in the import preview. Sample figures only.',
      'လစာသတ်မှတ်ချက်များကို Excel ထုတ်နိုင်သည်၊ သို့မဟုတ် Employee Code၊ Effective From၊ Salary Structure Code နှင့် Basic Salary ပါသော ဖိုင်ကို Import လုပ်နိုင်သည် (Data Exchange → Import)။ Import Preview တွင် Basic Salary ကို ဖျောက်ထားသည်။ နမူနာ ကိန်းဂဏန်းများသာ။',
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
  '/my/payslips': [
    b(
      'Your own payslips, newest period first (12 per page). Only periods that are approved or paid appear; nothing from a draft or pending period.',
      'သင့်ကိုယ်ပိုင် စလစ်များ၊ Period အသစ်ဆုံးအရင် (တစ်မျက်နှာလျှင် ၁၂)။ Approved သို့မဟုတ် Paid Period သာပေါ်ပြီး Draft/Pending Period မှ မပါပါ။',
    ),
    b(
      'Open one to see it as a sheet and use Print payslip. Only the masked account number is shown. Every signed-in user has this page; if your account is not linked to an employee it is empty.',
      'ဖွင့်၍ စလစ်စာရွက်အဖြစ်ကြည့်ပြီး Print payslip သုံးနိုင်သည်။ ဖုံးထားသော အကောင့်နံပါတ်သာပြသည်။ ဝင်ရောက်သူတိုင်းတွင် ဤစာမျက်နှာရှိပြီး ဝန်ထမ်းနှင့် မချိတ်ထားလျှင် အလွတ်ဖြစ်သည်။',
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
      'The hiring manager is chosen from current employees (name, code only). A manager sees only the openings they manage.',
      'Hiring manager ကို လက်ရှိဝန်ထမ်းများမှ (အမည်၊ ကုဒ်သာ) ရွေးသည်။ Manager သည် မိမိစီမံသော Opening များကိုသာမြင်ရသည်။',
    ),
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
      'A candidate matching the do-not-rehire list (by email, phone or national ID) shows the badge “Matches do-not-rehire list — contact HR”. National ID is optional, write-only and never shown again; when editing, a blank field keeps the stored value.',
      'Do-not-rehire စာရင်းနှင့် ကိုက်ညီသော Candidate (Email၊ ဖုန်း သို့မဟုတ် မှတ်ပုံတင်) တွင် “Matches do-not-rehire list — contact HR” Badge ပေါ်သည်။ မှတ်ပုံတင်နံပါတ်သည် မဖြည့်လည်းရ၊ ရေးသွင်းရုံသာ၊ ပြန်မပြပါ — ပြင်ချိန်တွင် အလွတ်ထားလျှင် သိမ်းထားသောတန်ဖိုးကို ဖယ်ရှားသည်။',
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
      'A card with the do-not-rehire badge cannot be moved to Selected until an HR Administrator approves with a reason (a dialog appears for that role; others see the refusal message).',
      'Do-not-rehire Badge ပါသော Card ကို HR Administrator က အကြောင်းပြချက်ဖြင့် မခွင့်ပြုမချင်း Selected သို့ မရွှေ့နိုင် (ထို Role အတွက် Dialog ပေါ်ပြီး အခြားသူများတွင် ငြင်းပယ်ချက်မက်ဆေ့ချ်ပေါ်သည်)။',
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
    b(
      'Importable: Employees, Companies, Branches, Departments, Sections, Positions, Job grades, Shift assignments and Salary assignments. Leave applications, leave balances and attendance records are export only.',
      'Import လုပ်နိုင်သည် — Employees၊ Companies၊ Branches၊ Departments၊ Sections၊ Positions၊ Job grades၊ Shift assignments နှင့် Salary assignments။ Leave applications၊ Leave balances နှင့် Attendance records များကို ထုတ်ရန်သာ။',
    ),
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
