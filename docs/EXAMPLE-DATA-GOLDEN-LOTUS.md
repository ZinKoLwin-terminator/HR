# Example data pack: Golden Lotus Trading Co., Ltd.

ဒီ pack က app ထဲ **အစကနေ အဆုံးထိ တစ်ကြောင်းချင်း ထည့်လို့ရတဲ့** စိတ်ကူးယဉ်ဒေတာပါ။ Company၊ လူ၊ ဖုန်း၊
email၊ NRC၊ bank account အားလုံး ဖန်တီးထားတာပါ (တကယ့်လူ/ကုမ္ပဏီ မဟုတ်ပါ)။ Email တွေက `.example` domain ပါ။

- App: `feature/empty-data` branch (စာရင်းအားလုံး အလွတ်စတင်၊ ထည့်တာတွေ browser မှာ သိမ်းထား၊ **Settings → General Settings → Reset data** နဲ့ ရှင်း)
- စစ်ထားတဲ့ရက်: today = 2026-11-07၊ payroll လ = **2026 အောက်တိုဘာ**
- ဂဏန်းတွေက `attendance-calc.ts`၊ `leave-balance.ts`၊ `payroll-calc.ts` ရဲ့ ဖော်မြူလာအတိုင်း လက်နဲ့တွက်ထားတာ။ App ထဲ ထည့်ပြီး စစ်ထားတာ မဟုတ်သေးပါ။ ကွာရင် ပြောပါ။

## ⚠️ ထည့်မယ့်အစီအစဉ်ကို ဒီအတိုင်း လိုက်ပါ (code ကနေ စစ်တွေ့ချက်)

| # | စည်းမျဉ်း | ဆိုလိုတာ |
|---|---|---|
| 1 | Job opening ရဲ့ hiring manager နဲ့ interviewer က **ရှိပြီးသား employee** ဖြစ်ရမယ် | Recruitment ကို employee ၄ ယောက် (EMP-001 ကနေ 004) ထည့်ပြီးမှ လုပ်ရမယ်။ EMP-005 ကို Recruitment ပြီးမှ ထည့်မယ် |
| 2 | Roles + Permissions က seed မှာ ပါပြီးသား (HR Administrator၊ HR Officer၊ Department Manager၊ Payroll Officer၊ Recruiter၊ Employee၊ Auditor) | Role အသစ် မဖန်တီးပါနဲ့။ ရှိတာကို သုံးပါ |
| 3 | Attendance record ကို **Check In / Out page** ကနေပဲ ဖန်တီးလို့ရတယ်။ **ယနေ့ရက် + browser နာရီ** ပဲ သုံးတယ်။ Attendance Records page က ဖတ်ရုံသက်သက် | အောက်တိုဘာ ရက်စဉ် In/Out တွေကို app ထဲ ထည့်လို့ **မရပါ**။ §9 မှာ reference table အဖြစ်ပဲ ပေးထားတယ်။ Adjustment ကလည်း record ရှိမှ တင်လို့ရတယ် |
| 4 | Leave balance ကို **ယနေ့ရက်** နဲ့ တွက်တယ် (Monthly accrual = ရက် × လ ÷ 12) | 2026-11-07 မှာ Casual = 6 × 11 ÷ 12 = **5.5**။ လပြောင်းရင် ဂဏန်းပြောင်းမယ် |
| 5 | Seed ထဲက admin user က **employee id 2** နဲ့ ချိတ်ထားတယ်။ Leave ကို **ကိုယ့်ကိုယ်ကိုယ် Approve မရ** | ဒါကြောင့် ဦးကျော်ဇင် (EMP-002) ကို **ဒုတိယအဖြစ်** ဖန်တီးပါ။ သူ့မှာ ခွင့်မရှိလို့ ပြဿနာမရှိဘူး။ Leave တွေကို seed admin အဖြစ် Approve လုပ်ပါ |
| 6 | Job opening ကို Publish လုပ်ဖို့ **closing date က ယနေ့ ဒါမှမဟုတ် နောက်** ဖြစ်ရမယ် | Closing = 2026-11-30 ထည့်ထားတယ် |
| 7 | Interview ကို **Scheduled** အဖြစ် အတိတ်ရက်ထည့်လို့မရ။ **Completed** ဆိုရင် အတိတ်ရက်ရတယ် (score + recommendation လိုတယ်) | စက်တင်ဘာ interview တွေကို Completed အဖြစ် တန်းထည့်တယ် |
| 8 | Payroll ကို ယခင် period ရှိနေမှသာ "အရင်ပြီးစီးဖို့" ပိတ်တယ် | ပထမဆုံး period ဖြစ်လို့ Oct ကို တန်း Process လုပ်လို့ရတယ် |
| 9 | **Holiday ထည့်တဲ့ screen app ထဲမှာ မရှိပါ** (route/menu မရှိ၊ data service ပဲရှိ) | Holiday မထည့်နိုင်လို့ Oct ရုံးဖွင့်ရက် = **22** (Mon–Fri အားလုံး)။ Payroll ဂဏန်းတွေကို ဒီအတိုင်း တွက်ထားတယ် |
| 10 | Application ရဲ့ applied date က **ထည့်တဲ့ရက် (ယနေ့)** အလိုအလျောက် | စက်တင်ဘာ 3–8 လို့ ရက်ပြောင်းထည့်လို့မရ။ Job opening ရဲ့ Posted on ကိုတော့ ရက်ရွေးလို့ရ |

