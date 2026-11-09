import { Bilingual, GuideBlock } from './module-guide';

/** Same text in both languages (names, statuses, codes). */
const same = (text: string): Bilingual => ({ en: text, my: text });

/** Long-form Attendance walkthrough: setup, daily records, status rules, overtime and corrections. */
export const ATTENDANCE_GUIDE: readonly GuideBlock[] = [
  {
    type: 'heading',
    text: { en: 'How attendance works', my: 'Attendance အလုပ်လုပ်ပုံ' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Set up shifts once and assign them to employees. After that the system builds each day’s record from the punches, the shift rules, holidays and approved leave.',
      my: 'Shift ကို တစ်ကြိမ်သတ်မှတ်ပြီး ဝန်ထမ်းများကို ခွဲပေးပါ။ ပြီးရင် Punch၊ Shift စည်းမျဉ်း၊ အားလပ်ရက်နှင့် ခွင့်ပြုပြီးခွင့်တို့မှ နေ့စဉ်မှတ်တမ်းကို စနစ်က ဖန်တီးပေးပါတယ်။',
    },
  },
  {
    type: 'flow',
    steps: [
      { en: '① Shift Management', my: '① Shift Management' },
      { en: '② Shift Assignment', my: '② Shift Assignment' },
      { en: '③ Check In / Out', my: '③ Check In / Out' },
      { en: '④ Records & Late/Early', my: '④ Records နှင့် Late/Early' },
      { en: '⑤ Overtime / Adjustments', my: '⑤ Overtime / Adjustments' },
    ],
  },

  // ───────────── Setup ─────────────
  {
    type: 'heading',
    text: { en: 'Setup', my: 'Setup လုပ်ပုံ' },
  },
  {
    type: 'heading',
    text: { en: '① Shift Management', my: '① Shift Management (Shift သတ်မှတ်)' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Define each shift: start and end time, break minutes, grace minutes, work days and whether it is a night shift.',
        my: 'Shift တစ်ခုချင်းစီကို သတ်မှတ်ပါ — စ/ဆုံးချိန်၊ အနားယူမိနစ်၊ Grace မိနစ်၊ အလုပ်ရက်များနှင့် ညဆိုင်းဟုတ်/မဟုတ်။',
      },
      {
        en: 'These settings drive the Late, Half Day and overtime calculations.',
        my: 'ဤ Setting များက Late၊ Half Day နှင့် OT တွက်ချက်မှုကို ဆုံးဖြတ်ပါတယ်။',
      },
      {
        en: 'An inactive shift cannot be newly assigned.',
        my: 'Inactive Shift ကို အသစ်မခွဲပေးနိုင်ပါ။',
      },
    ],
  },
  {
    type: 'heading',
    text: { en: '② Shift Assignment', my: '② Shift Assignment (Shift ခွဲပေး)' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Assign a shift to employees, one at a time or in bulk.',
        my: 'ဝန်ထမ်းများကို Shift ခွဲပေးပါ (တစ်ဦးချင်း သို့ အစုလိုက်)။',
      },
      {
        en: 'A change applies to future punches only; existing records keep their old shift.',
        my: 'ပြောင်းလဲမှုသည် အနာဂတ် Punch များအတွက်သာ — ရှိပြီးသားမှတ်တမ်းများသည် Shift အဟောင်းအတိုင်း ကျန်ပါတယ်။',
      },
    ],
  },

  // ───────────── Daily ─────────────
  {
    type: 'heading',
    text: { en: 'Daily use', my: 'နေ့စဉ်အသုံးပြုပုံ' },
  },
  {
    type: 'heading',
    text: { en: '③ Check In / Out', my: '③ Check In / Out' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Clock in and out (simulated). The system creates the day’s record from the employee’s shift.',
        my: 'အလုပ်ဝင်/ထွက် မှတ်တမ်း (နမူနာ)။ ဝန်ထမ်း၏ Shift အလိုက် ထိုနေ့ မှတ်တမ်းကို စနစ်က ဖန်တီးသည်။',
      },
      {
        en: 'Blocked on a day of approved leave. No double check-in, and check-out needs a check-in first.',
        my: 'ခွင့်ပြုပြီးနေ့တွင် မရ။ Check-in နှစ်ကြိမ်မရ၊ Check-out ရန် Check-in အရင်လို။',
      },
      {
        en: 'A punch on a weekend or holiday counts as Present, with a remark.',
        my: 'စနေ/တနင်္ဂနွေ/အားလပ်ရက်တွင် Punch လုပ်လျှင် မှတ်ချက်နှင့်အတူ Present ဖြစ်သည်။',
      },
    ],
  },
  {
    type: 'heading',
    text: {
      en: '④ Records, Dashboard and Late & Early',
      my: '④ Records၊ Dashboard နှင့် Late & Early',
    },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Attendance Records: daily check-in/out, worked time, late and early minutes, and status.',
        my: 'Attendance Records — နေ့စဉ် ဝင်/ထွက်အချိန်၊ အလုပ်လုပ်ချိန်၊ နောက်ကျ/စောပြန်မိနစ်နှင့် Status။',
      },
      {
        en: 'Dashboard: today’s present, late, on leave and absent counts with a department chart. On a weekend or holiday it shows the last working day.',
        my: 'Dashboard — ယနေ့ ရောက်သူ၊ နောက်ကျသူ၊ ခွင့်ယူသူ၊ ပျက်ကွက်သူနှင့် ဌာနအလိုက် Chart။ ပိတ်ရက်ဖြစ်လျှင် နောက်ဆုံးအလုပ်လုပ်သည့်နေ့ကို ပြသည်။',
      },
      {
        en: 'Late & Early: a filtered list of late arrivals and early departures so supervisors can follow up.',
        my: 'Late & Early — နောက်ကျ/စောစောပြန် ချွင်းချက်များကိုသာ စစ်ထုတ်ပြသည့်စာရင်း၊ Supervisor များ ဆက်လက်ဆောင်ရွက်ရန်။',
      },
      {
        en: 'Work Schedules: a read-only weekly grid built from shifts, holidays and leave. Change those to change the schedule.',
        my: 'Work Schedules — Shift၊ အားလပ်ရက်၊ ခွင့်မှ တည်ဆောက်သော ကြည့်ရုံသာ အပတ်စဉ် Grid။ ပြောင်းလိုလျှင် ထိုအရာများကို ပြင်ပါ။',
      },
    ],
  },

  // ───────────── Status rules ─────────────
  {
    type: 'heading',
    text: { en: 'How the status is decided', my: 'Status သတ်မှတ်ပုံ' },
  },
  {
    type: 'table',
    head: [
      { en: 'Status', my: 'Status' },
      { en: 'When', my: 'ဖြစ်သည့်အခါ' },
    ],
    rows: [
      [
        same('Present'),
        {
          en: 'Checked in on time (within start + grace).',
          my: 'အချိန်မီ ဝင်ခြင်း (စချိန် + Grace အတွင်း)။',
        },
      ],
      [
        same('Late'),
        {
          en: 'Checked in after shift start + grace. Late minutes are counted from the shift start.',
          my: 'Shift စချိန် + Grace ကျော်၍ ဝင်ခြင်း။ နောက်ကျမိနစ်ကို Shift စချိန်မှ ရေတွက်သည်။',
        },
      ],
      [
        same('Half Day'),
        {
          en: 'Worked less than 4 hours after the break (with a check-out).',
          my: 'အနားယူချိန်နုတ်ပြီး ၄ နာရီမပြည့်ခြင်း (Check-out ပါလျှင်)။',
        },
      ],
      [
        same('On Leave'),
        {
          en: 'The day is covered by approved leave.',
          my: 'ထိုနေ့သည် ခွင့်ပြုပြီးခွင့်ဖြင့် ဖုံးလွှမ်းနေခြင်း။',
        },
      ],
      [
        same('Absent'),
        {
          en: 'A working day with no check-in and no leave.',
          my: 'အလုပ်ရက်ဖြစ်ပြီး Check-in မရှိ၊ ခွင့်လည်းမရှိခြင်း။',
        },
      ],
    ],
  },
  {
    type: 'code',
    lines: [
      'Worked      = (check-out − check-in) − break      (night shifts roll into the next day)',
      'Early leave = minutes before the scheduled end',
      'Overtime    = minutes after the scheduled end, only if more than 30',
    ],
  },
  {
    type: 'paragraph',
    text: {
      en: 'Late and absent days do not change pay by themselves; only approved unpaid leave does.',
      my: 'နောက်ကျ/ပျက်ကွက်ခြင်းက လစာကို မပြောင်းပါ — ခွင့်ပြုပြီး လစာမဲ့ခွင့်သာ ပြောင်းသည်။',
    },
  },

  // ───────────── Overtime ─────────────
  {
    type: 'heading',
    text: { en: '⑤ Overtime', my: '⑤ Overtime (အချိန်ပို)' },
  },
  {
    type: 'steps',
    items: [
      {
        en: 'Request overtime for a date with start and end time. It must be 15 minutes to 12 hours and not in the future.',
        my: 'ရက်စွဲတစ်ခုအတွက် စ/ဆုံးအချိန်ဖြင့် OT တောင်းခံပါ။ ၁၅ မိနစ်မှ ၁၂ နာရီအတွင်းဖြစ်ရမည်၊ အနာဂတ်မရ။',
      },
      {
        en: 'One request per employee per date.',
        my: 'ဝန်ထမ်း၊ ရက်စွဲတစ်ခုလျှင် တောင်းခံမှု တစ်ခုသာ။',
      },
      { en: 'Approve or reject it.', my: 'ခွင့်ပြု သို့ ပယ်ချပါ။' },
      {
        en: 'Only approved requests are paid in Payroll, at 1.5× the hourly rate.',
        my: 'ခွင့်ပြုပြီး တောင်းခံမှုများကိုသာ Payroll တွင် နာရီစားနှုန်း ၁.၅ ဆဖြင့် ပေးသည်။',
      },
    ],
  },
  {
    type: 'paragraph',
    text: {
      en: 'Approve overtime before processing payroll. Approving later needs the payroll period returned to Draft.',
      my: 'Payroll Process မလုပ်မီ OT ကို ခွင့်ပြုပါ — နောက်မှခွင့်ပြုလျှင် Payroll Period ကို Draft သို့ပြန်ရမည်။',
    },
  },

  // ───────────── Adjustments ─────────────
  {
    type: 'heading',
    text: { en: 'Adjustments (correcting a record)', my: 'Adjustments (မှတ်တမ်းပြင်ခြင်း)' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Ask to correct a wrong record (new check-in/out times or status) with a reason. The record must already exist and the date cannot be in the future.',
        my: 'မှားယွင်းသောမှတ်တမ်းကို အကြောင်းပြချက်ဖြင့် ပြင်ရန်တောင်းဆိုပါ (အချိန်အသစ် သို့ Status)။ မှတ်တမ်းရှိပြီးသားဖြစ်ရမည်၊ အနာဂတ်ရက်မရ။',
      },
      {
        en: 'One Pending request per employee per date.',
        my: 'ဝန်ထမ်း၊ ရက်စွဲတစ်ခုလျှင် Pending တစ်ခုသာ။',
      },
      {
        en: 'When approved, the record is recalculated and marked Manual.',
        my: 'ခွင့်ပြုလျှင် မှတ်တမ်းကို ပြန်တွက်ပြီး Manual အဖြစ်မှတ်သည်။',
      },
    ],
  },

  // ───────────── Example ─────────────
  {
    type: 'heading',
    text: { en: 'Example', my: 'ဥပမာ' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Shift 09:00–18:00, break 60 min, grace 10 min:',
      my: 'Shift 09:00–18:00၊ အနားယူ ၆၀ မိနစ်၊ Grace ၁၀ မိနစ်:',
    },
  },
  {
    type: 'code',
    lines: [
      'In 09:05, out 18:00  →  Present   (within grace)',
      'In 09:30, out 18:00  →  Late 30 min',
      'In 09:00, out 12:30  →  Half Day  (3 h 30 m − 60 m break = 2 h 30 m, under 4 h)',
      'In 09:00, out 19:00  →  Present, overtime 60 min  (more than 30 after the end)',
    ],
  },
  {
    type: 'note',
    text: {
      en: 'Attendance records are not a payroll input. Payroll uses approved overtime requests and approved leave only; fix wrong records through Adjustments.',
      my: 'Attendance မှတ်တမ်းများသည် Payroll ၏ input မဟုတ်ပါ။ Payroll က ခွင့်ပြုပြီး OT တောင်းခံမှုနှင့် ခွင့်ပြုပြီးခွင့်ကိုသာ သုံးသည်။ အမှားများကို Adjustments ဖြင့် ပြင်ပါ။',
    },
  },
];
