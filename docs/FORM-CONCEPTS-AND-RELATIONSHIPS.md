# ဖောင်တစ်ခုချင်းစီရဲ့ အဓိပ္ပာယ်၊ လက်တွေ့အကြောင်းရင်း၊ ဆက်စပ်မှု

အောက်ပါအချက်တွေက `core/models` ရဲ့ id reference တွေနဲ့ mock service validation ကနေ ယူထားတာပါ။
"လက်တွေ့မှာ ဘာကြောင့်လိုလဲ" ဆိုတဲ့ အပိုင်းက HR လုပ်ငန်း အထွေထွေ အသိပေါ်အခြေခံတာဖြစ်ပြီး ဒီ app ရဲ့ code ကနေ
ထုတ်ယူတာ မဟုတ်ပါ။ Field အသေးစိတ်ကို [FORM-FIELD-REFERENCE.md](FORM-FIELD-REFERENCE.md)၊ ဥပမာဒေတာကို
[EXAMPLE-DATA-GOLDEN-LOTUS.md](EXAMPLE-DATA-GOLDEN-LOTUS.md) မှာ ကြည့်ပါ။

---

## ၁။ ဆက်စပ်မှုပုံ (အကြီးကြီး)

`A → B` ဆိုတာ "A က B ကို id နဲ့ ကိုးကားတယ်" (A ဖန်တီးမယ်ဆို B က အရင်ရှိရမယ်)။

```
ORGANIZATION (ဖွဲ့စည်းပုံ)                 SHIFT (အလုပ်ချိန်)       LEAVE TYPE
Company ◄─ Branch ◄─ Department ◄─ Section        │                     │
                        ▲   ▲                      │                 Leave Policy ──► Job Grade
Job Grade ◄─────── Position ──► (reports to)       │                     │
   ▲                  ▲                            ▼                     ▼
   │                  │                      ┌──────────────────────────────┐
   └──────────────────┴──────────────────────┤          EMPLOYEE            ├──► manager (Employee)
                                             └──┬────┬────┬────┬────┬─────┘
                       ┌─────────────────────────┘    │    │    │    └────────────────┐
                       ▼                              ▼    ▼    ▼                     ▼
     Documents / History      Attendance Record   Leave App  Overtime            User account ──► Role ──► Permissions
                                   ▲                  │         │
                       Adjustment ─┘                  │         │
                                                      ▼         ▼
SALARY: Component ◄─ Structure ◄─ Salary Assignment (Employee + basic) ──► Payroll Run ──► Payslip
                                                                 ▲            ▲
                                       Approved Overtime + Unpaid Leave + ကာလအတွင်း join/exit ──┘

RECRUITMENT: Job Opening ◄─ Application ─► Candidate        Application ◄─ Interview ─► Interviewer (Employee)
             (department + hiring manager = Employee)       Selected ⇒ (လက်နဲ့) Employee အသစ်ထည့်
```

**အဓိကအချက် ၂ ခု**
1. **Employee က အလယ်ဗဟို**: Organization က လူကို နေရာချပေးတယ်၊ Attendance/Leave/Payroll အားလုံးက လူကို ကိုးကားတယ်။
2. **ဝန်ထမ်း ↔ ဖွဲ့စည်းပုံ ပတ်ဝိုင်း (circular)**: Branch manager၊ Department head၊ Section supervisor က Employee ကို ကိုးကားတယ်။ ဒါပေမဲ့ Employee ကိုလည်း Branch/Department လိုတယ်။ ဒါကြောင့် **ဖွဲ့စည်းပုံကို manager မပါဘဲ အရင်ဖန်တီး → ဝန်ထမ်းထည့် → manager/head ကို ပြန်ဖြည့်** ဆိုတဲ့ အစဉ်ကို လိုက်ရတယ်။

---

## ၂။ Organization (ဖွဲ့စည်းပုံ)