Login: seed admin ဖြစ်တဲ့ **Su Myat Noe** (HR Administrator) · email `sumyat.noe@apexhorizon.example`၊ password `Welcome@123`။ Login page ရဲ့ demo button က အလိုအလျောက်ဖြည့်ပေးတယ်။

---

## အစီအစဉ်အကျဉ်း

```
1 General Settings
2 Organization   Company → Branches → Departments → Sections → Job Grades → Positions
3 Attendance setup (ကြိုတင်)   Shifts (Holiday screen မရှိ)
4 Employees EMP-001, 002, 003, 004 (ဒီအစဉ်အတိုင်း)
5 Recruitment   Opening → Candidates → Applications → Interviews → Selected/Rejected
6 Employee EMP-005 (ရွေးထားသူကို ဝန်ထမ်းအဖြစ်)
7 Shift Assignment (လိုရင် bulk) + Users
8 Leave setup → Payroll setup
9 Operations   Leave / Overtime (+ Attendance, Adjustment ကို ယနေ့ရက်နဲ့)
10 Payroll Oct 2026   Create → Process → Approve → Paid
```

---

## 1. General Settings

| Field | တန်ဖိုး |
|---|---|
| Organization name | Golden Lotus Trading |
| Contact email | hr@goldenlotus.example |
| Time zone | Asia/Yangon |
| Date format | yyyy-MM-dd |
| Currency code | MMK |
| Fiscal year starts in | January |
| Send email notifications | on |
| Send a weekly digest | off |

---

## 2. Organization

### 2.1 Company

| Field | တန်ဖိုး |
|---|---|
| Code | GLT |
| Name | Golden Lotus Trading |
| Legal name | Golden Lotus Trading Co., Ltd. |
| Registration number | GLT-REG-2015-0001 |
| Tax ID | GLT-TAX-000123 |
| Industry | Wholesale & Distribution |
| Email | info@goldenlotus.example |
| Phone | +95 1 555 0100 |
| Website | https://www.goldenlotus.example |
| Address | No. 25, Pyay Road, Kamayut Township |
| City / Country | Yangon / Myanmar |
| Founded date | 2015-06-01 |
| Status | Active |

### 2.2 Branches (Manager ကို EMP-001 ထည့်ပြီးမှ ပြန်ပြင်ပါ)

| Code | Name | Type | Email | Phone | Address | City | Opened |
|---|---|---|---|---|---|---|---|
| YGN-HQ | Yangon HQ | Head Office | yangon@goldenlotus.example | +95 1 555 0101 | No. 25, Pyay Road, Kamayut Township | Yangon | 2015-06-01 |
| MDY-WH | Mandalay Warehouse | Warehouse | mandalay@goldenlotus.example | +95 2 555 0102 | Plot 14, Industrial Zone (2), Chanmyathazi Township | Mandalay | 2019-03-15 |

Company = Golden Lotus Trading၊ Country = Myanmar၊ Status = Active (နှစ်ခုလုံး)။

### 2.3 Departments (Company = GLT၊ Head ကို နောက်မှ)

| Code | Name | Branch | Cost center | Description |
|---|---|---|---|---|
| HR-ADM | HR & Admin | Yangon HQ | CC-100 | Hiring, attendance, payroll, office administration |
| FIN | Finance | Yangon HQ | CC-200 | Accounting and budgeting |
| SAL | Sales | Yangon HQ | CC-300 | Retail and wholesale sales |
| WH | Warehouse | Mandalay Warehouse | CC-400 | Stock receiving, storage and dispatch |

### 2.4 Sections

