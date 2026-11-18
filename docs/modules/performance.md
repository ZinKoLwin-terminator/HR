# Performance Module — KPI နှင့် ဝန်ထမ်းစွမ်းဆောင်ရည် စီမံခန့်ခွဲမှု

> Customer Presentation အတွက် Module တစ်ခုချင်းစီ ရှင်းလင်းချက်။ အနှစ်ချုပ်ကို [CLIENT-MODULE-GUIDE.md](../CLIENT-MODULE-GUIDE.md) တွင် ကြည့်ပါ။ စည်းမျဉ်း အပြည့်အစုံ (English) ကို [FEATURES.md](../FEATURES.md) ၏ "Performance" အပိုင်း နှင့် `api/README.md` ၏ "HTTP contract (KPI ...)" အပိုင်းများတွင် ကြည့်ပါ။

> **အရေးကြီး:** ဤ Module ကို Branch `feature/api-kpi` (API နှင့် Screen များ) နှင့် Database Branch `feature/schema-kpi` တွင် တည်ဆောက်ထားပြီး `main` သို့ မရောက်သေးပါ။ Demo (GitHub Pages) Build တွင် ဒေတာအားလုံး **စိတ်ကူးယဉ် နမူနာဒေတာ** ဖြစ်ပြီး Sign-in နှင့် Permission များသည် **Simulation သာ** ဖြစ်သည် — လုံခြုံရေး မဟုတ်ပါ။ တရားဝင် Score ကို **API (Server) ကသာ** တွက်သည်။

## 1. ဘာကြောင့် လိုအပ်သလဲ

နှစ်စဉ်/သုံးလပတ် စွမ်းဆောင်ရည် အကဲဖြတ်ရာတွင် ပစ်မှတ် (Target) များကို Excel တွင် သီးခြားထားပြီး၊ ရလဒ်ကို Email ဖြင့် ပို့၊ Manager က လက်ဖြင့် တွက်ရသဖြင့် အချိန်ကုန်၍ ညီညွတ်မှု မရှိဖြစ်တတ်သည်။ Performance Module က **ပစ်မှတ် သတ်မှတ်ခြင်း → ရလဒ်ထည့်ခြင်း → သုံးသပ်ခြင်း → အတည်ပြုခြင်း** ကို တစ်နေရာတည်းတွင် စည်းမျဉ်းတူ တွက်ချက်မှုတူဖြင့် လုပ်ပေးသည်။

## 2. အလုပ်လုပ်ပုံ (Flow)

```
① Setup (HR)          KPI Categories၊ KPI Templates၊ Evaluation Periods၊ Rating Bands
        ↓
② Assignments (HR)    ဝန်ထမ်းတစ်ဦးချင်း သို့ အစုလိုက် (Bulk) KPI သတ်မှတ် → Plan ကို 100% ဖြင့် Finalize
        ↓
③ My KPIs (ဝန်ထမ်း)   ရလဒ် (Actual) ထည့်၊ Evidence ဖိုင်တွဲ၊ Submit
        ↓
④ Reviews (Manager)   Rating နှင့် Feedback ပေး → Approve / Return / Reject
        ↓
⑤ HR Final Review     (Period က လိုအပ်မှသာ) HR က Finalize
        ↓
⑥ Dashboard / Reports  Score၊ Ranking၊ Department နှိုင်းယှဉ်ချက်၊ Excel Export၊ Print
```

**Status များ:**

```
Planning → Draft → Submitted → Under Review → Approved (HR Final Review လိုအပ်မှသာ) → Finalized
Returned (အကြောင်းပြချက်ဖြင့် ပြန်ပို့) → Resubmitted (ဝန်ထမ်း ပြင်ပြီး ပြန်တင်)
Rejected (အပြီးအပိုင်)
```

- Period က HR Final Review မလိုလျှင် Manager Approve လုပ်သည်နှင့် **Finalized** ချက်ချင်း ဖြစ်သည်။
- Submitted သို့ Resubmitted မှ Start Review မလုပ်ဘဲ တိုက်ရိုက် Approve လုပ်နိုင်သည်။