| ဖောင် | ဘာလဲ | လက်တွေ့မှာ ဘာကြောင့်လိုလဲ | ဘာကို မှီတယ် | ဘယ်ဟာတွေက မှီလဲ |
|---|---|---|---|---|
| **Company** | ဥပဒေအရ ကုမ္ပဏီ (အုပ်စု) | Group တစ်ခုမှာ ကုမ္ပဏီအများကြီး ရှိနိုင်၊ အခွန်/မှတ်ပုံတင်အချက်အလက်နဲ့ payslip ခေါင်းစဉ်ကို ကုမ္ပဏီအလိုက် ခွဲဖို့ | – | Branch၊ Department၊ Employee၊ Payslip (ကုမ္ပဏီအမည်) |
| **Branch** | ရုံး/ဂိုဒေါင် တည်နေရာ | နေရာအလိုက် ဝန်ထမ်းရေ၊ ခွင့်၊ အလုပ်ချိန် ကွဲလို့ report/filter လုပ်ရ | Company၊ (Manager → Employee) | Department၊ Employee၊ Attendance dashboard filter |
| **Department** | ဌာန (HR၊ Finance...) | ဌာနအလိုက် ဘတ်ဂျက်၊ report၊ ခွင့်ပြုသူ ခွဲရ။ Cost center ဖြစ်လို့ စာရင်းကိုင်နဲ့ ချိတ်ရ | Company + **Branch** (ကိုက်ရမယ်)၊ (Head → Employee) | Section၊ Position၊ Employee၊ Job opening |
| **Section** | ဌာနအောက်က အဖွဲ့ငယ် | ဌာနကြီးတွေမှာ အဖွဲ့လိုက် ကြီးကြပ်ဖို့ | Department၊ (Supervisor → Employee) | Position၊ Employee |
| **Job Grade** | လစာ/အဆင့်ကွန်ရက် (band) | "ဒီအဆင့်ဆို လစာ ဒီအကွာအဝေးအတွင်း" ဆိုတဲ့ တရားမျှတမှုထိန်းချုပ်ချက်၊ ခွင့်ရက်ကွဲပြားမှုအတွက်လည်း သုံး | – | Position၊ Employee၊ **Leave Policy**၊ **Salary Assignment** (band စစ်) |
| **Position** | ရာထူး (အတွဲလိုက်ခန့်ထားနိုင်တဲ့နေရာ) | Headcount ခွင့်ပြုချက်၊ ဘယ်ရာထူးက ဘယ်သူ့ကို report တင်လဲ (organization chart) | Department၊ Section?၊ **Job Grade**၊ (Reports to → Position) | Employee၊ Reporting Structure view |

**ဘာကြောင့် ဒီလိုချည်နှောင်ထားလဲ**
- Department က Branch ရဲ့ Company နဲ့ ကိုက်ရမယ်: "Yangon ရုံးမှာ မရှိတဲ့ ဌာနကို ဝန်ထမ်းထည့်တာ" မျိုး မဖြစ်စေဖို့။
- Position က Department နဲ့ ကိုက်ရမယ်: Finance ဌာနကို "Warehouse Staff" ရာထူးနဲ့ ဝန်ထမ်းထည့်တာမျိုး တားဖို့။
- သုံးနေတဲ့ record ကို မဖျက်ဘဲ **Inactive** လုပ်: ဝန်ထမ်းသမိုင်း၊ payslip တွေ အမည်ပျောက်မသွားအောင်။

---

## ၃။ Employees (ဝန်ထမ်း)

| ဖောင် | ဘာလဲ | လက်တွေ့မှာ ဘာကြောင့်လိုလဲ | ဘာကို မှီတယ် | ဘယ်ဟာတွေက မှီလဲ |
|---|---|---|---|---|
| **Employee** | ဝန်ထမ်းတစ်ယောက်ရဲ့ master record | HR ရဲ့ "single source of truth"။ ခွင့်၊ လစာ၊ အလုပ်ချိန်၊ login အားလုံး ဒီလူကနေ ဆက်သွားတယ် | Company/Branch/Department/Section?/Position/Job Grade၊ (Manager → Employee)၊ (Shift)? | **Attendance၊ Leave၊ Overtime၊ Adjustment၊ Salary Assignment၊ Payslip၊ User၊ Interview (interviewer)၊ Job opening (hiring manager)၊ Documents** |
| **Employee Document** | စာချုပ်၊ ID၊ လက်မှတ် စသည့် စာရွက်စာတမ်း | ဥပဒေကိစ္စ၊ audit၊ သက်တမ်းကုန်တာ (work permit စသည်) ကို ခြေရာခံဖို့ | Employee | Employee profile |

