import { Bilingual, GuideBlock } from './module-guide';

/** Same text in both languages (names, statuses, codes). */
const same = (text: string): Bilingual => ({ en: text, my: text });

/**
 * Long-form Grievances walkthrough: reporting (named or anonymous), the case workflow, who does what,
 * what anonymity does and does not protect, and the dashboards and reports. The anonymity text says
 * plainly what the system can and cannot guarantee, and that the demo enforces nothing.
 */
export const GRIEVANCE_GUIDE: readonly GuideBlock[] = [
  {
    type: 'heading',
    text: { en: 'How a grievance moves', my: 'Grievance တစ်ခု ရွေ့လျားပုံ' },
  },
  {
    type: 'paragraph',
    text: {
      en: 'An employee writes a grievance (it can be saved as a draft), confirms the declaration and submits it. The case gets a number (GRV-year-number). HR acknowledges it with a priority and a target date, reviews it or assigns investigators, may ask the employee for more information, and a reviewer approves the proposed resolution before the case is resolved and closed.',
      my: 'ဝန်ထမ်းက Grievance ကို ရေးသည် (Draft အဖြစ် သိမ်းထားနိုင်သည်)၊ Declaration ကို အတည်ပြုပြီး Submit လုပ်သည်။ Case သည် နံပါတ် (GRV-နှစ်-နံပါတ်) ရသည်။ HR က Priority နှင့် Target date ဖြင့် Acknowledge လုပ်ပြီး Review လုပ်ခြင်း သို့ Investigator များ Assign လုပ်ခြင်း၊ ဝန်ထမ်းထံ အချက်အလက်ထပ်တောင်းခြင်း ပြုလုပ်နိုင်သည်။ Case ကို Resolve နှင့် Close မလုပ်မီ Reviewer က အဆိုပြုထားသော Resolution ကို Approve လုပ်ရသည်။',
    },
  },
  {
    type: 'flow',
    steps: [
      same('Draft'),
      same('Submitted'),
      same('Acknowledged'),
      { en: 'Under Review / Assigned', my: 'Under Review / Assigned' },
      same('Under Investigation'),
      same('Resolution Proposed'),
      same('Resolved'),
      same('Closed'),
    ],
  },
  {
    type: 'list',
    items: [
      {
        en: 'When an employee raises a grievance in person, by phone, email or letter, HR (with the Create permission of case management) can record it with Record grievance. It is submitted at once in that employee’s name, never anonymously, and the employee follows it under My Grievances. Nobody can record their own grievance this way.',
        my: 'ဝန်ထမ်းက Grievance ကို လူကိုယ်တိုင်၊ ဖုန်း၊ Email သို့ စာဖြင့် တင်ပြလျှင် HR (Case management ၏ Create ခွင့်ပြုချက်ရှိသူ) က Record grievance ဖြင့် မှတ်တမ်းတင်နိုင်သည်။ ထိုဝန်ထမ်း၏ အမည်ဖြင့် ချက်ချင်း Submit ဖြစ်ပြီး အမည်မဖော်ဘဲ ဘယ်တော့မှ မဟုတ်၊ ဝန်ထမ်းက My Grievances တွင် ဆက်ကြည့်နိုင်သည်။ ကိုယ့် Grievance ကို ဤနည်းဖြင့် မှတ်တမ်းမတင်ရ။',
      },
      {
        en: 'HR can reject a case while it is Submitted, Acknowledged or Under Review; the employee sees the reason.',
        my: 'Case သည် Submitted၊ Acknowledged သို့ Under Review ဖြစ်နေစဉ် HR က Reject လုပ်နိုင်သည် — ဝန်ထမ်းက အကြောင်းပြချက်ကို မြင်ရသည်။',
      },
      {
        en: 'An information request puts the case in Awaiting Employee Response; the employee’s answer returns it to where it was. The target date keeps running meanwhile.',
        my: 'အချက်အလက်တောင်းခံလျှင် Case သည် Awaiting Employee Response သို့ ရောက်ပြီး ဝန်ထမ်း၏ အဖြေက မူလနေရာသို့ ပြန်ပို့သည်။ ထိုအတောအတွင်း Target date ဆက်ရေတွက်သည်။',
      },
      {
        en: 'A reviewer who sends a proposal back returns the case to the investigation (or to review when there is no lead). Nobody decides their own proposal.',
        my: 'Reviewer က Proposal ကို ပြန်ပို့လျှင် Case သည် Investigation (Lead မရှိလျှင် Review) သို့ ပြန်ရောက်သည်။ ကိုယ့် Proposal ကို ကိုယ်တိုင် မဆုံးဖြတ်ရ။',
      },
      {
        en: 'A Resolved or Rejected case can be reopened with a reason; a Closed one only by someone who manages cases and also reviews. Reopening ends every investigator assignment: HR assigns a lead again (the same person is allowed). Closed and Rejected cases can be archived unless they are under legal hold; an archived case is read-only.',
        my: 'Resolved သို့ Rejected Case ကို အကြောင်းပြချက်ဖြင့် Reopen လုပ်နိုင်သည် — Closed Case ကိုမူ Case Management နှင့် Review နှစ်ခုလုံးရှိသူကသာ။ Reopen လုပ်လျှင် Investigator Assignment အားလုံး ပြီးဆုံးသွားပြီး HR က Lead ကို ပြန်လည် Assign လုပ်ရသည် (လူတူကိုလည်း ရသည်)။ Closed နှင့် Rejected Case များကို Legal hold မရှိလျှင် Archive လုပ်နိုင်ပြီး Archive လုပ်ထားသော Case သည် ဖတ်ရန်သာ ဖြစ်သည်။',
      },
      {
        en: 'A Critical priority escalates the case to the reviewers at once. A case is escalated only once and stays escalated.',
        my: 'Critical Priority ဖြစ်လျှင် Case ကို Reviewer များထံ ချက်ချင်း Escalate လုပ်သည်။ Case တစ်ခုကို တစ်ကြိမ်သာ Escalate လုပ်ပြီး Escalated အဖြစ် ဆက်ရှိနေသည်။',
      },
    ],
  },

  // ───────────── Roles ─────────────
  {
    type: 'heading',
    text: { en: 'Who does what', my: 'ဘယ်သူက ဘာလုပ်သလဲ' },
  },
  {
    type: 'table',
    head: [
      { en: 'Permission', my: 'Permission' },
      { en: 'Who', my: 'ဘယ်သူ' },
      { en: 'Sees', my: 'မြင်ရသည်' },
    ],
    rows: [
      [
        same('grievance'),
        { en: 'Every employee', my: 'ဝန်ထမ်းအားလုံး' },
        {
          en: 'Their own grievances only, with HR shown as “HR team”.',
          my: 'ကိုယ့် Grievance များသာ — HR ကို “HR team” ဟုသာ ပြသည်။',
        },
      ],
      [
        same('grievance-investigation'),
        { en: 'Investigators (HR Officer by default)', my: 'Investigator များ (ပုံသေ HR Officer)' },
        {
          en: 'The cases they are assigned to: notes, interviews, findings, messages and case-team files; no corrective actions or restricted files.',
          my: 'Assign လုပ်ထားသော Case များ — Note၊ Interview၊ Finding၊ Message နှင့် Case-team ဖိုင်များ။ Corrective action နှင့် Restricted ဖိုင် မမြင်ရ။',
        },
      ],
      [
        same('grievance-management'),
        { en: 'HR management', my: 'HR Management' },
        {
          en: 'Every submitted case, triage, assignment, corrective actions, categories, the dashboard and reports.',
          my: 'Submit လုပ်ပြီးသော Case အားလုံး၊ Triage၊ Assignment၊ Corrective action၊ Category၊ Dashboard နှင့် Report များ။',
        },
      ],
      [
        same('grievance-review'),
        {
          en: 'Reviewers (granted by HR Administrator)',
          my: 'Reviewer များ (HR Administrator က ခွင့်ပြုသည်)',
        },
        {
          en: 'Escalated cases and proposals to decide: findings, recommendations and the proposal, not the raw notes.',
          my: 'Escalated Case များနှင့် ဆုံးဖြတ်ရမည့် Proposal များ — Finding၊ Recommendation နှင့် Proposal၊ Note အကြမ်းများ မဟုတ်။',
        },
      ],
    ],
  },
  {
    type: 'note',
    text: {
      en: 'Nobody on the case team sees a case they submitted or a case about them (the subject employee): it is left out of their lists as if it did not exist.',
      my: 'Case team ရှိ မည်သူမျှ ကိုယ်တိုင် Submit လုပ်ခဲ့သော Case သို့ ကိုယ့်အကြောင်းဖြစ်သော Case (Subject employee) ကို မမြင်ရ — ၎င်းတို့၏ စာရင်းထဲတွင် မရှိသကဲ့သို့ ချန်ထားသည်။',
    },
  },

  // ───────────── Anonymity ─────────────
  {
    type: 'heading',
    text: {
      en: 'Anonymous grievances: what is and is not protected',
      my: 'အမည်မဖော်သော Grievance — ဘာကို ကာကွယ်ပေးပြီး ဘာကို မကာကွယ်နိုင်သလဲ',
    },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Where the category allows it (not Salary and Compensation), the employee can report anonymously. The choice is fixed when the draft is first saved; to report differently, start a new draft.',
        my: 'Category ခွင့်ပြုသည့်နေရာတွင် (Salary and Compensation မှလွဲ၍) ဝန်ထမ်းက အမည်မဖော်ဘဲ တင်ပြနိုင်သည်။ Draft ကို ပထမဆုံး Save လုပ်ပြီးလျှင် ပြောင်း၍မရ — အခြားနည်းဖြင့် တင်ပြလိုပါက Draft အသစ် စတင်ပါ။',
      },
      {
        en: 'The case stores a keyed pseudonym instead of the employee: HR sees no name, code, department or position, and the history and messages say “Anonymous submitter”. The employee still follows the case and answers HR under My Grievances.',
        my: 'Case တွင် ဝန်ထမ်းအစား Key ဖြင့်ပြုလုပ်ထားသော Pseudonym ကိုသာ သိမ်းသည် — HR က အမည်၊ Code၊ ဌာန သို့ ရာထူး မမြင်ရဘဲ History နှင့် Message များတွင် “Anonymous submitter” ဟုသာ ပြသည်။ ဝန်ထမ်းကမူ My Grievances တွင် Case ကို ဆက်ကြည့်ပြီး HR ကို ပြန်ဖြေနိုင်သည်။',
      },
      {
        en: 'Anonymous submitters get no notification (they see an unread badge instead), dashboards count anonymous cases as “Not disclosed”, and department groups under three cases are merged into “Other / suppressed”.',
        my: 'အမည်မဖော်သူများ Notification မရ (Unread badge ကိုသာ မြင်ရ)၊ Dashboard များတွင် အမည်မဖော်သော Case များကို “Not disclosed” ဟု ရေတွက်ပြီး Case ၃ ခုအောက် ဌာနအုပ်စုများကို “Other / suppressed” အဖြစ် ပေါင်းထားသည်။',
      },
      {
        en: 'What can still give someone away: when they submit or reply (timing), their writing style and details only they know, file names, details inside PDF files (the server removes them from Word, Excel, JPEG and PNG files, not from PDFs), and the facts themselves in a small department.',
        my: 'ဆက်လက်၍ ဖော်ထုတ်နိုင်သောအရာများ — Submit သို့ ပြန်ဖြေသည့်အချိန်၊ ရေးသားပုံနှင့် ကိုယ်တိုင်သာသိသော အသေးစိတ်၊ ဖိုင်အမည်၊ PDF ဖိုင်အတွင်းရှိ အချက်အလက် (Word၊ Excel၊ JPEG နှင့် PNG ဖိုင်များမှ Server က ဖယ်ရှားသော်လည်း PDF မှ မဖယ်ရှားပါ) နှင့် ဌာနငယ်တွင် ဖြစ်ရပ်ကိုယ်တိုင်။',
      },
    ],
  },
  {
    type: 'note',
    text: {
      en: 'In this demo everything is stored in your browser, the pseudonym is a simulation anyone could recompute, and roles and permissions enforce nothing. Only the real API protects anonymity and access.',
      my: 'ဤ Demo တွင် အားလုံးကို သင့် Browser ထဲတွင်သာ သိမ်းထားပြီး Pseudonym သည် မည်သူမဆို ပြန်တွက်နိုင်သော Simulation ဖြစ်ကာ Role နှင့် Permission များသည် မည်သည့်ကန့်သတ်ချက်ကိုမျှ အမှန်တကယ် မစစ်ဆေးပါ။ အမည်မဖော်မှုနှင့် Access ကို တကယ့် API ကသာ ကာကွယ်ပေးသည်။',
    },
  },

  // ───────────── Figures ─────────────
  {
    type: 'heading',
    text: { en: 'Dashboards and reports', my: 'Dashboard နှင့် Report များ' },
  },
  {
    type: 'list',
    items: [
      {
        en: 'Figures are the same for every viewer and count every submitted case, archived ones included. Overdue: open past the target date. Resolution rate: resolved or closed out of the cases not rejected. Average resolution: days from submission to resolution.',
        my: 'ကိန်းဂဏန်းများသည် ကြည့်သူတိုင်းအတွက် တူညီပြီး Archive လုပ်ထားသည်များအပါအဝင် Submit လုပ်ပြီးသော Case အားလုံးကို ရေတွက်သည်။ Overdue — Target date ကျော်ပြီး ဖွင့်လျက်ရှိသည်။ Resolution rate — Reject မလုပ်သော Case များထဲမှ Resolved သို့ Closed ဖြစ်သည့် ရာခိုင်နှုန်း။ Average resolution — Submit မှ Resolve အထိ ရက်။',
      },
      {
        en: 'Reports and the Excel export list case numbers, category, status, priority, department (“Not disclosed” for anonymous cases), dates and the lead investigator; never the subject, description, notes or submitter. Every export is logged.',
        my: 'Report များနှင့် Excel export တွင် Case နံပါတ်၊ Category၊ Status၊ Priority၊ ဌာန (အမည်မဖော်လျှင် “Not disclosed”)၊ ရက်စွဲများနှင့် Lead investigator ကိုသာ ပြသည် — Subject၊ ဖော်ပြချက်၊ Note သို့ Submit လုပ်သူကို ဘယ်တော့မှ မပြ။ Export တိုင်းကို မှတ်တမ်းတင်သည်။',
      },
    ],
  },
];
