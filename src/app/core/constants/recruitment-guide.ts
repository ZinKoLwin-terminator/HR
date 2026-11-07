import { Bilingual, GuideBlock } from './module-guide';

/** Same text in both languages (names, statuses, codes). */
const same = (text: string): Bilingual => ({ en: text, my: text });

/** Long-form Recruitment walkthrough: hiring flow, stage rules and what the system enforces. */
export const RECRUITMENT_GUIDE: readonly GuideBlock[] = [
  {
    type: 'heading',
    text: { en: 'How hiring works', my: 'Recruitment အလုပ်လုပ်ပုံ' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Recruitment follows one vacancy from opening to a selected candidate. Work in this order:',
      my: 'Recruitment က လစ်လပ်ချက်တစ်ခုကို ဖွင့်ခြင်းမှ ကိုယ်စားလှယ်လောင်း ရွေးချယ်ခြင်းအထိ ခြေရာခံပါတယ်။ အောက်ပါအစီအစဉ်အတိုင်း လုပ်ပါ။',
    },
  },
  {
    type: 'flow',
    steps: [
      { en: '① Job Openings', my: '① Job Openings' },
      { en: '② Candidates', my: '② Candidates' },
      { en: '③ Interviews', my: '③ Interviews' },
      { en: '④ Pipeline', my: '④ Pipeline' },
      { en: '⑤ Employees (by hand)', my: '⑤ Employees (လက်ဖြင့်)' },
    ],
  },

  // ───────────── Job openings ─────────────
  {
    type: 'heading',
    text: { en: '① Job Openings', my: '① Job Openings (လစ်လပ်ချက်)' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Create a vacancy as Draft, then move it through these statuses. Each move is only allowed from certain statuses.',
      my: 'လစ်လပ်ချက်ကို Draft အဖြစ်ဖန်တီးပြီး အောက်ပါ Status များသို့ ပြောင်းပါတယ်။ Status တစ်ခုချင်းစီမှ ပြောင်းလို့ရတာ အကန့်အသတ်ရှိပါတယ်။',
    },
  },
  {
    type: 'table',
    head: [
      { en: 'From', my: 'လက်ရှိ Status' },
      { en: 'Can move to', my: 'ပြောင်းနိုင်သည်' },
    ],
    rows: [
      [same('Draft'), same('Open, Closed')],
      [same('Open'), same('On Hold, Closed')],
      [same('On Hold'), same('Open, Closed')],
      [same('Closed'), same('Open (Reopen)')],
      [
        same('Filled'),
        { en: 'Automatic only — cannot be changed by hand', my: 'အလိုအလျောက်သာ — လက်ဖြင့် မပြောင်းနိုင်' },
      ],
    ],
  },
  {
    type: 'list',
    items: [
      {
        en: 'Reopening needs a closing date that has not passed and at least one vacancy still free.',
        my: 'ပြန်ဖွင့်ရန် Closing Date မကျော်သေးရမည်၊ နေရာလစ်လပ်နေရမည်။',
      },
      {
        en: 'Closed and Filled openings cannot be edited. An opening that has applications cannot be deleted.',
        my: 'Closed/Filled Opening ကို မပြင်နိုင်။ လျှောက်လွှာပါသော Opening ကို မဖျက်နိုင်။',
      },
      {
        en: 'An opening becomes Filled automatically when enough candidates are selected, and returns to Open if one of them is rejected later.',
        my: 'လူလုံလောက်စွာ ရွေးပြီးလျှင် Opening သည် Filled ဖြစ်သွားပြီး၊ နောက်မှ တစ်ဦးကို ပယ်ချလျှင် Open သို့ ပြန်ဖြစ်သည်။',
      },
    ],
  },

  // ───────────── Candidates ─────────────
  {
    type: 'heading',
    text: { en: '② Candidates', my: '② Candidates (လျှောက်ထားသူ)' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'The applicant database. Add a person once, then apply them to openings.',
      my: 'လျှောက်ထားသူ ဒေတာဖြစ်ပါတယ်။ လူတစ်ဦးကို တစ်ကြိမ်ထည့်ပြီး Opening များသို့ လျှောက်ထားပေးပါတယ်။',
    },
  },
  {
    type: 'list',
    items: [
      { en: 'Email must be unique.', my: 'Email မထပ်ရ။' },
      {
        en: 'A resume can be attached, but only the file name is kept in this version.',
        my: 'Resume တွဲနိုင်သော်လည်း ဤ Version တွင် ဖိုင်အမည်သာ သိမ်းသည်။',
      },
      {
        en: 'Applications open only for Open openings, one per candidate per opening.',
        my: 'Open ဖြစ်သော Opening တွင်သာ လျှောက်နိုင်၊ Candidate တစ်ဦးလျှင် Opening တစ်ခုအတွက် တစ်ခုသာ။',
      },
      {
        en: 'A candidate who has applications cannot be deleted.',
        my: 'လျှောက်လွှာပါသော Candidate ကို မဖျက်နိုင်။',
      },
    ],
  },

  // ───────────── Interviews ─────────────
  {
    type: 'heading',
    text: { en: '③ Interviews', my: '③ Interviews (အင်တာဗျူး)' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Schedule an interview (round, date, time, duration, mode, interviewer). Only candidates in Screening or Interview can be scheduled.',
        my: 'အင်တာဗျူးချိန်းပါ (Round၊ ရက်၊ အချိန်၊ ကြာချိန်၊ Mode၊ Interviewer)။ Screening သို့မဟုတ် Interview အဆင့်ရှိသူကိုသာ ချိန်းနိုင်သည်။',
      },
      {
        en: 'Scheduling moves a candidate in Screening to the Interview stage automatically.',
        my: 'ချိန်းလိုက်သည်နှင့် Screening ရှိသူသည် Interview အဆင့်သို့ အလိုအလျောက်ရောက်သည်။',
      },
      {
        en: 'An interviewer cannot be double-booked at the same time.',
        my: 'Interviewer ကို တစ်ချိန်တည်း နှစ်နေရာ မချိန်းနိုင်။',
      },
      {
        en: 'Complete an interview with a score (1–5) and a recommendation: Strong hire, Hire or No hire. Completed interviews cannot be deleted.',
        my: 'ရမှတ် (၁–၅) နှင့် အကြံပြုချက် (Strong hire / Hire / No hire) ဖြင့် ပြီးဆုံးစေသည်။ ပြီးသော အင်တာဗျူးကို မဖျက်နိုင်။',
      },
    ],
  },

  // ───────────── Pipeline ─────────────
  {
    type: 'heading',
    text: { en: '④ Pipeline', my: '④ Pipeline (အဆင့်ဆင့် Board)' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Move each application between stages. A stage can only move forward one step, or to Rejected.',
      my: 'လျှောက်လွှာတစ်ခုချင်းစီကို အဆင့်ပြောင်းပါ။ အဆင့်ကို တစ်ဆင့်စီ ရှေ့သို့သာ သို့မဟုတ် Rejected သို့သာ ပြောင်းနိုင်သည်။',
    },
  },
  {
    type: 'code',
    lines: [
      'Applied → Screening → Interview → Selected',
      'Applied / Screening / Interview / Selected → Rejected',
    ],
  },
  {
    type: 'table',
    head: [
      { en: 'Move to', my: 'ပြောင်းရန်' },
      { en: 'What the system requires', my: 'စနစ်က လိုအပ်သည်များ' },
    ],
    rows: [
      [
        same('Screening / Interview'),
        {
          en: 'The opening must be Open or On Hold.',
          my: 'Opening သည် Open သို့မဟုတ် On Hold ဖြစ်ရမည်။',
        },
      ],
      [
        same('Selected'),
        {
          en: 'A completed interview with “Hire” or “Strong hire”, and a vacancy that is still free.',
          my: 'Hire/Strong hire ပါသော အင်တာဗျူးပြီးခြင်းနှင့် လစ်လပ်နေသောနေရာ။',
        },
      ],
      [
        same('Rejected'),
        {
          en: 'A reason is required. Scheduled interviews of that application are cancelled.',
          my: 'အကြောင်းပြချက် လိုသည်။ ချိန်းထားသော အင်တာဗျူးများ ပယ်ဖျက်သွားမည်။',
        },
      ],
    ],
  },
  {
    type: 'paragraph',
    text: {
      en: 'A rejected application is final. A selected application cannot be deleted; reject it first.',
      my: 'Rejected ဖြစ်ပြီးလျှင် ပြန်မပြောင်းနိုင်။ Selected ဖြစ်သော လျှောက်လွှာကို မဖျက်နိုင် — အရင် Reject လုပ်ရမည်။',
    },
  },

  // ───────────── Example ─────────────
  {
    type: 'heading',
    text: { en: 'Example', my: 'ဥပမာ' },
  },
  {
    type: 'steps',
    items: [
      {
        en: 'Create “Accountant” (2 vacancies) and Publish it → Open.',
        my: '“Accountant” (နေရာ ၂ ခု) ဖန်တီးပြီး Publish → Open။',
      },
      {
        en: 'Add the candidate and apply them to the opening → Applied.',
        my: 'Candidate ထည့်ပြီး Opening သို့ လျှောက်ထားပေး → Applied။',
      },
      { en: 'Move to Screening in the Pipeline.', my: 'Pipeline တွင် Screening သို့ ရွှေ့။' },
      {
        en: 'Schedule an interview → the candidate moves to Interview automatically.',
        my: 'အင်တာဗျူးချိန်း → Candidate သည် Interview သို့ အလိုအလျောက်ရောက်။',
      },
      {
        en: 'Complete the interview with score 4 and “Hire”.',
        my: 'ရမှတ် ၄ နှင့် “Hire” ဖြင့် အင်တာဗျူးပြီးဆုံးစေ။',
      },
      {
        en: 'Move to Selected. When both vacancies are selected, the opening becomes Filled.',
        my: 'Selected သို့ ရွှေ့။ နေရာ ၂ ခုလုံး ရွေးပြီးလျှင် Opening သည် Filled ဖြစ်။',
      },
    ],
  },
  {
    type: 'note',
    text: {
      en: 'Selecting does not create the employee. HR registers them in Employees by hand.',
      my: 'Selected ဖြစ်ခြင်းက ဝန်ထမ်းကို မဖန်တီးပါ — HR က Employees တွင် လက်ဖြင့်စာရင်းသွင်းရသည်။',
    },
  },
];