**Employee ထဲက field တွေက တခြားဖောင်ကို ဘယ်လိုထိလဲ**
- `joiningDate` → Leave က ဒီရက်မတိုင်ခင် မစနိုင်၊ Salary က ဒီရက်မတိုင်ခင် မစနိုင်၊ Payroll က လလယ်ဝင်ရင် **prorate**။
- `exitDate` / status Resigned/Terminated → Payroll က ထွက်ပြီးရက်ကနေ မတွက်၊ Job opening/Interview မှာ ရွေးလို့မရတော့ (လက်ရှိဝန်ထမ်းသာ)။
- `employmentType` → Leave Policy ရွေးပုံ (Intern ဆို policy ကွဲ)။
- `shiftId` → Check in/out မှာ Late တွက်တဲ့ စည်းမျဉ်း။
- `bank` → Payslip မှာ account အဆုံး ၄ လုံးပြ (`•••• 0011`)။
- `managerId` → Reporting structure၊ approval ပို့ရမယ့်သူ။

---

## ၄။ Attendance (တက်ရောက်မှု)

| ဖောင် | ဘာလဲ | လက်တွေ့မှာ ဘာကြောင့်လိုလဲ | ဘာကို မှီတယ် | ဘယ်ဟာတွေက မှီလဲ |
|---|---|---|---|---|
| **Shift** | အလုပ်ချိန် template (စ၊ ဆုံး၊ break၊ grace၊ အလုပ်ရက်) | "ဘယ်အချိန်ရောက်ရင် နောက်ကျလဲ" ဆိုတာကို အားလုံးအတွက် တူညီစွာ သတ်မှတ်ဖို့။ ကုမ္ပဏီမှာ ရုံးဆိုင်း/ဂိုဒေါင်ဆိုင်း/ညဆိုင်း ကွဲလို့ | – | Employee (shiftId)၊ Attendance record၊ Work Schedules |
| **Check in / out** | ရောက်/ထွက် မှတ်တမ်း | ကိုယ်တိုင် စာရင်းထိန်းမယ့်အစား ဒေတာအစစ်ကို စုပေါင်းဖို့ (လက်တွေ့မှာ fingerprint/mobile နဲ့ ဝင်လာမယ်) | Employee၊ Shift | Attendance record |
| **Attendance record** | တစ်လူတစ်ရက် မှတ်တမ်း (Late/Worked/OT/Status) | လစာ၊ စည်းကမ်းစစ်ဆေးမှု၊ report | Employee၊ Shift | Adjustment၊ Dashboard၊ Late & Early report |
| **Adjustment** | မှတ်တမ်းမှားတာကို ပြင်ဖို့ တောင်းဆိုမှု | Punch မေ့၊ စက်ပျက်၊ ခရီးထွက်ဆိုတဲ့အတွက် manager ခွင့်ပြုချက်နဲ့ ပြင်ခွင့်ပေးရ။ ပြင်ခဲ့တာကို audit အတွက် မှတ်ထား (source = Manual) | Employee၊ **Attendance record (ရှိမှ)** | Approve ရင် Attendance record ကို ပြန်တွက် |
| **Overtime** | အချိန်ပိုအတွက် ခွင့်ပြုချက် | အလုပ်ချိန်ပိုလုပ်တိုင်း လစာမပေးဘဲ **ကြိုတင်/နောက်ကျပြီး ခွင့်ပြုချက်** နဲ့ပဲ ပေးနိုင်ဖို့ ထိန်းချုပ် | Employee | **Payroll (Approved ပဲ)** |
| **Work Schedules** | အပတ်စဉ် roster ကြည့်ရန် | "ဒီအပတ် ဘယ်သူ ဘယ်ဆိုင်းလဲ၊ ဘယ်သူ ခွင့်ယူ" ကို တစ်ချက်တည်းကြည့်ဖို့ | Employee၊ Shift၊ Leave၊ Holiday | – (ကြည့်ရုံ) |

**အရေးကြီးတဲ့ ခြားနားချက်**: Attendance record ထဲက OT မိနစ် (check-out ကနေ အလိုအလျောက်တွက်) နဲ့ **Overtime entry** (ခွင့်ပြုချက်) က
သီးခြားပါ။ Payroll က **Approved Overtime entry** ကိုပဲ ယူတယ်။ လက်တွေ့မှာလည်း "စက်ကပြတဲ့ အချိန်ပို" ကို
ခွင့်ပြုချက်မရှိဘဲ မပေးကြပါဘူး။

---

## ၅။ Leave (ခွင့်)

