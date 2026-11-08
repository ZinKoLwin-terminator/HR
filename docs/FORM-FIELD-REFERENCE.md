# Form field reference (ဖောင်တိုင်းရဲ့ field တစ်ခုချင်း ရှင်းလင်းချက်)

App ထဲက ဖောင်တွေကို code (`*-page.component.ts`၊ `employee-form.config.ts`၊ mock services) ကနေ ဖတ်ပြီး ရေးထားတာပါ။
ဥပမာတန်ဖိုးတွေက [EXAMPLE-DATA-GOLDEN-LOTUS.md](EXAMPLE-DATA-GOLDEN-LOTUS.md) ထဲက Golden Lotus data ပါ။

**ဖတ်နည်း**: `*` = မဖြည့်မနေရ (required)၊ 🔒 = ဖန်တီးပြီးရင် ပြန်ပြင်မရ။ "ပုံစံ" ကော်လံက input ပုံစံ (text၊ number၊ date၊ select ...)။

**အားလုံးနဲ့ သက်ဆိုင်တဲ့ စည်းမျဉ်း**
- **Code** field တွေ: အကြီးစာလုံး (A–Z)၊ ဂဏန်း၊ ဒက်ရှ် (`-`) ပဲ ရတယ်။ ဥပမာ `YGN-HQ`၊ `EMP-001`။
- **Status** (Active/Inactive): သုံးနေတဲ့ record (တခြား record က ကိုးကားထားတာ) ကို ဖျက်လို့မရ၊ Inactive လုပ်ရတယ်။ Inactive Company/Branch/Department/Position ကို ဝန်ထမ်းအသစ် assign မရ၊ Inactive shift/structure/leave type ကိုလည်း ရွေးလို့မရ။
- **Date**: "not in future" ဆိုတာ ယနေ့ ဒါမှမဟုတ် အတိတ်ပဲ ရမယ်။
- **Phone**: ဂဏန်း၊ space၊ `-` နဲ့ အစမှာ `+` ပါလို့ရ (ဥပမာ `+95 9 555 0111`)၊ အနည်းဆုံး ဂဏန်း ၈ လုံး။
- ဖောင်ထဲက ပြဿနာကို field အောက်မှာ အနီရောင်နဲ့ ပြတယ်။ Server (mock) ကနေ ပြန်လာတဲ့ message ကိုလည်း တူညီစွာ ပြတယ်။
- **Holiday ဖောင် မရှိပါ**: app ထဲမှာ Holiday ထည့်တဲ့ screen မရှိပါဘူး။

---

## ၁။ Settings

### General Settings (`/settings/general`)

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း / ဥပမာ |
|---|---|---|---|
| Organization name | ကုမ္ပဏီအမည် (UI မှာ ပြဖို့) | * | ≤ 80 လုံး။ `Golden Lotus Trading` |
| Contact email | HR ဆက်သွယ်ရမယ့် email | * | email ပုံစံ။ `hr@goldenlotus.example` |
| Time zone | အချိန်ဇုန် | * | စာရင်းထဲက ရွေးရ။ `Asia/Yangon` |
| Date format | ရက်စွဲပြပုံ | * | စာရင်းထဲက ရွေးရ။ `yyyy-MM-dd` |
| Currency code | ငွေကြေးကုဒ် | * | အကြီးစာလုံး ၃ လုံး။ `MMK` |
| Fiscal year starts in | ဘဏ္ဍာနှစ်စတဲ့လ | * | January–December |
| Send email notifications | email သတိပေးချက်ပို့မလား | | toggle |
| Send a weekly digest | အပတ်စဉ်အနှစ်ချုပ်ပို့မလား | | toggle |

⚠️ ဒီတန်ဖိုးတွေ သိမ်းထားပေမဲ့ တခြား screen တွေက မဖတ်ပါဘူး။

### Role (`/settings/roles`)

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Role name | Role အမည် | * | ≤ 40။ Built-in role (HR Administrator၊ Employee) ကို rename မရ |
| Status | Active / Inactive | * | Active user ရှိတဲ့ role ကို Inactive မလုပ်ရ၊ built-in ကို Inactive/ဖျက်မရ |
| Description | ဘာအတွက်လဲ ရှင်းချက် | | ≤ 200 |