| Code | Name | Department | Description |
|---|---|---|---|
| HR-OPS | HR Operations | HR & Admin | Day-to-day HR work |
| ACC | Accounts | Finance | Bookkeeping and payments |
| RTL | Retail Sales | Sales | Walk-in and online customers |
| STK | Stock Control | Warehouse | Inventory and dispatch |

### 2.5 Job Grades (လစာ band သတိထားပါ)

| Code | Name | Level | Min salary | Max salary | Description |
|---|---|---|---|---|---|
| STF | Staff | 1 | 400,000 | 700,000 | Entry and operational roles |
| SSTF | Senior Staff | 2 | 700,000 | 1,100,000 | Experienced specialists |
| MGR | Manager | 3 | 1,500,000 | 2,500,000 | Team and department managers |

### 2.6 Positions (HR-MGR ကို အရင်ဖန်တီးပါ)

| Code | Title | Department | Section | Grade | Headcount | Reports to |
|---|---|---|---|---|---|---|
| HR-MGR | HR Manager | HR & Admin | HR Operations | MGR | 1 | – |
| ADM-AST | Admin Assistant | HR & Admin | HR Operations | STF | 2 | HR Manager |
| ACCT | Accountant | Finance | Accounts | SSTF | 1 | HR Manager |
| SAL-EXE | Sales Executive | Sales | Retail Sales | STF | 3 | HR Manager |
| WH-STF | Warehouse Staff | Warehouse | Stock Control | STF | 4 | HR Manager |

---

## 3. Attendance setup (ကြိုတင်)

### 3.1 Shifts

| Code | Name | Start | End | Break | Grace | Working days | Night | Colour | Status |
|---|---|---|---|---|---|---|---|---|---|
| GEN | General Shift | 09:00 | 18:00 | 60 | 15 | Mon–Fri | off | #3f6fd8 | Active |
| WH-AM | Warehouse Morning | 06:00 | 14:30 | 30 | 10 | Mon–Sat | off | #0f9d76 | Active |

### 3.2 Holiday

**ဒီ app မှာ Holiday ထည့်တဲ့ screen မရှိပါ။** ဒါကြောင့် ဒီ pack မှာ Holiday မထည့်ပါ။ Work Schedules၊ Leave တွက်ရာ၊ Payroll working days အားလုံး weekend (စနေ၊ တနင်္ဂနွေ) ပဲ ဖယ်ပါတယ်။ Holiday screen ထည့်ချင်ရင် feature အသစ်အနေနဲ့ ပြောပါ။

---

## 4. Employees (ဒီအစဉ်အတိုင်း ဖန်တီးပါ)

အားလုံးမှာ: Nationality = Myanmar၊ Country = Myanmar၊ Company = Golden Lotus Trading၊ Employment type = Full-time။

### EMP-001 Khin Thida Oo (HR Manager)

| Field | တန်ဖိုး |
|---|---|
| First / Last name | Khin Thida / Oo |
| Gender / DOB / Marital | Female / 1985-04-12 / Married |
| National ID | 12/KaMaYa(N)000101 |
| Work email / Personal | khin.thida@goldenlotus.example / khinthida.oo@mail.example |
| Mobile / Alternate | +95 9 555 0111 / +95 1 555 0191 |
| Address | No. 12, Inya Road, Kamayut / Yangon / Yangon Region / 11041 |
| Emergency contact | Tun Oo / Spouse / +95 9 555 0112 |
| Employee code / Status | EMP-001 / Active |
| Joining / Probation end / Exit | 2022-03-01 / 2022-06-01 / – |
| Branch / Department / Section | Yangon HQ / HR & Admin / HR Operations |
| Position / Grade | HR Manager / Manager |
| Reporting manager | – (မရှိ) |
| Work shift | GEN |
| Bank | KBZ Bank / Kamayut Branch / Khin Thida Oo / 9990000000011 |

### EMP-002 Kyaw Zin Htun (Accountant) ← ဒုတိယအဖြစ် ဖန်တီးရမယ် (id 2)

| Field | တန်ဖိုး |
|---|---|
| First / Last name | Kyaw Zin / Htun |
| Gender / DOB / Marital | Male / 1990-09-23 / Married |
| National ID | 9/MaHaMa(N)000202 |
| Work email / Personal | kyaw.zin@goldenlotus.example / kyawzin.htun@mail.example |
| Mobile / Alternate | +95 9 555 0121 / – |
| Address | No. 48, Bogyoke Street, Sanchaung / Yangon / Yangon Region / 11111 |
| Emergency contact | Mya Mya / Spouse / +95 9 555 0122 |
| Code / Status | EMP-002 / Active |
| Joining / Probation end | 2023-06-15 / 2023-09-15 |
| Branch / Department / Section | Yangon HQ / Finance / Accounts |
| Position / Grade | Accountant / Senior Staff |
| Reporting manager / Shift | Khin Thida Oo / GEN |
| Bank | AYA Bank / Sanchaung Branch / Kyaw Zin Htun / 2220000000022 |