| ဖောင် | ဘာလဲ | လက်တွေ့မှာ ဘာကြောင့်လိုလဲ | ဘာကို မှီတယ် | ဘယ်ဟာတွေက မှီလဲ |
|---|---|---|---|---|
| **Leave Type** | ခွင့်အမျိုးအစား (Annual၊ Sick၊ Unpaid...) | ဥပဒေ/ကုမ္ပဏီစည်းမျဉ်းအရ ခွင့်တစ်မျိုးချင်း စည်းကမ်းကွာ (လစာပေးလား၊ ဆေးစာလိုလား၊ ကျားမကန့်သတ်လား) | – | Policy၊ Application၊ Balance၊ Payroll (Paid/Unpaid) |
| **Leave Policy** | ဘယ်အဆင့်/အမျိုးအစားလူ ဘယ်နှစ်ရက်ရမလဲ | "Manager က ၁၈ ရက်၊ ဝန်ထမ်းသာမန် ၁၅ ရက်၊ Intern လစဉ်စုလာ" ဆိုတဲ့ ကုမ္ပဏီမူဝါဒကို ပြဋ္ဌာန်းဖို့ | Leave Type၊ (Job Grade)၊ (Employment type) | **Balance တွက်ချက်ခြင်း** |
| **Leave Application** | ခွင့်တောင်းစာ | တောင်း → ခွင့်ပြုချက် → မှတ်တမ်း လုပ်ငန်းစဉ်၊ ရက်ထပ်ခြင်း၊ balance ကျော်ခြင်း မဖြစ်အောင် တားဖို့ | Employee၊ Leave Type | Balance၊ Calendar၊ Attendance (On Leave)၊ **Payroll (Unpaid)** |
| **Balances / Calendar** | ကြည့်ရုံ | ကျန်ရက်သိဖို့၊ အဖွဲ့လိုက် ဘယ်သူမရှိလဲ ကြည့်ဖို့ | Application + Policy | – |

**တောင်းဆိုမှုတစ်ခု ဘယ်လိုသွားလဲ (လက်တွေ့နဲ့ တူ)**
```
ဝန်ထမ်း တောင်း (Draft) → Submit (balance စစ်၊ ရက်ထပ်စစ်၊ ဆေးစာစစ်)
   → Manager/HR Approve ဒါမှမဟုတ် Reject (အကြောင်းပြချက်လို)
   → Approved: Calendar ပေါ်လာ + Attendance မှာ "On Leave" + (Unpaid ဆို) Payroll မှာ နှုတ်
```
**ဘာကြောင့် ကိုယ့်ခွင့်ကိုယ် Approve မရလဲ**: အတွင်းထိန်းချုပ်မှု (segregation of duties) ပါ။ မဟုတ်ရင် ကိုယ့်ကိုယ်ကိုယ် ခွင့်ပေးနိုင်မှာမို့။

---

## ၆။ Payroll (လစာ)

| ဖောင် | ဘာလဲ | လက်တွေ့မှာ ဘာကြောင့်လိုလဲ | ဘာကို မှီတယ် | ဘယ်ဟာတွေက မှီလဲ |
|---|---|---|---|---|
| **Salary Component** | လစာရဲ့ အစိတ်အပိုင်းတစ်ခု (Housing၊ Tax...) | လစာကို basic တစ်ခုတည်း မဟုတ်၊ ခွင့်ပြုငွေ/နှုတ်ငွေ အများကြီးနဲ့ ဖွဲ့စည်းလို့ တစ်ခုချင်း သတ်မှတ်ရ | – | Salary Structure |
| **Salary Structure** | Component စုစည်းထားတဲ့ ပုံစံ | "Manager တွေက Performance allowance ပါ၊ Staff မပါ" လို့ အုပ်စုလိုက် သတ်မှတ်ရ၊ တစ်ယောက်ချင်းမသတ်ရအောင် | Component | Salary Assignment |
| **Employee Salary** | ဝန်ထမ်းတစ်ယောက်ရဲ့ basic + သုံးမယ့် structure + စတင်ရက် | လစာတိုးတိုင်း **ရှိပြီးသားကို မပြင်ဘဲ row အသစ်** ထပ်ထည့်: လစာသမိုင်းကို ထိန်း၊ မကြာသေးမီ လစာတိုးရက်ကို အတိအကျ သိ | Employee၊ Structure၊ **Job Grade band** | Payroll Run |
| **Payroll Period (Run)** | တစ်လစာ လစာတွက်ချက်မှု | လကုန်တိုင်း အားလုံးတွက်၊ စစ်၊ ခွင့်ပြု၊ ပေးချေ ဆိုတဲ့ လုပ်ငန်းစဉ်ကို ထိန်းဖို့ | Employee၊ Salary၊ **Approved OT**၊ **Unpaid leave**၊ Holiday | Payslip |
| **Payslip** | ဝန်ထမ်းတစ်ယောက်ရဲ့ လစာစာရွက် | ဝန်ထမ်းက ဘယ်လိုတွက်လဲ ကြည့်ခွင့်ရ။ **Snapshot** ဖြစ်လို့ နောက်မှ rule ပြောင်းလည်း ဟောင်းက မပြောင်း (audit) | Payroll Run | – |