### User (`/settings/users`)

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Employee 🔒 | ဘယ်ဝန်ထမ်းရဲ့ account လဲ | * | လက်ရှိ ဝန်ထမ်း (exit date မရှိ) သာ၊ user မရှိသေးသူသာ။ Name နဲ့ email ကို employee ဆီက ကူးယူ |
| Role | ဘယ် role | * | Active role သာ |
| Status | Active / Inactive / Locked | * | ကိုယ့် account ကို ဖျက်/role ပြောင်း/Inactive မလုပ်ရ။ Active HR Administrator ၁ ယောက် အမြဲရှိရမယ်။ Locked ကို "Unlock account" နဲ့ ဖွင့် |
| Email / Last sign-in | ကြည့်ရုံ (details) | | |

---

## ၂။ Organization

### Company

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း / ဥပမာ |
|---|---|---|---|
| Company code 🔒 | ကုမ္ပဏီကုဒ် | * | ≤ 10၊ unique။ `GLT` |
| Company name | ခေါ်တဲ့အမည် | * | ≤ 100။ `Golden Lotus Trading` |
| Legal name | ဥပဒေအရ အမည်အပြည့်အစုံ | * | ≤ 150။ `Golden Lotus Trading Co., Ltd.` |
| Registration number | ကုမ္ပဏီမှတ်ပုံတင်အမှတ် | | ≤ 40 |
| Tax ID | အခွန်မှတ်ပုံတင်အမှတ် | | ≤ 40 |
| Industry | လုပ်ငန်းအမျိုးအစား | * | ≤ 80 |
| Founded date | တည်ထောင်သည့်ရက် | * | ယနေ့ ဒါမှမဟုတ် အတိတ် |
| Email / Phone | ဆက်သွယ်ရန် | * / * | email ပုံစံ / phone ပုံစံ |
| Website | ဝက်ဘ်ဆိုက် | | URL၊ ≤ 150 |
| Address / City / Country | လိပ်စာ | – / * / * | Address ≤ 200၊ City/Country ≤ 60 |
| Status | Active / Inactive | * | |

### Branch

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း / ဥပမာ |
|---|---|---|---|
| Company | ဘယ်ကုမ္ပဏီရဲ့ ဌာနခွဲ | * | |
| Branch code 🔒 | ဌာနခွဲကုဒ် | * | ≤ 12။ `YGN-HQ` |
| Branch name | အမည် | * | ≤ 100 |
| Branch type | Head Office / Branch / Regional Office / Warehouse | * | |
| Email / Phone | ဆက်သွယ်ရန် | – | |
| Address / City / Country | လိပ်စာ | – / * / * | |
| Branch manager | ဌာနခွဲမန်နေဂျာ | | ဝန်ထမ်းထဲက ရွေး (အလွတ်ထားလို့ရ) |
| Opened date | ဖွင့်သည့်ရက် | * | ယနေ့ ဒါမှမဟုတ် အတိတ် |
| Status | Active / Inactive | * | |

### Department

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း / ဥပမာ |
|---|---|---|---|
| Company | ကုမ္ပဏီ | * | |
| Branch | ဘယ်ဌာနခွဲမှာ ရှိလဲ | * | ရွေးထားတဲ့ Company ရဲ့ Branch သာ ပေါ်မယ်။ မကိုက်ရင် "branch does not belong to company" |
| Department code 🔒 | ကုဒ် | * | ≤ 10။ `HR-ADM` |
| Department name | အမည် | * | ≤ 100 |
| Department head | ဌာနအကြီးအကဲ | | ဝန်ထမ်းထဲက |
| Cost center | ကုန်ကျစရိတ်ခွဲတမ်းကုဒ် (စာရင်းကိုင်) | | ≤ 20။ `CC-100` |
| Description | ဖော်ပြချက် | | ≤ 300 |
| Status | | * | |

### Section

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း / ဥပမာ |
|---|---|---|---|
| Department | ဘယ်ဌာနအောက်မှာလဲ | * | |
| Section code 🔒 | ကုဒ် | * | ≤ 12။ `HR-OPS` |
| Section name | အမည် | * | ≤ 100 |
| Supervisor | အုပ်ချုပ်သူ | | ဝန်ထမ်းထဲက |
| Description / Status | | – / * | ≤ 300 |