### EMP-003 Su Hlaing Win (Sales Executive)

| Field | တန်ဖိုး |
|---|---|
| First / Last name | Su Hlaing / Win |
| Gender / DOB / Marital | Female / 1998-01-30 / Single |
| National ID | 12/LaMaNa(N)000303 |
| Work email / Personal | su.hlaing@goldenlotus.example / suhlaing98@mail.example |
| Mobile | +95 9 555 0131 |
| Address | No. 7, Kyuntaw Street, Hlaing / Yangon / Yangon Region / 11051 |
| Emergency contact | Aye Win / Mother / +95 9 555 0132 |
| Code / Status | EMP-003 / Active |
| Joining / Probation end | 2024-09-01 / 2024-12-01 |
| Branch / Department / Section | Yangon HQ / Sales / Retail Sales |
| Position / Grade | Sales Executive / Staff |
| Reporting manager / Shift | Khin Thida Oo / GEN |
| Bank | CB Bank / Hlaing Branch / Su Hlaing Win / 3330000000033 |

### EMP-004 Min Thu Aung (Warehouse Staff)

| Field | တန်ဖိုး |
|---|---|
| First / Last name | Min Thu / Aung |
| Gender / DOB / Marital | Male / 1996-07-08 / Single |
| National ID | 9/PaBaNa(N)000404 |
| Work email / Personal | min.thu@goldenlotus.example / minthu.aung96@mail.example |
| Mobile | +95 9 555 0141 |
| Address | No. 30, 73rd Street, Chanayethazan / Mandalay / Mandalay Region / 05011 |
| Emergency contact | Hla Aung / Father / +95 9 555 0142 |
| Code / Status | EMP-004 / Active |
| Joining / Probation end | 2025-01-10 / 2025-04-10 |
| Branch / Department / Section | Mandalay Warehouse / Warehouse / Stock Control |
| Position / Grade | Warehouse Staff / Staff |
| Reporting manager / Shift | Khin Thida Oo / WH-AM |
| Bank | KBZ Bank / Mandalay Branch / Min Thu Aung / 9990000000044 |

ဒီနေရာမှာ Branch manager (Yangon HQ) နဲ့ Department head (HR & Admin) ကို Khin Thida Oo အဖြစ် ပြန်သတ်မှတ်နိုင်ပြီ။

### EMP-005 Hnin Yadanar Myint (Admin Assistant): Recruitment (§5) ပြီးမှ ဖန်တီး

| Field | တန်ဖိုး |
|---|---|
| First / Last name  | Hnin Yadanar / Myint |
| Gender / DOB / Marital | Female / 2001-11-19 / Single |
| National ID | 12/TaMaNa(N)000505 |
| Work email / Personal | hnin.yadanar@goldenlotus.example / hninyadanar01@mail.example |
| Mobile | +95 9 555 0151 |
| Address | No. 5, Mahabandoola Road, Latha / Yangon / Yangon Region / 11131 |
| Emergency contact | Thant Myint / Father / +95 9 555 0152 |
| Code / Status | EMP-005 / Probation |
| Joining / Probation end | **2026-10-16** / 2027-01-16 |
| Branch / Department / Section | Yangon HQ / HR & Admin / HR Operations |
| Position / Grade | Admin Assistant / Staff |
| Reporting manager / Shift | Khin Thida Oo / GEN |
| Bank | AYA Bank / Latha Branch / Hnin Yadanar Myint / 2220000000055 |

စစ်မယ့် rule: Phone ပုံစံ (`+95 9 555 0111`)၊ DOB ≥ 18 နှစ်၊ Branch/Department/Position ဆက်စပ်မှု။ Work email နဲ့
employee code က unique ဖြစ်ရမယ်။

---

## 5. Recruitment (EMP-001 နဲ့ 002 ရှိပြီးမှ)

### 5.1 Job opening

| Field | တန်ဖိုး |
|---|---|
| Title | Admin Assistant |
| Department | HR & Admin |
| Hiring manager | Khin Thida Oo |
| Employment type | Full-time |
| Vacancies | 1 |
| Posted on | 2026-09-01 |
| Closing date | 2026-11-30 |
| Description | Handles HR records, attendance data entry and office supplies. |