## 3. Screen တစ်ခုချင်းစီ

| Screen | ရည်ရွယ်ချက် | လိုအပ်သော Permission |
|---|---|---|
| **Dashboard** | My performance (ကိုယ်ပိုင်)၊ My team (Manager)၊ HR overview (HR) — Period အလိုက် | `kpi:View` (Team: `kpi:Approve`၊ HR: `kpi:Approve` + `employees:Edit`) |
| **My KPIs** | ကိုယ်ပိုင် Scorecard များ၊ ရလဒ်ထည့်၊ Evidence တွဲ၊ Submit၊ ရလဒ်ကြည့် | `kpi:View`၊ ထည့်/Submit: `kpi:Create` |
| **Reviews** | သုံးသပ်ရန် စောင့်နေသော Scorecard များ (အရင်တင်သူ အရင်) နှင့် Review Screen | `kpi:Approve` |
| **Reports** | Period အလိုက် အကျဉ်းချုပ်၊ ဝန်ထမ်း မှတ်တမ်း၊ ဌာန၊ Ranking၊ KPI Achievement၊ Target vs Actual — Excel Export နှင့် Print | `kpi:View` |
| **Assignments** | KPI သတ်မှတ် (တစ်ဦးချင်း/Bulk)၊ Plan ပြင်၊ Plan Finalize | `kpi-setup` |
| **Templates** | KPI ပုံစံ — တိုင်းတာပုံ၊ Direction၊ Scoring Method၊ Default Target နှင့် Weight | `kpi-setup` |
| **Categories** | KPI အုပ်စု (Productivity၊ Quality ...) | `kpi-setup` |
| **Evaluation Periods** | အကဲဖြတ်ကာလ — Draft → Active → Completed → Closed | `kpi-setup` |
| **Rating Bands** | Score → အဆင့် (Outstanding 90၊ Exceeds Expectations 75၊ Meets Expectations 60၊ Needs Improvement 40၊ Unsatisfactory 0) | `kpi-setup` |

**Role များ (Starter Grants):** HR Officer — Setup အားလုံး နှင့် Scorecard အားလုံး (HR tier)၊ Department Manager — မိမိ Team ကို Review၊ Employee / Payroll Officer / Recruiter — ကိုယ်ပိုင် ရလဒ်ထည့်ခြင်းနှင့် Submit၊ Auditor — ကြည့်ရုံသာ။ HR Administrator — အားလုံး။

## 4. တွက်ချက်ပုံ

| Scoring / Direction | Achievement % |
|---|---|
| **Higher Is Better** | Actual ÷ Target × 100 |
| **Lower Is Better** (Cap မဖြစ်မနေ လို) | Target ÷ Actual × 100 — Actual 0 ဆိုလျှင် Cap အပြည့် ရ |
| **Exact Target** | 100 − (Target နှင့် ကွာခြားချက် ÷ Target × 100) |
| **Range Based** | အပိုင်းအခြားအတွင်း 100၊ အောက် ဆိုလျှင် Actual ÷ Min × 100၊ အထက် ဆိုလျှင် Max ÷ Actual × 100 |
| **Rating Based** | Rating ÷ Maximum Rating × 100 |
| **Boolean** | ပြီး (1) = 100၊ မပြီး (0) = 0 |

- **Capped Percentage** — Template ၏ Cap (1–500%) ထက် မကျော်။ **Direct Percentage** — Cap မရှိ။
- ဘယ်စည်းမျဉ်းမဆို **0 ထက် မနည်း**၊ **1000% (Safety Ceiling)** ထက် မကျော်။
- **ဒသမ ၂ နေရာ၊ Half Away From Zero** ဖြင့် ပိုင်းဖြတ်သည် (Decimal တွက်ချက်မှု — Floating Point မသုံး)။
- Weighted Score = Achievement × Weight ÷ 100။ **Overall Score = Weighted Score များ ပေါင်းလဒ်**။ Weight များ ပေါင်း **100% တိတိ** ဖြစ်ရမည်။
- Score တိုင်းကို **Calculation Version 1** ဖြင့် သိမ်းသည် — ဖော်မြူလာ သို့ Rounding ပြောင်းလျှင် Version အသစ် ဖြစ်ရမည်၊ ဒါကြောင့် ဟောင်း Score ကို ရှင်းပြနိုင်သည်။
- API နှင့် Demo တွက်ချက်မှု မကွဲစေရန် Golden Cases ဖိုင် ၂ ခု (`docs/test-data/kpi-calculation-cases.json`၊ `kpi-scorecard-cases.json`) ဖြင့် နှစ်ဘက်လုံး စစ်သည် (API Tests နှင့် Vitest)။