### Job Grade

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း / ဥပမာ |
|---|---|---|---|
| Grade code 🔒 | ကုဒ် | * | ≤ 6။ `STF` |
| Grade name | အမည် | * | ≤ 60 |
| Level | အဆင့် (1 = အငယ်ဆုံး) | * | 1–20 |
| Minimum / Maximum monthly salary | လစာ band အနည်းဆုံး/အများဆုံး | * / * | 0–1,000,000,000။ Max ≥ Min ဖြစ်ရမယ်။ Employee ရဲ့ basic salary ဒီ band ထဲ ရှိရမယ် |
| Description / Status | | – / * | |

### Position

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း / ဥပမာ |
|---|---|---|---|
| Department | ဘယ်ဌာန | * | |
| Section | ဘယ်ကဏ္ဍ | | ရွေးထားတဲ့ Department ရဲ့ Section သာ |
| Position code 🔒 | ကုဒ် | * | ≤ 12။ `HR-MGR` |
| Position title | ရာထူးအမည် | * | ≤ 100 |
| Job grade | လစာအဆင့် | * | |
| Approved headcount | ခွင့်ပြုထားသူအရေအတွက် | * | 1–500 |
| Reports to | ဘယ်ရာထူးကို report တင်လဲ | | ကိုယ့်ကိုယ်ကိုယ် မရ |
| Description / Status | | – / * | ≤ 300 |

---

## ၃။ Employees

### Employee form (အပိုင်း ၆ ခု)

**Personal**

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း / ဥပမာ |
|---|---|---|---|
| First name / Last name | အမည် | * / * | ≤ 60 |
| Gender | Male / Female / Other | * | |
| Date of birth | မွေးသက္ကရာဇ် | * | အနာဂတ်မရ၊ **အသက် ၁၈ နှစ်အထက်** |
| Marital status | Single / Married / Divorced / Widowed | * | |
| Nationality | နိုင်ငံသား | * | ≤ 60။ `Myanmar` |
| National ID / passport no. | မှတ်ပုံတင်/ပတ်စပို့အမှတ် | * | ≤ 40 (ပုံစံစစ်ခြင်းမရှိ) |

**Contact**

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Work email | ကုမ္ပဏီ email | * | **unique**၊ ≤ 120 |
| Personal email | ကိုယ်ပိုင် email | | ≤ 120 |
| Mobile phone | လက်ကိုင်ဖုန်း | * | phone ပုံစံ (`+95 9 123 456 789`) |
| Alternate phone | အခြားဖုန်း | | |
| Address line 1 / 2 | လိပ်စာ | * / – | line 1 required |
| City / State-region / Postal code / Country | | * / – / – / * | postal ≤ 12 |

**Emergency contact**: Contact name (*)၊ Relationship (*)၊ Contact phone (*)။

**Employment**

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Employee code 🔒 | ဝန်ထမ်းကုဒ် | * | ≤ 12၊ **unique**။ App က နောက်ထပ်ကုဒ်ကို အကြံပြုတယ် (`EMP061` ပုံစံ) |
| Employment type | Full-time / Part-time / Contract / Intern | * | Leave policy ရွေးချယ်ရာမှာ သုံး |
| Employment status | Active / Probation / On Leave / Suspended / Resigned / Terminated | * | Resigned/Terminated ဆို Exit date လို |
| Joining date | စတင်အလုပ်ဝင်ရက် | * | Leave နဲ့ လစာ ဒီရက်မတိုင်ခင် မရ၊ Payroll က ဒီကနေ prorate |
| Probation end date | စမ်းသပ်ကာလပြီးရက် | | Joining ထက် စောမရ |
| Exit date | ထွက်ခွာသည့်ရက် | | Joining ထက် စောမရ |

