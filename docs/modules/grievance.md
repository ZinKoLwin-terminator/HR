# Grievance Module — ဝန်ထမ်း တိုင်ကြားချက် (Employee Grievance) စီမံခန့်ခွဲမှု

> Customer Presentation အတွက် Module တစ်ခုချင်းစီ ရှင်းလင်းချက်။ အနှစ်ချုပ်ကို [CLIENT-MODULE-GUIDE.md](../CLIENT-MODULE-GUIDE.md) တွင် ကြည့်ပါ။ စည်းမျဉ်း အပြည့်အစုံ (English) ကို [FEATURES.md](../FEATURES.md) ၏ "Grievances" အပိုင်း နှင့် `api/README.md` ၏ "HTTP contract (grievances)" အပိုင်းတွင် ကြည့်ပါ။

> **အရေးကြီး:** ဤ Module ကို Branch `feature/grievance` (Screen များ)၊ `feature/api-grievance` (API) နှင့် Database Branch `feature/schema-grievance` တွင် တည်ဆောက်ထားပြီး `main` သို့ မရောက်သေးပါ။ Demo (GitHub Pages) Build တွင် ဒေတာအားလုံး **စိတ်ကူးယဉ် နမူနာဒေတာ** ဖြစ်ပြီး Sign-in၊ Permission နှင့် **အမည်မဖော်မှု (Anonymity) သည် Simulation သာ** ဖြစ်သည် — လုံခြုံရေး မဟုတ်ပါ။ Scope၊ Redaction နှင့် Workflow ကို **API (Server) ကသာ** တကယ် ကာကွယ်သည်။

## 1. ဘာကြောင့် လိုအပ်သလဲ

ဝန်ထမ်း တိုင်ကြားချက်များ (နှောင့်ယှက်မှု၊ ခွဲခြားဆက်ဆံမှု၊ လုံခြုံရေး၊ လစာ ...) ကို ပါးစပ်၊ Email သို့ စာရွက်ဖြင့် လက်ခံလေ့ရှိသဖြင့် ပျောက်ဆုံးတတ်၊ ဘယ်သူ ကိုင်တွယ်နေသလဲ မသိရ၊ တိုင်ကြားသူ၏ လျှို့ဝှက်ချက် မကာကွယ်နိုင်ဖြစ်တတ်သည်။ Grievance Module က **တင်ပြခြင်း → လက်ခံခြင်း → စုံစမ်းခြင်း → ဖြေရှင်းချက် အတည်ပြုခြင်း → ပိတ်ခြင်း** ကို တစ်နေရာတည်းတွင် Case နံပါတ်၊ တာဝန်ခံ၊ Target date နှင့် ပြင်၍မရသော History ဖြင့် လုပ်ပေးပြီး **အမည်မဖော်ဘဲ တင်ပြခွင့်** (Category ခွင့်ပြုလျှင်) ပေးသည်။

## 2. အလုပ်လုပ်ပုံ (Flow)

```
① တင်ပြခြင်း (ဝန်ထမ်း)      Draft အဖြစ် သိမ်း → Declaration အတည်ပြု → Submit (Case နံပါတ် GRV-yyyy-nnnnn ရ)
        ↓
② လက်ခံခြင်း (HR Management) Priority နှင့် Target date ဖြင့် Acknowledge (Critical ဆိုလျှင် ချက်ချင်း Escalate)
        ↓
③ Review / Assign             HR ကိုယ်တိုင် Review လုပ် သို့ Investigator (Lead / Member) Assign
        ↓
④ စုံစမ်းခြင်း (Investigator)  Note၊ Interview၊ Finding၊ Recommendation၊ ဖိုင်၊ ဝန်ထမ်းထံ အချက်အလက် ထပ်တောင်း
        ↓
⑤ ဖြေရှင်းချက် အဆိုပြု        Lead Investigator (သို့ Review အဆင့်တွင် HR Management)
        ↓
⑥ အတည်ပြုခြင်း (Reviewer)    Approve → Resolved၊ သို့ အကြောင်းပြချက်ဖြင့် ပြန်ပို့
        ↓
⑦ Close → (ထိန်းသိမ်းကာလ ပြည့်လျှင်) Archive
```

**Status များ:**

```
Draft → Submitted → Acknowledged → Under Review / Assigned → Under Investigation → Resolution Proposed → Resolved → Closed
Awaiting Employee Response (ဝန်ထမ်း ပြန်ဖြေလျှင် မူလ Status သို့ ပြန်)
Rejected (Submitted / Acknowledged / Under Review မှ အကြောင်းပြချက်ဖြင့်)
Reopened (Resolved / Rejected / Closed မှ အကြောင်းပြချက်ဖြင့်)
```

**Transition ဇယား** (API ၏ `GrievanceCaseWorkflow` နှင့် UI ၏ `core/utilities/grievance-workflow.ts` တူညီ၊ Golden file `docs/test-data/grievance-transition-cases.json` ဖြင့် နှစ်ဘက်လုံး စစ်သည်):