ဥပမာ (ရှင်းပြရန်သာ၊ စိတ်ကူးယဉ်): Tasks completed — Target 100၊ Actual 85၊ Weight 40 → 85.00% → 34.00။ Customer rating — 4/5၊ Weight 25 → 80.00% → 20.00။ Response time (အပိုင်းအခြားအတွင်း)၊ Weight 20 → 20.00။ Training ပြီး၊ Weight 15 → 15.00။ **Overall 89.00 = "Exceeds Expectations"**။

## 5. ထိန်းချုပ်မှု နှင့် ကာကွယ်မှု

- **ကိုယ့် Scorecard ကို ကိုယ်တိုင် Plan မလုပ်ရ** — HR ဝန်ထမ်း၏ Scorecard ကို အခြား HR တစ်ဦးက Plan လုပ်ရသည် (API နှင့် Demo နှစ်ခုလုံးတွင် 409 ဖြင့် ငြင်းသည်)။
- **ကိုယ့် Scorecard ကို ကိုယ်တိုင် Review မလုပ်ရ** — Role မည်သို့ပင်ဖြစ်စေ။
- **Manager Approve လုပ်သူသည် HR Final Review ကို ထပ်မပေးရ** — HR အခြားတစ်ဦး လိုသည်။
- **Return နှင့် Reject တွင် အကြောင်းပြချက် မဖြစ်မနေ** ထည့်ရသည်။ Reject သည် အပြီးအပိုင်။
- **Reviewer** ကို Plan Finalize လုပ်ချိန်ရှိ **တိုက်ရိုက် Manager** အဖြစ် သတ်မှတ်ပြီး နောက်မှ ပြောင်းရွှေ့လည်း မပြောင်း။
- **Template ပြောင်းလဲမှုက သတ်မှတ်ပြီးသား Scorecard ကို မထိခိုက်** — Assign လုပ်ချိန်တွင် Target၊ Weight၊ စည်းမျဉ်းများ ကူးယူထားသည်။ ဌာနကိုလည်း ကူးယူထားသဖြင့် ဌာနပြောင်းပြီးနောက်လည်း ယခင် Period ၏ ဌာန Report တွင် ယခင်ဌာနအတိုင်း ပေါ်သည်။
- **Review History သည် ပြင်/ဖျက်၍ မရ** (Database အဆင့်တွင် DENY)။
- **မြင်ခွင့်:** ဝန်ထမ်း — ကိုယ်ပိုင်သာ (Planning အဆင့်ကို မမြင်)၊ Manager — ကိုယ်ပိုင် + တိုက်ရိုက်လက်အောက် + ကိုယ် Reviewer ဖြစ်သော Scorecard၊ HR — အားလုံး။
- **ဝန်ထမ်းက Manager ၏ Rating နှင့် Comment ကို Finalized သို့ Rejected ဖြစ်မှသာ မြင်ရသည်** (Approved အဆင့်တွင် HR က ပြန်ပို့နိုင်သေးသဖြင့် မပြ)။ HR Comment ကို Finalized မှသာ။ Return/Reject ၏ အကြောင်းပြချက်ကိုမူ အမြဲ မြင်ရသည်။
- **Period:** ရလဒ်ထည့်ခြင်း၊ Evidence တွဲ/ဖြုတ်ခြင်း နှင့် Submit ကို Period Active ဖြစ်စဉ်သာ လုပ်နိုင် — **Returned** Scorecard ဆိုလျှင် Period **Completed** ဖြစ်နေလည်း ပြင်ပြီး ပြန်တင်နိုင်သည် (Draft ဆိုလျှင် Active လိုသည်)။ Review ပြီးဆုံးခြင်း မရှိသေးသော (Review အဆင့် သို့ Returned) Scorecard ရှိနေလျှင် Period ကို Close မလုပ်နိုင်။ Review Queue တွင် Active နှင့် Completed Period များ၏ Scorecard များသာ ပေါ်သည်။
- **Return လုပ်လျှင်** Score နှင့် Review Draft (Overall နှင့် KPI အလိုက် Rating၊ Comment၊ HR Comment) အားလုံး ရှင်းလင်းသွားပြီး ပြန်တင်လာသော ရလဒ်ကို အသစ်ပြန် Review လုပ်သည်။ အကြောင်းပြချက်ကိုမူ History တွင် ထားသည်။
- **Manager Dashboard** တွင် Team Scorecard စာရင်းကို ဝန်ထမ်း Code အလိုက် **အများဆုံး ၂၀၀** ပြပြီး ပိုများလျှင် မှတ်ချက်နှင့် KPI Reports Link ပြသည် — ကိန်းဂဏန်းများကိုမူ အားလုံးမှ တွက်သည်။
- **Activity Log** တွင် ဝန်ထမ်း Code နှင့် Period သာ ပါပြီး Comment၊ ရလဒ် သို့ ဖိုင်အမည် မပါ။ Review ဆုံးဖြတ်ချက် (Approve၊ Return၊ Reject၊ HR Final Review) အားလုံးကို "was reviewed" ဟုသာ ရေးသဖြင့် ဆုံးဖြတ်ချက်ကို မဖော်ပြ။