**Organization assignment**

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Company → Branch → Department | ကုမ္ပဏီ၊ ဌာနခွဲ၊ ဌာန | * | ဆက်စပ်မှုမှန်ရမယ် (Branch က Company ရဲ့၊ Department က Branch ရဲ့) |
| Section | ကဏ္ဍ | | Department ရဲ့ Section သာ |
| Position | ရာထူး | * | Department နဲ့ ကိုက်ရမယ် |
| Job grade | လစာအဆင့် | * | |
| Reporting manager | ကိုယ့်အထက်လူ | | ကိုယ့်ကိုယ်ကိုယ် မရ |
| Work shift | အလုပ်ချိန် | | Active shift သာ |

Inactive ဖြစ်နေတဲ့ Company/Branch/Department/Position ကို ဝန်ထမ်းအသစ်ထည့်လို့ မရ။

**Bank**

| Field | * | စည်းမျဉ်း |
|---|---|---|
| Bank name | * | ≤ 60 |
| Bank branch | – | ≤ 60 |
| Account holder name | * | ≤ 80 |
| Account number | * | ဂဏန်းသာ၊ **၈–၂၀ လုံး** |

### Employee document

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Employee | ဘယ်သူ့ document | * | (ဝန်ထမ်း profile ထဲကဆို ကြိုရွေးထား) |
| Document type | အမျိုးအစား (စာချုပ်၊ ID ...) | * | |
| Title | ခေါင်းစဉ် | * | ≤ 80 |
| File | ဖိုင် | * | PDF/Word/PNG/JPG ≤ 5 MB။ **ဖိုင်နာမည်ပဲ သိမ်းတယ်** |
| Uploaded on | တင်သည့်ရက် | * | ယနေ့ ဒါမှမဟုတ် အတိတ် |
| Expiry date | သက်တမ်းကုန်ရက် | | မကုန်ရင် အလွတ်ထား |

---

## ၄။ Attendance

### Shift

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း / ဥပမာ |
|---|---|---|---|
| Shift code 🔒 | ကုဒ် | * | ≤ 10။ `GEN` |
| Shift name | အမည် | * | ≤ 60 |
| Start time / End time | စ/ဆုံး အချိန် | * / * | နေ့တာဝန်ဆို End > Start ဖြစ်ရမယ်။ Night shift ဆို နောက်နေ့ ဆင်းလို့ရ |
| Unpaid break (minutes) | လစာမပေးတဲ့ အနားယူချိန် | * | 0–240။ Worked = (out − in) − break |
| Late grace period (minutes) | နောက်ကျခွင့် | * | 0–120။ ဒီမိနစ်အတွင်း ရောက်ရင် Late မဟုတ်၊ ကျော်ရင် Late မိနစ်ကို shift start ကနေ ရေတွက် |
| Working days | အလုပ်လုပ်ရက်များ | * | အနည်းဆုံး ၁ ရက် |
| Night shift | ညဆိုင်း (နောက်နေ့ဆင်း) | | toggle |
| Calendar colour | schedule grid အရောင် | | |
| Status | | * | Inactive shift ကို assign မရ |

### Overtime

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Employee | ဘယ်သူ | * | |
| Date | OT လုပ်ခဲ့တဲ့ရက် | * | ယနေ့ ဒါမှမဟုတ် အတိတ်။ လူတစ်ယောက်ကို တစ်ရက်ကို entry တစ်ခုပဲ (status မရွေး) |
| Reason | အကြောင်းရင်း | * | ≤ 120 |
| Overtime start | စချိန် | * | ပုံမှန်အားဖြင့် shift ဆုံးချိန် |
| Overtime end | ဆုံးချိန် | * | ကာလ **၁၅ မိနစ် – ၁၂ နာရီ** |
| Status / Decided by / Remarks | ကြည့်ရုံ | | Pending ဖြစ်မှ ပြင်/ဖျက်လို့ရ။ Approve ဖြစ်မှ payroll ထဲ ပါ |