| From | Action | To | လုပ်နိုင်သူ |
|---|---|---|---|
| Draft | Submit (Declaration မဖြစ်မနေ) | Submitted | ပိုင်ရှင် (`grievance:Create`) |
| Submitted | Acknowledge (Priority + Target date) | Acknowledged | Management Edit |
| Acknowledged / Reopened | Start review | Under Review | Management Edit |
| Acknowledged / Under Review / Reopened | Assign Lead | Assigned | Management Edit |
| Assigned / Under Investigation / Awaiting Employee Response | Lead အသစ်ဖြင့် အစားထိုး (အကြောင်းပြချက် လို) | မပြောင်း | Management Edit |
| Acknowledged မှ စ၍ ဖွင့်ထားသော Status | Member ထည့် / ဖြုတ် (Lead ကို ဖြုတ်၍မရ၊ အစားထိုးရုံသာ) | မပြောင်း | Management Edit |
| Assigned | Start investigation | Under Investigation | Assign ခံရသော Investigator Edit သို့ Management Edit |
| Under Review / Assigned / Under Investigation | Request information | Awaiting Employee Response | Management Edit သို့ Assign ခံရသော Investigator Edit |
| Awaiting Employee Response | Respond (Message) | မူလ Status (`ReturnStatus`) | ပိုင်ရှင် |
| Under Investigation | Propose resolution | Resolution Proposed | Lead Investigator Edit |
| Under Review | Propose resolution | Resolution Proposed | Management Edit |
| Resolution Proposed | Approve resolution | Resolved | `grievance-review:Approve` — အဆိုပြုသူ မဟုတ်ရ၊ ထို Case ၏ Investigator မဟုတ်ရ |
| Resolution Proposed | Send proposal back (အကြောင်းပြချက်) | Under Investigation (Lead ရှိလျှင်) / Under Review | အထက်ပါအတိုင်း |
| Submitted / Acknowledged / Under Review | Reject (အကြောင်းပြချက်) | Rejected | Management Edit |
| Resolved | Close | Closed | Management Edit |
| Resolved / Rejected | Reopen (အကြောင်းပြချက်) | Reopened | Management Edit |
| Closed | Reopen (အကြောင်းပြချက်) | Reopened | Management Edit **နှင့်** `grievance-review:Approve` |
| ဖွင့်ထားသော Status တိုင်း | Escalate (အကြောင်းပြချက်၊ တစ်ကြိမ်သာ) | မပြောင်း | Management Edit သို့ Assign ခံရသော Investigator Edit |
| Closed / Rejected (Legal hold မရှိ) | Archive | မပြောင်း (Archive ရက် ထည့်) | Management Delete |

- Transition တိုင်းကို `grv.StatusHistory` တွင် ရေးသည် (ပြင်/ဖျက်၍ မရ)။
- Error များ: `rowVersion` ပုံစံမှား **400**၊ ကိုယ့် Scope ပြင်ပ Case **404**၊ Role မမှန် **403**၊ Status မမှန် သို့ ဒေတာ ဟောင်းနေ (Stale) **409**။
- **Overdue** = ဖွင့်လျက်ရှိပြီး Target date ကျော်သွားသည် (Query လုပ်ချိန်တွင် တွက်သည်)။ Awaiting Employee Response အတွင်း Target date **ရပ်မထား** — ဆက်ရေတွက်သည်။
- ဝန်ထမ်းက Submit ပြီးနောက် Withdraw လုပ်၍ မရ (v1)။ Draft ကိုသာ ဖျက်နိုင်သည်။
- Submit သည် Draft ကို အရင် စစ်ပြီးမှ Case နံပါတ် ယူသည်။ Critical ဖြင့် Acknowledge လုပ်လျှင် Escalate **တစ်ကြိမ်သာ** ဖြစ်သည် (နှစ်ကြိမ် မဖြစ်)။
- **Reopen လုပ်လျှင် Active Assignment အားလုံး ပြီးဆုံးသွားသည်** (တိုင်ကြားသူ မမြင်ရသော "Case reopened" Unassign မှတ်တမ်းများ)။ HR က Lead ကို ပြန် Assign လုပ်ရသည် (လူတူကို ပြန်ရွေးနိုင်သည်)။

## 3. Screen တစ်ခုချင်းစီ

Sidebar Group **Grievances**၊ Route များ `/grievances` အောက်တွင်။ Route တိုင်းတွင် ကိုယ်ပိုင် Permission ရှိသည် (UI တွင် UX သာ၊ Server က တူညီသော Key ဖြင့် ထပ်စစ်သည်)။

| Screen | Route | လိုအပ်သော Permission | ရည်ရွယ်ချက် |
|---|---|---|---|
| **Overview** | `/grievances/overview` | `grievance:View` | ကိုယ့် Case အရေအတွက်၊ မဖတ်ရသေးသော Update၊ နောက်ဆုံး Update များ၊ "New grievance" |
| **My Grievances** | `/grievances/my`၊ `/my/:id` | `grievance:View` | ကိုယ့် Case များ (Draft ပါ)၊ Timeline၊ HR နှင့် Message၊ Shared ဖိုင်၊ ဖြေရှင်းချက် |
| **New / Edit draft** | `/grievances/my/new`၊ `/my/:id/edit` | `grievance:Create` | တင်ပြချက် ဖောင် (မသိမ်းရသေးလျှင် ထွက်ခါနီး မေးသည်) |
| **Dashboard** | `/grievances/dashboard` | `grievance-management:View` | HR ကိန်းဂဏန်းများ နှင့် Chart များ |
| **Case Management** | `/grievances/cases`၊ `/cases/:id` | `grievance-management:View` | Submit ပြီးသော Case အားလုံး၊ Triage၊ Assignment၊ Corrective action၊ Command အားလုံး |
| **Investigations** | `/grievances/investigations`၊ `/:id` | `grievance-investigation:View` | ကိုယ့်ကို Assign ထားသော Case များ |
| **Reviews** | `/grievances/reviews`၊ `/:id` | `grievance-review:View` | Escalated Case များ နှင့် ဆုံးဖြတ်ရမည့် Proposal များ |
| **Review Summary** | `/grievances/review-summary` | `grievance-review:View` | Review Queue ကိန်းဂဏန်း နှင့် Summary (Case စာရင်း၊ အမည် မပါ) |
| **Reports** | `/grievances/reports` | `grievance-management:View` | Case Report၊ Excel Export၊ Print (PDF) |
| **Access log** | `/grievances/access-log` | `grievance-management:View` + HR Administrator | Module တစ်ခုလုံး၏ Access Log (Reports မှ Link၊ Sidebar တွင် မပါ) |
| **Categories** | `/grievances/categories` | `grievance-management:View` (ပြင်ရန် Create/Edit/Delete) | Grievance အမျိုးအစား |

