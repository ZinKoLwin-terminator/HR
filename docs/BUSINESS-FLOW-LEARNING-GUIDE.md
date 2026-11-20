# APEX HRMS လက်တွေ့လေ့ကျင့်မှု လမ်းညွှန် (Business Flows)

Nov 19, 2026 · @ApexStudy

## စတင်ရန်

Flow ၈ ခုကို အစဉ်လိုက် လုပ်ကြည့်ပါ။ တစ်ခုကို ၆၀–၉၀ မိနစ် ကြာပါတယ်။ Step တစ်ခုပြီးတိုင်း checkbox ကို ခြစ်ပါ။ မျှော်လင့်ထားတဲ့ result မထွက်ရင် အောက်မှာ note ရေးထားပါ။ ဒီမှာ သုံးထားတဲ့ ဝန်ထမ်းတွေ၊ ကုမ္ပဏီတွေက အမှန်မဟုတ်တဲ့ sample data တွေပါ။

**App ဖွင့်နည်း**

| Version | ဘယ် module တွေ ပါလဲ | ဖွင့်နည်း |
| --- | --- | --- |
| Live demo | Organization, Employees, Attendance, Leave, Payroll, Recruitment, Data Exchange, Settings (Flow 1–5, 8) | [zinkolwin-terminator.github.io/HR](https://zinkolwin-terminator.github.io/HR/) |
| Full version (local) | Module အားလုံး၊ Performance နဲ့ Grievances ပါ (Flow 1–8) | Developer က `integrate/all-features` branch မှာ `cd ui` → `npm start` လုပ်ပြီး http://localhost:4200 ကို ဖွင့်ပေးပါမယ် |

**Sign in အကောင့်တွေ** (password အားလုံး `Welcome@123`)

Flow တစ်ခုထဲမှာ လူတစ်ယောက်ထက်ပိုပါတယ်။ ဝန်ထမ်းက လျှောက်၊ မန်နေဂျာက အတည်ပြု၊ HR က ဆုံးဖြတ်ပါတယ်။ လူပြောင်းဖို့ ညာဘက်အပေါ်ထောင့်က နာမည်ကို နှိပ်ပြီး Sign out လုပ်၊ နောက်အကောင့်နဲ့ ပြန်ဝင်ပါ။

| လူ | Role | Email | ဘာလုပ်ဖို့ |
| --- | --- | --- | --- |
| Su Myat Noe (EMP002) | HR Administrator | sumyat.noe@apexhorizon.example | HR အလုပ်အားလုံး၊ settings |
| Thura Aung (EMP001) | Department Manager | thura.aung@apexhorizon.example | အဖွဲ့ဝင်တွေရဲ့ leave, attendance, KPI အတည်ပြု |
| May Thu Kyaw (EMP004) | Payroll Officer | maythu.kyaw@apexhorizon.example | လစာထုတ် |
| Sophia Bennett (EMP011) | Recruiter | sophia.bennett@apexhorizon.example | ဝန်ထမ်းခေါ်ယူ |
| Ye Yint Naing (EMP009) | HR Officer | yeyint.naing@apexhorizon.example | Grievance စုံစမ်းသူ |
| Daniel Carter (EMP005) | Employee | daniel.carter@apexhorizon.example | ဝန်ထမ်း self-service |

**သိထားရမယ့်အချက်တွေ**

- ထည့်လိုက်တဲ့ data တွေက ကိုယ့် browser ထဲမှာပဲ သိမ်းထားပါတယ်။ တခြားလူတွေ မမြင်ရပါဘူး။
- အစကနေ ပြန်စချင်ရင် **Settings > General Settings** ထဲက Demo data မှာ Reset data ကို နှိပ်ပါ။
- Sidebar က role အလိုက် မပြောင်းပါဘူး။ Demo မှာ အကောင့်တိုင်း menu အားလုံးကို မြင်ရပါတယ် (အောက်ဆုံး section မှာ ရှင်းပြထားပါတယ်)။

## Real system (HR database) နဲ့ စမ်းချင်ရင်

Setup အပြည့်အစုံ (prerequisites, database publish, ပြဿနာ ဖြေရှင်းနည်း): [LOCAL-DEV-SETUP.md](LOCAL-DEV-SETUP.md)

Demo မှာ data ကို browser ထဲမှာပဲ သိမ်းပါတယ်။ Real system မှာတော့ UI → API → local HR database အထိ ရောက်ပါတယ်။ Sample data အားလုံးကို seed script ၂ ခုနဲ့ DB ထဲ ထည့်လို့ရပါတယ်။ ဒီအဆင့်တွေကို **သင်ကိုယ်တိုင်** လုပ်ရပါမယ်။ Script တွေက local HR database မှာပဲ run လို့ရပြီး ရှိပြီးသား HR data တွေကို **ဖျက်ပြီး** sample data နဲ့ အစားထိုးပါတယ်။

**Setup (တစ်ကြိမ်ပဲ၊ \~၃၀ မိနစ်)**

1. `HR DBPJ` project ကို local database `HR` (LocalDB) မှာ publish လုပ်ပါ (လုပ်ပြီးသားဆိုရင် ကျော်ပါ)
2. Seed script ကို ဒီနေ့အတွက် ထုတ်ပါ: `cd ui` → `node scripts/generate-db-seed.mjs`
3. ပထမဆုံး admin account ဖန်တီးပါ: `cd api` → `dotnet run --project src/Apex.Hrms.Api -- bootstrap-admin --email sumyat.noe@apexhorizon.example --name "Su Myat Noe"` (password အနည်းဆုံး ၁၂ လုံး)
4. **API ကို ရပ်ပါ။** ပြီးရင် backup ယူပါ (SSMS သို့မဟုတ် sqlcmd): `BACKUP DATABASE [HR] TO DISK = N'C:\Backup\HR-before-seed.bak' WITH COPY_ONLY, INIT`
5. Script ၂ ခုကို အစဉ်လိုက် run ပါ:
   - `sqlcmd -S "(localdb)\MSSQLLocalDB" -d HR -E -b -i docs\test-data\sql\seed-organization-employees.sql`
   - `sqlcmd -S "(localdb)\MSSQLLocalDB" -d HR -E -b -i docs\test-data\sql\seed-modules.sql`
6. API ကို run ပါ: `cd api` → `dotnet run --project src/Apex.Hrms.Api`။ UI ကို run ပါ: `cd ui` → `npm run start:api` → http://localhost:4200
7. Su Myat Noe နဲ့ ဝင်ပါ။ **Settings > Users** မှာ EMP001 Thura Aung၊ EMP004 May Thu Kyaw၊ EMP011 Sophia Bennett၊ EMP009 Ye Yint Naing၊ EMP005 Daniel Carter အတွက် account ဖန်တီးပါ။ Role ကို "စတင်ရန်" table အတိုင်း ရွေးပြီး initial password ကို အနည်းဆုံး ၁၂ လုံး ပေးပါ (ဥပမာ `Learn@HRMS2026`)
8. API ကို ထပ်ရပ်ပြီး `seed-modules.sql` ကို ထပ် run ပါ။ ဒီတစ်ခါမှာ approval, review စတဲ့ မှတ်တမ်းတွေက account အသစ်တွေနဲ့ ချိတ်ဆက်သွားပါတယ်။ ပြီးရင် API ကို ပြန် run ပါ

တစ်ခုခု မှားသွားရင် step 4 က backup ကို restore လုပ်ပါ။ Script တွေက error တက်ရင် ကိုယ်တိုင် rollback လုပ်ပါတယ်။

နောက်တစ်ရက် ပြန်စချင်ရင် step 2, 4, 5 ကို ပြန်လုပ်ပါ (ရက်စွဲတွေက ယနေ့ပေါ် မူတည်လို့)။ Accounts တွေက ဆက်ရှိနေပါတယ်။

**Demo နဲ့ ကွာတာတွေ** (အောက်က flow တွေကို real system မှာ လုပ်ရင်)

| အချက် | Demo | Real system |
| --- | --- | --- |
| Password | `Welcome@123` တစ်ခုတည်း | Account တစ်ခုချင်းစီ၊ ၁၂ လုံးအနည်းဆုံး၊ မှားရင် ၅ ကြိမ်မှာ ၁၅ မိနစ် lock |
| Menu နဲ့ ခလုတ်တွေ | Account တိုင်း အကုန် မြင် | Role မှာ ခွင့်ရှိတာပဲ မြင် |
| EMP012 Locked (Flow 8) | Seed မှာ Locked | Locked အဖြစ် ဖန်တီးလို့ မရ။ Password မှား ၅ ကြိမ် ရိုက်မှ Locked ဖြစ် |
| Payroll approve (Flow 4) | Process လုပ်တဲ့သူ approve လုပ်လို့ရ | တခြားလူပဲ approve လုပ်လို့ရ (May Thu Kyaw နဲ့ approve လုပ်ပါ) |
| Grievance sample ထဲက file တွေ | Sample PDF ဖွင့်လို့ရ | File မရှိ၊ download လုပ်ရင် "file … not available" |
| Anonymous grievance | Simulation | Server က တကယ် ဖုံးထား (sample case တွေက development key နဲ့သာ အလုပ်လုပ်) |
| Notification | Sample တွေ | Sample တွေ။ Read လုပ်ရင် လူတိုင်းအတွက် read ဖြစ် |

Data တွေက database ထဲမှာ သိမ်းလို့ Reset data ခလုတ်က HR database ကို မဖျက်ပါဘူး။ အစကနေ ပြန်စချင်ရင် step 2, 4, 5 ကို ပြန် run ပါ။

## အမြန်လမ်းကြောင်း (\~၇၀ မိနစ်)

အချိန်မရရင် ဒီ step ၁၀ ခုကိုပဲ လုပ်ပါ။ Main business flow တိုင်းရဲ့ happy path ကို တစ်ကြိမ်စီ ဖြတ်သွားပါတယ်။ Data ကို အောက်က table ကနေ copy ယူပါ။ အသေးစိတ် သိချင်ရင် အောက်က Flow 1–8 ကို ကြည့်ပါ။

| # | မိနစ် | ဝင်ရမယ့်သူ | လုပ်ရမယ့်အလုပ် | မြင်ရမယ့် result |
| --- | --- | --- | --- | --- |
| 1 | 5 | Su Myat Noe | **Employees > Add Employee**: Data A နဲ့ ဝန်ထမ်းသစ် ထည့် | Profile history မှာ "Joined as HR Executive" |
| 2 | 5 | Su Myat Noe | **Leave Applications**: Data B နဲ့ လျှောက်၊ Submit၊ **Leave Approvals** မှာ Approve။ **Leave Balances** မှာ Year ကို **2027** ရွေးပြီး Hnin Ei Phyu ကို ဖွင့် | ရက် 5 ရက်၊ Approved။ 2027 Annual မှာ Taken 5 (2026 balance မပြောင်း) |
| 3 | 5 | Su Myat Noe | **Attendance > Check In / Out**: EMP056 Check in (real system မှာ ကိုယ့်အတွက်ပဲ check in လုပ်လို့ရလို့ Su Myat Noe ကိုယ်တိုင်)။ **Overtime**: Data C ထည့်ပြီး Approve | Present/Late (holiday နဲ့ ရုံးပိတ်ရက်ဆိုရင် အမြဲ Present)၊ overtime 120 မိနစ် Approved |
| 4 | 10 | Su Myat Noe → May Thu Kyaw | **Employee Salaries**: Data D။ **Payroll Processing**: ယခုလ Draft ကို Process။ May Thu Kyaw နဲ့ Approve → Mark paid | EMP056 payslip မှာ Overtime pay ပါ၊ Paid |
| 5 | 10 | Sophia Bennett | **Job Openings**: Data E နဲ့ ဖန်တီး၊ Publish။ **Candidates**: Data F။ **Pipeline**: application ထည့် → Screening → interview schedule | Stage Interview |
| 6 | 5 | Sophia Bennett | **Interviews**: Completed + Strong hire။ Pipeline မှာ Selected သို့ ရွှေ့ | Selected၊ job Filled |
| 7 | 5 | Thura Aung → Su Myat Noe | **Performance > Reviews**: EMP002 Start review → Approve (Data G) | "approved and finalized" |
| 8 | 15 | Daniel → Su Myat Noe → Ye Yint Naing → Su Myat Noe | **My Grievances**: Data H နဲ့ submit။ Case Management: Acknowledge + Ye Yint Naing ကို Lead assign။ Investigations: Start → Propose resolution။ Reviews: Approve။ Case Management: Close | Closed၊ history မှာ အဆင့်တိုင်း |
| 9 | 3 | Su Myat Noe | **Settings > Users**: EMP056 အတွက် account (role Employee) | Name နဲ့ email အလိုလို ဖြည့် |
| 10 | 3 | Su Myat Noe | **Dashboard** နဲ့ Recent activity | ဒီနေ့လုပ်တာတွေ ပေါ် |

Step 7 နဲ့ 8 က Full version မှာသာ ရပါတယ်။ Live demo မှာဆိုရင် ကျော်ပါ။

**Test data** (အားလုံး အမှန်မဟုတ်တဲ့ data)

| Data | Field | Value |
| --- | --- | --- |
| A ဝန်ထမ်းသစ် | First / Last name | Nilar / Htay |
|  | Gender, DOB, Marital | Female, 1996-04-12, Single |
|  | National ID | 12/KaMaYa(N)123456 |
|  | Work email | nilar.htay@apexhorizon.example |
|  | Company / Branch / Department | APEX Technologies / Yangon Head Office / Human Resources |
|  | Position | HR Executive (grade G2 အလိုလို) |
| B Leave | Employee / Type | EMP006 Hnin Ei Phyu / Annual Leave |
|  | From – To | 2027-03-01 – 2027-03-05 |
|  | Reason / Approve remark | Family trip / OK |
| C Overtime | Employee / Date | EMP056 Sai Htoo Aung / ယခုလထဲက ယနေ့ သို့မဟုတ် မနေ့ |
|  | Time | 18:00 – 20:00 |
| D Salary | Employee / Structure | EMP056 / STAFF |
|  | Basic / Effective date | 800000 / ယခုလ 1 ရက်နေ့ |
| E Job opening | Title / Department | Test Analyst / Human Resources |
|  | Hiring manager / Vacancies | Su Myat Noe / 1 |
|  | Closing date / Type | 2027-03-31 / Full-time |
| F Candidate | Name / Email | Mya Test / mya.test@candidate.example |
| G KPI review | Overall rating / Feedback | 4 / Good progress this quarter |
| H Grievance | Category / Subject | Working Conditions / Lift out of order |
|  | Description | The lift on the 3rd floor has not worked for two weeks. |
|  | Investigator note / Resolution | Facilities confirmed the fault. / The lift is repaired and checked monthly. |

## Flow 1: ကုမ္ပဏီ ဖွဲ့စည်းပုံနဲ့ ဝန်ထမ်းသစ် ခန့်တာ

**သင်ယူရမယ့်အချက်:** ဝန်ထမ်းတစ်ယောက်က company → branch → department → position (နဲ့ grade) ပေါ်မှာ မူတည်ပါတယ်။ ဒါကြောင့် organization ကို အရင် သတ်မှတ်ရပါတယ်။ လုပ်သူ: **Su Myat Noe (HR Administrator)**

- [ ] **Organization > Companies** ကို ဖွင့်ပါ။ Company ၃ ခု (AHT, AHL, AHV) ကို ကြည့်ပါ။ ပြီးရင် Add နဲ့ code `BNC`, name `Brand New Co` ကို ဖန်တီးပါ → "created successfully" ပေါ်ပြီး row ၄ ခု ဖြစ်ရပါမယ်
- [ ] **Organization > Departments** မှာ company အလိုက် filter လုပ်ပါ။ Department တစ်ခုမှာ Add နှိပ်ပြီး Company `AHT` ရွေးရင် Branch မှာ AHT ရဲ့ branch ၄ ခုပဲ ပေါ်ပါတယ် (Cancel နဲ့ ပိတ်ပါ)
- [ ] **Organization > Job Grades** နဲ့ **Positions** ကို ကြည့်ပါ။ Position တိုင်းမှာ grade (G1–G9) တစ်ခု ပါပါတယ်
- [ ] **Organization > Reporting Structure** မှာ ဘယ်သူက ဘယ်သူ့ကို report လုပ်လဲ chart ကို expand လုပ်ပြီး ကြည့်ပါ
- [ ] **Employees > Add Employee** မှာ Code က အလိုအလျောက် ဖြည့်ထားပါတယ်။ Nilar Htay, Female, DOB 1996-04-12, work email `nilar.htay@apexhorizon.example`, APEX Technologies / Yangon Head Office / Human Resources / HR Executive ကို ဖြည့်ပြီး save လုပ်ပါ → Grade G2 က အလိုလို ဖြည့်ပေးပြီး profile ရဲ့ history မှာ "Joined as HR Executive" ပေါ်ရပါမယ်
- [ ] Nilar ရဲ့ profile မှာ tab တွေကို ဖွင့်ကြည့်ပါ။ **Employees > Employee Documents** မှာ PDF တစ်ခုကို type Contract အဖြစ် upload လုပ်ပါ
- [ ] **Leave > Leave Applications** မှာ Nilar အတွက် Annual Leave ၁ ရက်ကို **ဒီနှစ်ထဲက** ရုံးဖွင့်ရက် (ဥပမာ နောက်အပတ် တနင်္လာနေ့) နဲ့ လျှောက်ပြီး Submit လုပ်ကြည့်ပါ → "Insufficient … balance" ပေါ်ရပါမယ်။ ဝန်ထမ်းသစ်ဟာ အလုပ်ဝင်တာ ၃ လ မပြည့်သေးလို့ annual leave မရသေးပါဘူး။ 2027 ရက်နဲ့ဆိုရင်တော့ လက်ခံပါတယ်။ နောက်နှစ်ရဲ့ ခွင့်ရက်ကို ၃၁ ဒီဇင်ဘာ အထိ တွက်လို့ အဲဒီအချိန်မှာ ၃ လ ပြည့်ပြီးသားမို့ပါ
- [ ] **ရာထူးတိုး:** EMP046 Hein Thant Zin ကို Edit လုပ်ပြီး position ကို Senior Backend Engineer (G4) သို့ ပြောင်းပါ → History မှာ "Promotion" ပေါ်ရပါမယ်
- [ ] **Letter:** EMP001 Thura Aung ရဲ့ profile → Letters and certificates → Employment certificate ကို ရွေးပါ → ဝန်ထမ်း data နဲ့ ဖြည့်ထားတဲ့ စာကို preview မှာ မြင်ရပါမယ်
- [ ] **အလုပ်ထွက်ပြီးသား ဝန်ထမ်း (Full version):** EMP044 Tun Tun Naing ရဲ့ profile မှာ "Add to do-not-rehire list" ကို reason နဲ့ လုပ်ပါ → **Employees > Do-not-rehire list** မှာ ပေါ်ရပါမယ်

မေးခွန်း: ဝန်ထမ်း တစ်ယောက်ကို ဘာကြောင့် ဖျက်လို့ မရတာလဲ? (ဥပမာ EMP001 မှာ direct reports ၆ ယောက် ရှိလို့ ဖျက်လို့ မရပါဘူး)

## Flow 2: ခွင့်လျှောက်တာနဲ့ အတည်ပြုတာ

**သင်ယူရမယ့်အချက်:** Leave တစ်ခုက Draft → Submitted → Approved သို့မဟုတ် Rejected အဆင့်တွေကို ဖြတ်ပါတယ်။ ရနိုင်တဲ့ ရက်ကို policy (grade) က ဆုံးဖြတ်ပြီး holiday နဲ့ weekend တွေကို မရေတွက်ပါဘူး။ လုပ်သူ: **Su Myat Noe (HR Administrator)**။ ရက်စွဲတွေအတွက် 2027 ကို သုံးပါ။

- [ ] **Leave Management > Leave Types** မှာ type ၇ ခု (Annual, Casual, Medical, Maternity, Paternity, Unpaid, Compassionate) ကို ကြည့်ပါ။ **Leave Policies** မှာ grade အလိုက် ရက်အရေအတွက် ကွာပါတယ်
- [ ] **Leave Balances** မှာ Annual leave ကို ကြည့်ပါ → EMP056 = 15 (standard)၊ EMP005 Daniel Carter = 18 (G6)၊ EMP058 = 6 (intern)
- [ ] **Attendance > Holiday Calendar** မှာ `2027-03-03`, name `Test Holiday`, type Company ကို ထည့်ပါ
- [ ] **Leave Applications** မှာ New နှိပ်ပြီး EMP006 Hnin Ei Phyu ကို ရွေးပါ → Leave type မှာ Maternity ပါပြီး Paternity မပါပါဘူး (အမျိုးသမီးမို့)
- [ ] Annual Leave `2027-03-01` ကနေ `2027-03-05` ကို ရွေးပါ → ရက် **၄** ရက် ဖြစ်ရပါမယ် (holiday ကို ခုန်လို့)။ Save လုပ်ပြီး Submit လုပ်ပါ
- [ ] Medical Leave တစ်ခုကို document မပါဘဲ submit လုပ်ကြည့်ပါ → "needs a supporting document" ပေါ်ရပါမယ်
- [ ] **Leave Approvals** မှာ EMP006 ရဲ့ Annual leave ကို remark နဲ့ Approve လုပ်ပါ → Status Approved
- [ ] **Leave Balances** မှာ Hnin Ei Phyu ကို ရှာပြီး ဖွင့်ပါ → 2026 Annual က မပြောင်းပါဘူး။ Filter ထဲက Year ကို **2027** ပြောင်းပြီး ပြန်ဖွင့်ပါ → Annual Taken 4 (holiday မထည့်ရင် 5)၊ Available က အဲဒီလောက် လျော့ပါတယ်
- [ ] EMP006 ရဲ့ profile → Leave tab မှာ Leave history ထဲမှာ ဒီ leave ပေါ်ပါတယ်။ **Balance year** ကို 2027 ရွေးရင် balance လျော့တာ မြင်ရပါမယ်
- [ ] **ဘာကြောင့်လဲ:** Leave ကို **စတဲ့နေ့ရဲ့ နှစ်** ထဲမှာ ရေတွက်ပါတယ်။ Application number (ဥပမာ `LV-2026-0030`) ထဲက နှစ်က လျှောက်တဲ့နေ့ရဲ့ နှစ်ပါ။ ဒါကြောင့် 2026 မှာ လျှောက်ပြီး 2027 မှာ စတဲ့ leave က 2027 balance ကနေပဲ လျော့ပါတယ်
- [ ] နောက် leave တစ်ခု တင်ပြီး Reject လုပ်ပါ → Reason မပါရင် reject လုပ်လို့ မရပါဘူး။ Reject ဖြစ်ရင် ရက်တွေ ပြန်ရပါတယ်
- [ ] ကိုယ့်အတွက် (EMP002 Su Myat Noe) leave တင်ပြီး Leave Approvals မှာ ကြည့်ပါ → ကိုယ့် row မှာ Approve / Reject မပါပါဘူး (ကိုယ့်ခွင့် ကိုယ်မအတည်ပြုရ)
- [ ] **Leave Calendar** မှာ 2027 မတ်လကို သွားပါ → Approved leave တွေ ပေါ်ပါတယ်။ Pending ကို filter ဖွင့်မှ ပေါ်ပါတယ်

မေးခွန်း: Holiday ကို ဖျက်ပြီး ရက်တူ leave ပြန်လျှောက်ရင် ဘယ်နှရက် ဖြစ်မလဲ? (အဖြေ: ၅ ရက်)

## Flow 3: ရုံးတက်ချိန်၊ ပြင်ဆင်ချက်၊ overtime

**သင်ယူရမယ့်အချက်:** Shift က ရုံးချိန်ကို သတ်မှတ်ပါတယ်။ Check-in အချိန်က Present / Late ကို ဆုံးဖြတ်ပါတယ်။ မှားရင် adjustment နဲ့ ပြင်ပြီး၊ အချိန်ပိုကို overtime နဲ့ တင်ပါတယ်။ အတည်ပြုထားတဲ့ overtime က payroll ထဲ ဝင်ပါတယ် (Flow 4)။ လုပ်သူ: **Su Myat Noe**

- [ ] **Attendance > Shift Management** မှာ shift တွေကို ကြည့်ပါ (GEN 09:00–18:00 စသည်)။ ပြီးရင် Add နဲ့ code `TST-DAY`, 08:00–16:00, break 60, grace 10, Monday–Friday ကို ဖန်တီးပါ
- [ ] **Shift Assignment** မှာ `TST-DAY` ကို ဝန်ထမ်း ၂ ယောက်ကို တစ်ခါတည်း assign လုပ်ပါ → **Work Schedules** မှာ သူတို့ရဲ့ shift ပေါ်ပါတယ်။ Holiday နဲ့ approved leave ရက်တွေလည်း ပေါ်ပါတယ်
- [ ] Employee EMP056 ကို Edit လုပ်ပြီး shift NIGHT (Inactive) ကို ရွေးကြည့်ပါ → "This shift is inactive." (Cancel နဲ့ ပိတ်ပါ)
- [ ] **Check In / Out** မှာ EMP056 Sai Htoo Aung ကို Check in လုပ်ပါ → "Checked in at HH:MM" နဲ့ Present (သို့) နောက်ကျရင် Late။ နောက်မှ Check out လုပ်ပါ
- [ ] **Holiday မှာ ရုံးတက်ရင်:** Check In / Out မှာ "Today is a holiday: …" ဒါမှမဟုတ် "Today is not a working day of the … Shift." ဆိုတဲ့ စာ ပေါ်ရင် ဒီနေ့က holiday ဒါမှမဟုတ် shift ရဲ့ ရုံးပိတ်ရက် ဖြစ်ပါတယ်။ ဒီနေ့မှာ ဘယ်အချိန် check in လုပ်လုပ် **Present** ပဲ ဖြစ်ပြီး late မိနစ် 0 ပါ။ ရုံးတက်ရမယ့်နေ့ မဟုတ်လို့ နောက်ကျတယ်လို့ မတွက်ပါဘူး။ Toast မှာ "worked on a holiday, recorded as Present" ပေါ်ပြီး Attendance Records ရဲ့ remark မှာ "Worked on a holiday" ပါပါတယ်။ ဥပမာ sample data မှာ `2026-11-20` (Company Foundation Day) ပါ။ Late ကို စမ်းချင်ရင် ရုံးဖွင့်ရက် (holiday မဟုတ်တဲ့ တနင်္လာ–သောကြာ) မှာ 09:15 နောက်ပိုင်း check in လုပ်ပါ
- [ ] **Attendance Records** မှာ status Late နဲ့ filter လုပ်ပါ။ **Late & Early** မှာ နောက်ကျတာ၊ စောပြန်တာတွေကို ကြည့်ပါ
- [ ] **Adjustments** မှာ EMP006 အတွက် record ရှိတဲ့ ရက်ဟောင်းတစ်ရက်၊ check-in 09:05 ကို တင်ပါ → Pending။ ပြီးရင် Approve လုပ်ပါ → Record မှာ check-in 09:05 ဖြစ်ပြီး remark "Adjusted (ADJ-…)" ပေါ်ရပါမယ်
- [ ] **Overtime** မှာ EMP056 အတွက် ယခုလထဲက ရက်တစ်ရက် 18:00–20:00 ကို ထည့်ပါ → 120 မိနစ်၊ Pending။ Approve လုပ်ပါ။ နောက်တစ်ခု ထည့်ပြီး Pending အတိုင်း ထားပါ (Flow 4 မှာ သုံးမယ်)
- [ ] ကိုယ့်အတွက် (EMP002) adjustment တင်ပါ → ကိုယ့် row မှာ Approve မပါပါဘူး
- [ ] **Attendance > Dashboard** မှာ ယနေ့ရဲ့ Present, Late, Absent, On leave အရေအတွက်နဲ့ attendance rate ကို ကြည့်ပါ

မေးခွန်း: 09:00 shift မှာ 18:30 ထိ လုပ်ရင် overtime ရလား? (အဖြေ: မရပါဘူး။ မိနစ် ၃၀ က threshold မကျော်လို့ overtime 0 ပါ)

မေးခွန်း: Holiday နေ့မှာ 11:59 ကျမှ check in လုပ်ရင် status ဘာဖြစ်မလဲ? (အဖြေ: Present ပါ၊ late 0။ ရုံးဖွင့်ရက်ဆိုရင်တော့ Late၊ 179 မိနစ်)

## Flow 4: လစဉ်လစာထုတ်တာနဲ့ payslip

**သင်ယူရမယ့်အချက်:** လစာက salary structure (components) နဲ့ ဝန်ထမ်းရဲ့ salary assignment ပေါ် မူတည်ပါတယ်။ Payroll run က Draft → Pending Approval → Approved → Paid အဆင့်တွေကို ဖြတ်ပါတယ်။ Unpaid leave က လစာကို နှုတ်ပြီး approved overtime က ပေါင်းပေးပါတယ်။ လုပ်သူ: **Su Myat Noe** (process) နဲ့ **May Thu Kyaw, Payroll Officer** (approve)

- [ ] **Payroll > Salary Components** နဲ့ **Salary Structures** ကို ကြည့်ပါ (HOUSING, TRANSPORT, TAX, SOCIAL စသည်၊ structure STAFF, MGMT, EXEC)။ Component တစ်ခု Add ကြည့်ပါ: `TST-ALW`, Earning, Fixed 25000
- [ ] **Employee Salaries** မှာ EMP056 Sai Htoo Aung အတွက် structure STAFF, basic 800,000, effective date = ယခုလ ၁ ရက်နေ့ ကို ထည့်ပါ။ G2 ရဲ့ band (600,000–1,000,000) ပြင်ပကောင်းရင် လက်မခံပါဘူး
- [ ] Flow 2 အတိုင်း EMP056 အတွက် ယခုလထဲက အလုပ်ရက် ၂ ရက် Unpaid Leave တင်ပြီး approve လုပ်ပါ။ Flow 3 က overtime (approved တစ်ခု၊ pending တစ်ခု) လည်း ယခုလထဲမှာ ရှိရပါမယ်
- [ ] **Payroll Processing** မှာ ယခုလရဲ့ Draft period ကို Process လုပ်ပါ → Pending Approval ဖြစ်ပြီး payslip တွေ ထွက်လာပါမယ်။ Salary assignment မရှိတဲ့သူတွေကို warning အဖြစ် ပြပါတယ်
- [ ] EMP056 ရဲ့ payslip ကို ဖွင့်ပါ → Basic, earnings, "Unpaid leave" deduction (2 ရက်), "Overtime pay" (approved ဖြစ်တဲ့ 2 နာရီပဲ), gross, deductions, net ပါရပါမယ်။ Bank account က နောက်ဆုံး ၄ လုံးပဲ ပေါ်ပါတယ်
- [ ] Sign out လုပ်ပြီး **May Thu Kyaw** နဲ့ ဝင်ပါ။ Period ကို Approve လုပ်ပြီး Mark paid လုပ်ပါ → Approved, ပြီးရင် Paid
- [ ] **Payroll History** မှာ processed period တွေ (Draft မပါ) ကို chart နဲ့ table မှာ ကြည့်ပါ
- [ ] Su Myat Noe နဲ့ ပြန်ဝင်ပြီး sidebar က **My Payslips** ကို ဖွင့်ပါ → Approved နဲ့ Paid လတွေရဲ့ ကိုယ့် payslip တွေပဲ ပေါ်ပါတယ်။ Payslip တစ်ခုကို ဖွင့်ပြီး Print payslip ကို နှိပ်ပါ

မေးခွန်း: Process လုပ်ပြီးမှ မှားနေတာတွေ့ရင် ဘာလုပ်လို့ရလဲ? (အဖြေ: Approve မလုပ်ခင် reason နဲ့ Return to draft လုပ်ပြီး ပြန် process လုပ်လို့ရပါတယ်)

သတိပြုရန်: လစာပမာဏတွေက demo အတွက် တွက်ထားတာပါ။ Tax နဲ့ SSB ကို မြန်မာနိုင်ငံ ဥပဒေအတိုင်း မတွက်ပါဘူး။

## Flow 5: ဝန်ထမ်းခေါ်ယူတာ (recruitment)

**သင်ယူရမယ့်အချက်:** Job opening → candidate → application → interview → selection ဆိုတဲ့ အဆင့်တွေကို သင်ယူပါမယ်။ Application က pipeline မှာ Applied → Screening → Interview → Selected အဆင့်တွေကို ဖြတ်ပါတယ်။ Interview မှာ "Hire" အကြံပြုချက် မရရင် candidate ကို Selected မလုပ်နိုင်ပါဘူး။ လုပ်သူ: **Sophia Bennett (Recruiter)**

- [ ] **Recruitment > Job Openings** မှာ Add နှိပ်ပြီး အောက်ပါအတိုင်း ဖြည့်ပါ → Status Draft နဲ့ code `JOB-yyyy-nnn` ရပါမယ်။ ပြီးရင် Publish လုပ်ပြီး Open ဖြစ်အောင် လုပ်ပါ
  - Title: `Test Analyst`
  - Department: Human Resources
  - Hiring manager: Su Myat Noe
  - Vacancies: 1
  - Closing date: `2027-03-31`
- [ ] **Candidates** မှာ candidate `Mya Test` ကို email `Mya.Test@Candidate.Example` နဲ့ ထည့်ပါ → Email ကို စာလုံးသေးနဲ့ သိမ်းပါတယ်
- [ ] **Pipeline** မှာ `Test Analyst` ကို ရွေးပြီး Mya Test ရဲ့ application ကို ထည့်ပါ → Stage Applied။ ပြီးရင် Screening သို့ ရွှေ့ပါ
- [ ] Interview တစ်ခုကို နောက်ရက်တစ်ရက်မှာ schedule လုပ်ပါ → Stage က Interview သို့ အလိုလို ရွှေ့ပါတယ်
- [ ] Interview မပြီးခင် Selected သို့ ရွှေ့ကြည့်ပါ → "Selecting a candidate needs a completed interview with a "Hire" recommendation."
- [ ] **Interviews** မှာ အဲဒီ interview ကို Completed, recommendation **Strong hire** လို့ မှတ်ပါ။ ပြီးရင် Selected သို့ ရွှေ့ပါ → Selected ဖြစ်ပြီး vacancy ၁ ခုပဲရှိလို့ job က **Filled** ဖြစ်သွားပါမယ်
- [ ] Application ကို reason မပါဘဲ Reject လုပ်ကြည့်ပါ → လက်မခံပါဘူး။ Reason နဲ့ Reject လုပ်ရင် job က Open ပြန်ဖြစ်ပြီး scheduled interview တွေ Cancelled ဖြစ်သွားပါတယ်
- [ ] **Employees > Employee List** မှာ ရှာကြည့်ပါ → Selected ဖြစ်တဲ့ candidate က employee အဖြစ် အလိုအလျောက် မဖြစ်ပါဘူး။ Add Employee နဲ့ ကိုယ်တိုင် ထည့်ရပါတယ် (Flow 1)
- [ ] **(Full version)** Flow 1 မှာ do-not-rehire list ထည့်ထားတဲ့ EMP044 ရဲ့ email နဲ့ candidate ဖန်တီးပြီး application ထည့်ပါ → "Matches do-not-rehire list — contact HR" badge ပေါ်ရပါမယ်

မေးခွန်း: Interviewer တစ်ယောက်ကို အချိန်ထပ်တဲ့ interview ၂ ခု ပေးလို့ရလား? (အဖြေ: မရပါဘူး။ "The interviewer already has an interview …" ပေါ်ပါတယ်)

## Flow 6: KPI နဲ့ စွမ်းဆောင်ရည် အကဲဖြတ်တာ (Full version)

**သင်ယူရမယ့်အချက်:** HR က KPI template နဲ့ evaluation period ကို သတ်မှတ်ပြီး ဝန်ထမ်းတွေကို KPI assign လုပ်ပါတယ် (weight စုစုပေါင်း 100%)။ ဝန်ထမ်းက ရလဒ်တွေကို ဖြည့်ပြီး submit လုပ်၊ မန်နေဂျာက review လုပ်ပြီး approve သို့မဟုတ် ပြန်ပို့ပါတယ်။ Scorecard က Planning → Draft → Submitted → Under Review → Finalized အဆင့်တွေကို ဖြတ်ပါတယ်။

**HR setup** (Su Myat Noe)

- [ ] **Performance > Categories**, **Templates**, **Rating Bands** ကို ကြည့်ပါ။ Template တစ်ခုစီမှာ direction (Higher Is Better / Lower Is Better / Rating Based), target နဲ့ weight ပါပါတယ်
- [ ] **Evaluation Periods** မှာ AHT, `2027 Q1`, Quarterly, `2027-01-01` ကနေ `2027-03-31` ကို ဖန်တီးပါ → Draft
- [ ] **Assignments > Assign KPIs** မှာ EMP046 အတွက် `2027 Q1` မှာ OUTPUT-TASKS (weight 40) နဲ့ DEFECT-RATE (35) ကို ထည့်ပါ → "Scorecard created … with 2 KPIs"။ Weight စုစုပေါင်း 100% ကျော်ရင် လက်မခံပါဘူး
- [ ] Plan မှာ Add KPIs နဲ့ ATTENDANCE-RATE (25) ထည့်ပြီး Finalize plan လုပ်ပါ → Reviewer က သူ့မန်နေဂျာ Daniel Carter ဖြစ်ပြီး plan က read-only ဖြစ်သွားပါတယ်

**Review cycle** (seed ထဲက `2026 Q4` မှာ Su Myat Noe ရဲ့ scorecard က Submitted ဖြစ်နေပြီး reviewer က Thura Aung ပါ)

- [ ] **Thura Aung** နဲ့ ဝင်ပါ။ **Performance > Reviews** မှာ EMP002 ကို ဖွင့်ပြီး Start review လုပ်ပါ → Under Review
- [ ] Reason နဲ့ Return လုပ်ပါ → Returned
- [ ] **Su Myat Noe** နဲ့ ဝင်ပါ။ **My KPIs** မှာ return reason ကို ဖတ်ပါ။ ATTENDANCE-RATE ကို 99 လို့ ပြင်၊ evidence PDF တစ်ခု upload လုပ်ပြီး submit လုပ်ပါ → Score အသစ်နဲ့ Resubmitted
- [ ] **Thura Aung** နဲ့ ပြန်ဝင်ပါ။ Rating, feedback ထည့်ပြီး Approve လုပ်ပါ → "approved and finalized"။ History မှာ Start Review, Return, Resubmit, Approve တွေ ပေါ်ပါတယ်

**Dashboards**

- [ ] Su Myat Noe နဲ့ **Performance > Dashboard** ကို ဖွင့်ပါ → My performance, My team, HR overview tab တွေ ပါပါတယ်။ `2026 Q3` (Closed) နဲ့ `2026 Q4` ကို နှိုင်းယှဉ်ပါ
- [ ] Thura Aung နဲ့ ဝင်ရင် My performance နဲ့ My team ပဲ ပေါ်ပါတယ်

မေးခွန်း: HR ဝန်ထမ်းက ကိုယ့် scorecard ကို ကိုယ် plan လုပ်လို့ရလား? (အဖြေ: မရပါဘူး။ အခြား HR တစ်ယောက်က plan လုပ်ရပါတယ်)

## Flow 7: မကျေနပ်မှု တင်ပြတာ (grievance, Full version)

**သင်ယူရမယ့်အချက်:** ဝန်ထမ်းက grievance တင်ပြီး HR က acknowledge လုပ်ပါတယ်။ Investigator က စုံစမ်းပြီး ဖြေရှင်းချက် propose လုပ်၊ reviewer က approve လုပ်ပြီး HR က ပိတ်ပါတယ်။ Propose လုပ်တဲ့သူနဲ့ approve လုပ်တဲ့သူ မတူရပါဘူး။ ဝန်ထမ်းက investigation note တွေကို ဘယ်တော့မှ မမြင်ရပါဘူး။

- [ ] **Daniel Carter** နဲ့ ဝင်ပါ။ **Grievances > My Grievances** → New grievance။ Category Working Conditions, subject `Lift out of order`, description စာလုံး ၂၀ ကျော်၊ declaration ကို ခြစ်ပြီး submit လုပ်ပါ → "HR will receive it with your name." ပြီး case number `GRV-yyyy-nnnnn` ရပါမယ်
- [ ] Category Salary and Compensation Issues ကို ရွေးကြည့်ပါ → "Report anonymously" ကို ပိတ်ထားပါတယ် (named only)။ Workplace Harassment ဆိုရင် ဖွင့်လို့ရပြီး "What can still identify you" သတိပေးချက်ကို ဖတ်ပါ
- [ ] Anonymous grievance တစ်ခု တင်ပါ (Discrimination, switch on) → "HR will receive it without your name."
- [ ] **Su Myat Noe** နဲ့ ဝင်ပါ။ **Case Management** မှာ anonymous case ကို ဖွင့်ပါ → "Anonymous", "Department not disclosed" ပဲ ပေါ်ပြီး Daniel ရဲ့နာမည် ဘယ်မှာမှ မပေါ်ပါဘူး
- [ ] `Lift out of order` case ကို Acknowledge လုပ်ပါ (priority High, target date ၁၄ ရက်အတွင်း) → Acknowledged။ ပြီးရင် Ye Yint Naing ကို Lead investigator အဖြစ် assign လုပ်ပါ → Assigned
- [ ] **Ye Yint Naing** နဲ့ ဝင်ပါ။ **Investigations** မှာ Start investigation လုပ်ပါ။ Note တစ်ခု ထည့်ပြီး ဝန်ထမ်းဆီ မေးခွန်းတစ်ခု ပို့ပါ → Awaiting Employee Response
- [ ] **Daniel Carter** နဲ့ ဝင်ပြီး မေးခွန်းကို ဖြေပါ → Note ကို မမြင်ရဘဲ "Your answer was sent to HR." ပဲ ပေါ်ပါတယ်
- [ ] **Ye Yint Naing** နဲ့ resolution summary ကို propose လုပ်ပါ → Resolution Proposed။ သူကိုယ်တိုင် approve လုပ်လို့ မရပါဘူး
- [ ] **Su Myat Noe** နဲ့ **Reviews** မှာ resolution ကို Approve လုပ်ပါ (share with employee on) → Resolved။ **Case Management** မှာ Close လုပ်ပါ → Closed။ History tab မှာ အဆင့်တိုင်း ပေါ်ပါတယ်
- [ ] **Daniel Carter** နဲ့ resolution ကို ဖတ်ပါ။ ပြီးရင် Su Myat Noe နဲ့ reason ပေးပြီး Reopen လုပ်ကြည့်ပါ → Reopened
- [ ] **Grievances > Dashboard** နဲ့ **Reports** မှာ category, department, status အလိုက် ကြည့်ပြီး Excel export လုပ်ပါ

မေးခွန်း: Anonymous တင်ရင် ဘာတွေက သင့်ကို ဖော်ပြနိုင်သေးလဲ? (အဖြေ: ရေးထားတဲ့ စာ၊ file name နဲ့ PDF ထဲက အချက်အလက်)

## Flow 8: Users နဲ့ permissions၊ Excel၊ dashboard နဲ့ reports

**သင်ယူရမယ့်အချက်:** ဝန်ထမ်းတစ်ယောက်က system ကို သုံးဖို့ user account နဲ့ role လိုပါတယ်။ Role က ဘယ် module မှာ ဘာလုပ်ခွင့် (View, Create, Edit, Approve) ရှိလဲ သတ်မှတ်ပါတယ်။ Data အများကြီးကို Excel နဲ့ ထည့်ထုတ်လုပ်ပြီး မန်နေဂျမင့်အတွက် dashboard နဲ့ report တွေ ရှိပါတယ်။ လုပ်သူ: **Su Myat Noe**

**Users နဲ့ permissions**

- [ ] **Settings > Roles** မှာ role ၇ ခုကို ကြည့်ပါ။ `Test Coordinator` role ကို ဖန်တီးပါ။ နာမည်တူ ထပ်ဖန်တီးရင် လက်မခံပါဘူး
- [ ] **Settings > Permissions** မှာ role တစ်ခုစီရဲ့ module အလိုက် ခွင့်တွေကို ကြည့်ပါ။ Recruiter ကို တစ်ခုခု ပြင်ပြီး Save လုပ်ပါ။ Reset နဲ့ နောက်ဆုံး save လုပ်ထားတာကို ပြန်ရပါတယ်
- [ ] **Settings > Users** မှာ EMP056 Sai Htoo Aung အတွက် account ဖန်တီးပါ (role Employee) → နာမည်နဲ့ email က employee record ကနေ အလိုလို လာပါတယ်။ Locked ဖြစ်နေတဲ့ EMP012 ကို Unlock account လုပ်ပါ
- [ ] **Settings > General Settings** မှာ Organization name `APEX Group Test` နဲ့ Date format `dd MMM yyyy` ကို save လုပ်ပါ → Sidebar, list တွေနဲ့ letter မှာ ချက်ချင်း ပြောင်းသွားပါတယ်

**Excel**

- [ ] **Employees > Employee List** မှာ `Daniel` နဲ့ ရှာပြီး Export လုပ်ပါ → Excel ထဲမှာ ရှာတွေ့တဲ့ row တွေပဲ ပါပါတယ်။ Organization > Departments မှာလည်း Export လုပ်ကြည့်ပါ
- [ ] **Data Exchange > Import Data** → Employees → Download template လုပ်ပါ။ Instructions နဲ့ Field guide sheet တွေကို ဖတ်ပြီး အမှန်မဟုတ်တဲ့ ဝန်ထမ်း ၂ ယောက် ဖြည့်ပြီး import လုပ်ပါ → Preview မှာ Valid ဖြစ်ရင် "N inserted" ပေါ်ပါတယ်
- [ ] Required field တစ်ခုကို ဗလာထားပြီး ထပ် import လုပ်ကြည့်ပါ → Error report ကို download လုပ်ပြီး Row, Field, Error, Suggested correction တွေကို ဖတ်ပါ။ **Import History** မှာ batch တွေကို ကြည့်ပါ

**Dashboard နဲ့ reports**

- [ ] **Dashboard** မှာ total employees, new joiners, pending leave approvals, attendance today, recruitment နဲ့ payroll summary card တွေကို ကြည့်ပါ။ Card တစ်ခုကို နှိပ်ရင် သက်ဆိုင်ရာ screen ကို ရောက်ပါတယ်
- [ ] Recent activity card မှာ ဒီနေ့ လုပ်ခဲ့တဲ့ အလုပ်တွေ (shift ဖန်တီးတာ၊ leave approve လုပ်တာ) ပေါ်လား ကြည့်ပါ
- [ ] List တစ်ခုခုမှာ Print > "Print current view" လုပ်ပါ → Actions column မပါတဲ့ print layout ပေါ်ပါတယ်

မေးခွန်း: ဝန်ထမ်း ၁၀၀ ယောက်ကို တစ်ခါတည်း ထည့်ချင်ရင် ဘယ်လိုလုပ်မလဲ? (အဖြေ: Template နဲ့ Excel import လုပ်ပါ)

## Demo ရဲ့ ကန့်သတ်ချက်တွေ

Demo က business flow တွေကို သင်ယူဖို့ လုပ်ထားတာပါ။ အောက်ပါအချက်တွေက demo မှာသာ ဖြစ်ပြီး server နဲ့ database ပါတဲ့ တကယ့် system မှာ မဖြစ်ပါဘူး။

| Demo မှာ | တကယ့် system မှာ |
| --- | --- |
| Role တိုင်း menu အားလုံးကို မြင်ပြီး လုပ်လို့ရပါတယ်။ Permission ကို တကယ် မစစ်ပါဘူး | Role မှာ ခွင့်ရှိတဲ့ menu နဲ့ ခလုတ်တွေပဲ ပေါ်ပါတယ်။ Server က request တိုင်း စစ်ပါတယ် |
| Password တစ်ခုတည်း (`Welcome@123`)၊ မှားရင် lock မဖြစ်ပါဘူး | User တစ်ယောက်ချင်း password၊ မှားရင် lock ဖြစ်၊ email နဲ့ password reset |
| Anonymous grievance က ပတ်ဝန်းကျင်မှုပဲ ပြပါတယ် | Server က တင်တဲ့သူရဲ့ နာမည်ကို ပိတ်ထားပါတယ် |
| Payroll မှာ process လုပ်တဲ့သူကိုယ်တိုင် approve လုပ်လို့ရပါတယ် | တခြားလူတစ်ယောက်ကပဲ approve လုပ်ရပါတယ် |
| Data က ကိုယ့် browser ထဲမှာပဲ ရှိပါတယ် | Database မှာ သိမ်းပြီး လူတိုင်း တူတူ မြင်ရပါတယ် |

တကယ့် system မှာလည်း အောက်ပါအချက်တွေက ဒီအတိုင်းပဲ ဖြစ်ပါတယ်။

- လစာပမာဏတွေက demonstration အတွက်ပဲ ဖြစ်ပါတယ်။ Tax, SSB နဲ့ bank file ကို implementation မှာ သတ်မှတ်ရပါမယ်
- Selected candidate က employee အဖြစ် အလိုအလျောက် မဖြစ်ပါဘူး။ Add Employee နဲ့ ထည့်ရပါတယ်
- Notification တွေက sample တွေပါ။ အလုပ်လုပ်ရင် notification အသစ် မထွက်သေးပါဘူး

မရှင်းတာတွေ့ရင်၊ မျှော်လင့်တဲ့ result မထွက်ရင် ဒီ doc ထဲမှာပဲ အဲဒီ step နားမှာ comment ရေးထားပါ။