### Attendance adjustment

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Employee | ဘယ်သူ | * | |
| Date to correct | ပြင်ချင်တဲ့ရက် | * | ယနေ့ ဒါမှမဟုတ် အတိတ်၊ **အဲဒီရက်မှာ attendance record ရှိရမယ်** |
| Set status (optional) | Status အတင်းသတ်မှတ် (Present/Late/Half Day/Absent) | | |
| Correct check-in / check-out | မှန်ကန်တဲ့ In/Out | – | မဖြည့်ရင် မူလအတိုင်း။ **အနည်းဆုံး time တစ်ခု ဒါမှမဟုတ် status တစ်ခု** ပြောင်းရမယ်။ Out > In (night shift မဟုတ်ရင်) |
| Reason | အကြောင်းရင်း | * | ≤ 250 |

တူညီတဲ့ employee + date ကို Pending request နှစ်ခု မရ။ Approve မှ record ပြန်တွက်။

### Check In / Out (ဖောင်မဟုတ်၊ ခလုတ်)

"Acting as" ကနေ ဝန်ထမ်းရွေးပြီး Check in/out နှိပ်ရင် **ယနေ့ရက် + browser နာရီ** နဲ့ record ဖြစ်တယ်။

---

## ၅။ Leave

### Leave type

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း / ဥပမာ |
|---|---|---|---|
| Code 🔒 | ကုဒ် | * | ≤ 6။ `AL` |
| Leave type name | အမည် | * | ≤ 60 |
| Description | ဖော်ပြချက် | | ≤ 250 |
| Default days per year | တစ်နှစ်ကို ရက်အရေအတွက် (policy မရှိရင် ဒါကိုသုံး) | * | 0–365 |
| Maximum consecutive days | တစ်ကြိမ်ယူလို့ရတဲ့ အများဆုံးရက် | * | 1–365 |
| Eligibility | ဘယ်ကျားမ ယူလို့ရလဲ | | အလွတ် = လူတိုင်း |
| Calendar colour | calendar အရောင် | | |
| Paid leave | လစာရလား | | **ပိတ်ထားရင် Payroll မှာ နှုတ်** |
| Supporting document required | စာရွက်စာတမ်းလိုလား | | Submit မလုပ်ခင် လိုတယ် |
| Unused days carry forward | မသုံးရက်ကို နောက်နှစ်ရွှေ့မလား | | |
| Maximum carry-forward days | ရွှေ့လို့ရတဲ့ အများဆုံး | * | 0–365၊ **တစ်နှစ်ရက်ထက် မကျော်ရ** |
| Status | | * | |

### Leave policy

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Policy name | အမည် | * | ≤ 80 |
| Leave type | ဘယ်ခွင့်အတွက် | * | |
| Days per year | တစ်နှစ်ရက် | * | 0–365 |
| Job grade | ဘယ်အဆင့်အတွက် | | အလွတ် = All grades |
| Employment type | ဘယ်အမျိုးအစားအတွက် | | အလွတ် = All types |
| Accrual | Upfront (တစ်ခါတည်းပေး) / Monthly (လစဉ်စုလာ) | * | Monthly = ရက် × လ ÷ 12 (ယနေ့ရဲ့ လ အရ၊ half-day အဆင့်) |
| Min. service (months) | အနည်းဆုံး အလုပ်လုပ်ထားရမယ့်လ | * | မပြည့်ရင် policy ကျော်၊ policy ရှိပေမဲ့ မပြည့်ရင် **0 ရက်** |
| Status | | * | |

Policy ရွေးပုံ: ကိုက်တဲ့ policy တွေထဲ grade ကိုက်ရင် ၂ မှတ်၊ employment type ကိုက်ရင် ၁ မှတ်။ အမှတ်များတာ အနိုင်ရ။

### Leave application

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Employee | ဘယ်သူ | * | |
| Leave type | ဘယ်ခွင့် | * | လူနဲ့ ကိုက်တဲ့ type သာ (gender) |
| Half day | တစ်ဝက်နေ့ | | Start = End ဖြစ်ရမယ် |
| Start date / End date | စ/ဆုံးရက် | * / * | End ≥ Start၊ **Joining date မတိုင်ခင် မရ** |
| Number of days | ရက်ပေါင်း | auto | weekend နဲ့ holiday ဖယ်ပြီး အလိုအလျောက်။ 0 ဆို မရ၊ type ရဲ့ max consecutive ကျော်ရင် မရ |
| Reason | အကြောင်းရင်း | * | ≤ 250 |
| Supporting document | စာရွက် | | Medical ကဲ့သို့ type တွေက Submit မလုပ်ခင် လို (ဖိုင်နာမည်ပဲ) |