Code က `JOB-2026-001` အလိုအလျောက်ရမယ်၊ Draft အဖြစ် စတယ်။ **Publish** နှိပ်ပါ → Open။

### 5.2 Candidates

| Name | Email | Phone | Source | Current employer | Experience (yrs) | Resume file | Notes |
|---|---|---|---|---|---|---|---|
| Hnin Yadanar Myint | hninyadanar01@mail.example | +95 9 555 0151 | Career site | Fresh graduate | 0 | hnin-yadanar-cv.pdf | BA (English), good Excel skills |
| Zaw Htet Naing | zawhtet.naing@mail.example | +95 9 555 0161 | Job board | Shwe Pyi Trading | 3 | zaw-htet-cv.pdf | Asks for higher salary |
| Thin Zar Aye | thinzar.aye@mail.example | +95 9 555 0171 | Referral | Lotus Stationery | 2 | thin-zar-cv.pdf | Referred by Kyaw Zin Htun |

### 5.3 Pipeline

| အဆင့် | လုပ်ရပ် | ရလဒ် |
|---|---|---|
| 1 | Application ၃ ခု ဖန်တီး (Job = Admin Assistant)။ Applied date က ယနေ့ရက် အလိုအလျောက် | Stage = Applied |
| 2 | ၃ ခုလုံး → **Screening** | Screening |
| 3 | Interview ဖန်တီး: Hnin / Round **HR** / Date **2026-09-15** / 10:00 / 45 min / Onsite / Interviewer Khin Thida Oo / Location "Yangon HQ, Meeting Room 1" / Status **Completed** / Score **5** / Recommendation **Strong hire** / Feedback "Clear communication, strong Excel." | Application က Interview stage ကို ရောက်မယ် |
| 4 | Interview ဖန်တီး: Zaw Htet / HR / **2026-09-16** / 14:00 / 45 min / Phone / Interviewer Khin Thida Oo / Completed / Score **3** / Recommendation **No hire** / Feedback "Salary expectation above the grade band." | Interview stage |
| 5 | Zaw Htet → **Selected** ကို စမ်း | ❌ "needs a completed interview with a Hire recommendation" |
| 6 | Hnin → **Selected** | ✅ Opening က **Filled** အလိုအလျောက်ဖြစ်မယ် (1/1) |
| 7 | Zaw Htet → **Rejected**၊ Reason "Salary expectation too high" | ✅ Reason မဖြည့်ရင် မရ |
| 8 | Thin Zar Aye → **Rejected**၊ Reason "Position filled; keep CV for future openings" | ✅ |

ပြီးရင် §4 အတိုင်း EMP-005 ကို Employee အဖြစ် လက်နဲ့ ထည့်ပါ (auto-convert မရှိပါ)။

---

## 6. Users (Settings → Users)

| Employee | Role | Status | လုပ်နည်း |
|---|---|---|---|
| Khin Thida Oo | HR Officer | Active | **Add user** |
| Kyaw Zin Htun | Payroll Officer | Active | **Add user** |
| Su Hlaing Win | Employee | Active | **Add user** |

Built-in admin user က **"Su Myat Noe"** (HR Administrator) နာမည်နဲ့ ရှိပြီးသားပါ။ ဝန်ထမ်းတစ်ဦးဦးနဲ့ မချိတ်ထားပါဘူး
(employee id 0)၊ ဒါကြောင့် Employee dropdown မှာ ဝန်ထမ်းအားလုံး ရွေးလို့ရပါတယ်။ ဒီ admin ကို သူ့ကိုယ်တိုင်
Role ပြောင်းခြင်း၊ Deactivate၊ Delete မလုပ်နိုင်ပါ (နောက်ဆုံး Administrator ကို ကာကွယ်ထားတယ်)။

Name နဲ့ email ကို employee ဆီကနေ ကူးယူတယ်။ Employee dropdown မှာ User ရှိပြီးသား ဝန်ထမ်းနဲ့ ထွက်သွားပြီးသူ
မပေါ်ပါဘူး (တစ်ဦးကို User တစ်ခုပဲ)။ Permission က ဘာကိုမှ enforce မလုပ်ပါဘူး။

---

## 7. Leave setup

### 7.1 Leave types