`/grievances` ကို ဖွင့်လျှင် Role ကြည့်ခွင့်ရှိသော ပထမဆုံး Screen သို့ ရောက်သည်။

### Role များ နှင့် Permission များ

| Permission Module | Action | ဘယ်သူ (Starter Grant) | ဘာကို မြင်/လုပ်ရသလဲ |
|---|---|---|---|
| `grievance` | View, Create | Role အားလုံး (Employee၊ HR Officer၊ Department Manager၊ Payroll Officer၊ Recruiter) | ကိုယ့် Case များသာ (Draft ပါ)။ HR ဘက်က မည်သူမဆိုကို "HR team" ဟုသာ ပြ |
| `grievance-investigation` | View, Edit | HR Officer | Active Assignment ရှိသော Case များ (Draft မဟုတ်၊ Archive မလုပ်ရသေး)။ Corrective action နှင့် Restricted ဖိုင် မမြင်ရ |
| `grievance-management` | View, Create, Edit, Delete | Starter Role မရှိ (HR Administrator သာ) | Submit ပြီးသော Case အားလုံး (Archived ကို Filter ဖြင့်သာ)၊ Category CRUD၊ Dashboard၊ Report။ Delete = Archive |
| `grievance-review` | View, Approve | Starter Role မရှိ (HR Administrator က Settings တွင် ပေးရ) | Escalated Case၊ Pending Proposal၊ ကိုယ်ဆုံးဖြတ်ခဲ့သော Case (ဖတ်ရန်သာ)။ Finding၊ Recommendation နှင့် Proposal သာ — Note အကြမ်း၊ Interview၊ Message မမြင်ရ |

- **HR Administrator** သည် Permission အားလုံး အလိုအလျောက် ရသည်။ Auditor Role တွင် Grievance Module မပါ (လျှို့ဝှက် Case ဒေတာ)။
- **Case team (Investigation၊ Management၊ Review) မည်သူမျှ ကိုယ်တိုင် Submit လုပ်ခဲ့သော Case (Named သို့ Anonymous) နှင့် ကိုယ့်အကြောင်း Case (Subject employee) ကို မမြင်ရ** — စာရင်းထဲ မပါ၊ ဖွင့်လျှင် 404။
- Employee Record မချိတ်ထားသော Built-in Administrator သည် Grievance မတင်နိုင်၊ Investigator အဖြစ် Assign မခံရ (403)။

## 4. အမည်မဖော်သော Grievance (Anonymity)

**ပုံစံ (Keyed pseudonym၊ ဆုံးဖြတ်ချက် A2):** Anonymous Case တွင် ဝန်ထမ်း ID ကို **မသိမ်းဘဲ** `grv.CaseSubmitter.SubmitterPseudonym` တွင် HMACSHA256 (Key = `Grievance:PseudonymKey`၊ Message = `apex-hrms:grievance-submitter:<employee id>`) ကိုသာ သိမ်းသည်။ Key သည် Database ထဲတွင် မရှိ။ "My Grievances" က `EmployeeId = ကိုယ်` **သို့** `Pseudonym = HMAC(ကိုယ်)` ဖြင့် ကိုယ့် Case များကို ရှာသည်။ Legal disclosure (တရားဝင် ဖော်ထုတ်ခြင်း) ကို API v1 တွင် မထည့်ထားပါ။

- **Anonymous ဖြစ်မဖြစ်ကို Draft ကို ပထမဆုံး Save လုပ်ချိန်တွင် သတ်မှတ်ပြီး ပြင်၍ မရ** — ပြောင်းလိုလျှင် Draft အသစ် စရမည် (API က 400 "Start a new draft to change how you report.")။ Submit ပြီးလျှင် Database Trigger ကလည်း ပြောင်းခွင့် မပေး။
- **Salary and Compensation Issues (`COMPENSATION`)** Category သည် Anonymous ခွင့် မပြု (ကိုယ်ပိုင် လစာ အချက်အလက် လိုသဖြင့်)။ Category တိုင်းတွင် "Anonymous reports allowed" ရွေးချယ်မှု ရှိသည်။

**ဘာကို ကာကွယ်ပေးသလဲ:**

| HR / Case team မြင်ရသည် | HR / Case team မမြင်ရ |
|---|---|
| Category၊ Subject၊ Description၊ Incident date၊ Location | ဝန်ထမ်း အမည်၊ Code၊ ဌာန၊ ရာထူး (Placement ကို လုံးဝ မသိမ်း) |
| တိုင်ကြားသူ ဖော်ပြသော ပါဝင်သူ နှင့် ဌာန၊ တွဲထားသော ဖိုင် | User account — History၊ Message၊ ဖိုင်၊ Access log တွင် User/Employee ID မပါ |
| Message များ ("Anonymous submitter" ဟု လက်မှတ်) | Report နှင့် Chart တွင် ဌာန = "Not disclosed" |

