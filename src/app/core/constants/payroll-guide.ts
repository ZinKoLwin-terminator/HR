import { Bilingual, GuideBlock } from './module-guide';

/** Same text in both languages (codes, numbers, formulas). */
const same = (text: string): Bilingual => ({ en: text, my: text });

/** Long-form Payroll walkthrough: one-time setup, then the monthly calculation. */
export const PAYROLL_GUIDE: readonly GuideBlock[] = [
  // ───────────── Setup ─────────────
  {
    type: 'heading',
    text: { en: 'Salary module setup', my: 'Salary Module Setup ရှင်းလင်းချက်' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Payroll needs three one-time setup steps. After that you only run Payroll Processing each month.',
      my: 'Payroll Module ကို အလုပ်စဖြစ်စေဖို့ တစ်ကြိမ် Setup လုပ်ရမယ့် အဆင့် ၃ ဆင့် ရှိပါတယ်။ ပြီးမှ လစဉ် Payroll Processing ကို လုပ်ပါမယ်။',
    },
  },
  {
    type: 'flow',
    steps: [
      { en: '① Salary Components', my: '① Salary Components' },
      { en: '② Salary Structures', my: '② Salary Structures' },
      { en: '③ Employee Salaries', my: '③ Employee Salaries' },
      { en: '④ Payroll Processing (monthly)', my: '④ Payroll Processing (လစဉ်)' },
    ],
  },

  {
    type: 'heading',
    text: {
      en: '① Salary Components',
      my: '① Salary Components (လစာအစိတ်အပိုင်း)',
    },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Define each earning and deduction. There are three ways to set a value.',
      my: 'ဝင်ငွေ (Earning) နဲ့ နုတ်ငွေ (Deduction) တစ်ခုချင်းစီကို သတ်မှတ်ပါတယ်။ တန်ဖိုးသတ်မှတ်ပုံ ၃ မျိုးရှိပါတယ်။',
    },
  },
  {
    type: 'table',
    head: [
      { en: 'Method', my: 'Calculation' },
      { en: 'Meaning', my: 'အဓိပ္ပာယ်' },
      { en: 'Sample', my: 'နမူနာ' },
    ],
    rows: [
      [
        same('Fixed'),
        { en: 'Fixed amount', my: 'ပုံသေပမာဏ' },
        same('TRANSPORT = 50,000'),
      ],
      [
        same('PercentOfBasic'),
        { en: '% of basic', my: 'Basic ၏ %' },
        same('HOUSING = 10%'),
      ],
      [
        same('PercentOfGross'),
        { en: '% of gross', my: 'Gross ၏ %' },
        same('TAX = 5%, SOCIAL = 2%'),
      ],
    ],
  },
  {
    type: 'paragraph',
    text: {
      en: 'Sample data has 5 earnings (Housing, Transport, Meal, Performance, Executive) and 3 deductions (Tax, Social, Pension — Pension is inactive). Tax and Social are demonstration rates, not real statutory calculations.',
      my: 'Sample data ထဲမှာ Earning ၅ ခု (Housing, Transport, Meal, Performance, Executive) နဲ့ Deduction ၃ ခု (Tax, Social, Pension — Pension က Inactive) ရှိပါတယ်။ Tax နဲ့ Social က Demo နှုန်းသာ ဖြစ်ပြီး တကယ့်ဥပဒေအရ တွက်ချက်မှု မဟုတ်ပါ။',
    },
  },

  {
    type: 'heading',
    text: { en: '② Salary Structures', my: '② Salary Structures (လစာ Template)' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'A structure groups components into a template. Sample data has three, by job grade.',
      my: 'Component များကို အစုအဝေးလုပ်ထားတဲ့ Template ဖြစ်ပါတယ်။ Sample data မှာ Job Grade အလိုက် ၃ မျိုး ရှိပါတယ်။',
    },
  },
  {
    type: 'table',
    head: [
      { en: 'Structure', my: 'Structure' },
      { en: 'Grades', my: 'Grade' },
      { en: 'Components', my: 'ပါဝင်သော Component' },
    ],
    rows: [
      [same('STAFF'), same('G1–G5'), same('Housing, Transport, Meal, Tax, Social')],
      [same('MGMT'), same('G6–G7'), same('Housing, Transport, Performance, Tax, Social')],
      [same('EXEC'), same('G8–G9'), same('Housing, Transport, Executive, Tax, Social')],
    ],
  },

  {
    type: 'heading',
    text: { en: '③ Employee Salaries', my: '③ Employee Salaries (ဝန်ထမ်းအလိုက် လစာ)' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Give each employee a basic salary, a structure and an effective date.',
      my: 'ဝန်ထမ်းတစ်ဦးစီအတွက် Basic လစာ + Structure + Effective Date ကို သတ်မှတ်ပါတယ်။',
    },
  },
  {
    type: 'list',
    items: [
      {
        en: 'A raise is a new row with a new effective date; old rows are never edited, so salary history stays.',
        my: 'လစာတိုးရင် မှတ်တမ်းဟောင်းကို မပြင်ဘဲ Effective Date အသစ်နဲ့ အတန်းအသစ် ထပ်ထည့်ပါတယ်။ ဒါကြောင့် လစာမှတ်တမ်း ကျန်ရစ်ပါတယ်။',
      },
      {
        en: 'Basic must be inside the employee’s Job Grade salary band (min–max); otherwise a validation error is shown.',
        my: 'Basic က ဝန်ထမ်းရဲ့ Job Grade Salary Band (min–max) ထဲမှာ ရှိရပါမယ်။ ပြင်ပရောက်ရင် Validation error ပေးပါတယ်။',
      },
      {
        en: 'Employees with no salary are skipped by payroll, with a warning.',
        my: 'လစာမသတ်မှတ်ထားတဲ့ ဝန်ထမ်းကို Payroll က ကျော်ပြီး သတိပေးပါတယ်။',
      },
    ],
  },

  {
    type: 'heading',
    text: { en: 'Prerequisites', my: 'လိုအပ်တဲ့ ကြိုတင်ပြင်ဆင်မှု' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Employees (employee list, joining/exit dates)',
        my: 'Employees (ဝန်ထမ်းစာရင်း၊ ဝင်/ထွက်ရက်)',
      },
      {
        en: 'Organization → Job Grades (salary bands)',
        my: 'Organization → Job Grades (Salary Band)',
      },
      {
        en: 'Holiday Calendar, Leave and Attendance overtime (for the monthly calculation)',
        my: 'Holiday Calendar, Leave, Attendance OT (လစဉ်တွက်ချက်မှုအတွက်)',
      },
    ],
  },

  // ───────────── Monthly flow ─────────────
  {
    type: 'heading',
    text: {
      en: 'What comes next: how payroll is calculated',
      my: 'Payroll ကို ဘယ်လိုဆက်ပြီး calculate လုပ်လဲ',
    },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Once setup is done, payroll is calculated every month in Payroll Processing. All calculation is done by one function, calculatePay(), for each employee and period.',
      my: 'Setup ပြီးရင် လစဉ် Payroll Processing မှာ တွက်ပါတယ်။ တွက်ချက်မှုအားလုံးကို ဝန်ထမ်းတစ်ဦးစီ၊ Period တစ်ခုစီအတွက် calculatePay() function တစ်ခုတည်းက လုပ်ပါတယ်။',
    },
  },
  {
    type: 'heading',
    text: { en: 'Monthly flow', my: 'လစဉ် လုပ်ရမယ့် Flow' },
  },
  {
    type: 'steps',
    items: [
      {
        en: 'In Payroll Processing, create a period (year + month) → status Draft. Future months cannot be created, and an earlier month must be finished first.',
        my: 'Payroll Processing မှာ Period (ခုနှစ်+လ) ဖန်တီးပါ → status Draft။ အနာဂတ်လကို မဖန်တီးနိုင်ပါ၊ အရင်လ မပြီးရင် နောက်လ မလုပ်နိုင်ပါ။',
      },
      {
        en: 'Click Process → a payslip is calculated for every employee → Pending Approval.',
        my: 'Process နှိပ်ပါ → ဝန်ထမ်းအားလုံးအတွက် Payslip တွက်ချက်ထုတ်ပေးပါတယ် → Pending Approval။',
      },
      {
        en: 'In Payroll Review, check gross / deductions / net per employee.',
        my: 'Payroll Review မှာ ဝန်ထမ်းအလိုက် Gross / Deduction / Net စစ်ပါ။',
      },
      { en: 'Approve → Approved.', my: 'Approve → Approved။' },
      {
        en: 'Mark as paid → Paid. This only records the status; no bank transfer is made. Paid is final.',
        my: 'Mark as Paid → Paid (Status မှတ်ရုံသာ၊ ဘဏ်ငွေမလွှဲပါ)။ Paid ပြီးရင် ပြန်မပြင်နိုင်ပါ။',
      },
    ],
  },
  {
    type: 'paragraph',
    text: {
      en: 'If you find a mistake before Paid, use Return to Draft (with a reason). Payslips are deleted and you can process again.',
      my: 'Paid မတိုင်ခင် အမှားတွေ့ရင် Return to Draft (reason နဲ့) လုပ်ပြီး Payslip ဖျက်ကာ ပြန်တွက်နိုင်ပါတယ်။',
    },
  },

  // ───────────── Calculation ─────────────
  {
    type: 'heading',
    text: { en: 'How pay is calculated', my: 'Calculation အဆင့်ဆင့်' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Each employee and period is calculated in 5 steps.',
      my: 'ဝန်ထမ်းတစ်ဦးစီ၊ Period တစ်ခုစီအတွက် အောက်ပါအဆင့် ၅ ဆင့်အတိုင်း တွက်ပါတယ်။',
    },
  },
  {
    type: 'heading',
    text: {
      en: 'Step 1 — Proration (joiners / leavers)',
      my: 'အဆင့် ၁ — Proration (လဝက်အလုပ်ဝင်/ထွက်)',
    },
  },
  {
    type: 'code',
    lines: ['proration = daysEmployed ÷ periodDays   (calendar days)', 'basic     = Basic × proration'],
  },
  {
    type: 'paragraph',
    text: {
      en: 'An employee with no employed day in the period gets no payslip.',
      my: 'ဝန်ထမ်းက Period တစ်ရက်မှ မရှိရင် Payslip မထုတ်ပါ။',
    },
  },
  {
    type: 'heading',
    text: { en: 'Step 2 — Earnings', my: 'အဆင့် ၂ — Earnings' },
  },
  {
    type: 'list',
    items: [
      { en: 'Fixed → amount × proration', my: 'Fixed → ပမာဏ × proration' },
      {
        en: 'PercentOfBasic → prorated basic × %',
        my: 'PercentOfBasic → (prorated basic) × %',
      },
      {
        en: 'Overtime → hours × hourly rate × 1.5, where hourly rate = full basic ÷ (working days × 8). Only approved overtime counts.',
        my: 'Overtime → နာရီ × နာရီစားနှုန်း × 1.5 ဖြစ်ပြီး နာရီစားနှုန်း = Basic အပြည့် ÷ (အလုပ်ရက် × 8)။ Approved OT ကိုသာ ယူပါတယ်။',
      },
    ],
  },
  {
    type: 'heading',
    text: { en: 'Step 3 — Gross', my: 'အဆင့် ၃ — Gross' },
  },
  { type: 'code', lines: ['Gross = prorated basic + all earnings'] },
  {
    type: 'heading',
    text: { en: 'Step 4 — Deductions', my: 'အဆင့် ၄ — Deductions' },
  },
  {
    type: 'list',
    items: [
      { en: 'Fixed → amount × proration', my: 'Fixed → ပမာဏ × proration' },
      { en: 'PercentOfBasic → basic × %', my: 'PercentOfBasic → basic × %' },
      { en: 'PercentOfGross → gross × %', my: 'PercentOfGross → gross × %' },
      {
        en: 'Unpaid leave → days × (full basic ÷ working days). Only approved leave of an unpaid type counts; a half day is 0.5.',
        my: 'Unpaid leave → ရက် × (Basic အပြည့် ÷ အလုပ်ရက်)။ Approved ဖြစ်ပြီး လစာမဲ့ခွင့် အမျိုးအစားသာ၊ half-day = 0.5 ရက်။',
      },
    ],
  },
  {
    type: 'heading',
    text: { en: 'Step 5 — Net', my: 'အဆင့် ၅ — Net' },
  },
  { type: 'code', lines: ['Net = Gross − Total Deductions'] },

  {
    type: 'heading',
    text: { en: 'Example', my: 'ဥပမာ' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Basic 600,000 (full month), STAFF structure:',
      my: 'Basic 600,000 (လအပြည့်)၊ STAFF structure:',
    },
  },
  {
    type: 'code',
    lines: [
      'Housing 10% = 60,000 · Transport 50,000 · Meal 30,000  →  Gross = 740,000',
      'Tax 5% = 37,000 · Social 2% = 14,800  →  Deductions = 51,800',
      'Net = 688,200',
    ],
  },

  {
    type: 'heading',
    text: { en: 'How working days are counted', my: 'အလုပ်ရက်ရေတွက်ပုံ' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Working days exclude the configured weekend and the holidays in the Holiday Calendar. Overtime and unpaid leave both use this count.',
      my: 'အလုပ်ရက်ရေတွင် သတ်မှတ်ထားတဲ့ ပိတ်ရက်ကိုနှုတ်ပြီး Holiday Calendar ရက်တွေကိုလည်း နှုတ်ပါတယ်။ OT နဲ့ Unpaid leave နှစ်ခုလုံး ဒီအလုပ်ရက်ကို အခြေခံပါတယ်။',
    },
  },
  {
    type: 'note',
    text: {
      en: 'Current calculation uses demonstration rules: no income tax, no social security (SSB), no bank integration. Data is not stored in a database, so it resets on reload.',
      my: 'လက်ရှိ တွက်ချက်မှုက Demo စည်းမျဉ်း ဖြစ်ပါတယ်။ Income Tax၊ SSB၊ ဘဏ်ချိတ်ဆက်မှု မပါသေးပါဘူး။ Data ကို Database မှာ မသိမ်းထားလို့ Reload လုပ်ရင် ပျောက်ပါတယ်။',
    },
  },
];