လုပ်ငန်းစဉ်: Draft ဖြစ်မှ ပြင်/ဖျက်လို့ရ → **Submit** (balance မလောက်ရင် ပယ်၊ ရက်ထပ်နေရင် ပယ်) → Approver က Approve/Reject။
- Approve: Remarks (optional)
- Reject: Reason (**မဖြစ်မနေ**)
- Cancel: Reason (optional)၊ Approve ပြီးပြီး စတင်ပြီးသားဆို Cancel မရ
- **ကိုယ့်ခွင့်ကိုယ် Approve မရ**

---

## ၆။ Payroll

### Salary component

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း / ဥပမာ |
|---|---|---|---|
| Code 🔒 | ကုဒ် | * | ≤ 14၊ unique။ `HOUSING` |
| Component name | အမည် | * | ≤ 60 |
| Type | Earning (ရ) / Deduction (နှုတ်) | * | |
| Calculation | **Fixed** (ပမာဏသေ) / **PercentOfBasic** (basic ရဲ့ %) / **PercentOfGross** (gross ရဲ့ %) | * | PercentOfGross က **Deduction** အတွက်ပဲ အဓိပ္ပာယ်ရှိတယ်။ Earning မှာ ရွေးရင်လည်း code က basic ပေါ်မှာ တွက်တယ် |
| Amount or percentage | တန်ဖိုး | * | 0 ထက်ကြီး။ % ဆို ≤ 100 |
| Description / Status | | – / * | ≤ 250 |

### Salary structure

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Code 🔒 | ကုဒ် | * | ≤ 10။ `STAFF` |
| Structure name | အမည် | * | ≤ 60 |
| Components | ပါဝင်မယ့် component များ | * | Active component သာ၊ **Earning အနည်းဆုံး ၁ ခု** |
| Description / Status | | – / * | |

### Employee salary

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Employee 🔒 | ဘယ်သူ | * | |
| Salary structure | ဘယ် structure သုံးမလဲ | * | Active သာ |
| Monthly basic salary | လစာအခြေခံ (တစ်လစာ) | * | ≥ 1၊ **Job grade band အတွင်း** |
| Effective from | စတင်သက်ရောက်ရက် | * | Joining date မတိုင်ခင် မရ။ တူညီတဲ့ရက် assignment နှစ်ခု မရ |
| Remarks | မှတ်ချက် | | ≤ 120 |

လစာတိုးရင် Effective from အသစ်နဲ့ **row အသစ်** ထပ်ထည့်တယ် (ရှိပြီးသားကို မပြင်)။ Employee တစ်ယောက်မှာ assignment အနည်းဆုံး ၁ ခု ရှိရမယ်။

### Payroll period

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Year | နှစ် | * | ယခုနှစ် ဒါမှမဟုတ် ယခင်နှစ် |
| Month | လ | * | **အနာဂတ်လ မရ**။ တူညီတဲ့ year+month နှစ်ခု မရ |
| Pay date | လစာပေးမယ့်ရက် | | မဖြည့်ရင် လကုန်ရက်။ Period စတဲ့ရက်ထက် စောမရ |

ကျန် field တွေ (Employees paid၊ Total gross/net ...) က Process လုပ်မှ ပေါ်တဲ့ ကြည့်ရုံတန်ဖိုးတွေ။
လုပ်ဆောင်ချက်: Process → Approve → Mark as paid၊ Pending/Approved ကို Return to draft လုပ်လို့ရ (reason ပါ)။

---

## ၇။ Recruitment

