# Local development setup: UI → API → DB

APEX HRMS ကို ကိုယ့်စက်ပေါ်မှာ အပြည့်အစုံ (Angular UI → .NET API → local SQL Server `HR` database) run ပြီး
business flow တွေ စမ်းဖို့ အဆင့်ဆင့် လမ်းညွှန်ပါ။ ပထမဆုံးအကြိမ် ~၄၅–၆၀ မိနစ် ကြာပါတယ်။ နောက်ပိုင်း ပြန်စရင်
အဆင့် 5, 7, 8, 9, 11 ကိုပဲ လုပ်ရပါတယ်။

- **Local database (`(localdb)\MSSQLLocalDB`, database `HR`) ကိုပဲ သုံးပါ။** Production database ကို ဘယ်တော့မှ မချိတ်ပါနဲ့။
- Database ကို ပြောင်းတဲ့ command တွေ (publish, seed, backup) ကို **သင်ကိုယ်တိုင်** run ရပါတယ်။ Claude Code ကို ခွင့်မပြုထားပါဘူး။
- Data အားလုံး အမှန်မဟုတ်တဲ့ sample data ပါ။ Real ဝန်ထမ်း data မထည့်ပါနဲ့။
- Commands တွေကို repo root (`D:\HR`) ကနေ PowerShell မှာ run ပါ။ `npm` ကို execution policy က ပိတ်ရင် `npm.cmd` သုံးပါ။

## 0. လိုအပ်တာတွေ (2026-11-20 မှာ ဒီစက်ကို စစ်ထားပြီး)

| Tool | ဒီစက်မှာ | စစ်နည်း |
| --- | --- | --- |
| .NET SDK 10 | 10.0.403 ✓ | `dotnet --version` |
| Node.js | v26.13.0 ✓ | `node --version` |
| SQL Server LocalDB | `MSSQLLocalDB` ✓ | `sqllocaldb info` |
| sqlcmd | ODBC 170 ✓ | `sqlcmd -?` |
| SqlPackage | **မရှိသေး** | `dotnet tool install -g microsoft.sqlpackage` နဲ့ ထည့်ပါ |
| Branch | `integrate/all-features` ✓ | `git branch --show-current` |

UI packages မထည့်ရသေးရင်: `cd ui; npm ci; cd ..`

## 1. Build (database မထိ)

```powershell
dotnet build "HR DBPJ\HR DBPJ.sqlproj"        # → HR DBPJ\bin\Debug\HR DBPJ.dacpac
dotnet build api\Apex.Hrms.slnx               # warning 0 ဖြစ်ရပါမယ်
```

## 2. Database publish (တစ်ကြိမ်ပဲ၊ schema ပြောင်းမှ ပြန်လုပ်)

အရင် script ထုတ်ပြီး ဖတ်ကြည့်ပါ။ ပြီးမှ publish လုပ်ပါ။

```powershell
$cs = "Server=(localdb)\MSSQLLocalDB;Database=HR;Integrated Security=True;TrustServerCertificate=True"
sqlpackage /Action:Script  /SourceFile:"HR DBPJ\bin\Debug\HR DBPJ.dacpac" /TargetConnectionString:$cs /OutputPath:"HR-publish-preview.sql"
sqlpackage /Action:Publish /SourceFile:"HR DBPJ\bin\Debug\HR DBPJ.dacpac" /TargetConnectionString:$cs
```

- `HR` database မရှိရင် အသစ် ဖန်တီးပေးပါတယ်။ Post-deployment script က roles, permissions, categories တွေကို ထည့်ပေးပါတယ်။
- Data ပျောက်နိုင်တဲ့ ပြောင်းလဲမှု ပါရင် publish က ရပ်ပါတယ် (default)။ အဲဒီအခါ preview script ကို ဖတ်ပြီး ဆုံးဖြတ်ပါ။
- Visual Studio ဒါမှမဟုတ် VS Code နဲ့လည်း လုပ်လို့ရပါတယ် (အောက်က 2a ကို ကြည့်ပါ)။

### 2a. VS Code မှာ Database Project သုံးနည်း (optional)

အဆင့် 1–2 ကို command line အစား VS Code ထဲကနေ လုပ်ချင်ရင်:

1. Extension နှစ်ခု ထည့်ပါ: **SQL Server (mssql)** (`ms-mssql.mssql`) နဲ့ **SQL Database Projects**
   (`ms-mssql.sql-database-projects-vscode`)။ .NET SDK ရှိဖို့ လိုပါတယ် (အဆင့် 0)။
2. `D:\HR` folder ကို ဖွင့်ပြီး Activity Bar က **Database Projects** ကို နှိပ်ပါ။ `HR DBPJ` project ပေါ်လာပါမယ်။
   မပေါ်ရင် **Open Existing** ကနေ `HR DBPJ\HR DBPJ.sqlproj` ကို ရွေးပါ။
3. Activity Bar က **SQL Server** > **Add Connection** နဲ့ local DB ကို ချိတ်ပါ: Server `(localdb)\MSSQLLocalDB`၊
   **Windows Authentication**၊ Database `HR`၊ **Trust server certificate** ဖွင့်ပါ။

| လုပ်ချင်တာ | VS Code မှာ |
| --- | --- |
| Table, view, procedure အသစ် | Schema folder (`hr`, `lve`, `pay` …) ပေါ် right-click > **Add Table** / **Add View** / **Add Stored Procedure** |
| Build (`.dacpac` ထုတ်) | Project ပေါ် right-click > **Build** (အဆင့် 1 ရဲ့ `dotnet build` နဲ့ အတူတူ) |
| Publish | Right-click > **Publish** > အဆင့် 3 ရဲ့ connection ကို ရွေး > **Generate Script** ကို အရင်လုပ်ပြီး ဖတ်ပါ၊ ပြီးမှ **Publish** |
| Live DB နဲ့ နှိုင်းယှဉ် | Right-click > **Schema Compare** |

- `Ctrl+Shift+P` မှာ `Database Projects:` လို့ ရိုက်ရင် command အားလုံး ပေါ်ပါတယ်။
- `TreatTSqlWarningsAsErrors` ဖွင့်ထားလို့ T-SQL warning တစ်ခုပဲ ရှိရင်တောင် build fail ပါတယ်။ **Problems** panel မှာ ကြည့်ပါ။
- Publish dialog ရဲ့ "block on possible data loss" ကို မပိတ်ပါနဲ့။ Script ကို ဖတ်ပြီးမှ ဆုံးဖြတ်ပါ။
- **Save Profile** နဲ့ `.publish.xml` သိမ်းလို့ရပါတယ်။ Connection string မှာ password ပါရင် commit မလုပ်ပါနဲ့။
- Local DB ကိုပဲ ချိတ်ပါ။ Schema ပြောင်းချင်ရင် ဒီ project ထဲမှာပဲ ပြင်ပါ (EF Core က migration မ run ပါဘူး)။

## 3. File folders (တစ်ကြိမ်ပဲ)

API က upload file တွေကို ဒီ folder တွေမှာ သိမ်းပါတယ် (`appsettings.Development.json`)။ API က ပထမဆုံး upload မှာ
အလိုလို ဖန်တီးပေးပေမယ့် ကြိုဖန်တီးထားလို့ရပါတယ်:

```powershell
New-Item -ItemType Directory -Force D:\HrmsDev\kpi-evidence, D:\HrmsDev\grievance-files
```

## 4. ပထမဆုံး administrator (တစ်ကြိမ်ပဲ)

```powershell
cd api
dotnet run --project src/Apex.Hrms.Api -- bootstrap-admin --email sumyat.noe@apexhorizon.example --name "Su Myat Noe"
cd ..
```

- Target database ကို ပြပြီး `yes` ရိုက်ခိုင်းပါတယ်။ Password ကို နှစ်ကြိမ် ရိုက်ရပါတယ် (မပေါ်ပါဘူး၊ **အနည်းဆုံး ၁၂ လုံး**)။
- ဒီ account က HR Administrator ပါ။ အဆင့် 7 ရဲ့ seed က EMP002 Su Myat Noe နဲ့ email ကနေ ချိတ်ပေးပါတယ်။
- Seed scripts (အဆင့် 7) ကို run ပြီးမှ ဒီ command နဲ့ account ဖန်တီးရင် employee နဲ့ **မချိတ်ရသေးပါဘူး** (Settings > Users ရဲ့ Employee column မှာ "Not linked")။ My KPIs, My Payslips လို own data တွေ ဗလာ ဖြစ်နေပါမယ်။ **Settings > Users** မှာ အဲဒီ row ရဲ့ Actions > **Link to employee** ကို နှိပ်ပါ (account email နဲ့ work email တူတဲ့ current employee နဲ့ ချိတ်ပေးပါတယ်)၊ ပြီးရင် sign out ဖြစ်သွားလို့ ပြန် sign in ဝင်ပါ။
- **သတိ:** Link လုပ်ပြီးရင် app ထဲကနေ ပြန်ဖြုတ်လို့ (undo) မရပါဘူး။ Link မလုပ်ခင် တခြား active HR Administrator account တစ်ခု ရှိထားပါ။ မှားချိတ်မိရင် ပြန်ပြင်တာက လူကိုယ်တိုင် run ရတဲ့ database change ပါ ([api/README.md](../api/README.md) မှာ ကြည့်ပါ)။

