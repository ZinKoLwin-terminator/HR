import { Bilingual, GuideBlock } from './module-guide';

/** Same text in both languages (names, statuses, codes). */
const same = (text: string): Bilingual => ({ en: text, my: text });

/**
 * Long-form Performance (KPI) walkthrough: setup, scoring methods, the evaluation period lifecycle,
 * assigning KPIs and finalizing a plan, then the employee's results (My KPIs) and the review
 * (Reviews): reviewer, then HR's final review where the period asks for one.
 */
export const PERFORMANCE_GUIDE: readonly GuideBlock[] = [
  {
    type: 'heading',
    text: { en: 'How KPI planning works', my: 'KPI Planning အလုပ်လုပ်ပုံ' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Set up the building blocks once: categories, reusable KPI templates, evaluation periods and rating bands. For each period HR then gives every employee a scorecard with KPIs whose weights add up to exactly 100%, and finalizes the plan.',
      my: 'အခြေခံအစိတ်အပိုင်းများကို တစ်ကြိမ် Setup လုပ်ပါ — Category၊ ပြန်သုံးနိုင်သော KPI Template၊ Evaluation Period နှင့် Rating Band။ ထို့နောက် Period တစ်ခုစီအတွက် HR က ဝန်ထမ်းတစ်ဦးချင်းစီကို Weight ပေါင်း ၁၀၀% အတိအကျရှိသော KPI များပါသည့် Scorecard တစ်ခုပေးပြီး Plan ကို Finalize လုပ်ပါတယ်။',
    },
  },
  {
    type: 'flow',
    steps: [
      { en: '① Categories', my: '① Categories' },
      { en: '② Templates', my: '② Templates' },
      { en: '③ Evaluation Periods', my: '③ Evaluation Periods' },
      { en: '④ Rating Bands', my: '④ Rating Bands' },
      { en: '⑤ Assignments', my: '⑤ Assignments' },
      { en: '⑥ Finalize plan', my: '⑥ Plan ကို Finalize လုပ်ခြင်း' },
    ],
  },

  // ───────────── Setup ─────────────
  {
    type: 'heading',
    text: { en: 'Setup', my: 'Setup လုပ်ပုံ' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Categories group KPIs (Productivity, Quality, Attendance…). The code and the name must be unique. A category that templates use cannot be deleted: deactivate it instead.',
        my: 'Category များသည် KPI များကို အုပ်စုဖွဲ့ပေးသည် (Productivity၊ Quality၊ Attendance…)။ Code နှင့် အမည် မထပ်ရ။ Template များ သုံးနေသော Category ကို ဖျက်၍မရပါ — Deactivate လုပ်ပါ။',
      },
      {
        en: 'A template is a reusable KPI: what is measured, in which direction, how it is scored, and the default target and weight. Leave “Department-specific” empty to offer it to every department.',
        my: 'Template ဆိုသည်မှာ ပြန်သုံးနိုင်သော KPI ဖြစ်သည် — ဘာကိုတိုင်းမလဲ၊ ဘယ် Direction လဲ၊ ဘယ်လို Score တွက်မလဲ နှင့် ပုံသေ Target နှင့် Weight။ ဌာနအားလုံးအတွက် ပေးလိုလျှင် “Department-specific” ကို ကွက်လပ်ထားပါ။',
      },
      {
        en: 'An evaluation period is the window a scorecard belongs to, for one company. Periods of the same type cannot overlap in a company; a Monthly period inside a Quarterly one is fine.',
        my: 'Evaluation Period ဆိုသည်မှာ ကုမ္ပဏီတစ်ခုအတွက် Scorecard ပါဝင်သော ကာလဖြစ်သည်။ ကုမ္ပဏီတစ်ခုတွင် အမျိုးအစားတူ Period များ ရက်မထပ်ရ — Quarterly တစ်ခုအတွင်းရှိ Monthly Period ဆိုလျှင် ရပါတယ်။',
      },
      {
        en: 'Rating bands turn an overall score into a label: a score gets the band with the highest minimum score it reaches.',
        my: 'Rating Band များသည် စုစုပေါင်းရမှတ်ကို Label အဖြစ် ပြောင်းပေးသည် — ရမှတ်သည် ၎င်းရောက်သော အနည်းဆုံးရမှတ် အမြင့်ဆုံး Band ကို ရသည်။',
      },
    ],
  },

  // ───────────── Scoring ─────────────
  {
    type: 'heading',
    text: { en: 'Scoring methods', my: 'Scoring Method များ' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'The template form adapts to the scoring method you choose, and checks the same rules as the server while you type.',
      my: 'ရွေးလိုက်သော Scoring Method အလိုက် Template Form ပြောင်းလဲပြီး ရိုက်နေစဉ်မှာပင် Server နှင့် စည်းကမ်းတူကို စစ်ပေးပါတယ်။',
    },
  },
  {
    type: 'table',
    head: [
      { en: 'Scoring method', my: 'Scoring Method' },
      { en: 'What the form asks', my: 'Form က တောင်းသည်' },
      { en: 'Example', my: 'ဥပမာ' },
    ],
    rows: [
      [
        same('Direct Percentage'),
        {
          en: 'Achievement = result ÷ target. No maximum achievement.',
          my: 'Achievement = ရလဒ် ÷ Target။ Maximum achievement မရှိ။',
        },
        { en: 'Target 100, result 130 → 130%', my: 'Target 100၊ ရလဒ် 130 → 130%' },
      ],
      [
        same('Capped Percentage'),
        {
          en: 'The maximum achievement (1 to 500%) is required.',
          my: 'Maximum achievement (1 မှ 500%) လိုအပ်သည်။',
        },
        {
          en: 'Target 100, cap 120, result 130 → 120%',
          my: 'Target 100၊ Cap 120၊ ရလဒ် 130 → 120%',
        },
      ],
      [
        same('Rating Based'),
        {
          en: 'The maximum rating; the measurement type is Rating.',
          my: 'Maximum rating၊ Measurement type သည် Rating ဖြစ်သည်။',
        },
        { en: 'Maximum 5, rating 4 → 80%', my: 'Maximum 5၊ Rating 4 → 80%' },
      ],
      [
        same('Boolean'),
        {
          en: 'Nothing more: the measurement type is Boolean and the target 1.',
          my: 'ထပ်မံမလို — Measurement type သည် Boolean ဖြစ်ပြီး Target 1။',
        },
        { en: 'Achieved (1) → 100%', my: 'ပြည့်မီ (1) → 100%' },
      ],
      [
        same('Range Based'),
        {
          en: 'The range minimum and maximum; the direction is Range Based.',
          my: 'Range အနည်းဆုံးနှင့် အများဆုံး၊ Direction သည် Range Based ဖြစ်သည်။',
        },
        {
          en: 'Range 95 to 105, result 90 → 94.74%',
          my: 'Range 95 မှ 105၊ ရလဒ် 90 → 94.74%',
        },
      ],
    ],
  },
  {
    type: 'note',
    text: {
      en: 'Lower Is Better (fewer defects, fewer hours) needs a maximum achievement, so use Capped Percentage: a result of 0 scores the cap. Higher Is Better and Exact Target need a target above 0.',
      my: 'Lower Is Better (Defect နည်းလေ၊ နာရီနည်းလေ ကောင်းလေ) တွင် Maximum achievement လိုသဖြင့် Capped Percentage ကိုသုံးပါ — ရလဒ် 0 သည် Cap ရမှတ်ကို ရသည်။ Higher Is Better နှင့် Exact Target တွင် Target သည် 0 ထက်ကြီးရမည်။',
    },
  },

  // ───────────── Periods ─────────────
  {
    type: 'heading',
    text: { en: 'Evaluation period lifecycle', my: 'Evaluation Period ၏ အဆင့်များ' },
  },
  {
    type: 'flow',
    steps: [same('Draft'), same('Active'), same('Completed'), same('Closed')],
  },
  {
    type: 'table',
    head: [
      { en: 'Status', my: 'Status' },
      { en: 'What can change', my: 'ဘာပြင်နိုင်သလဲ' },
      { en: 'Next step', my: 'နောက်အဆင့်' },
    ],
    rows: [
      [
        same('Draft'),
        {
          en: 'Every field. It can be deleted while no scorecard uses it.',
          my: 'အကွက်အားလုံး။ Scorecard မသုံးသေးသမျှ ဖျက်နိုင်သည်။',
        },
        {
          en: 'Activate: employees can enter and submit results.',
          my: 'Activate — ဝန်ထမ်းများ ရလဒ်ထည့်ပြီး Submit လုပ်နိုင်သည်။',
        },
      ],
      [
        same('Active'),
        {
          en: 'Only the name and the submission due date.',
          my: 'အမည်နှင့် Submission due date သာ။',
        },
        {
          en: 'Complete: no more results or new KPIs; reviews can finish.',
          my: 'Complete — ရလဒ်နှင့် KPI အသစ် ထပ်မထည့်ရ၊ Review များ ဆက်ပြီးနိုင်သည်။',
        },
      ],
      [
        same('Completed'),
        { en: 'Nothing (read-only).', my: 'ဘာမှမပြင်ရ (ဖတ်ရန်သာ)။' },
        {
          en: 'Close: the period becomes final.',
          my: 'Close — Period သည် အပြီးသတ် ဖြစ်သွားသည်။',
        },
      ],
      [
        same('Closed'),
        { en: 'Nothing. This cannot be undone.', my: 'ဘာမှမပြင်ရ။ ပြန်ဖွင့်၍ မရပါ။' },
        { en: '—', my: '—' },
      ],
    ],
  },

  // ───────────── Assignments ─────────────
  {
    type: 'heading',
    text: { en: 'Assigning KPIs', my: 'KPI များ Assign လုပ်ခြင်း' },
  },
  {
    type: 'steps',
    items: [
      {
        en: 'Open Performance › Assignments and choose Assign KPIs.',
        my: 'Performance › Assignments ကိုဖွင့်ပြီး Assign KPIs ကို နှိပ်ပါ။',
      },
      {
        en: 'Choose a Draft or Active period, then search for the employee by code or name.',
        my: 'Draft သို့ Active Period ကိုရွေးပြီး ဝန်ထမ်းကို Code သို့ အမည်ဖြင့် ရှာပါ။',
      },
      {
        en: 'Add KPIs from the active templates. Department-specific ones appear only for an employee of that department.',
        my: 'Active Template များမှ KPI များ ထည့်ပါ။ ဌာနအလိုက် Template များသည် ထိုဌာနမှ ဝန်ထမ်းအတွက်သာ ပေါ်ပါမည်။',
      },
      {
        en: 'The target and weight start at the template’s defaults; change them per KPI. The running total cannot go above 100%.',
        my: 'Target နှင့် Weight သည် Template ၏ ပုံသေတန်ဖိုးဖြင့် စတင်ပြီး KPI တစ်ခုချင်း ပြင်နိုင်သည်။ Weight စုစုပေါင်း ၁၀၀% ထက် မကျော်ရ။',
      },
      {
        en: 'Save. The message says whether a new scorecard was created or the KPIs were added to the one already in Planning.',
        my: 'Save လုပ်ပါ။ Scorecard အသစ် ဖန်တီးသလား၊ Planning ရှိပြီးသား Scorecard ထဲ KPI ထပ်ထည့်သလား စာက ပြောပြပါမည်။',
      },
    ],
  },
  {
    type: 'paragraph',
    text: {
      en: 'Bulk assign gives the same KPIs to everyone in a department, branch, position or job grade, or to hand-picked employees. The result lists who was assigned and who was skipped, with the reason: already assigned, has left, another company, a template for another department, a plan past Planning or weights above 100%. Running it again assigns nobody twice.',
      my: 'Bulk assign သည် ဌာန၊ Branch၊ Position၊ Job Grade တစ်ခုလုံး သို့ ရွေးထားသော ဝန်ထမ်းများကို KPI တူများ ပေးသည်။ ရလဒ်တွင် Assign ပြီးသူနှင့် ကျော်သွားသူများကို အကြောင်းပြချက်နှင့်တကွ ပြသည် — Assign ပြီးသား၊ ထွက်သွားပြီ၊ အခြားကုမ္ပဏီ၊ အခြားဌာနအတွက် Template၊ Planning ကျော်သွားသော Plan သို့ Weight ၁၀၀% ကျော်ခြင်း။ ထပ် Run လည်း မည်သူ့ကိုမျှ နှစ်ခါ Assign မလုပ်ပါ။',
    },
  },
  {
    type: 'note',
    text: {
      en: 'Assigning copies the template into the scorecard. Editing the template later never changes a plan that already holds it.',
      my: 'Assign လုပ်လျှင် Template ကို Scorecard ထဲ ကူးထည့်သည်။ နောက်မှ Template ကိုပြင်လည်း ၎င်းပါပြီးသော Plan မပြောင်းပါ။',
    },
  },

  // ───────────── Plan ─────────────
  {
    type: 'heading',
    text: { en: 'The plan and finalizing it', my: 'Plan နှင့် Finalize လုပ်ခြင်း' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Open a row of the list to see the plan. While it is in Planning you can edit a KPI (target, weight, dates, order), remove it, add more KPIs or delete the scorecard. Each removal asks for confirmation.',
        my: 'စာရင်းမှ အတန်းတစ်ခုကိုဖွင့်ပြီး Plan ကိုကြည့်ပါ။ Planning ဖြစ်နေစဉ် KPI ကိုပြင် (Target၊ Weight၊ ရက်စွဲ၊ အစဉ်)၊ ဖယ်ရှား၊ KPI ထပ်ထည့် သို့ Scorecard ကို ဖျက်နိုင်သည်။ ဖယ်ရှားခြင်းတိုင်းတွင် အတည်ပြုချက် မေးပါမည်။',
      },
      {
        en: 'Finalize plan is enabled only when the weights add up to exactly 100%. The confirmation names the reviewer: the employee’s current manager. Without a manager only HR can review.',
        my: 'Weight ပေါင်း ၁၀၀% အတိအကျ ဖြစ်မှသာ Finalize plan ကို နှိပ်နိုင်သည်။ အတည်ပြုချက်တွင် Reviewer (ဝန်ထမ်း၏ လက်ရှိ Manager) အမည်ကို ပြသည်။ Manager မရှိလျှင် HR သာ Review လုပ်နိုင်သည်။',
      },
      {
        en: 'Finalizing moves the scorecard to Draft, where the employee enters results. From then on the plan is read-only and the review history records every step.',
        my: 'Finalize လုပ်လျှင် Scorecard သည် Draft သို့ရောက်ပြီး ဝန်ထမ်းက ရလဒ်ထည့်ပါမည်။ ထိုအချိန်မှစ၍ Plan သည် ဖတ်ရန်သာဖြစ်ပြီး Review history တွင် အဆင့်တိုင်းကို မှတ်တမ်းတင်သည်။',
      },
      {
        en: 'If someone else changed the scorecard meanwhile, the server refuses the change and the page offers Reload. Reload, check, and try again.',
        my: 'အခြားသူက Scorecard ကို ကြားထဲတွင် ပြောင်းထားလျှင် Server က ပယ်ပြီး စာမျက်နှာတွင် Reload ပြပါမည်။ Reload လုပ်၊ စစ်ဆေးပြီး ထပ်ကြိုးစားပါ။',
      },
    ],
  },
  {
    type: 'flow',
    steps: [
      same('Planning'),
      same('Draft'),
      same('Submitted'),
      same('Under Review'),
      same('Approved'),
      same('Finalized'),
    ],
  },
  {
    type: 'note',
    text: {
      en: 'Returned goes back to the employee and comes back as Resubmitted; Rejected is final. Approved waits for HR only when the period requires an HR final review.',
      my: 'Returned သည် ဝန်ထမ်းထံပြန်သွားပြီး Resubmitted အဖြစ် ပြန်လာသည်။ Rejected သည် အပြီးသတ်ဖြစ်သည်။ Period က HR final review လိုအပ်မှသာ Approved သည် HR ကို စောင့်သည်။',
    },
  },

  // ───────────── Employee: My KPIs ─────────────
  {
    type: 'heading',
    text: { en: 'Entering your results (My KPIs)', my: 'ရလဒ်ထည့်ခြင်း (My KPIs)' },
  },
  {
    type: 'steps',
    items: [
      {
        en: 'Open Performance › My KPIs and choose the scorecard of the current period.',
        my: 'Performance › My KPIs ကိုဖွင့်ပြီး လက်ရှိ Period ၏ Scorecard ကို ရွေးပါ။',
      },
      {
        en: 'For each KPI enter the actual: Yes or No for a yes-or-no KPI, a rating on the KPI’s scale, otherwise a number in the KPI’s unit. Add a comment if it helps the reviewer.',
        my: 'KPI တစ်ခုစီအတွက် Actual ကိုထည့်ပါ — Yes/No KPI အတွက် Yes သို့ No၊ Rating KPI အတွက် ၎င်း၏ Scale အတွင်း Rating၊ ကျန်သည် KPI ၏ ယူနစ်ဖြင့် ဂဏန်း။ Reviewer အတွက် အထောက်အကူဖြစ်လျှင် Comment ရေးပါ။',
      },
      {
        en: 'Attach evidence if you have it: up to 5 files per KPI, PDF, PNG, JPEG, Word (.docx) or Excel (.xlsx), 10 MB each at most.',
        my: 'Evidence ရှိလျှင် တွဲပါ — KPI တစ်ခုလျှင် ဖိုင် ၅ ခုအထိ၊ PDF၊ PNG၊ JPEG၊ Word (.docx) သို့ Excel (.xlsx)၊ တစ်ခုလျှင် အများဆုံး 10 MB။',
      },
      {
        en: 'Save draft as often as you like. The achievement and weighted score shown after saving are the server’s.',
        my: 'Save draft ကို လိုသလောက် လုပ်နိုင်သည်။ Save ပြီးနောက် ပြသော Achievement နှင့် Weighted score သည် Server ၏ ရလဒ် ဖြစ်သည်။',
      },
      {
        en: 'Submit when every KPI has a result. The server calculates the overall score; your results are then read-only.',
        my: 'KPI တိုင်းတွင် ရလဒ်ရှိမှ Submit လုပ်ပါ။ Server က စုစုပေါင်းရမှတ်ကို တွက်ပြီး သင့်ရလဒ်များ ဖတ်ရန်သာ ဖြစ်သွားသည်။',
      },
    ],
  },
  {
    type: 'list',
    items: [
      {
        en: 'A submission after the period’s due date is accepted and marked Late.',
        my: 'Period ၏ Due date ကျော်ပြီး Submit လုပ်လျှင် လက်ခံပြီး Late ဟု မှတ်သည်။',
      },
      {
        en: 'Returned: the reviewer’s reason is shown at the top. Correct your results and submit again; it comes back as Resubmitted.',
        my: 'Returned — Reviewer ၏ အကြောင်းပြချက်ကို အပေါ်ဆုံးတွင် ပြသည်။ ရလဒ်ကို ပြင်ပြီး ထပ် Submit လုပ်ပါ — Resubmitted အဖြစ် ပြန်ရောက်သည်။',
      },
      {
        en: 'Finalized or Rejected: the page is read-only and shows the reviewer’s ratings and feedback and the HR comment.',
        my: 'Finalized သို့ Rejected — စာမျက်နှာသည် ဖတ်ရန်သာဖြစ်ပြီး Reviewer ၏ Rating၊ Feedback နှင့် HR Comment ကို ပြသည်။',
      },
      {
        en: 'The number under an input while you type is a preview only. Official scores always come from the server.',
        my: 'ရိုက်နေစဉ် Input အောက်ရှိ ဂဏန်းသည် Preview သာ ဖြစ်သည်။ တရားဝင်ရမှတ်များကို Server ကသာ ပေးသည်။',
      },
    ],
  },

  // ───────────── Reviewer: Reviews ─────────────
  {
    type: 'heading',
    text: { en: 'Reviewing a scorecard (Reviews)', my: 'Scorecard ကို Review လုပ်ခြင်း (Reviews)' },
  },
  {
    type: 'flow',
    steps: [
      { en: '① Employee submits', my: '① ဝန်ထမ်း Submit' },
      { en: '② Manager reviews', my: '② Manager Review' },
      { en: '③ HR final review', my: '③ HR Final review' },
      same('Finalized'),
    ],
  },
  {
    type: 'table',
    head: [
      { en: 'Status', my: 'Status' },
      { en: 'Who acts', my: 'ဘယ်သူ လုပ်သလဲ' },
      { en: 'Buttons', my: 'ခလုတ်များ' },
    ],
    rows: [
      [
        { en: 'Submitted, Resubmitted', my: 'Submitted, Resubmitted' },
        { en: 'The reviewer, or HR', my: 'Reviewer သို့ HR' },
        {
          en: 'Start review, Approve, Return for revision, Reject',
          my: 'Start review, Approve, Return for revision, Reject',
        },
      ],
      [
        same('Under Review'),
        { en: 'The reviewer, or HR', my: 'Reviewer သို့ HR' },
        { en: 'Approve, Return for revision, Reject', my: 'Approve, Return for revision, Reject' },
      ],
      [
        same('Approved'),
        {
          en: 'HR, but not whoever approved it',
          my: 'HR (Approve လုပ်ခဲ့သူ မဟုတ်ရ)',
        },
        { en: 'Finalize, Return, Reject', my: 'Finalize, Return, Reject' },
      ],
    ],
  },
  {
    type: 'list',
    items: [
      {
        en: 'Rate each KPI and the whole scorecard from 1 to 5 (up to 2 decimals) with feedback, and Save review notes. Notes do not change the status; a decision saves unsaved notes first.',
        my: 'KPI တစ်ခုချင်းနှင့် Scorecard တစ်ခုလုံးကို 1 မှ 5 (ဒဿမ ၂ နေရာအထိ) Rating နှင့် Feedback ပေးပြီး Save review notes လုပ်ပါ။ Notes သည် Status မပြောင်းပါ — ဆုံးဖြတ်ချက်ပေးလျှင် မသိမ်းရသေးသော Notes ကို အရင်သိမ်းသည်။',
      },
      {
        en: 'Approve recalculates the score on the server. If the period has no HR final review, approving finalizes the scorecard at once; the confirmation tells you which.',
        my: 'Approve လုပ်လျှင် Server က ရမှတ်ကို ပြန်တွက်သည်။ Period တွင် HR final review မလိုလျှင် Approve သည် Scorecard ကို ချက်ချင်း Finalize လုပ်သည် — အတည်ပြုချက်က ဘယ်ဟာဖြစ်မလဲ ပြောပြသည်။',
      },
      {
        en: 'Return for revision and Reject need a reason (at most 1000 characters). Return clears the score until the employee resubmits; Reject is final.',
        my: 'Return for revision နှင့် Reject တွင် အကြောင်းပြချက် လိုသည် (စာလုံး ၁၀၀၀ အထိ)။ Return သည် ဝန်ထမ်း ထပ် Submit မလုပ်မချင်း ရမှတ်ကို ရှင်းထားသည် — Reject သည် အပြီးသတ် ဖြစ်သည်။',
      },
      {
        en: 'Nobody reviews their own scorecard, whatever their role. The buttons only help: the server decides who may act and shows its message if a step is refused.',
        my: 'မည်သည့် Role ဖြစ်စေ ကိုယ့် Scorecard ကို ကိုယ်တိုင် Review မလုပ်ရ။ ခလုတ်များသည် အထောက်အကူသာ — မည်သူ လုပ်ဆောင်ခွင့်ရှိသည်ကို Server က ဆုံးဖြတ်ပြီး ပယ်လျှင် ၎င်း၏ စာကို ပြသည်။',
      },
      {
        en: 'The HR person who approved a scorecard at the manager stage cannot also give its final review. The screen recognises them by their user account, never by their name.',
        my: 'Manager အဆင့်တွင် Approve လုပ်ခဲ့သော HR ဝန်ထမ်းသည် ထို Scorecard ၏ Final review ကို ထပ်မပေးရ။ Screen က ၎င်းတို့ကို အမည်ဖြင့်မဟုတ်ဘဲ User account ဖြင့် သိသည်။',
      },
    ],
  },

  // ───────────── Dashboards and reports ─────────────
  {
    type: 'heading',
    text: { en: 'Dashboards and reports', my: 'Dashboard နှင့် Report များ' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'Performance › Dashboard shows a period at a glance and Performance › Reports lists the details. You only ever see the scorecards you may open: your own; as a reviewer also your team’s (your direct reports and the scorecards you review); as HR everyone’s. Every figure is calculated by the server; the screens only display it.',
      my: 'Performance › Dashboard သည် Period တစ်ခုကို တစ်ချက်တည်းမြင်စေပြီး Performance › Reports က အသေးစိတ်ကို ပြသည်။ သင်ဖွင့်ခွင့်ရှိသော Scorecard များကိုသာ မြင်ရသည် — ကိုယ့်ဟာ၊ Reviewer ဆိုလျှင် အဖွဲ့၏ဟာ (တိုက်ရိုက်လက်အောက်ငယ်သားများနှင့် ကိုယ် Review လုပ်ရသော Scorecard များ)၊ HR ဆိုလျှင် အားလုံး။ ကိန်းဂဏန်းတိုင်းကို Server က တွက်ပြီး Screen များက ပြရုံသာ ပြသည်။',
    },
  },
  {
    type: 'table',
    head: [
      { en: 'Figure', my: 'ကိန်းဂဏန်း' },
      { en: 'How it is worked out', my: 'တွက်ပုံ' },
    ],
    rows: [
      [
        { en: 'Average score', my: 'ပျမ်းမျှရမှတ်' },
        {
          en: 'The average overall score of the Finalized scorecards only, to 2 decimals. Rejected scorecards and scorecards still in the workflow (Submitted, Approved and so on) never count. With no Finalized scorecard it shows a dash, not 0.',
          my: 'Finalized Scorecard များ၏ စုစုပေါင်းရမှတ် ပျမ်းမျှကိုသာ ဒဿမ ၂ နေရာအထိ။ Rejected နှင့် Workflow အတွင်းရှိနေဆဲ Scorecard များ (Submitted၊ Approved စသည်) ကို ဘယ်တော့မှ မရေတွက်ပါ။ Finalized Scorecard မရှိလျှင် 0 မဟုတ်ဘဲ မျဉ်းတို (—) ပြသည်။',
        },
      ],
      [
        { en: 'Lowest and highest score', my: 'အနိမ့်ဆုံးနှင့် အမြင့်ဆုံးရမှတ်' },
        {
          en: 'Of the same Finalized scores as the average.',
          my: 'ပျမ်းမျှတွင် သုံးသော Finalized ရမှတ်များထဲမှပင်။',
        },
      ],
      [
        { en: 'Completion rate', my: 'Completion rate' },
        {
          en: 'Finalized scorecards divided by every scorecard that has left Planning, as a percentage with 2 decimals. 0 when no scorecard has left Planning yet.',
          my: 'Finalized Scorecard အရေအတွက်ကို Planning မှ ထွက်ပြီးသော Scorecard အားလုံးဖြင့် စား၍ ဒဿမ ၂ နေရာပါ ရာခိုင်နှုန်းအဖြစ်။ Planning မှ မထွက်ရသေးလျှင် 0။',
        },
      ],
      [
        { en: 'Ranking', my: 'Ranking' },
        {
          en: 'Finalized scorecards with a score, highest first. Equal scores share a rank and the next rank is skipped: 90, 90, 80 rank 1, 1, 3. Equal scores are listed by employee code. Choose a period for a ranking within one period.',
          my: 'ရမှတ်ရှိသော Finalized Scorecard များ၊ အမြင့်ဆုံးအရင်။ ရမှတ်တူလျှင် အဆင့်တူရပြီး နောက်အဆင့်ကို ကျော်သည် — 90, 90, 80 သည် 1, 1, 3။ ရမှတ်တူများကို ဝန်ထမ်း Code အလိုက် စီသည်။ Period တစ်ခုအတွင်း Ranking လိုလျှင် Period ကိုရွေးပါ။',
        },
      ],
      [
        { en: 'Requiring attention', my: 'အာရုံစိုက်ရန်လိုသူ' },
        {
          en: 'A Finalized scorecard below the threshold: the minimum score of the lowest active rating band that starts at 60 or above (Meets Expectations with the default bands), or 60 when there is no such band. The HR overview shows the threshold it used.',
          my: 'Threshold အောက်ရှိ Finalized Scorecard — 60 နှင့်အထက်မှ စသော အနိမ့်ဆုံး Active Rating Band ၏ အနည်းဆုံးရမှတ် (ပုံသေ Band များတွင် Meets Expectations)၊ ထိုသို့ Band မရှိလျှင် 60။ HR overview က သုံးထားသော Threshold ကို ပြသည်။',
        },
      ],
      [
        { en: 'Default period', my: 'ပုံသေ Period' },
        {
          en: 'When you have not chosen a period: the most recent Active period among the scorecards of the view, else the most recent period whatever its status.',
          my: 'Period မရွေးရသေးလျှင် — မြင်ကွင်း၏ Scorecard များထဲမှ နောက်ဆုံး Active Period၊ မရှိလျှင် Status မရွေး နောက်ဆုံး Period။',
        },
      ],
      [
        { en: 'Pending and overdue', my: 'Pending နှင့် Overdue' },
        {
          en: 'Your submission is pending while your scorecard is Draft or Returned and the period is Active; it is overdue once the period’s submission due date has passed. A late submission is still accepted and marked Late.',
          my: 'သင့် Scorecard သည် Draft သို့ Returned ဖြစ်ပြီး Period က Active ဖြစ်နေသရွေ့ Submit ရန် Pending ဖြစ်သည် — Period ၏ Submission due date ကျော်လျှင် Overdue ဖြစ်သည်။ နောက်ကျ Submit လုပ်လည်း လက်ခံပြီး Late ဟု မှတ်သည်။',
        },
      ],
    ],
  },
  {
    type: 'list',
    items: [
      {
        en: 'Department figures use the department the employee was in when the scorecard was created, so someone who moves later still counts in their old department for that period.',
        my: 'ဌာနကိန်းဂဏန်းများသည် Scorecard ဖန်တီးစဉ်က ဝန်ထမ်းရှိခဲ့သော ဌာနကို သုံးသည် — နောက်မှ ပြောင်းသွားသူသည် ထို Period အတွက် ဌာနဟောင်းတွင်ပင် ရေတွက်ခံရသည်။',
      },
      {
        en: 'Rejected scorecards never move an average, a minimum, a maximum or a rank, but the row reports list them with their status.',
        my: 'Rejected Scorecard များသည် ပျမ်းမျှ၊ အနိမ့်ဆုံး၊ အမြင့်ဆုံး သို့ အဆင့်ကို ဘယ်တော့မှ မပြောင်းစေပါ — သို့သော် အတန်းလိုက် Report များတွင် ၎င်းတို့၏ Status နှင့်အတူ ပြသည်။',
      },
      {
        en: 'Monthly, Quarterly and Annual summaries are the period summary filtered by period type (and year if you choose one).',
        my: 'Monthly၊ Quarterly နှင့် Annual Summary များသည် Period အမျိုးအစား (ရွေးလျှင် နှစ်) ဖြင့် စစ်ထုတ်ထားသော Period Summary ပင် ဖြစ်သည်။',
      },
      {
        en: 'Excel export (Ranking, Target vs actual and the scorecard list) uses the report’s filters and needs the Excel export permission; it never includes comments, manager ratings or evidence files. Print opens the browser’s print dialog: choose “Save as PDF” there for a PDF.',
        my: 'Excel export (Ranking၊ Target vs actual နှင့် Scorecard စာရင်း) သည် Report ၏ Filter များကို သုံးပြီး Excel export ခွင့်လိုသည် — Comment၊ Manager rating သို့ Evidence ဖိုင်များ ဘယ်တော့မှ မပါပါ။ Print သည် Browser ၏ Print dialog ကို ဖွင့်သည် — PDF လိုလျှင် ထိုနေရာတွင် “Save as PDF” ကို ရွေးပါ။',
      },
      {
        en: 'In the demo build the figures are worked out in the browser with the same definitions, from fictional sample data; with the API the server calculates them.',
        my: 'Demo Build တွင် ကိန်းဂဏန်းများကို တူညီသော သတ်မှတ်ချက်များဖြင့် Browser ထဲတွင် စိတ်ကူးယဉ်နမူနာဒေတာမှ တွက်သည် — API ဖြင့်သုံးလျှင် Server က တွက်သည်။',
      },
    ],
  },
];