### Job opening

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Job title | ရာထူးခေါင်းစဉ် | * | ≤ 80 |
| Department | ဘယ်ဌာနအတွက် | * | |
| Hiring manager | ခန့်မယ့်တာဝန်ခံ | * | **လက်ရှိ ဝန်ထမ်း** (exit date မရှိ) |
| Employment type | Full-time / Part-time / Contract / Internship | * | |
| Vacancies | လိုအပ်တဲ့လူ | * | 1–50၊ Selected ဖြစ်ပြီးသူထက် နည်းလို့မရ |
| Posted on | ကြော်ငြာရက် | * | ယနေ့ ဒါမှမဟုတ် အတိတ် |
| Closing date | လက်ခံပိတ်ရက် | * | Posted ထက် စောမရ။ **Publish/Reopen လုပ်ဖို့ ယနေ့ ဒါမှမဟုတ် နောက်ဖြစ်ရ** |
| Description | ဖော်ပြချက် | | ≤ 500 |
| Code / Status | ကြည့်ရုံ | | Code = `JOB-<နှစ်>-<နံပါတ်>` အလိုအလျောက်၊ Draft အဖြစ်စ |

Status အပြောင်း: Draft → Open/Closed၊ Open ↔ On Hold၊ Open/On Hold → Closed၊ Closed → Open။ Closed/Filled ကို ပြင်မရ။

### Candidate

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| First name / Last name | အမည် | * / * | ≤ 40 |
| Email | email | * | ≤ 100၊ **unique** |
| Phone | ဖုန်း | * | phone ပုံစံ |
| Current employer | လက်ရှိအလုပ်ရှင် | | ≤ 80 |
| Years of experience | အတွေ့အကြုံနှစ် | * | 0–50 |
| Source | Career site / Referral / Job board / LinkedIn / Agency | * | |
| Resume | CV | | ဖိုင်နာမည်ပဲ |
| Notes | မှတ်ချက် | | ≤ 300 |

### Application (Pipeline → New application)

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Job opening | ဘယ်ရာထူးကို လျှောက်လဲ | * | **Open** ဖြစ်တဲ့ opening သာ |
| Candidate | ဘယ်သူ | * | တစ်ယောက်ကို opening တစ်ခုကို တစ်ခါပဲ |
| Notes | မှတ်ချက် | | ≤ 300 |
| Stage / Applied on | auto | | Stage = Applied၊ Applied on = ယနေ့ |

Stage ရွှေ့ပုံ: Applied → Screening → Interview → Selected၊ ဘယ်အဆင့်ကမဆို → Rejected (reason မဖြစ်မနေ)။ **Selected** ဖို့ Hire/Strong hire ပါတဲ့ Completed interview နဲ့ vacancy လွတ်ရမယ်။

### Interview

| Field | ဆိုလိုတာ | * | စည်းမျဉ်း |
|---|---|---|---|
| Application 🔒 | ဘယ်သူ/ဘယ်ရာထူး | * | Screening ဒါမှမဟုတ် Interview stage ဖြစ်နေသူသာ |
| Round | Phone screen / Technical / HR / Final | * | |
| Date / Start time | ရက်/အချိန် | * / * | **Scheduled** ဆို ယနေ့ ဒါမှမဟုတ် နောက်ရက်၊ **Completed** ဆို ယနေ့ ဒါမှမဟုတ် အတိတ် |
| Duration (minutes) | ကြာချိန် | * | 15–240 |
| Mode | Onsite / Video / Phone | * | |
| Interviewer | မေးမြန်းသူ | * | လက်ရှိဝန်ထမ်း။ အချိန်ထပ်ရင် ပယ် (Scheduled တွေ) |
| Room or link | အခန်း/လင့် | | ≤ 120 |
| Status | Scheduled / Completed / Cancelled | * | Cancelled ပြီးရင် ပြင်မရ၊ Completed ကို ဖျက်မရ |
| Score (1–5) | ရမှတ် | Completed ဆို * | 1–5၊ Completed မဟုတ်ရင် ထည့်မရ |
| Recommendation | Strong hire / Hire / No hire | Completed ဆို * | |
| Feedback | မှတ်ချက် | | ≤ 500 |

---

## Holiday ဖောင် မရှိပါ

Holiday (`date`၊ `name`၊ `type` = Public/Company) ဆိုတဲ့ data model ရှိပေမဲ့ ထည့်/ပြင်တဲ့ screen၊ route မရှိပါဘူး။
ဒါကြောင့် ရုံးပိတ်ရက်ကို Leave ရက်တွက်တာ၊ Work Schedules နဲ့ Payroll working days မှာ မထည့်နိုင်ပါဘူး။