| Code | Name | Default days | Paid | Needs document | Max consecutive | Carry forward | Max carry | Colour | Eligible gender |
|---|---|---|---|---|---|---|---|---|---|
| AL | Annual Leave | 15 | yes | no | 10 | yes | 5 | #3f6fd8 | (all) |
| CL | Casual Leave | 6 | yes | no | 3 | no | 0 | #0f9d76 | (all) |
| UL | Unpaid Leave | 30 | **no** | no | 10 | no | 0 | #8a4fd1 | (all) |

### 7.2 Policies

| Name | Leave type | Grade | Employment type | Days/yr | Accrual | Min service (months) |
|---|---|---|---|---|---|---|
| Annual leave: standard | Annual Leave | (any) | (any) | 15 | Upfront | 3 |
| Annual leave: managers | Annual Leave | Manager | (any) | 18 | Upfront | 0 |
| Casual leave: all staff | Casual Leave | (any) | (any) | 6 | Monthly | 0 |
 
### 7.3 Entitlement ဘယ်လိုရမလဲ (ယနေ့ 2026-11-07 ကိုက်)

| Employee | Annual | Casual | ရှင်းချက် |
|---|---|---|---|
| Khin Thida Oo | **18** | 5.5 | Manager policy က grade ကိုက်လို့ (2 မှတ်) Standard (0 မှတ်) ကို အနိုင်ရ |
| Kyaw Zin Htun | **15** | 5.5 | Standard |
| Su Hlaing Win | **15** | 5.5 | Standard |
| Min Thu Aung | **15** | 5.5 | Standard |
| Hnin Yadanar Myint | **0** | 5.5 | ဝင်တာ 3 လမပြည့်လို့ Annual = 0။ Casual က min 0 လို့ 5.5 ရတယ် (join ရက်နဲ့ prorate မလုပ်) |

Casual 5.5 = 6 × 11 ÷ 12 ကို တစ်ဝက်ရက်အဆင့် ဝိုက်တာ။

---

## 8. Payroll setup

### 8.1 Salary components

| Code | Name | Type | Calculation | Value |
|---|---|---|---|---|
| HOUSING | Housing allowance | Earning | % of basic | 10 |
| TRANSPORT | Transport allowance | Earning | Fixed | 50,000 |
| MEAL | Meal allowance | Earning | Fixed | 30,000 |
| PERFORMANCE | Performance allowance | Earning | % of basic | 5 |
| TAX | Sample income tax | Deduction | % of gross | 5 |
| SOCIAL | Sample social contribution | Deduction | % of gross | 2 |

Tax နဲ့ Social က **ဥပဒေအရ နှုန်းမဟုတ်ပါ**၊ နမူနာပဲ။

### 8.2 Salary structures (Earning အနည်းဆုံး ၁ ခု လိုတယ်)

| Code | Name | Components |
|---|---|---|
| STAFF | Staff structure | HOUSING, TRANSPORT, MEAL, TAX, SOCIAL |
| MANAGER | Manager structure | HOUSING, TRANSPORT, MEAL, PERFORMANCE, TAX, SOCIAL |

### 8.3 Employee salaries (Effective from = joining date)

| Employee | Structure | Basic (MMK) | Effective from | Grade band စစ်ချက် |
|---|---|---|---|---|
| Khin Thida Oo | MANAGER | 1,800,000 | 2022-03-01 | 1.5M–2.5M ✅ |
| Kyaw Zin Htun | STAFF | 900,000 | 2023-06-15 | 0.7M–1.1M ✅ |
| Su Hlaing Win | STAFF | 650,000 | 2024-09-01 | 0.4M–0.7M ✅ |
| Min Thu Aung | STAFF | 600,000 | 2025-01-10 | ✅ |
| Hnin Yadanar Myint | STAFF | 600,000 | 2026-10-16 | ✅ |

စမ်းကြည့်ရန်: Min Thu ကို 750,000 ထည့်ကြည့်ပါ → ❌ "outside the STF band (400000 – 700000)"။

---

## 9. Operations (အောက်တိုဘာ)

### 9.1 Leave applications

ခွင့်ပုံစံ: Create (Draft) → **Submit** (balance စစ်) → seed admin က **Approve**။

| Employee | Type | Start | End | Half day | Days | Reason | လုပ်ရပ် |
|---|---|---|---|---|---|---|---|
| Khin Thida Oo | Annual | 2026-10-19 | 2026-10-21 | no | 3 | Family trip | Submit → Approve |
| Su Hlaing Win | Unpaid | 2026-10-12 | 2026-10-13 | no | 2 | Personal matter | Submit → Approve |
| Min Thu Aung | Casual | 2026-10-07 | 2026-10-07 | no | 1 | Family errand | Submit → Approve |
| Min Thu Aung | Casual | 2026-10-30 | 2026-10-30 | no | 1 | Bank appointment | Submit ထားပြီး **Approve မလုပ်ပါနဲ့** |