## 5. Sample data script ထုတ်ပါ (run မယ့်နေ့တိုင်း)

```powershell
cd ui; node scripts/generate-db-seed.mjs; cd ..
```

ရက်စွဲတွေက ဒီနေ့ပေါ် မူတည်လို့ run မယ့်နေ့မှာ ထုတ်ပါ (2026-11-20 အတွက် ထုတ်ပြီးသား)။

## 6. API ရပ်ပြီး backup ယူပါ

API run နေရင် ရပ်ပါ (Ctrl+C)။ Database မှာ data ရှိပြီးသားဆိုရင် backup ယူပါ:

```powershell
New-Item -ItemType Directory -Force C:\Backup | Out-Null
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -Q "BACKUP DATABASE [HR] TO DISK = N'C:\Backup\HR-before-seed.bak' WITH COPY_ONLY, INIT"
```

## 7. Sample data ထည့်ပါ

**သတိ:** ဒီ script တွေက `HR` database ထဲက HR data (organization, employees နဲ့ module data အားလုံး) ကို **ဖျက်ပြီး**
sample data နဲ့ အစားထိုးပါတယ်။ Settings, roles, permissions, user accounts တွေကို ထိန်းထားပါတယ်။ အသေးစိတ်:
[test-data/README.md](test-data/README.md)။

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d HR -E -b -i docs\test-data\sql\seed-organization-employees.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -d HR -E -b -i docs\test-data\sql\seed-modules.sql
```

ပြီးရင် စစ်ပါ (SSMS ဒါမှမဟုတ် `sqlcmd -S "(localdb)\MSSQLLocalDB" -d HR -E -Q "..."`):

```sql
SELECT OBJECT_SCHEMA_NAME(parent_id) AS s, name, is_disabled FROM sys.triggers WHERE parent_class = 1;      -- is_disabled အားလုံး 0
SELECT SCHEMA_NAME(schema_id) AS s, name FROM sys.tables WHERE temporal_type = 2;                          -- 10 rows
SELECT name FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1;                             -- 0 rows
SELECT COUNT(*) FROM hr.Employee;                                                                          -- 60
```

## 8. API နဲ့ UI ကို run ပါ (terminal ၂ ခု)

```powershell
# Terminal 1
cd api; dotnet run --project src/Apex.Hrms.Api          # http://localhost:5045
```

```powershell
# Terminal 2
cd ui; npm run start:api                                 # http://localhost:4200 (/api ကို 5045 သို့ proxy)
```

Browser မှာ http://localhost:4200 ကို ဖွင့်ပြီး `sumyat.noe@apexhorizon.example` နဲ့ အဆင့် 4 က password နဲ့ ဝင်ပါ။

## 9. Sample accounts ဖန်တီးပါ

**Settings > Users** မှာ Add user နဲ့ ဖန်တီးပါ။ Initial password ကို အနည်းဆုံး ၁၂ လုံး ပေးပါ (ဥပမာ `Learn@HRMS2026`၊ local မှာပဲ သုံးပါ)။

| Employee | Role | Learning guide မှာ ဘာအတွက် |
| --- | --- | --- |
| EMP001 Thura Aung | Department Manager | Leave, KPI approve |
| EMP004 May Thu Kyaw | Payroll Officer | Payroll approve |
| EMP011 Sophia Bennett | Recruiter | Recruitment |
| EMP009 Ye Yint Naing | HR Officer | Grievance investigator |
| EMP005 Daniel Carter | Employee | Self-service, grievance submit |
| EMP026 Shwe Sin Oo (optional) | Employee | ဖန်တီးပြီး Deactivate လုပ်ပါ (inactive account စမ်းဖို့) |

## 10. Accounts တွေကို sample data နဲ့ ချိတ်ပါ

API ကို ရပ်ပြီး `seed-modules.sql` ကို နောက်တစ်ကြိမ် run ပါ။ Approval, review, investigator စတဲ့ မှတ်တမ်းတွေက
အဆင့် 9 က account တွေနဲ့ ချိတ်သွားပါမယ်။ ပြီးရင် API ကို ပြန် run ပါ။

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d HR -E -b -i docs\test-data\sql\seed-modules.sql
```

