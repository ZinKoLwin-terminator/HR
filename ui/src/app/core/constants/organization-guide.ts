import { Bilingual, GuideBlock } from './module-guide';

/** Same text in both languages (names, codes, examples). */
const same = (text: string): Bilingual => ({ en: text, my: text });

/** Long-form Organization walkthrough: setup order, hierarchy rules and links to other modules. */
export const ORGANIZATION_GUIDE: readonly GuideBlock[] = [
  {
    type: 'heading',
    text: {
      en: 'Why set up Organization first',
      my: 'Organization ကို ဘာကြောင့် အရင်ဆုံး Setup လုပ်ရလဲ',
    },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Organization is the foundation of the whole system. When you add an employee you must choose a company, branch, department, position and job grade, so these have to exist first.',
      my: 'Organization သည် စနစ်တစ်ခုလုံး၏ အခြေခံဖြစ်ပါတယ်။ ဝန်ထမ်းတစ်ဦးထည့်ရာတွင် ကုမ္ပဏီ၊ ဘဏ်ခွဲ၊ ဌာန၊ ရာထူးနှင့် Grade ကို ရွေးရမှာဖြစ်လို့ ဤအချက်အလက်များ ကြိုတင်ရှိနေရပါမယ်။',
    },
  },

  {
    type: 'heading',
    text: { en: 'Structure', my: 'ဖွဲ့စည်းပုံ အဆင့်ဆင့်' },
  },
  {
    type: 'code',
    lines: [
      'Company',
      ' └─ Branch',
      '     └─ Department',
      '         └─ Section',
      '',
      'Position  → belongs to a Department',
      'Job Grade → seniority level with a salary band (separate list)',
    ],
  },

  {
    type: 'heading',
    text: { en: 'Setup order', my: 'Setup လုပ်ရမည့် အစီအစဉ်' },
  },
  {
    type: 'flow',
    steps: [
      { en: '① Companies', my: '① Companies' },
      { en: '② Branches', my: '② Branches' },
      { en: '③ Departments', my: '③ Departments' },
      { en: '④ Sections', my: '④ Sections' },
      { en: '⑤ Positions', my: '⑤ Positions' },
      { en: '⑥ Job Grades', my: '⑥ Job Grades' },
    ],
  },
  {
    type: 'table',
    head: [
      { en: 'Screen', my: 'Screen' },
      { en: 'What you define', my: 'သတ်မှတ်ရမည့်အရာ' },
      { en: 'Sample', my: 'နမူနာ' },
    ],
    rows: [
      [
        same('Companies'),
        {
          en: 'Legal entities. Name and code must be unique.',
          my: 'ကုမ္ပဏီ (Legal Entity)။ အမည်နှင့် ကုဒ် မထပ်ရ။',
        },
        same('APEX Group'),
      ],
      [
        same('Branches'),
        { en: 'Offices under one company.', my: 'ကုမ္ပဏီတစ်ခုအောက်ရှိ ရုံးခွဲများ။' },
        { en: 'Yangon HQ, Mandalay', my: 'ရန်ကုန်ရုံးချုပ်၊ မန္တလေးရုံးခွဲ' },
      ],
      [
        same('Departments'),
        { en: 'Departments under a branch.', my: 'ဘဏ်ခွဲအောက်ရှိ ဌာနများ။' },
        same('HR, Finance, IT'),
      ],
      [
        same('Sections'),
        { en: 'Teams inside a department.', my: 'ဌာနအတွင်းရှိ အဖွဲ့ငယ်များ။' },
        same('IT → Development, Support'),
      ],
      [
        same('Positions'),
        { en: 'Job titles of a department.', my: 'ဌာနတစ်ခု၏ ရာထူးအမည်များ။' },
        same('HR Officer, Software Engineer'),
      ],
      [
        same('Job Grades'),
        {
          en: 'Seniority levels with a minimum and maximum salary.',
          my: 'ရာထူးအဆင့်နှင့် အနည်းဆုံး/အများဆုံးလစာ။',
        },
        same('Grade 3: 600,000 – 900,000'),
      ],
    ],
  },
  {
    type: 'paragraph',
    text: {
      en: 'Sample figures only; the demo data may differ.',
      my: 'ဥပမာ ဂဏန်းများသည် ရှင်းပြရန်သာ — Demo ဒေတာနှင့် မတူနိုင်ပါ။',
    },
  },

  {
    type: 'heading',
    text: { en: 'Rules the system enforces', my: 'စနစ်က ကာကွယ်ပေးသော စည်းမျဉ်းများ' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'A branch belongs to exactly one company, and a department must belong to a branch of its own company.',
        my: 'ဘဏ်ခွဲတိုင်းသည် ကုမ္ပဏီတစ်ခုတည်းနှင့်သာဆိုင်ပြီး ဌာနသည် ကိုယ့်ကုမ္ပဏီ၏ ဘဏ်ခွဲအောက်တွင်သာ ရှိနိုင်သည်။',
      },
      {
        en: 'A section must belong to a department of the same branch.',
        my: 'ဌာနခွဲသည် ဌာနနှင့် ဘဏ်ခွဲ ကိုက်ညီရမည်။',
      },
      {
        en: 'A position belongs to one department and can report to another position, but not to itself.',
        my: 'ရာထူးသည် ဌာနတစ်ခုနှင့်သာဆိုင်ပြီး အခြားရာထူးသို့ Report လုပ်နိုင်သည် (ကိုယ်တိုင်ကို မရ)။',
      },
      {
        en: 'A job grade’s maximum salary must not be lower than its minimum.',
        my: 'Job Grade ၏ အများဆုံးလစာသည် အနည်းဆုံးထက် မနိမ့်ရ။',
      },
      {
        en: 'Records that other records refer to cannot be deleted. Deactivate them instead; this protects data from being broken.',
        my: 'အခြား Record များက ကိုးကားထားသော အချက်အလက်ကို ဖျက်၍မရ — Deactivate သာလုပ်နိုင်သည်။ ဒေတာပျက်စီးခြင်းမှ ကာကွယ်သည်။',
      },
      {
        en: 'An inactive company, branch, department or position cannot receive a new employee; employees already assigned may stay.',
        my: 'Inactive ဖြစ်သော ကုမ္ပဏီ၊ ဘဏ်ခွဲ၊ ဌာန၊ ရာထူးသို့ ဝန်ထမ်းအသစ် မခွဲပေးနိုင်၊ ရှိပြီးသား ဝန်ထမ်းများ ဆက်ရှိနိုင်သည်။',
      },
    ],
  },

  {
    type: 'heading',
    text: { en: 'Reporting Structure', my: 'Reporting Structure' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'A read-only view of who reports to whom, built from each employee’s manager. To change it, edit the manager on the employee form.',
      my: 'ဝန်ထမ်းတစ်ဦးစီ၏ Manager အပေါ်အခြေခံသော “ဘယ်သူက ဘယ်သူ့ကို Report လုပ်သည်” ကြည့်ရန် စာမျက်နှာ (ကြည့်ရုံသာ)။ ပြောင်းလိုလျှင် ဝန်ထမ်း Form တွင် Manager ကို ပြင်ပါ။',
    },
  },

  {
    type: 'heading',
    text: { en: 'How other modules use it', my: 'အခြား Module များနှင့် ချိတ်ဆက်ပုံ' },
  },
  {
    type: 'table',
    head: [
      { en: 'Module', my: 'Module' },
      { en: 'Uses Organization for', my: 'Organization ကို ဘယ်လိုသုံးသလဲ' },
    ],
    rows: [
      [
        same('Employees'),
        {
          en: 'Choosing company, branch, department, section, position and grade.',
          my: 'ကုမ္ပဏီ / ဘဏ်ခွဲ / ဌာန / ဌာနခွဲ / ရာထူး / Grade ရွေးရန်။',
        },
      ],
      [
        same('Attendance'),
        { en: 'Department breakdown chart.', my: 'ဌာနအလိုက် တက်ရောက်မှု Chart။' },
      ],
      [same('Leave'), { en: 'Leave policy by job grade.', my: 'Grade အလိုက် ခွင့် Policy။' }],
      [
        same('Payroll'),
        {
          en: 'A grade’s salary band limits the basic salary.',
          my: 'Grade ၏ လစာအကွာအဝေးက Basic လစာကို ကန့်သတ်သည်။',
        },
      ],
      [
        same('Recruitment'),
        { en: 'Linking a job opening to a department.', my: 'Job Opening ကို ဌာနနှင့် ချိတ်ရန်။' },
      ],
      [
        same('Dashboard'),
        { en: 'Employees by department chart.', my: 'ဌာနအလိုက် ဝန်ထမ်းခွဲခြမ်း Chart။' },
      ],
    ],
  },
  {
    type: 'note',
    text: {
      en: 'In the demo build the data resets when the page is reloaded; with the API it is saved. Companies, branches, departments, sections, positions (job titles) and job grades can be exported to Excel and imported from Excel (Data Exchange → Import); rows are matched by the code and parents are given by their codes.',
      my: 'Demo Build တွင် Page Reload လုပ်လျှင် ဒေတာ မူလအတိုင်းပြန်ဖြစ်ပြီး API ဖြင့်သုံးလျှင် သိမ်းဆည်းထားသည်။ Companies၊ Branches၊ Departments၊ Sections၊ Positions (Job Title) နှင့် Job Grades များကို Excel ထုတ်နိုင်ပြီး Excel မှ Import လုပ်နိုင်သည် (Data Exchange → Import)။ အတန်းများကို ကုဒ်ဖြင့်ချိတ်ပြီး မိခင် Record များကို ကုဒ်ဖြင့်ပေးရသည်။',
    },
  },
];
