import { Bilingual, GuideBlock } from './module-guide';

/** Same text in both languages (statuses, tab names). */
const same = (text: string): Bilingual => ({ en: text, my: text });

/** Long-form Employees walkthrough: registering, the profile, statuses and validation rules. */
export const EMPLOYEES_GUIDE: readonly GuideBlock[] = [
  {
    type: 'heading',
    text: { en: 'How employee records work', my: 'ဝန်ထမ်းမှတ်တမ်း အလုပ်လုပ်ပုံ' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Each employee has one profile. Register them once, then salary, attendance and leave link to that profile.',
      my: 'ဝန်ထမ်းတစ်ဦးလျှင် Profile တစ်ခု။ တစ်ကြိမ်စာရင်းသွင်းပြီးလျှင် လစာ၊ တက်ရောက်မှုနှင့် ခွင့်တို့ ထို Profile နှင့် ချိတ်ဆက်ပါတယ်။',
    },
  },
  {
    type: 'flow',
    steps: [
      { en: '① Organization ready', my: '① Organization အဆင်သင့်' },
      { en: '② Add Employee', my: '② Add Employee' },
      { en: '③ Documents', my: '③ Documents' },
      { en: '④ Salary / Shift (separate)', my: '④ လစာ / Shift (သီးခြား)' },
      { en: '⑤ Employee List & Profile', my: '⑤ Employee List နှင့် Profile' },
    ],
  },

  // ───────────── Add ─────────────
  {
    type: 'heading',
    text: { en: '① Add Employee', my: '① Add Employee (ဝန်ထမ်းစာရင်းသွင်းခြင်း)' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Before you start, the company, branch, department, position and job grade must already exist in Organization.',
      my: 'မစတင်မီ ကုမ္ပဏီ၊ ဘဏ်ခွဲ၊ ဌာန၊ ရာထူးနှင့် Job Grade တို့ Organization တွင် ရှိပြီးသားဖြစ်ရမည်။',
    },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Fill in personal details, national ID, contact, emergency contact, bank, employment type, joining date and the chain company → branch → department → section → position, plus the job grade.',
        my: 'ကိုယ်ရေး၊ မှတ်ပုံတင်၊ ဆက်သွယ်ရန်၊ အရေးပေါ်ဆက်သွယ်ရန်၊ ဘဏ်၊ အလုပ်အမျိုးအစား၊ ဝင်ရက်နှင့် ကုမ္ပဏီ→ဘဏ်ခွဲ→ဌာန→ဌာနခွဲ→ရာထူး၊ Job Grade တို့ကို ဖြည့်ပါ။',
      },
      {
        en: 'The employee code is generated automatically (EMP + the next number).',
        my: 'ဝန်ထမ်းကုဒ်ကို အလိုအလျောက်ပေးသည် (EMP + နောက်နံပါတ်)။',
      },
      {
        en: 'New employees start as Probation.',
        my: 'ဝန်ထမ်းအသစ်သည် Probation ဖြင့် စသည်။',
      },
    ],
  },
  {
    type: 'heading',
    text: { en: 'Validation rules', my: 'စစ်ဆေးချက်များ' },
  },
  {
    type: 'table',
    head: [
      { en: 'Field', my: 'အကွက်' },
      { en: 'Rule', my: 'စည်းမျဉ်း' },
    ],
    rows: [
      [
        { en: 'Employee code, work email', my: 'ဝန်ထမ်းကုဒ်၊ Work Email' },
        { en: 'Must be unique.', my: 'မထပ်ရ။' },
      ],
      [
        { en: 'Date of birth', my: 'မွေးသက္ကရာဇ်' },
        {
          en: 'Not in the future; the employee must be at least 18.',
          my: 'အနာဂတ်မရ၊ အသက် ၁၈ နှစ်နှင့်အထက်ဖြစ်ရမည်။',
        },
      ],
      [
        { en: 'Probation end, exit date', my: 'Probation ပြီးရက်၊ ထွက်ရက်' },
        {
          en: 'Cannot be before the joining date.',
          my: 'ဝင်ရက်မတိုင်မီ မဖြစ်ရ။',
        },
      ],
      [
        { en: 'Organization chain', my: 'ဖွဲ့စည်းပုံ ချိတ်ဆက်မှု' },
        {
          en: 'Branch must belong to the company, department to the branch, section to the department, position to the department.',
          my: 'ဘဏ်ခွဲသည် ကုမ္ပဏီနှင့်၊ ဌာနသည် ဘဏ်ခွဲနှင့်၊ ဌာနခွဲနှင့် ရာထူးသည် ဌာနနှင့် သက်ဆိုင်ရမည်။',
        },
      ],
      [
        { en: 'Manager', my: 'Manager' },
        {
          en: 'Must exist and cannot be the employee themselves.',
          my: 'ရှိပြီးသားဖြစ်ရမည်၊ ကိုယ်တိုင်ကို မရွေးရ။',
        },
      ],
      [
        { en: 'Shift, inactive units', my: 'Shift၊ Inactive ဖြစ်သောအရာများ' },
        {
          en: 'An inactive shift, company, branch, department or position cannot be newly assigned.',
          my: 'Inactive Shift၊ ကုမ္ပဏီ၊ ဘဏ်ခွဲ၊ ဌာန၊ ရာထူးကို အသစ်မခွဲပေးနိုင်။',
        },
      ],
    ],
  },
  {
    type: 'note',
    text: {
      en: 'Saving does not create a salary, a shift or a user account. Add those separately in Payroll → Employee Salaries, Attendance → Shift Assignment and Settings → Users.',
      my: 'သိမ်းလိုက်ခြင်းက လစာ၊ Shift၊ User Account မဖန်တီးပါ — Payroll → Employee Salaries၊ Attendance → Shift Assignment၊ Settings → Users တွင် သီးခြားထည့်ရမည်။',
    },
  },

  // ───────────── List ─────────────
  {
    type: 'heading',
    text: { en: '② Employee List', my: '② Employee List' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Search by name or code, filter, sort and page through employees.',
        my: 'အမည်/ကုဒ်ဖြင့် ရှာ၊ Filter၊ စီ၊ စာမျက်နှာခွဲကြည့်နိုင်သည်။',
      },
      {
        en: 'Export the current list to Excel. New employees can also be created or updated through Data Exchange → Import.',
        my: 'လက်ရှိစာရင်းကို Excel ထုတ်နိုင်သည်။ Data Exchange → Import ဖြင့်လည်း ဝန်ထမ်းအသစ်ဖန်တီး/Update လုပ်နိုင်သည်။',
      },
      {
        en: 'Open a profile to see its tabs.',
        my: 'Profile ကိုဖွင့်လျှင် Tab များကို ကြည့်နိုင်သည်။',
      },
    ],
  },

  // ───────────── Profile ─────────────
  {
    type: 'heading',
    text: { en: 'Employee Profile tabs', my: 'Employee Profile Tab များ' },
  },
  {
    type: 'table',
    head: [
      { en: 'Tab', my: 'Tab' },
      { en: 'What it shows', my: 'ပြသည်များ' },
    ],
    rows: [
      [
        same('Overview'),
        {
          en: 'Personal information, contact details, address, emergency contact and bank information.',
          my: 'ကိုယ်ရေးအချက်အလက်၊ ဆက်သွယ်ရန်၊ လိပ်စာ၊ အရေးပေါ်ဆက်သွယ်ရန်နှင့် ဘဏ်အချက်အလက်။',
        },
      ],
      [
        same('Employment'),
        {
          en: 'Employment details, the organization chain (company to position and grade) and direct reports.',
          my: 'အလုပ်အကိုင်အသေးစိတ်၊ ဖွဲ့စည်းပုံ (ကုမ္ပဏီမှ ရာထူးနှင့် Grade)၊ လက်အောက်ဝန်ထမ်းများ။',
        },
      ],
      [
        same('History'),
        { en: 'Employment history events.', my: 'အလုပ်အကိုင် ပြောင်းလဲမှု မှတ်တမ်းများ။' },
      ],
      [same('Documents'), { en: 'Documents of this employee.', my: 'ဤဝန်ထမ်း၏ စာရွက်စာတမ်းများ။' }],
      [
        same('Salary'),
        {
          en: 'Monthly salary (sample) and where the basic sits inside the job grade band.',
          my: 'လစဉ်လစာ (နမူနာ) နှင့် Basic သည် Job Grade Band အတွင်း ဘယ်နေရာရှိသည်ကို ပြသည်။',
        },
      ],
      [same('Attendance'), { en: 'Attendance records.', my: 'တက်ရောက်မှု မှတ်တမ်းများ။' }],
      [
        same('Leave'),
        { en: 'Leave balances and requests.', my: 'ခွင့်လက်ကျန်နှင့် လျှောက်လွှာများ။' },
      ],
    ],
  },

  // ───────────── Status ─────────────
  {
    type: 'heading',
    text: { en: 'Employment status', my: 'Employment Status' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Status values: Active, Probation, On Leave, Suspended, Resigned and Terminated.',
      my: 'Status များ — Active, Probation, On Leave, Suspended, Resigned, Terminated။',
    },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Resigned and Terminated need an exit date, and it cannot be before the joining date.',
        my: 'Resigned/Terminated ဖြစ်လျှင် ထွက်ရက်လိုပြီး ဝင်ရက်မတိုင်မီ မဖြစ်ရ။',
      },
      {
        en: 'Payroll prorates the month of joining and leaving by calendar days, using the joining and exit dates.',
        my: 'Payroll က ဝင်ရက်နှင့် ထွက်ရက်အလိုက် ဝင်/ထွက်လကို ပြက္ခဒိန်ရက်ဖြင့် အချိုးကျ တွက်ချက်သည်။',
      },
    ],
  },

  // ───────────── Documents ─────────────
  {
    type: 'heading',
    text: { en: '③ Employee Documents', my: '③ Employee Documents' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'A library per employee: contract, ID copy, certificates and so on. Upload, filter and delete entries.',
        my: 'ဝန်ထမ်းအလိုက် စာကြည့်တိုက် — စာချုပ်၊ မှတ်ပုံတင်မိတ္တူ၊ လက်မှတ်များ။ Upload လုပ်၊ Filter စစ်၊ ဖျက်နိုင်သည်။',
      },
      {
        en: 'In this version only the file name and details are stored, not the file content.',
        my: 'ဤ Version တွင် ဖိုင်အမည်နှင့် အချက်အလက်သာ သိမ်းပြီး ဖိုင်အကြောင်းအရာကို မသိမ်းပါ။',
      },
    ],
  },

  // ───────────── Delete ─────────────
  {
    type: 'heading',
    text: { en: 'Deleting an employee', my: 'ဝန်ထမ်းကို ဖျက်ခြင်း' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'An employee who is the manager of others cannot be deleted. Reassign their direct reports first.',
      my: 'တခြားသူများ၏ Manager ဖြစ်နေသော ဝန်ထမ်းကို ဖျက်၍မရ။ ၎င်း၏ လက်အောက်ဝန်ထမ်းများကို အရင် ပြောင်းပေးပါ။',
    },
  },
];