ရလဒ်:
- Khin Thida: Annual 18 − 3 = **15** ကျန်။ သူ့ကိုယ်သူ Approve ဖို့ကြိုးစားရင် ❌ "You cannot decide your own leave request" (သူ့ user နဲ့ ဝင်စမ်းကြည့်လို့ရ)။
- Min Thu: Casual 5.5 − taken 1 − pending 1 = **3.5**။
- ရက်တွက်ရာမှာ weekend နဲ့ holiday ကို ဖယ်တယ်။ Khin Thida ရဲ့ Oct 19–21 က တနင်္လာ–ဗုဒ္ဓဟူး ဖြစ်လို့ 3 ရက်။

### 9.2 Overtime

| Employee | Date | Start | End | Minutes | Reason | ရလဒ် |
|---|---|---|---|---|---|---|
| Kyaw Zin Htun | 2026-10-29 | 18:00 | 20:30 | 150 | Month-end closing | Pending → **Approve** (2.5 h) |
| Su Hlaing Win | 2026-10-20 | 18:00 | 18:10 | 10 | Stay for call | ❌ "Overtime must be between 15 minutes and 12 hours" |

Approve လုပ်မှ payroll ထဲ ပါမယ်။

### 9.3 Attendance: app ထဲ ထည့်လို့ရတာ vs reference

**ထည့်လို့မရ (အတိတ်ရက်)**: အောက်ပါ In/Out တွေက `deriveAttendance` ရဲ့ စည်းမျဉ်းကို ပြတဲ့ **reference** ပါ။

| ရက် | Employee | In / Out | Late | Worked | Early | OT | Status |
|---|---|---|---|---|---|---|---|
| Oct 8 | Kyaw Zin | 09:12 / 18:03 | 0 (grace 15 အတွင်း) | 471 | 0 | 0 | Present |
| Oct 9 | Su Hlaing | 09:00 / 12:30 | 0 | 210 (break မနှုတ်) | 0 | 0 | Half Day |
| Oct 14 | Kyaw Zin | 09:32 / 18:10 | **32** | 458 | 0 | 0 | Late |
| Oct 15 | Min Thu (WH-AM) | 06:08 / 14:30 | 0 | 472 | 0 | 0 | Present |
| Oct 16 | Min Thu | 06:25 / 14:30 | **25** | 455 | 0 | 0 | Late |
| Oct 29 | Kyaw Zin | 09:00 / 20:30 | 0 | 630 | 0 | **150** | Present |

စည်းမျဉ်း: Late မိနစ်ကို shift start ကနေ ရေတွက် (grace ကို မနှုတ်)၊ OT က ဆင်းချိန်ပြီး **30 မိနစ်ကျော်မှ**၊ Worked < 240 မိနစ်ဆို Half Day။

**ယနေ့ ထည့်စမ်းလို့ရတာ** (Attendance → Check In / Out):
1. "Acting as" မှာ Su Hlaing Win ကို ရွေး → **Check in** နှိပ်။ နာရီအရ Late/Present ထွက်မယ်။
2. Late ကို မျက်မြင်ကြည့်ချင်ရင် shift အသစ် `TEST` (start = ယခုအချိန်ထက် မိနစ် 30 စောတဲ့အချိန်၊ grace 10) ဖန်တီးပြီး assign လုပ်ပြီးမှ check in လုပ်ပါ။
3. Check out နှိပ်။ ပြီးရင် **Adjustments → New**: Employee = Su Hlaing၊ Date = ယနေ့၊ Correct check-in = `09:00`၊ Correct check-out = `18:05`၊ Reason "Forgot to punch" → Approve။
4. Record ပြန်ကြည့်: In 09:00 / Out 18:05 → late 0၊ worked **485**၊ Present၊ source **Manual**၊ remarks `Adjusted (ADJ-0001)`။

### 9.4 Work Schedules စစ်ချက်

Work Schedules → 2026-10-19 အပတ်သို့ ပြန်သွား: Min Thu ရဲ့ စနေ cell = Shift (06:00–14:30)၊ ကျန်သူတွေ စနေ = **Off**။ Khin Thida ရဲ့ Oct 19–21 = **Leave**။

---

## 10. Payroll: October 2026

### 10.1 Period ဖန်တီး

| Field | တန်ဖိုး |
|---|---|
| Year / Month | 2026 / 10 (October) |
| Pay date | 2026-10-31 |