- Anonymous တိုင်ကြားသူ **Notification မရ** — Unread badge ကိုသာ မြင်ရသည်။
- Anonymous Case ၏ တိုင်ကြားသူကို Investigator အဖြစ် Assign လုပ်လျှင် API က **မငြင်း** (ငြင်းလျှင် တိုင်ကြားသူကို ဖော်ပြသကဲ့သို့ ဖြစ်မည်) — သို့သော် ထိုသူသည် Case ကို ဖွင့်လျှင် **404** သာ ရသည်။
- Anonymous တိုင်ကြားသူ Upload လုပ်သော **DOCX/XLSX** (Document properties)၊ **JPEG** (EXIF၊ XMP၊ IPTC၊ Comment) နှင့် **PNG** (Text၊ EXIF၊ Time chunk) ဖိုင်များမှ ကိုယ်ရေးအချက်အလက် Metadata ကို Server က ဖယ်ရှားပြီးမှ သိမ်းသည်။ ဖယ်ရှား၍ မရသော ဖိုင်ကို ငြင်းသည်။ **PDF ကိုမူ ဖယ်ရှားခြင်း မရှိ** (ဖောင်တွင် သတိပေးသည်)။

**ကျန်ရှိနေသော အန္တရာယ်များ (Residual risks — Customer ကို ရိုးသားစွာ ပြောရန်):**

| အန္တရာယ် | ရှင်းလင်းချက် |
|---|---|
| **အချိန် (Timing)** | Submit / ပြန်ဖြေသည့် အချိန်ကို အလုပ်ရှိသူ၊ Online ရှိသူ နှင့် တိုက်ဆိုင်ကြည့်နိုင်သည်။ `RowVersion` သည် Database တစ်ခုလုံး၏ Counter ဖြစ်ပြီး History/Access log/Notification တွင် အချိန် ပါသည် |
| **ရေးသားပုံ** | ရေးသားဟန် နှင့် ကိုယ်တိုင်သာ သိသော အသေးစိတ်က ညွှန်ပြနိုင်သည် |
| **PDF Metadata** | PDF ၏ Author၊ Software၊ ရက်စွဲ မဖယ်ရှား |
| **ဖိုင်အမည်** | ဖိုင်အမည်ကို HR မြင်ရသည် — Upload မလုပ်မီ အမည်ပြောင်းရန် |
| **ဌာနငယ်** | ဖြစ်ရပ်ကိုယ်တိုင်က လူအနည်းငယ်အထိ ကျဉ်းစေနိုင်သည်။ Summary ၏ Date range ကို တစ်ရက်ခြား မေးပြီး နှိုင်းယှဉ်လျှင် Case ၃ ခုအောက် ဌာန၏ **Named** Case အရေအတွက်ကို ပြန်တွက်နိုင်သည် (Anonymous Case တွင် ဌာန မပါသဖြင့် မထိခိုက်) |
| **Count differencing** | Dashboard ကိန်းဂဏန်းသည် ကြည့်သူတိုင်း တူသော်လည်း Case စာရင်း၊ Report နှင့် Recent activity တွင် ကိုယ့်အကြောင်း Case များ မပါ။ စုစုပေါင်း နှင့် စာရင်းအရှည် နှိုင်းယှဉ်လျှင် "ငါ့အကြောင်း Case ရှိသည်" ဟု သိနိုင်သည် |
| **Database အဆင့် ချိတ်ဆက်နိုင်မှု** | API သည် Employee ID နှင့် Pseudonym ကို SQL Statement တစ်ခုတည်းတွင် မပို့ပါ — သို့သော် Parameter ဖမ်းသော Trace/Extended Events/Query Store ရှိလျှင် Administrator က Statement များကို ဆက်စပ်ကြည့်နိုင်သည် (အပိုင်း ၉) |
| **Key ပျောက်ခြင်း / ပြောင်းခြင်း** | Key ပြောင်းလျှင် သို့ ပျောက်လျှင် Anonymous Case အားလုံး ၎င်းတို့ တိုင်ကြားသူနှင့် အပြီးတိုင် ပြတ်သွားသည် ("My Grievances" တွင် မပေါ်တော့၊ ပြန်ချိတ်၍ မရ — ဒီဇိုင်းအရ) |

## 5. ထိန်းချုပ်မှု နှင့် ကာကွယ်မှု

