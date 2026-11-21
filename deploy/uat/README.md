# HRMS UAT: Database ဆောက်ပြီး API နဲ့ ချိတ်နည်း

| | |
|---|---|
| Database server | RDS `core-uat-db` (SQL Server Express 2019): `<RDS_ENDPOINT>,1433` (`uat.local.env` ထဲမှာ) |
| Database name | `HR` |
| API server | Core-UAT-API `<API_HOST>` (Swagger: https://<API_HOST>/swagger) |
| Schema source | `HR DBPJ/` in this repo (SQL Server 2019 target) |
| Scripts | this folder, `deploy/uat/` |

ဒီ script တွေကို **ကိုယ်တိုင် အစဉ်လိုက် run ရပါမယ်**။ Password တွေကို run တဲ့အချိန်မှ ရိုက်ထည့်ရမှာဖြစ်ပြီး ဘယ် file ထဲမှာမှ မသိမ်းပါဘူး။

**ပထမဆုံး:** `uat.local.env.example` ကို `uat.local.env` အဖြစ် copy ကူးပြီး ကိုယ့် UAT ရဲ့ တန်ဖိုးတွေ (RDS endpoint၊ master login၊ security group၊ API host) ဖြည့်ပါ။ အဲ့ file ကို git က ignore လုပ်ထားလို့ **repo ထဲ ဘယ်တော့မှ မဝင်ပါဘူး**။ Endpoint နဲ့ login name ကို GitHub ပေါ် မရောက်အောင် ခွဲထားတာပါ။ ဒီ README ထဲက `<RDS_ENDPOINT>`၊ `<API_HOST>` စတဲ့ placeholder တွေကို အဲ့ file ထဲက တန်ဖိုးတွေနဲ့ အစားထိုး ဖတ်ပါ။

---

## Step 1: Database ဆောက်ပြီး schema publish လုပ်ခြင်း (ကိုယ့် PC ကနေ)

**လိုအပ်ချက်များ**
- .NET SDK 8 ဒါမှမဟုတ် အသစ်
- ကိုယ့် PC ရဲ့ public IP ကို RDS security group `<RDS_SECURITY_GROUP>` က port 1433 မှာ ခွင့်ပြုထားရပါမယ်
- RDS master login (`<RDS_ADMIN_LOGIN>`) ရဲ့ password

```powershell
cd C:\ApexCore\Apex_HRMS\HR\deploy\uat
powershell -ExecutionPolicy Bypass -File .\1-publish-schema.ps1
```

Script က ဒါတွေကို အစဉ်လိုက် လုပ်ပါမယ်:
1. SqlPackage မရှိရင် install လုပ်ပါမယ် (`dotnet tool install -g microsoft.sqlpackage`)
2. DB project ကို build လုပ်ပါမယ်
3. RDS master login (`<RDS_ADMIN_LOGIN>`) password ကို မေးပါမယ်
4. ဘာတွေ ပြောင်းမလဲ ပြတဲ့ review script `HR-deploy-<time>.sql` ကို ထုတ်ပေးပါမယ်။ **ဒီအဆင့်မှာ database ကို ဘာမှ မပြောင်းသေးပါဘူး**
5. **Review file ကို ဖတ်ပြီးမှ** `yes` ရိုက်ပါ။ `HR` database မရှိသေးရင် ဆောက်ပြီး schema နဲ့ seed data ကို ထည့်ပါမယ်

Publish setting တွေ: `BlockOnPossibleDataLoss=True` (data ပျက်နိုင်တဲ့ ပြောင်းလဲမှုတွေကို ရပ်ပါတယ်)၊ `DropObjectsNotInSource=False` (project ထဲမှာ မပါတဲ့ object တွေကို မဖျက်ပါဘူး)။

## Step 2: API အတွက် SQL login ဆောက်ခြင်း (SSMS)

1. SSMS မှာ RDS master login (`<RDS_ADMIN_LOGIN>`) နဲ့ ချိတ်ပါ။ **Encryption: Mandatory**၊ **Trust server certificate** ကို ✔ လုပ်ပါ
2. `2-create-api-login.sql` ကို ဖွင့်ပါ
3. `<STRONG-PASSWORD>` ကို password အသစ်နဲ့ အစားထိုးပါ
   - 20 လုံးနဲ့ အထက် ရှိရပါမယ်
   - `;` `'` `"` နဲ့ space မပါရပါ
   - Password manager ထဲ သိမ်းထားပါ
4. **Execute** လုပ်ပါ။ နောက်ဆုံး query က `hrms_api_uat | hrms_api` ဆိုတဲ့ row တစ်ကြောင်း ပြရပါမယ်
5. File ကို password မပါဘဲ ပြန်သိမ်းပါ (ဒါမှမဟုတ် save မလုပ်ပါနဲ့)

API က RDS master login (`<RDS_ADMIN_LOGIN>`) နဲ့ မချိတ်ရပါဘူး။ `hrms_api` role မှာ data ဖတ်/ရေး ခွင့်ပဲ ရှိပါတယ်။ Table ဆောက်တာ၊ ပြင်တာ (DDL) မလုပ်နိုင်ပါဘူး။

## Step 3: API ကို database နဲ့ ချိတ်ခြင်း (API server ပေါ်မှာ)

Server ကို SSH နဲ့ ဝင်လို့ရတာ server ရဲ့ security group က ခွင့်ပြုထားတဲ့ IP ကနေပဲ ရပါတယ်။

```powershell
scp -i "$env:USERPROFILE\.ssh\hrms-ec2" C:\ApexCore\Apex_HRMS\HR\deploy\uat\3-connect-api.sh ubuntu@<API_HOST>:
ssh -i "$env:USERPROFILE\.ssh\hrms-ec2" ubuntu@<API_HOST>
RDS_ENDPOINT=<RDS_ENDPOINT> bash 3-connect-api.sh   # or run without it and type the endpoint when asked
```

Script က ဒါတွေကို လုပ်ပါမယ်:
1. Amazon RDS CA certificate တွေကို install လုပ်ပါမယ်။ ဒါကြောင့် API က RDS certificate ကို အမှန်တကယ် စစ်ပြီးမှ ချိတ်ပါတယ် (`TrustServerCertificate=False`)
2. `hrms_api_uat` password ကို မေးပြီး `/etc/hrms/hrms.env` (root ပဲ ဖတ်လို့ရ) ထဲ ထည့်ပါမယ်။ File အဟောင်းကို `.bak-<time>` အဖြစ် backup လုပ်ထားပါမယ်
3. API ကို restart လုပ်ပြီး `/health/ready` က **200** ဖြစ်တဲ့အထိ စောင့်ပါမယ်
4. ပထမဆုံး HR Administrator ကို ဆောက်ပါမယ် (`bootstrap-admin`)
   - Target database ကို ပြမှာမို့ မှန်မှ `yes` ရိုက်ပါ
   - Password ကို နှစ်ခါ ရိုက်ရပါမယ်

Admin account ကိုပဲ သီးသန့် ဆောက်ချင်ရင် (ဥပမာ step 3 ကို run ပြီးသားဆိုရင်) `4-create-admin.sh` ကို သုံးပါ:
```powershell
scp -i "$env:USERPROFILE\.ssh\hrms-ec2" .\4-create-admin.sh ubuntu@<API_HOST>:
ssh -t -i "$env:USERPROFILE\.ssh\hrms-ec2" ubuntu@<API_HOST> "bash 4-create-admin.sh"
```
Ubuntu 26.04 ရဲ့ `sudo` (sudo-rs) က `sudo -E` ကို လျစ်လျူရှုလို့ script တွေက `runuser -u hrms` ကို သုံးထားပါတယ်။

## (Optional) Sample data ထည့်ခြင်း: data အားလုံး အတုပါ

SSMS မှာ RDS master login (`<RDS_ADMIN_LOGIN>`) နဲ့ `HR` database ကို ရွေးပြီး ဒီအစဉ်အတိုင်း Execute လုပ်ပါ:
1. `uat-seed\seed-organization-employees.sql`: company 3၊ department 11၊ ဝန်ထမ်း 60 စသည်
2. `uat-seed\seed-modules.sql`: leave၊ attendance၊ payroll၊ recruitment၊ KPI နဲ့ grievance

ဒီ file တွေက `docs/test-data/sql` ထဲက file တွေရဲ့ UAT copy ပါ။ မူရင်း file တွေက ကိုယ့်စက်ပေါ်က database (local) မှာပဲ run လို့ ရပါတယ်။ UAT copy မှာ "local server only" guard ကို ဒီ guard တွေနဲ့ အစားထိုးထားပါတယ်:
- File 1 က ဝန်ထမ်းနဲ့ company **တစ်ခုမှ မရှိတဲ့** database မှာပဲ run လို့ ရပါတယ်
- File 2 က **sample ဝန်ထမ်း 60 ယောက်ပဲ** ရှိတဲ့ database မှာပဲ run လို့ ရပါတယ်

ဒါကြောင့် data အစစ် ထည့်ပြီးသား database မှာ run မိရင် ငြင်းပါမယ်။ Script တစ်ခုချင်းစီက transaction တစ်ခုတည်းနဲ့ run လို့ error တက်ရင် အားလုံး rollback ဖြစ်ပါတယ်။

## ပြီးရင် စစ်ရန်

- https://<API_HOST>/health/ready → `Healthy`
- https://<API_HOST>/swagger မှာ:
  1. `GET /api/auth/antiforgery`
  2. `POST /api/auth/login` (step 3 က admin account နဲ့)
  3. `GET /api/auth/me` → admin user ပြရပါမယ်

## ပြဿနာ ဖြေရှင်းနည်း

| ပြဿနာ | ဖြေရှင်းနည်း |
|---|---|
| Step 1 မှာ timeout | ကိုယ့် IP ပြောင်းသွားတာ ဖြစ်နိုင်ပါတယ် (VPN)။ https://checkip.amazonaws.com မှာ ကြည့်ပြီး security group ထဲ ထည့်ပါ |
| `/health/ready` = 503 | `sudo journalctl -u hrms-api -n 50` ကို ကြည့်ပါ။ Login failed ဆိုရင် password မှားနေတာ၊ ဒါမှမဟုတ် step 2 မ run ရသေးတာ ဖြစ်ပါတယ် |
| Certificate error | Step 3 ရဲ့ CA install အဆင့် ပျက်သွားတာပါ။ `sudo rm /usr/local/share/ca-certificates/rds/.installed` လုပ်ပြီး script ကို ပြန် run ပါ |
| Rollback | `/etc/hrms/hrms.env.bak-*` ကို ပြန်ကူးပြီး `sudo systemctl restart hrms-api` လုပ်ပါ |

## သတိထားရန်

- UAT အတွက်ပဲ ဖြစ်ပါတယ်။ **ဝန်ထမ်း data အစစ် မထည့်ပါနဲ့** (malware scan နဲ့ backup policy မရှိသေးလို့ပါ)
- Developer တိုင်းကို login သီးသန့် ပေးပါ။ RDS master login (`<RDS_ADMIN_LOGIN>`) password ကို မဝေပါနဲ့
- UAT ပြီးရင် RDS public access ကို ပြန်ပိတ်ပါ:
  `aws rds modify-db-instance --region ap-southeast-1 --db-instance-identifier core-uat-db --no-publicly-accessible --apply-immediately`