Period = 2026-10-01 → 2026-10-31 (ရက် 31)။ Working days = Mon–Fri **22** ရက် (Holiday မရှိ)။

### 10.2 Process → Payslip ၅ စောင်

Input: Kyaw Zin ရဲ့ approved OT = 150 မိနစ်၊ Su Hlaing ရဲ့ approved unpaid leave = 2 ရက်။

| Employee | Basic | Housing | Transport | Meal | Perf. | OT | **Gross** | Tax 5% | Social 2% | Unpaid leave | **Deductions** | **Net** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Khin Thida Oo | 1,800,000.00 | 180,000.00 | 50,000.00 | 30,000.00 | 90,000.00 | – | **2,150,000.00** | 107,500.00 | 43,000.00 | – | 150,500.00 | **1,999,500.00** |
| Kyaw Zin Htun | 900,000.00 | 90,000.00 | 50,000.00 | 30,000.00 | – | 19,176.14 | **1,089,176.14** | 54,458.81 | 21,783.52 | – | 76,242.33 | **1,012,933.81** |
| Su Hlaing Win | 650,000.00 | 65,000.00 | 50,000.00 | 30,000.00 | – | – | **795,000.00** | 39,750.00 | 15,900.00 | 59,090.91 | 114,740.91 | **680,259.09** |
| Min Thu Aung | 600,000.00 | 60,000.00 | 50,000.00 | 30,000.00 | – | – | **740,000.00** | 37,000.00 | 14,800.00 | – | 51,800.00 | **688,200.00** |
| Hnin Yadanar Myint | 309,677.42 | 30,967.74 | 25,806.45 | 15,483.87 | – | – | **381,935.48** | 19,096.77 | 7,638.71 | – | 26,735.48 | **355,200.00** |

တွက်ပုံ:
- **Kyaw Zin OT** = 900,000 ÷ (22 × 8) = 5,113.64 ကို 2.5 နာရီ × 1.5 နဲ့ မြှောက် = 19,176.14
- **Su Hlaing unpaid** = 650,000 ÷ 22 = 29,545.45 × 2 = 59,090.91 (Gross ကနေ မနှုတ်ဘဲ Deduction အဖြစ် ထည့်ပါတယ်၊ ဒါကြောင့် Tax က 795,000 အပေါ် တွက်တယ်)
- **Hnin** ဝင်ရက် 16 ရက် ÷ 31 = 0.516129။ Basic၊ Transport၊ Meal ကို prorate လုပ်၊ Housing က prorate လုပ်ပြီးသား basic ရဲ့ 10%
- Khin Thida က Annual leave (paid) ယူထားလို့ ဘာမှ မနှုတ်ဘူး

### 10.3 Period စုစုပေါင်း (Payroll Processing / History မှာ ပေါ်မယ့်)

| Employees | Basic | Allowances (incl. OT) | Gross | Deductions | Net |
|---|---|---|---|---|---|
| 5 | 4,259,677.42 | 896,434.20 | 5,156,111.62 | 420,018.72 | 4,736,092.90 |

### 10.4 Status အဆင့်ဆင့်

| လုပ်ရပ် | Status |
|---|---|
| Create | Draft |
| **Process** | Pending Approval (payslip ၅ စောင်) |
| (OT မေ့ထားတာ တွေ့ရင်) **Return to draft** → Overtime Approve → Process ပြန် | Draft → Pending Approval |
| **Approve** | Approved |
| **Mark as paid (sample)** | Paid (ပြင်မရတော့) |

Payslip တွေက snapshot၊ နောက်မှ Housing % ပြောင်းလည်း ဒီ payslip မပြောင်းပါ။

---

## စစ်ဖို့ checklist (အားလုံးပြီးရင်)

- [ ] Organization: Company 1၊ Branch 2၊ Department 4၊ Section 4၊ Grade 3၊ Position 5
- [ ] Employees 5 ယောက်၊ EMP-002 က id 2
- [ ] Job opening = **Filled**၊ Hnin = Selected၊ Zaw Htet + Thin Zar = Rejected
- [ ] Leave balance: Khin Thida Annual 15၊ Min Thu Casual 3.5
- [ ] Overtime: Kyaw Zin Approved 2.5 h
- [ ] Payroll Oct 2026 Net စုစုပေါင်း **4,736,092.90**
- [ ] Browser ကို reload လုပ်ပြီး ဒေတာ ကျန်ကြောင်း၊ **Reset data** နဲ့ ရှင်းလို့ရကြောင်း