**လစာတွက်ပေးတဲ့ input အားလုံး (တစ်နေရာပေါင်း)**
```
Basic ← Employee Salary          Allowances ← Structure ← Components
OT      ← Overtime (Approved)    Unpaid leave ← Leave Application (Approved + Unpaid type)
ရက်ပိုင်း ← joiningDate / exitDate     ရုံးဖွင့်ရက် ← Holiday + weekend setting
```
**ဘာကြောင့် status အဆင့်ဆင့်လဲ**: Draft → Process (Pending Approval) → Approve → Paid။ လက်တွေ့မှာ HR → Finance Manager →
ဘဏ်လွှဲ ဆိုတဲ့ ထိန်းချုပ်မှုအဆင့်ပါ။ Paid ဖြစ်ပြီးရင် ပြန်မပြင်၊ ဒါမှ ပေးပြီးသားစာရင်းက ပျက်မသွားမယ်။

⚠️ Tax၊ Social နှုန်းတွေက **နမူနာသက်သက်**၊ တကယ့်ဥပဒေအရ တွက်နည်း မဟုတ်ပါ။

---

## ၇။ Recruitment (ခန့်ထားမှု)

| ဖောင် | ဘာလဲ | လက်တွေ့မှာ ဘာကြောင့်လိုလဲ | ဘာကို မှီတယ် | ဘယ်ဟာတွေက မှီလဲ |
|---|---|---|---|---|
| **Job Opening** | လူလိုကြောင်း ကြော်ငြာ | ဘယ်ဌာန ဘယ်နှစ်ယောက်လိုလဲ၊ ဘယ်သူတာဝန်ခံလဲ ကို ရှင်းရှင်းလင်းလင်း၊ လိုတာထက်ပို မခန့်မိအောင် | Department၊ Hiring manager (**Employee**) | Application |
| **Candidate** | လျှောက်ထားသူ | လူတစ်ယောက်က အလုပ်အများကြီး လျှောက်နိုင်လို့ လူကို သီးသန့်သိမ်း (email unique) | – | Application |
| **Application** | Candidate တစ်ဦး + Opening တစ်ခု | ဘယ်သူ ဘယ်အဆင့်ရောက်လဲ (pipeline) ကို ခြေရာခံ | Opening (Open ဖြစ်မှ)၊ Candidate | Interview |
| **Interview** | အင်တာဗျူးတစ်ကြိမ် | ရမှတ်၊ အကြံပြုချက်ကို မှတ်တမ်းတင်ပြီး ရွေးချယ်ရာမှာ အကြောင်းပြချက်ပြဖို့ | Application၊ Interviewer (**Employee**) | Application ကို Selected လုပ်ခွင့် |

**အဆင့်ဆင့် ထိန်းချုပ်ပုံ**
```
Applied → Screening → Interview → Selected      (ဘယ်အဆင့်ကမဆို Rejected ရ၊ ဒါပေမဲ့ အကြောင်းပြချက်လို)
```
- **Selected** ဖို့: Completed interview တစ်ခု၊ Hire/Strong hire ပါရမယ်၊ vacancy လွတ်ရမယ်။ လက်တွေ့မှာ "အင်တာဗျူးမဖြတ်ဘဲ ခန့်တာ" မျိုး ကာကွယ်တာ။
- Vacancy ပြည့်ရင် Opening က **Filled** အလိုအလျောက်၊ တစ်ယောက်ပြန်ပြုတ်ရင် ပြန်ဖွင့်။
- **Selected → Employee** ကို လက်နဲ့ထည့်ရတယ် (auto မရှိ)။ လက်တွေ့မှာလည်း offer letter၊ စာရွက်စာတမ်းစစ်၊ join ရက်ညှိ ပြီးမှ ဝန်ထမ်းစာရင်းသွင်း။

---

## ၈။ Settings (စနစ်)