## 11. Smoke test (၅ မိနစ်)

- [ ] Su Myat Noe နဲ့ ဝင်ပြီး **Dashboard** ရဲ့ cards (employees, pending leave approvals, attendance today) မှာ data ပေါ်ပါသလား
- [ ] **Employees > Employee List** မှာ sample ဝန်ထမ်းတွေ (EMP001 Thura Aung စသည်) ပေါ်ပါသလား
- [ ] **Payroll > Payroll History** မှာ Paid လ ၆ လ ပေါ်ပါသလား
- [ ] Daniel Carter နဲ့ ဝင်ရင် menu နည်းနည်းပဲ ပေါ်ပါသလား (permission အလုပ်လုပ်ကြောင်း)
- [ ] Browser DevTools > Network မှာ `/api/...` requests တွေ 200 ပြန်ပါသလား

ပြီးရင် [BUSINESS-FLOW-LEARNING-GUIDE.md](BUSINESS-FLOW-LEARNING-GUIDE.md) ရဲ့ "အမြန်လမ်းကြောင်း" နဲ့ Flow 1–8 ကို
စမ်းပါ။ Demo နဲ့ ကွာတာတွေကို အဲဒီ guide ရဲ့ "Real system" section မှာ ကြည့်ပါ။ Data ကို DB မှာ စစ်ချင်ရင်
[E2E-API-TEST-ASSIGNMENT.xlsx](E2E-API-TEST-ASSIGNMENT.xlsx) ရဲ့ "DB check" column ကို သုံးပါ (SELECT ပဲ)။

## ပြဿနာ ဖြေရှင်းနည်း

| ပြဿနာ | အကြောင်းရင်း / ဖြေရှင်းနည်း |
| --- | --- |
| Sign in မရ | Account မရှိသေးလို့ (အဆင့် 4)၊ ဒါမှမဟုတ် password ၅ ကြိမ် မှားလို့ ၁၅ မိနစ် lock ဖြစ်နေတာ |
| API စတာနဲ့ ရပ်သွား (pseudonym key) | Development key ကို LocalDB connection နဲ့ပဲ လက်ခံပါတယ်။ `ConnectionStrings:Hrms` ကို မပြောင်းပါနဲ့ |
| `bootstrap-admin` refused | Active HR Administrator ရှိပြီးသားပါ။ အဲဒီ account နဲ့ ဝင်ပါ |
| My KPIs / My Payslips ဗလာ (admin account) | Account က employee နဲ့ မချိတ်ရသေးလို့ပါ (seed မတိုင်ခင် bootstrap မလုပ်ခဲ့ရင်)။ Settings > Users > **Link to employee**၊ ပြီးရင် ပြန် sign in ဝင်ပါ (အဆင့် 4) |
| UI မှာ data မပေါ်၊ `/api` 404/502 | API (port 5045) run မနေလို့ပါ။ Terminal 1 ကို စစ်ပါ |
| 403 Forbidden | Role မှာ permission မရှိလို့ပါ (Settings > Permissions)။ ဒါက မှန်ကန်တဲ့ behaviour ပါ |
| Seed script THROW 50001 / 50002 | `HR` database နဲ့ local server မဟုတ်လို့ ငြင်းတာပါ (safety guard) |
| Seed script THROW 50003 | `seed-organization-employees.sql` ကို အရင် run ပါ |
| Seed script ရပ်ပြီး lock ဖြစ်နေ | API run နေလို့ပါ။ API ကို ရပ်ပြီး ပြန် run ပါ |
| အစကနေ ပြန်စချင် | အဆင့် 6 ရဲ့ backup ကို restore လုပ်ပါ၊ ဒါမှမဟုတ် အဆင့် 5, 7, 10 ကို ပြန်လုပ်ပါ |