### Evidence ဖိုင် ဘာကြောင့် တွဲသလဲ

KPI ရလဒ်သည် ဝန်ထမ်း ရိုက်ထည့်သော **ဂဏန်း** တစ်ခုသာ ဖြစ်သည်။ Evidence ဖိုင် (ဥပမာ အရောင်း Report၊ Training Certificate၊ လက်မှတ်ထိုးထားသော ပို့ဆောင်မှု Delivery Note) ကို တွဲထားလျှင် **Reviewer က ရိုက်ထည့်ထားသော ဂဏန်းကို ယုံရုံမက အမှန်တကယ် စစ်ဆေးနိုင်သည်**။ နောင်တွင် ရလဒ်အပေါ် အငြင်းပွားမှု ဖြစ်လာပါက **HR အတွက် Audit Trail** လည်း ဖြစ်သည်။ Evidence သည် Score ကို မပြောင်းလဲ၊ Workflow အဆင့်လည်း မဟုတ်ပါ။

| ကန့်သတ်ချက် | တန်ဖိုး |
|---|---|
| ဖိုင်အမျိုးအစား | PDF၊ PNG၊ JPEG၊ Word (.docx)၊ Excel (.xlsx) — **ဖိုင်ထဲက Content ဖြင့် စစ်သည်** (Extension နှင့် ကိုက်ရမည်) |
| ဖိုင်အရွယ် | တစ်ခုလျှင် **10 MB** အထိ |
| KPI တစ်ခုလျှင် | ဖိုင် **၅ ခု** အထိ |
| Upload အမြန်နှုန်း | Account တစ်ခုလျှင် **တစ်မိနစ် ၁၀ ကြိမ်** အထိ |
| တွဲ/ဖြုတ်နိုင်သူ | Scorecard ပိုင်ရှင် ဝန်ထမ်းသာ (Draft သို့ Returned အဆင့်) |
| Download လုပ်နိုင်သူ | ဝန်ထမ်း၊ Reviewer/Manager၊ HR |

**သတိပြုရန်:** လက်ရှိတွင် **Virus/Malware စစ်ဆေးမှု မရှိသေးပါ**။ Production မတိုင်မီ ထည့်ရန် လိုသည်။

## 6. အခြား Module များနှင့် ချိတ်ဆက်မှု