- **Scope ကို Database Query ထဲတွင် စစ်သည်** — Scope ပြင်ပ Case သည် 404 ဖြစ်ပြီး စာရင်း၊ Report နှင့် Export တွင် မပါ။ Tier များစွာ ရှိသူ၏ Scope = Tier များ ပေါင်းလဒ်။
- **Conflict of interest:** Subject employee ကို သို့ Named Case ၏ တိုင်ကြားသူကို Investigator အဖြစ် Assign လုပ်လျှင် **အထွေထွေ Message တစ်ခုတည်း** ("conflict of interest") ဖြင့် ငြင်းပြီး (ဘယ်အကြောင်းကြောင့်လဲ မပြော) AccessRefused အဖြစ် Log ရေးသည်။ Investigator ရွေးရန် စာရင်းကို Case အလိုက် မစစ်ထုတ်ပါ (အမည် ပျောက်နေလျှင် တိုင်ကြားသူကို ဖော်ပြသကဲ့သို့ ဖြစ်မည်)။ Triage တွင်လည်း ထို Case ၏ Active Investigator ကို Subject employee အဖြစ် မသတ်မှတ်ရ (Triage request တွင် Legal hold တန်ဖိုး မဖြစ်မနေ ပါရမည်)။
- **ကိုယ့် Proposal ကို ကိုယ်တိုင် မဆုံးဖြတ်ရ**၊ Investigator အဖြစ် Assign ခံထားရစဉ်လည်း မဆုံးဖြတ်ရ (403)။ Closed Case ကို Reopen လုပ်ရန် Management Edit နှင့် Review Approve နှစ်ခုလုံး လိုသည်။
- **တိုင်ကြားသူ မြင်ရသည်များ:** Timeline (Status ပြောင်းခြင်းများ — Proposal နှင့် ၎င်းကို ပြန်ပို့ခြင်းမှလွဲ၍)၊ Message၊ Shared ဖိုင်၊ Reject အကြောင်းပြချက်၊ ဖြေရှင်းချက် (Resolved/Closed ဖြစ်ပြီး Reviewer က မျှဝေခွင့်ပြုမှသာ)။ Note၊ Corrective action၊ Priority၊ Assignment၊ Case-team ဖိုင် နှင့် HR အမည် **မမြင်ရ**။
- **Detail ကို Tier အလိုက် ချုံ့သည်:** Management — အားလုံး၊ Investigator — Corrective action နှင့် Restricted ဖိုင် မပါ၊ Reviewer သက်သက် — Finding နှင့် Recommendation Note၊ Shared ဖိုင်၊ Proposal သာ (Message၊ Corrective action၊ Assignment/Escalation အကြောင်းပြချက်၊ Request information မေးခွန်း မပါ)။
- **ဖိုင် Visibility:** Shared (တိုင်ကြားသူ + Case team)၊ CaseTeam (Management + Assign ခံရသော Investigator)၊ Restricted (Management သာ)။ တိုင်ကြားသူ၏ ဖိုင်သည် အမြဲ Shared။
- **Append-only:** `grv.StatusHistory`၊ `grv.InvestigationNote`၊ `grv.Communication`၊ `grv.AuditLog` ကို API ၏ Database Account က ပြင်/ဖျက်၍ မရ (DENY)။ Trigger များက Submit ပြီးသော Case ၏ နံပါတ်၊ Submit ချိန်၊ Anonymity၊ Placement ကို ပြောင်းခွင့် မပေး၊ Closed Case ကို Reopen မှလွဲ၍ ပြောင်းခွင့် မပေး၊ Draft မှလွဲ၍ ဖျက်ခွင့် မပေး (409)။ Reporting Account (`hrms_report`) သည် `grv` Schema ကို လုံးဝ မဖတ်နိုင် (DENY SELECT)။
- **Company-wide Activity Log (`app.ActivityLog`) တွင် Grievance ဘာမှ မရေး** — Module ကိုယ်ပိုင် Access Log သာ။

### Access Log နှင့် Notification

| အကြောင်းအရာ | စည်းမျဉ်း |
|---|---|
| **Access Log (`grv.AuditLog`)** | Case team ၏ ဖတ်ခြင်း (ViewCase၊ ViewHistory)၊ Download၊ Upload၊ Export၊ Assign/Unassign၊ Status ပြောင်းခြင်း၊ Triage၊ Note၊ Message၊ Corrective action၊ AccessRefused။ Detail တွင် Code နှင့် ID သာ — Case အကြောင်းအရာ မပါ |
| တိုင်ကြားသူ၏ လုပ်ဆောင်ချက် | **ဖတ်ခြင်း နှင့် Draft ကို လုံးဝ Log မရေး**။ Submit ပြီးသော Case တွင် Message၊ ဖိုင် စသည်ကို **User ID မပါဘဲ** (Role = Submitter) ရေးသည် — Named ဖြစ်စေ Anonymous ဖြစ်စေ |
| ဘယ်သူ ကြည့်နိုင်သလဲ | Case တစ်ခု၏ Log နှင့် Module တစ်ခုလုံး၏ Log — `grievance-management:View` **နှင့်** HR Administrator Role (ကိုယ် Submit လုပ်ခဲ့သော / ကိုယ့်အကြောင်း Case မပါ) |
| **Notification** | **In-app သာ** (Email မရှိ)။ Title `Grievance GRV-…`၊ စာသား `Grievance GRV-…: <ဖြစ်ရပ်>.` သာ (Category၊ Subject၊ အမည် မပါ)။ Demo (Mock) တွင်လည်း တူညီ |
| "a new grievance was submitted" | `grievance-management:View` ရှိသူများ |
| "the employee responded" | `grievance-management:View` ရှိသူများ နှင့် ထို Case ၏ Active Investigator များ |
| "you were assigned to investigate it" | Assign ခံရသော Investigator |
| "a resolution was proposed"၊ "it was escalated as urgent" (Immediate escalation သာ) | `grievance-review:Approve` ရှိသူများ |
| "there is an update on your grievance" | **Named** တိုင်ကြားသူ — မြင်ရသော History အဆင့်၊ HR Message သို့ Shared ဖိုင် တိုင်း |
| ဘယ်သူ မရ | **Anonymous တိုင်ကြားသူ** (Unread badge သာ)၊ **Subject employee** (ဘယ်တော့မှ)၊ Case team ဖြစ်ရပ်တွင် လုပ်ဆောင်သူ ကိုယ်တိုင် |

### ဖိုင်တွဲခြင်း နှင့် ကန့်သတ်ချက်များ