| ဖောင် | ဘာလဲ | လက်တွေ့မှာ ဘာကြောင့်လိုလဲ | ဘာကို မှီတယ် |
|---|---|---|---|
| **General Settings** | ကုမ္ပဏီတစ်ခုလုံးအတွက် ရွေးချယ်မှု (time zone၊ currency ...) | စနစ်တစ်ခုလုံး ရက်စွဲ/ငွေကြေး ပုံစံတူအောင် | – (ဒီ demo မှာ တခြား screen က မဖတ်) |
| **Role** | အခန်းကဏ္ဍ (HR Admin၊ Manager ...) | လူတစ်ယောက်ချင်း permission မသတ်ဘဲ အခန်းကဏ္ဍအလိုက်သတ်မှတ် | – |
| **Permissions** | Role တစ်ခုက module တစ်ခုမှာ ဘာလုပ်လို့ရလဲ (View/Create/Edit/Approve) | ပေးမှသာ ကြည့်/ပြင်/ခွင့်ပြုလို့ရ (least privilege) | Role |
| **User** | Login account | ဝန်ထမ်းတစ်ယောက်ကို ဝင်ခွင့်ပေးဖို့ | **Employee**၊ Role |

⚠️ ဒီ app မှာ Permission က **ဘာကိုမှ ကန့်သတ်မထားပါ** (UI ပြတာပဲ)။ တကယ့်စနစ်မှာတော့ server ဘက်က ပြဋ္ဌာန်းရမှာပါ။

---

## ၉။ ဖြစ်လေ့ရှိတဲ့ မေးခွန်းတွေနဲ့ ဆက်စပ်မှုက ဘာကြောင့်လဲ

| မေးခွန်း | အဖြေ (ဆက်စပ်မှုကြောင့်) |
|---|---|
| Branch ကို ဘာလို့ ဖျက်မရလဲ | Department နဲ့ Employee တွေ ရှိနေလို့။ Inactive လုပ်ပါ |
| Department ဖန်တီးတာ Branch ရွေးပေမဲ့ Company ကိုလည်း ဘာလို့ ရွေးရလဲ | Branch က Company ပိုင်ဆိုင်တာ တိုက်စစ်ဖို့ (မကိုက်ရင် ပယ်) |
| Salary ထည့်တာ ဘာလို့ ပယ်လဲ | Employee ရဲ့ Job Grade band အပြင်ရောက်နေ၊ ဒါမှမဟုတ် Joining date မတိုင်ခင်၊ ဒါမှမဟုတ် တူညီရက်မှာ ရှိပြီး |
| ခွင့်တောင်းတာ ဘာလို့ ပယ်လဲ | balance မလောက်၊ ရက်ထပ်၊ Joining မတိုင်ခင်၊ ရက်မရှိ (weekend ပဲ) ၊ ဒါမှမဟုတ် ဆေးစာမပါ |
| Adjustment ဘာလို့ မတင်နိုင်လဲ | အဲဒီရက်မှာ attendance record မရှိ |
| Payroll Process ဘာလို့ မရလဲ | ယခင် period Draft/Pending ဖြစ်နေ၊ ဒါမှမဟုတ် ပေးရမယ့်ဝန်ထမ်း မရှိ |
| Job opening ထဲမှာ hiring manager ဘာလို့ မပေါ်လဲ | Employee မရှိသေး သို့မဟုတ် ထွက်သွားပြီ (exit date) |
| Overtime Approve လုပ်ပြီး ဘာလို့ Attendance record မပြောင်းလဲ | OT approval က Payroll အတွက်ပဲ၊ Attendance record နဲ့ သီးသန့် |
| Employee ဘာလို့ ဖျက်မရလဲ | သူ့အောက်မှာ report တင်သူတွေ ရှိနေတတ် (manager ဖြစ်နေ) |

---

## ၁၀။ တစ်လစာ လုပ်ငန်းစဉ်ကို ဆက်စပ်မှုနဲ့ ပြန်ကြည့်ရင်

```
Organization (တစ်ကြိမ်သတ်မှတ်) ──► Employee (လူဝင်တိုင်း) ──► Shift/Leave policy/Salary assignment (လူတစ်ယောက်ချင်း)
                                          │
       နေ့စဉ်:  Check in/out ──► Attendance record ──(မှားရင်)──► Adjustment
       လစဉ်ထဲ:  Leave application ──► Approve ──► Attendance (On Leave) + Payroll (Unpaid)
                Overtime ──► Approve ──────────────────────────────────► Payroll (OT)
       လကုန်:   Payroll run ──► Payslip ──► Payroll History
       လိုအပ်ရင်: Recruitment ──► Selected ──► (လက်နဲ့) Employee အသစ်
```