| ဒေတာ | ဘယ်ကရသလဲ |
|---|---|
| ဝန်ထမ်း၊ ဌာန၊ ရာထူး၊ ဌာနခွဲ | Employees၊ Organization (Assign လုပ်ချိန်တွင် ကူးယူ) |
| Reviewer (တိုက်ရိုက် Manager) | Employees ၏ Manager |
| Permission များ | Settings → Permissions (`kpi-setup`၊ `kpi`) |
| Excel Export | Data Exchange (`kpi-scorecards`၊ `kpi-ranking`၊ `kpi-target-vs-actual` — Export သာ၊ Import မရ) |
| Activity Feed | Dashboard ၏ Recent activities (Category `performance`) |

## 7. Customer ထံ ပြောနိုင်သော အကျိုးကျေးဇူး

1. ပစ်မှတ်မှ ရလဒ်အထိ တစ်နေရာတည်း — Excel နှင့် Email လွှဲပြောင်းမှု မလို။
2. တွက်ချက်ပုံ တစ်မျိုးတည်း၊ Version ပါ — Score တိုင်းကို ရှင်းပြနိုင်သည်။
3. Return / Reject တွင် အကြောင်းပြချက် မဖြစ်မနေ၊ History ပြင်မရ — တရားမျှတမှုနှင့် Audit အတွက် ယုံကြည်ရ။
4. Evidence ဖိုင်ဖြင့် ရလဒ်ကို အတည်ပြုနိုင်။
5. Dashboard နှင့် Report များဖြင့် ဌာန/Period အလိုက် နှိုင်းယှဉ်နိုင်၊ Excel ထုတ်နိုင်၊ Print/PDF ထုတ်နိုင်။

## 8. Demo ပြသရန် အဆင့်များ (၄–၅ မိနစ်)

1. **Templates** တွင် Template တစ်ခုဖွင့်ပြီး Scoring Method ပြောင်းလျှင် Form ပြောင်းပုံ ပြ (Lower Is Better တွင် Cap မထည့်လျှင် ငြင်းပုံ)။
2. **Assignments** → Assign KPIs → ဝန်ထမ်းရွေး၊ KPI ထည့်၊ Weight 100% မပြည့်လျှင် Finalize မရပုံ ပြ → 100% ဖြင့် Finalize။
3. **My KPIs** တွင် ရလဒ်ထည့်၊ Evidence (PDF) တွဲ၊ Submit — Score ချက်ချင်း ပေါ်ပုံ ပြ။
4. **Reviews** တွင် Rating နှင့် Feedback ပေးပြီး Approve (သို့ အကြောင်းပြချက်ဖြင့် Return)။
5. **Dashboard** ၏ HR overview နှင့် **Reports** ၏ Ranking → Excel Export။

## 9. Production သို့ မရောက်မီ လိုအပ်သည်များ (Customer ကို ရိုးသားစွာ ပြောရန်)

| လိုအပ်ချက် | အခြေအနေ |
|---|---|
| Evidence ဖိုင် **Virus/Malware စစ်ဆေးမှု** | မပါသေး — ထည့်ရန် လိုသည် |
| Evidence Folder ကို Encryption၊ Backup၊ ထိန်းသိမ်းမှု Policy | Deployment တွင် လုပ်ရန် ([DEPLOYMENT.md](../DEPLOYMENT.md)) |
| Database Schema (`feature/schema-kpi`) ကို Publish | လူကိုယ်တိုင် Script စစ်ပြီးမှ — Local မှ စ |
| Demo Build ၏ Permission | Simulation သာ — Role အမည်ဖြင့် HR/Manager ခွဲ၊ ဘာမှ မတားဆီး (API က တားဆီးသည်) |
| Demo Build ၏ Evidence ဖိုင် | Memory ထဲတွင်သာ — Reload လျှင် ပျောက် |
| `main` သို့ ပေါင်းခြင်း | မလုပ်ရသေး — ပေါင်းလျှင် Demo ကို Deploy လုပ်သည် |

ဤအချက်များကို Project Scope တွင် သီးခြားထည့်သွင်း သဘောတူညီပါ။