| ကန့်သတ်ချက် | တန်ဖိုး |
|---|---|
| ဖိုင်အမျိုးအစား | PDF၊ PNG၊ JPEG၊ Word (.docx)၊ Excel (.xlsx) — **ဖိုင်ထဲက Content ဖြင့် စစ်သည်** |
| ဖိုင်အရွယ် | တစ်ခုလျှင် **10 MB** အထိ |
| Case တစ်ခုလျှင် | တိုင်ကြားသူ ဖိုင် **၁၀ ခု**၊ Case team ဖိုင် **၂၀ ခု** (သီးခြား ရေတွက်) |
| တိုင်ကြားသူ တစ်ရက် Upload | Account တစ်ခုလျှင် **100 MB** (`Grievance:SubmitterDailyUploadBytes`) |
| Upload အမြန်နှုန်း | Account တစ်ခုလျှင် **တစ်မိနစ် ၁၀ ကြိမ်** (`Grievance:FileUploadsPerMinute`၊ KPI နှင့် သီးခြား) |
| Draft အရေအတွက် | ဝန်ထမ်း တစ်ဦးလျှင် ဖွင့်ထားသော Draft **၅ ခု** အထိ |
| ဘယ်အချိန် တွဲနိုင်သလဲ | တိုင်ကြားသူ — Draft သို့ ဖွင့်ထားသော Case၊ Case team — ဖွင့်ထားသော Case သာ (Resolved၊ Rejected၊ Closed၊ Archived တွင် မရ) |

**သတိပြုရန်:** လက်ရှိတွင် **Virus/Malware စစ်ဆေးမှု မရှိသေးပါ**။ Production မတိုင်မီ ထည့်ရန် လိုသည်။

### ထိန်းသိမ်းကာလ (Retention)၊ Archive နှင့် Legal hold

- Close သို့ Reject လုပ်ချိန်တွင် `RetentionUntil` = ထိုရက် + **၇ နှစ်** (`Grievance:RetentionYears`)။ Reopen လုပ်လျှင် ရှင်းလင်းသွားသည်။
- **Archive** (Soft delete) — Closed သို့ Rejected Case ကို Management Delete ဖြင့်။ Archive လုပ်ထားသော Case သည် **ဖတ်ရန်သာ** ဖြစ်ပြီး Case Management ၏ "Archived" Filter ဖြင့်သာ ပေါ်သည် (Investigator နှင့် Reviewer စာရင်းတွင် မပါ)။
- **Legal hold** ရှိသော Case ကို Archive မလုပ်နိုင် (Triage တွင် ဖွင့်/ပိတ်)။
- **Purge (အပြီးဖျက်ခြင်း) v1 တွင် မပါ** — နောင်တွင် လူကိုယ်တိုင် Run ရမည့် Command အဖြစ်သာ စီစဉ်ထားသည်။

## 6. Dashboard၊ Report၊ Excel နှင့် PDF

- **ကိန်းဂဏန်းများ ကြည့်သူတိုင်း တူညီသည်** — Submit ပြီးသော Case အားလုံး (Archived ပါ) ကို ရေတွက်ပြီး ကြည့်သူ၏ ကိုယ်ပိုင် Case များကို မချန်ပါ (ချန်လျှင် HR နှစ်ဦး၏ စုစုပေါင်းကို နှိုင်းယှဉ်၍ တိုင်ကြားသူကို သိနိုင်မည်)။
- **HR Dashboard:** စုစုပေါင်း၊ New၊ Under Review၊ Under Investigation၊ Pending resolution၊ Resolved၊ Closed၊ Overdue၊ Category / ဌာန / Priority / Status / လအလိုက် (နောက်ဆုံး ၁၂ လ) Chart များ၊ Recent activity (နောက်ဆုံး ၁၀ ခု၊ Case နံပါတ်သာ၊ ကိုယ့်အကြောင်း Case မပါ)။
- **Resolution rate** = (Resolved + Closed) ÷ (ရေတွက်သော Case − Rejected) × 100 (ဒသမ ၂ နေရာ)။ **Average resolution days** = Submit ရက်မှ Resolve ရက်အထိ (ဒသမ ၁ နေရာ)။
- **ဌာန:** Submit ချိန်က ကူးယူထားသော ဌာန။ Anonymous = "Not disclosed"။ Case **၃ ခုအောက်** ဌာနများကို "Other / suppressed" အဖြစ် ပေါင်းသည်။
- **Review Summary:** Reviewer ၏ Queue ကိန်းဂဏန်း နှင့် Summary — ဌာန Filter **မပါ**၊ Case စာရင်း နှင့် အမည် မပါ။
- **Reports:** Submit ရက်၊ ဌာန၊ Category၊ Status၊ Priority၊ Investigator၊ ဖြေရှင်းရက်အရေအတွက်၊ Overdue ဖြင့် Filter။ Row များတွင် ကိုယ့်အကြောင်း Case မပါ၊ ကိုယ် Submit လုပ်ခဲ့သော Case ကိုမူ ထားသည် (Row တွင် တိုင်ကြားသူ မပြ)။
- **Excel Export** (`grievance-cases`၊ **Export သာ၊ Import မရ**၊ `excel:Export` + `grievance-management:View`): Case No၊ Category၊ Status၊ Priority၊ ဌာန ("Not disclosed")၊ ရက်စွဲများ၊ Days၊ Overdue၊ Lead Investigator၊ Escalated (Status/Priority ကို Code ဖြင့်၊ Overdue/Escalated ကို Yes/No၊ နောက်ဆုံး Submit အရင်၊ Case နံပါတ် Search ပါ — Demo နှင့် API တူညီ)။ **Subject၊ Description၊ Note၊ တိုင်ကြားသူ ဘယ်တော့မှ မပါ**။ Export တိုင်းကို Access Log တွင် (Row အရေအတွက်ဖြင့်) ရေးသည်။
- **PDF:** Report Screen ၏ Print (Browser ၏ "Save as PDF") — ဖွင့်ထားသော စာမျက်နှာကိုသာ Print လုပ်သည်။

