import { Bilingual, GuideBlock } from './module-guide';

/** Same text in both languages (names, statuses, codes). */
const same = (text: string): Bilingual => ({ en: text, my: text });

/** Long-form Leave Management walkthrough: setup, request lifecycle, balance rules and links to other modules. */
export const LEAVE_GUIDE: readonly GuideBlock[] = [
  {
    type: 'heading',
    text: { en: 'How leave works', my: 'Leave Management အလုပ်လုပ်ပုံ' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Define the leave types and policies once. After that employees apply, approvers decide, and balances and the calendar update on their own.',
      my: 'ခွင့်အမျိုးအစားနှင့် Policy ကို တစ်ကြိမ်သတ်မှတ်ပါ။ ပြီးရင် ဝန်ထမ်းက လျှောက်၊ ခွင့်ပြုသူက ဆုံးဖြတ်ပြီး လက်ကျန်နှင့် ပြက္ခဒိန်က အလိုအလျောက် ပြောင်းလဲပါတယ်။',
    },
  },
  {
    type: 'flow',
    steps: [
      { en: '① Leave Types', my: '① Leave Types' },
      { en: '② Leave Policies', my: '② Leave Policies' },
      { en: '③ Leave Applications', my: '③ Leave Applications' },
      { en: '④ Leave Approvals', my: '④ Leave Approvals' },
      { en: '⑤ Balances & Calendar', my: '⑤ Balances နှင့် Calendar' },
    ],
  },

  // ───────────── Setup ─────────────
  {
    type: 'heading',
    text: { en: 'Setup', my: 'Setup လုပ်ပုံ' },
  },
  {
    type: 'heading',
    text: { en: '① Leave Types', my: '① Leave Types (ခွင့်အမျိုးအစား)' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Define each type (annual, casual, sick, maternity, unpaid…) with default days, paid or unpaid, maximum consecutive days, whether a document is required and any gender restriction.',
        my: 'ခွင့်အမျိုးအစားတစ်ခုချင်းစီကို သတ်မှတ်ပါ (နှစ်ပတ်လည်၊ ရံဖန်ရံခါ၊ နာမကျန်း၊ မီးဖွား၊ လစာမဲ့…) — ပုံသေရက်၊ လစာရ/မရ၊ ဆက်တိုက်ယူနိုင်ရက်၊ စာရွက်လို/မလို၊ ကျား/မ ကန့်သတ်ချက်။',
      },
      {
        en: 'Maternity leave is for female employees and paternity leave for male employees.',
        my: 'မီးဖွားခွင့်သည် အမျိုးသမီးများအတွက်၊ Paternity ခွင့်သည် အမျိုးသားများအတွက်။',
      },
      {
        en: 'If carry-forward is on, the maximum carry-forward days cannot exceed the yearly entitlement.',
        my: 'Carry-forward ဖွင့်ထားလျှင် အများဆုံး Carry-forward ရက်သည် တစ်နှစ်ရထိုက်ရက်ထက် မကျော်ရ။',
      },
      {
        en: 'Whether a type is paid decides if approved leave becomes a payroll deduction (unpaid only).',
        my: 'ခွင့်အမျိုးအစား လစာရ/မရ က ခွင့်ပြုပြီးခွင့်ကို Payroll တွင် နုတ်မလား ဆုံးဖြတ်သည် (လစာမဲ့သာ နုတ်သည်)။',
      },
    ],
  },
  {
    type: 'heading',
    text: { en: '② Leave Policies', my: '② Leave Policies (ခွင့်ရက် Policy)' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Decide how many days an employee gets, by leave type, job grade, employment type and minimum months of service.',
        my: 'ခွင့်အမျိုးအစား၊ Job Grade၊ အလုပ်အမျိုးအစားနှင့် အနည်းဆုံးအလုပ်သက်လအလိုက် ရက်အရေအတွက် သတ်မှတ်ပါ။',
      },
      {
        en: 'Only active policies that match the employee count. The most specific one wins: a job-grade match beats an employment-type match, which beats a general policy.',
        my: 'Active Policy များထဲက ဝန်ထမ်းနှင့်ကိုက်သည့်ဟာသာ ယူသည်။ အတိကျဆုံးကို သုံးသည် — Job Grade ကိုက်လျှင် အလုပ်အမျိုးအစားကိုက်သည်ထက်၊ ၎င်းက ယေဘုယျ Policy ထက် သာသည်။',
      },
      {
        en: 'A policy is skipped until the employee reaches its minimum months of service. If policies exist but none is satisfied, the entitlement is 0; if none exist, the leave type’s default days apply.',
        my: 'အနည်းဆုံးအလုပ်သက်လ မပြည့်သေးလျှင် ထို Policy ကို ကျော်သည်။ Policy ရှိသော်လည်း တစ်ခုမှမကိုက်လျှင် ရထိုက်ရက် ၀၊ Policy လုံးဝမရှိလျှင် ခွင့်အမျိုးအစား၏ ပုံသေရက်ကို သုံးသည်။',
      },
      {
        en: 'A policy can accrue monthly: days × month ÷ 12, in half-day steps.',
        my: 'Policy တစ်ခုသည် လစဉ်စုဆောင်းနိုင်သည် — ရက် × လ ÷ ၁၂၊ တစ်ဝက်နေ့ အဆင့်ဖြင့်။',
      },
    ],
  },

  // ───────────── Applying ─────────────
  {
    type: 'heading',
    text: { en: 'Request lifecycle', my: 'ခွင့်လျှောက်လွှာ အဆင့်ဆင့်' },
  },
  {
    type: 'code',
    lines: [
      'Draft → Submitted → Approved',
      '             └────→ Rejected   (a reason is required)',
      'Submitted / Approved → Cancelled   (Approved only before it starts)',
    ],
  },
  {
    type: 'heading',
    text: { en: '③ Leave Applications', my: '③ Leave Applications (ခွင့်တင်ခြင်း)' },
  },
  {
    type: 'steps',
    items: [
      {
        en: 'Create a request: leave type, dates (or a half day), reason, and a document if the type requires one. Save it as Draft.',
        my: 'ခွင့်လျှောက်လွှာဖန်တီးပါ — ခွင့်အမျိုးအစား၊ ရက်စွဲ (သို့ တစ်ဝက်ခွင့်)၊ အကြောင်းပြချက်၊ လိုအပ်လျှင် စာရွက်။ Draft အဖြစ်သိမ်းပါ။',
      },
      {
        en: 'Submit it. The system checks the rules below.',
        my: 'Submit လုပ်ပါ။ စနစ်က အောက်ပါစည်းမျဉ်းများကို စစ်ဆေးသည်။',
      },
      {
        en: 'Only a Draft can be edited or deleted. After Submit, use Cancel instead.',
        my: 'Draft ကိုသာ ပြင်/ဖျက်နိုင်သည်။ Submit ပြီးလျှင် Cancel လုပ်ရသည်။',
      },
    ],
  },
  {
    type: 'table',
    head: [
      { en: 'Check', my: 'စစ်ဆေးချက်' },
      { en: 'What happens', my: 'ဖြစ်ပုံ' },
    ],
    rows: [
      [
        { en: 'Days counted', my: 'ရေတွက်သောရက်' },
        {
          en: 'Working days only. Weekends and public holidays are not counted; dates with no working day are refused.',
          my: 'အလုပ်ရက်သာ ရေတွက်သည်။ စနေ၊ တနင်္ဂနွေ၊ အားလပ်ရက် မပါ။ အလုပ်ရက်မပါသော ရက်စွဲကို ပယ်သည်။',
        },
      ],
      [
        { en: 'Dates', my: 'ရက်စွဲ' },
        {
          en: 'End date on or after start date. A half day must start and end on the same date. Cannot start before the joining date.',
          my: 'ဆုံးရက်သည် စရက်နောက်ဖြစ်ရမည်။ တစ်ဝက်ခွင့်သည် တစ်ရက်တည်းဖြစ်ရမည်။ ဝင်ရက်မတိုင်မီ မစနိုင်။',
        },
      ],
      [
        { en: 'Overlap', my: 'ထပ်နေခြင်း' },
        {
          en: 'Refused if it overlaps another request of the same employee.',
          my: 'ထိုဝန်ထမ်း၏ အခြားလျှောက်လွှာနှင့် ထပ်လျှင် ပယ်သည်။',
        },
      ],
      [
        { en: 'Balance', my: 'လက်ကျန်' },
        {
          en: 'Refused if the days exceed the available balance or the maximum consecutive days.',
          my: 'ရက်သည် လက်ကျန် သို့ ဆက်တိုက်ယူနိုင်ရက် ကျော်လျှင် ပယ်သည်။',
        },
      ],
      [
        { en: 'Document', my: 'စာရွက်' },
        {
          en: 'A type that requires a document cannot be submitted without one.',
          my: 'စာရွက်လိုသော ခွင့်အမျိုးအစားကို စာရွက်မပါဘဲ Submit မလုပ်နိုင်။',
        },
      ],
    ],
  },
  {
    type: 'heading',
    text: { en: '④ Leave Approvals', my: '④ Leave Approvals (ခွင့်ပြုခြင်း)' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'The queue lists submitted requests waiting for a decision. Approve, or reject with a reason.',
        my: 'ဆုံးဖြတ်ချက်စောင့်နေသော Submit လုပ်ပြီးလျှောက်လွှာများကို ပြသည်။ ခွင့်ပြု သို့ အကြောင်းပြချက်ဖြင့် ပယ်ချပါ။',
      },
      {
        en: 'You cannot decide your own request.',
        my: 'ကိုယ့်လျှောက်လွှာကို ကိုယ်မဆုံးဖြတ်နိုင်။',
      },
      {
        en: 'A submitted or approved request can be cancelled. Approved leave that has already started cannot be cancelled.',
        my: 'Submit လုပ်ပြီး သို့ ခွင့်ပြုပြီးလျှောက်လွှာကို Cancel လုပ်နိုင်သည်။ စပြီးသွားသော ခွင့်ပြုပြီးခွင့်ကို Cancel မလုပ်နိုင်။',
      },
    ],
  },

  // ───────────── Balance ─────────────
  {
    type: 'heading',
    text: { en: '⑤ Balances and Calendar', my: '⑤ Balances နှင့် Calendar' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Balances are calculated, never typed in. Leave Balances shows entitled, used, pending and available days per leave type for the year.',
      my: 'လက်ကျန်ကို လက်ဖြင့်မထည့်ရ၊ တွက်ချက်ထားသည်။ Leave Balances တွင် ခွင့်အမျိုးအစားအလိုက် ရထိုက်ရက်၊ သုံးပြီးရက်၊ စောင့်ဆိုင်းရက်နှင့် လက်ကျန်ရက်ကို ပြသည်။',
    },
  },
  {
    type: 'code',
    lines: [
      'Entitled  = days of the chosen policy (or the leave type default if no policy matches)',
      '            monthly accrual: days × month ÷ 12, in half-day steps',
      'Available = Entitled − Approved days − Submitted (pending) days',
    ],
  },
  {
    type: 'paragraph',
    text: {
      en: 'Leave Calendar is a monthly team calendar of who is away, with holidays marked. Use it to plan cover before approving.',
      my: 'Leave Calendar သည် ဘယ်သူ ခွင့်ယူထားသည်ကို အားလပ်ရက်များနှင့်တကွ ပြသော လစဉ်အဖွဲ့ပြက္ခဒိန်ဖြစ်သည်။ ခွင့်မပြုမီ အစားထိုးစီစဉ်ရန် သုံးပါ။',
    },
  },

  // ───────────── Links ─────────────
  {
    type: 'heading',
    text: { en: 'What approval changes elsewhere', my: 'ခွင့်ပြုပြီးနောက် တခြား Module များတွင် ဖြစ်သည်များ' },
  },
  {
    type: 'table',
    head: [
      { en: 'Module', my: 'Module' },
      { en: 'Effect of an approved request', my: 'ခွင့်ပြုပြီးခွင့်၏ သက်ရောက်မှု' },
    ],
    rows: [
      [
        same('Attendance'),
        {
          en: 'Past working days in the leave range become On Leave. Check-in is blocked on a leave day.',
          my: 'ခွင့်ကာလအတွင်း ယနေ့အထိ ကျော်ပြီးသော အလုပ်ရက်များ On Leave ဖြစ်သည်။ ခွင့်ရက်တွင် Check-in မရ။',
        },
      ],
      [
        same('Payroll'),
        {
          en: 'Unpaid leave becomes an “Unpaid leave” deduction: days × (full basic ÷ working days). Paid leave does not change pay.',
          my: 'လစာမဲ့ခွင့်သည် “Unpaid leave” နုတ်ငွေဖြစ်သည် — ရက် × (Basic အပြည့် ÷ အလုပ်ရက်)။ လစာရခွင့်က လစာကို မပြောင်း။',
        },
      ],
    ],
  },

  // ───────────── Example ─────────────
  {
    type: 'heading',
    text: { en: 'Example', my: 'ဥပမာ' },
  },
  {
    type: 'code',
    lines: [
      'Annual leave policy: 12 days a year, paid in full (no monthly accrual)',
      'Approved 3 days · Submitted (pending) 2 days',
      'Available = 12 − 3 − 2 = 7 days',
      'A new request for 8 working days → refused (insufficient balance)',
    ],
  },
  {
    type: 'note',
    text: {
      en: 'Approved leave affects Attendance and Payroll automatically. Cancel a leave before it starts if plans change.',
      my: 'ခွင့်ပြုပြီးခွင့်က Attendance နှင့် Payroll ကို အလိုအလျောက် သက်ရောက်သည်။ အစီအစဉ်ပြောင်းလျှင် မစမီ Cancel လုပ်ပါ။',
    },
  },
];