## 7. Demo (Mock) နှင့် API ကွာခြားချက်

| | Demo Build (Mock) | API Build (`npm run start:api`) |
|---|---|---|
| ဒေတာ | Browser ထဲတွင်သာ (`hrms.data.v2.grievance_*`)၊ နမူနာ Case ၁၈ ခု (Anonymous ၆ ခု) — Sample data Build တွင်သာ | SQL Server (`grv` Schema) |
| Permission / Scope | Settings ရှိ Simulated session ၏ Grant ဖြင့် API ၏ စည်းမျဉ်းကို **ပုံတူ ပြရုံသာ — ဘာမှ မတားဆီး** | Request တိုင်းတွင် Server က စစ်သည် |
| Pseudonym | Key ပုံသေ FNV-1a Hash — **လျှို့ဝှက်ချက် မဟုတ်၊ Bundle မှ မည်သူမဆို ပြန်တွက်နိုင်** | HMACSHA256 + Secret key |
| ဖိုင် | Memory ထဲတွင်သာ (Reload လျှင် ပျောက်)၊ Metadata **မဖယ်ရှား**၊ Rate limit မရှိ | Folder တွင် အမြဲ သိမ်း၊ Anonymous ဖိုင်၏ Metadata ဖယ်ရှား၊ Rate limit |
| Notification | Demo Notification စာရင်းတွင် လက်ခံသူ တစ်ဦးချင်း (စာသား နှင့် လက်ခံသူ API နှင့် တူ) | `app.Notification` |

Demo Build တွင် အမည်မဖော်မှု နှင့် Access control **မရှိပါ**။ ယခင်က ဒေတာ သိမ်းထားသော Browser တွင် Role Grant အဟောင်း ကျန်နေနိုင်သဖြင့် Grievance Menu မပေါ်လျှင် **Settings → General → Reset data** ကို နှိပ်ပါ။

## 8. Configuration (API)

| Key | အဓိပ္ပာယ် |
|---|---|
| `Grievance:FileFolder` | Grievance ဖိုင်များ **အမြဲ** သိမ်းသော Folder (Default `App_Data/grievance-files`၊ `appsettings.json` တွင် `D:\HrmsData\grievance-files`၊ Development တွင် `D:\HrmsDev\grievance-files`)။ KPI Evidence Folder နှင့် တူ၍ မရ (Startup ငြင်း) |
| `Grievance:PseudonymKey` | **Secret**။ Base64 (Decode လုပ်လျှင် ၃၂ Byte အနည်းဆုံး)။ User secrets သို့ Environment variable `Grievance__PseudonymKey` ဖြင့်သာ — **appsettings၊ Source control၊ Log ထဲ ဘယ်တော့မှ မထည့်ရ**။ `appsettings.Development.json` ရှိ Development တန်ဖိုးကို LocalDB / ဤစက် Database မှလွဲ၍ ပတ်ဝန်းကျင်တိုင်းတွင် ငြင်းသည်။ Key မရှိ၊ Base64 မဟုတ်၊ ၃၂ Byte မပြည့်လျှင် Startup မလုပ် |
| `Grievance:RetentionYears` | Closed/Rejected Case ထိန်းသိမ်းနှစ် (Default 7၊ 1–100) |
| `Grievance:FileUploadsPerMinute` | Account တစ်ခုလျှင် တစ်မိနစ် Upload (Default 10) |
| `Grievance:SubmitterDailyUploadBytes` | တိုင်ကြားသူ Account တစ်ခု တစ်ရက် Upload Byte (Default 100 MB၊ 1 MB–10 GB) |

## 9. Operator များအတွက် မှတ်ချက်

- **`grv` Table များပေါ်တွင် Parameter တန်ဖိုး ဖမ်းသော Statement-level Trace သို့ Extended Events Session မ Run ပါနှင့်**၊ Query Store ၏ Parameter capture (နှင့် Captured parameter ဖြင့် Plan forcing) ကို ပိတ်ထားပါ — Session တစ်ခု၏ ဆက်တိုက် Statement များကို ဆက်စပ်၍ Anonymous တိုင်ကြားသူကို ခန့်မှန်းနိုင်သည်။
- **Pseudonym Key ကို Database Credential ကဲ့သို့ Secret store တွင် Backup လုပ်ပါ**။ Anonymous Case ရှိပြီးနောက် Key ကို **ဘယ်တော့မှ မပြောင်း၊ မပျောက်စေရ**။
- **File Folder:** Web root ပြင်ပ၊ API ၏ Account တစ်ခုတည်းသာ Read/Write၊ Encryption at rest၊ Database နှင့် **တစ်ပြိုင်နက်** Backup (ဖိုင်မပါသော Restore သည် Download 404 ဖြစ်)၊ Git ထဲ မထည့်။
- **Virus/Malware စစ်ဆေးမှု မရှိ** — Content၊ အရွယ်၊ အရေအတွက်ကိုသာ စစ်ပြီး Download အဖြစ်သာ ပေးသည်။
- Manager တစ်ဦးသည် Grievance ၏ Subject ဖြစ်လာလျှင် (သို့ တိုင်ကြားလျှင်) Case ကြာသရွေ့ ၎င်း၏ `grievance-management` Grant ကို ဖယ်ပြီး အခြား Manager ကို Assign လုပ်ပါ (Count differencing ကြောင့်)။
- **Separation of duties အပြည့် မရှိ** — HR Administrator သည် Grant အားလုံး ရှိသည်။ Review Grant ကို Management နှင့် မတူသော Role တွင် ထားပြီး Grant များကို ပုံမှန် ပြန်စစ်ပါ။
- Upload Rate limit နှင့် Daily quota သည် Instance တစ်ခုချင်း Memory ထဲတွင်သာ ([DEPLOYMENT.md](../DEPLOYMENT.md) အပိုင်း ၅)။

## 10. အခြား Module များနှင့် ချိတ်ဆက်မှု

| ဒေတာ | ဘယ်ကရသလဲ |
|---|---|
| ဝန်ထမ်း၊ ဌာန၊ ရာထူး၊ ဌာနခွဲ | Employees၊ Organization (Named Case ကို Submit လုပ်ချိန်တွင် ကူးယူ၊ Anonymous တွင် မကူး) |
| Investigator စာရင်း | `grievance-investigation:Edit` ရှိသော ဝန်ထမ်းများ |
| Permission များ | Settings → Permissions (`grievance`၊ `grievance-investigation`၊ `grievance-management`၊ `grievance-review`) |
| Notification | Header ၏ Notification (In-app) |
| Excel Export | Data Exchange (`grievance-cases` — Export သာ) |
| Activity Feed | **မချိတ်** — Grievance ကို Company-wide Activity တွင် မပြ |

## 11. Customer ထံ ပြောနိုင်သော အကျိုးကျေးဇူး

1. တိုင်ကြားချက်တိုင်း Case နံပါတ်၊ တာဝန်ခံ၊ Target date ဖြင့် ခြေရာခံနိုင် — ပျောက်ဆုံးခြင်း မရှိ။
2. Category ခွင့်ပြုလျှင် အမည်မဖော်ဘဲ တင်ပြနိုင်ပြီး HR နှင့် ဆက်သွယ်နိုင် — ကာကွယ်ပုံ နှင့် ကန့်သတ်ချက်ကို ရိုးသားစွာ ပြသည်။
3. Subject နှင့် တိုင်ကြားသူ ကိုယ်တိုင် Case ကို မမြင်ရ၊ Conflict of interest ရှိသူကို Assign မရ။
4. ဖြေရှင်းချက်ကို အခြား Reviewer က အတည်ပြုရ၊ History ပြင်မရ၊ ဘယ်သူ ဘာကြည့်ခဲ့သလဲ Access Log ရှိ။
5. Dashboard၊ Report၊ Excel နှင့် PDF — ကိုယ်ရေးအချက်အလက် မပါဘဲ။

## 12. Demo ပြသရန် အဆင့်များ (၄–၅ မိနစ်)

1. **My Grievances → New grievance** — Category ပြောင်းလျှင် Anonymous ခလုတ် ပိတ်ပုံ (Salary and Compensation)၊ "HR will see / will not see" နှင့် Residual risk စာသား ပြ → Draft သိမ်း → Declaration ဖြင့် Submit (Case နံပါတ် ရ)။
2. **Case Management** တွင် ထို Case ကို Acknowledge (Priority Critical ဆိုလျှင် Escalate ဖြစ်ပုံ) → Investigator Assign။
3. **Investigations** တွင် Start investigation → Note / Finding ထည့် → Request information → (My Grievances တွင် ပြန်ဖြေ) → Propose resolution။
4. **Reviews** တွင် Approve (မျှဝေမည်/မမျှဝေ ရွေး) → Case Management တွင် Close။
5. **Dashboard** ("Not disclosed"၊ "Other / suppressed") နှင့် **Reports** → Excel Export။

## 13. Production သို့ မရောက်မီ လိုအပ်သည်များ (Customer ကို ရိုးသားစွာ ပြောရန်)

| လိုအပ်ချက် | အခြေအနေ |
|---|---|
| ဖိုင် **Virus/Malware စစ်ဆေးမှု** | မပါသေး — ထည့်ရန် လိုသည် |
| PDF Metadata ဖယ်ရှားခြင်း | မပါ — တိုင်ကြားသူကို သတိပေးရုံသာ |
| Pseudonym Key ကို Secret store တွင် ထားခြင်း နှင့် Backup | Deployment တွင် လုပ်ရန် ([DEPLOYMENT.md](../DEPLOYMENT.md)) |
| File Folder ကို ACL၊ Encryption၊ Backup | Deployment တွင် လုပ်ရန် |
| `grv` ပေါ်တွင် Parameter ဖမ်းသော Trace / Query Store ပိတ်ခြင်း | DBA နှင့် သဘောတူရန် |
| Database Schema (`feature/schema-grievance`) ကို Publish | လူကိုယ်တိုင် Script စစ်ပြီးမှ — Local Database သို့သာ |
| Purge (ထိန်းသိမ်းကာလ ပြည့်ပြီးနောက် အပြီးဖျက်) | v1 တွင် မပါ |
| Email Notification၊ Withdraw၊ Legal disclosure | v1 တွင် မပါ |
| Demo Build ၏ Permission နှင့် Anonymity | Simulation သာ — ဘာမှ မတားဆီး (API က တားဆီးသည်) |
| `main` သို့ ပေါင်းခြင်း | မလုပ်ရသေး — ပေါင်းလျှင် Demo ကို Deploy လုပ်သည် |

ဤအချက်များကို Project Scope တွင် သီးခြားထည့်သွင်း သဘောတူညီပါ။
