# HR Employee App — API Contracts (PLACEHOLDER SPEC)

> **STATUS: PLACEHOLDER / PROPOSED.**
> No backend API contract was supplied when this app was built. Everything in this
> document is a *proposal* authored by the Flutter team to unblock UI development.
> It is **not** a description of an existing ASP.NET Core API.
>
> The backend team must review this document and either (a) implement it as-is, or
> (b) return corrections. Until a contract is agreed, the app ships with
> `AppConfig.useMockData == true`, which serves local fixtures and displays a
> permanent **"MOCK DATA"** banner so no one mistakes fixtures for real data.

---

## 1. Conventions

| Topic | Decision |
|---|---|
| Base URL | `{scheme}://{host}/api/{version}` — e.g. `https://hr-api.example.com/api/v1` |
| Version | Path segment `v1` |
| Auth | `Authorization: Bearer {accessToken}` on every endpoint except the public auth ones |
| Content type | `application/json; charset=utf-8`, except file upload (`multipart/form-data`) and PDF download (`application/pdf`) |
| Casing | JSON uses **camelCase** (ASP.NET Core default `JsonSerializerOptions`) |
| Dates | ISO-8601 UTC, e.g. `2026-03-14T09:02:11Z`. Date-only fields use `yyyy-MM-dd` |
| Money | `decimal` serialized as a JSON **number**, 2 dp. Currency sent separately as ISO-4217 code |
| IDs | `string` (GUID) — avoids int/long overflow issues in JS-adjacent tooling |
| Idempotency | Check-in/check-out accept an `Idempotency-Key` header; replaying returns the original result |

### 1.1 Success envelope

Every JSON endpoint returns this envelope:

```json
{
  "success": true,
  "message": null,
  "data": {},
  "errors": null,
  "traceId": "0HMV9K2L3N4P5"
}
```

### 1.2 Error envelope

```json
{
  "success": false,
  "message": "Validation failed.",
  "data": null,
  "errors": {
    "password": ["Password must be at least 8 characters."],
    "employeeId": ["Employee not found."]
  },
  "traceId": "0HMV9K2L3N4P5"
}
```

`errors` is a `Dictionary<string, string[]>` keyed by field name. For non-field errors
the key is `"_"`.

### 1.3 Paginated payload

`data` for list endpoints:

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 134,
  "totalPages": 7,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

Query params: `?pageNumber=1&pageSize=20`. `pageNumber` is **1-based**.

### 1.4 HTTP status codes

| Code | Meaning | App behaviour |
|---|---|---|
| 200 | OK | parse `data` |
| 201 | Created | parse `data` |
| 204 | No content | success, no body |
| 400 | Validation / bad request | surface `errors` against form fields |
| 401 | Access token expired or invalid | attempt **one** silent refresh, then retry; on second 401 force logout |
| 403 | Authenticated but not permitted | show "not permitted" message, do **not** log out |
| 404 | Not found | show empty/not-found state |
| 409 | Conflict (e.g. already checked in) | show `message` |
| 422 | Business rule violation (e.g. insufficient leave balance) | show `message` |
| 429 | Rate limited | show retry-after message |
| 5xx | Server error | generic "something went wrong", log `traceId` |

---

## 2. Authentication

> **IMPLEMENTED** by the mobile API in `mobile-api/` (see `mobile-api/README.md`): login, refresh (rotated, with reuse
> detection), logout, `GET /auth/me` and change-password, in this envelope. Differences from the proposal below:
> a refused sign-in or refresh is `401` with one generic message (never which part was wrong); `userId` is the
> `sec.AppUser` id and `employeeId` the employee code, both as text; `mustChangePassword` is always `false`;
> logout takes the session from the access token and ignores the body. Forgot/reset password are not on the mobile
> API yet (use the web app).

### POST `/auth/login` — public

```json
{
  "userName": "EMP-00421",
  "password": "********",
  "deviceId": "a1b2c3d4-...",
  "deviceName": "Pixel 8 / Android 15"
}
```

`userName` accepts **either** the employee ID or the work email.

Response `data`:

```json
{
  "accessToken": "eyJhbGciOi...",
  "refreshToken": "9f2c1b7e-...",
  "accessTokenExpiresAt": "2026-03-14T10:02:11Z",
  "refreshTokenExpiresAt": "2026-04-13T09:02:11Z",
  "tokenType": "Bearer",
  "user": {
    "userId": "6f1d-...",
    "employeeId": "EMP-00421",
    "fullName": "Aye Chan Moe",
    "email": "aye.chan@example.com",
    "department": "Finance",
    "designation": "Senior Accountant",
    "profilePhotoUrl": "https://cdn.example.com/p/6f1d.jpg",
    "mustChangePassword": false
  }
}
```

### POST `/auth/refresh` — public (the refresh token is itself the credential)

```json
{ "refreshToken": "9f2c1b7e-...", "deviceId": "a1b2c3d4-..." }
```

Response `data`: same token payload as login.

> **Rotation:** the server MUST issue a new refresh token and invalidate the old one.
> A reused refresh token MUST return 401 and revoke the whole token family.

### POST `/auth/logout`

```json
{ "refreshToken": "9f2c1b7e-..." }
```

Returns `204`. The server revokes the refresh-token family. The client clears secure
storage regardless of the response.

### POST `/auth/forgot-password` — public

```json
{ "email": "aye.chan@example.com" }
```

Returns `200` **always**, even for unknown emails (prevents account enumeration).

### POST `/auth/reset-password` — public

```json
{ "email": "...", "token": "emailed-token", "newPassword": "********" }
```

### POST `/auth/change-password` — authenticated

```json
{ "currentPassword": "********", "newPassword": "********" }
```

Returns `204`. The server revokes all other refresh tokens for the user.

---

## 3. Dashboard

### GET `/dashboard/summary`

```json
{
  "profile": {
    "employeeId": "EMP-00421",
    "fullName": "Aye Chan Moe",
    "department": "Finance",
    "designation": "Senior Accountant",
    "profilePhotoUrl": "https://cdn.example.com/p/6f1d.jpg"
  },
  "todayAttendance": {
    "attendanceDate": "2026-03-14",
    "status": "Present",
    "checkInAt": "2026-03-14T09:02:11Z",
    "checkOutAt": null,
    "isLateArrival": true,
    "isEarlyDeparture": false,
    "workedMinutes": 184
  },
  "leaveBalances": [
    {
      "leaveTypeId": "lt-1",
      "leaveTypeName": "Annual Leave",
      "entitledDays": 14,
      "usedDays": 5,
      "pendingDays": 1,
      "remainingDays": 8,
      "colorHex": "#2563EB"
    }
  ],
  "recentNotifications": [
    {
      "id": "n-1",
      "title": "Leave approved",
      "body": "Your 2-day annual leave was approved.",
      "type": "LeaveApproval",
      "createdAt": "2026-03-13T04:11:00Z",
      "isRead": false
    }
  ],
  "unreadNotificationCount": 3
}
```

`todayAttendance` is `null` when the employee has not checked in today.

---

## 4. Attendance

### Enums

`attendanceStatus`: `Present` · `Absent` · `OnLeave` · `Holiday` · `WeeklyOff` · `HalfDay`

### GET `/attendance/today`

Response `data`: an `AttendanceRecord` (shape below) or `null`.

### POST `/attendance/check-in`

```json
{
  "capturedAt": "2026-03-14T09:02:11Z",
  "location": {
    "latitude": 16.8409,
    "longitude": 96.1735,
    "accuracyMeters": 12.4,
    "address": "Yangon HQ, Kyauktada",
    "isMocked": false
  },
  "deviceId": "a1b2c3d4-...",
  "remark": null
}
```

`location` is **nullable** — when the tenant has not enabled geofencing, or the user
declined the permission and the tenant allows that, the client sends `null`.

> **The server, not the client, decides whether a location is acceptable.**
> The client never evaluates a geofence and never reports "verified". If the location
> falls outside the allowed radius the server returns `422` with a message, and the
> app shows that message verbatim.

Response `data`: the created `AttendanceRecord`. `409` if already checked in today.

### POST `/attendance/check-out`

Same request body. `409` if not checked in, or already checked out.

### GET `/attendance/history?pageNumber=1&pageSize=20&fromDate=2026-03-01&toDate=2026-03-31`

Paginated `AttendanceRecord`.

### GET `/attendance/{id}`

Single `AttendanceRecord`.

**`AttendanceRecord`**

```json
{
  "id": "att-9001",
  "attendanceDate": "2026-03-14",
  "status": "Present",
  "checkInAt": "2026-03-14T09:02:11Z",
  "checkOutAt": "2026-03-14T18:10:44Z",
  "checkInLocation": {
    "latitude": 16.8409,
    "longitude": 96.1735,
    "accuracyMeters": 12.4,
    "address": "Yangon HQ",
    "isMocked": false
  },
  "checkOutLocation": null,
  "shiftStartAt": "2026-03-14T09:00:00Z",
  "shiftEndAt": "2026-03-14T18:00:00Z",
  "isLateArrival": true,
  "lateByMinutes": 2,
  "isEarlyDeparture": false,
  "earlyByMinutes": 0,
  "workedMinutes": 548,
  "overtimeMinutes": 10,
  "remark": null
}
```

### GET `/attendance/summary?year=2026&month=3`

```json
{
  "year": 2026,
  "month": 3,
  "totalWorkingDays": 21,
  "presentDays": 18,
  "absentDays": 1,
  "leaveDays": 2,
  "holidayDays": 0,
  "lateArrivals": 3,
  "earlyDepartures": 1,
  "totalWorkedMinutes": 9120,
  "totalOvertimeMinutes": 240
}
```

---

## 5. Leave

### Enums

`leaveStatus`: `Pending` · `Approved` · `Rejected` · `Cancelled`
`leaveDayPortion`: `FullDay` · `FirstHalf` · `SecondHalf`

### GET `/leave/types`

```json
[
  {
    "id": "lt-1",
    "name": "Annual Leave",
    "code": "AL",
    "requiresAttachment": false,
    "maxConsecutiveDays": 14,
    "colorHex": "#2563EB",
    "isActive": true
  }
]
```

### GET `/leave/balances?year=2026`

```json
[
  {
    "leaveTypeId": "lt-1",
    "leaveTypeName": "Annual Leave",
    "year": 2026,
    "entitledDays": 14,
    "usedDays": 5,
    "pendingDays": 1,
    "remainingDays": 8,
    "colorHex": "#2563EB"
  }
]
```

Day counts are `decimal` to allow half-days (e.g. `0.5`).

### POST `/leave/requests` — `multipart/form-data`

| Part | Type | Notes |
|---|---|---|
| `leaveTypeId` | text | required |
| `startDate` | text `yyyy-MM-dd` | required |
| `endDate` | text `yyyy-MM-dd` | required, `>= startDate` |
| `dayPortion` | text enum | required |
| `reason` | text | required, 10–500 chars |
| `contactDuringLeave` | text | optional |
| `attachments` | file[] | optional, max 5 files, max 5 MB each, `pdf jpg jpeg png` |

`422` when the balance is insufficient or the dates overlap an existing request.
Response `data`: the created `LeaveRequest`.

### GET `/leave/requests?pageNumber=1&pageSize=20&status=Pending`

Paginated `LeaveRequest`. `status` is optional.

### GET `/leave/requests/{id}`

Single `LeaveRequest`.

### POST `/leave/requests/{id}/cancel`

Returns the updated `LeaveRequest`. Allowed only while the status is `Pending`.

**`LeaveRequest`**

```json
{
  "id": "lr-5501",
  "leaveTypeId": "lt-1",
  "leaveTypeName": "Annual Leave",
  "colorHex": "#2563EB",
  "startDate": "2026-04-02",
  "endDate": "2026-04-03",
  "dayPortion": "FullDay",
  "totalDays": 2,
  "reason": "Family matters out of town.",
  "contactDuringLeave": "+95 9 123 456 789",
  "status": "Pending",
  "appliedAt": "2026-03-14T03:20:00Z",
  "decidedAt": null,
  "approverName": null,
  "decisionRemark": null,
  "attachments": [
    {
      "id": "at-1",
      "fileName": "medical.pdf",
      "contentType": "application/pdf",
      "sizeBytes": 148221,
      "downloadUrl": "https://.../leave/attachments/at-1"
    }
  ]
}
```

---

## 6. Payslip

> **Sensitive.** Payslip endpoints MUST be served over HTTPS with
> `Cache-Control: no-store`. The app never writes payslip JSON to disk and stores the
> downloaded PDF only in the app-private directory.

### GET `/payslips?pageNumber=1&pageSize=12&year=2026`

Paginated `PayslipSummary`:

```json
{
  "id": "ps-2026-03",
  "year": 2026,
  "month": 3,
  "periodLabel": "March 2026",
  "payDate": "2026-03-28",
  "netPay": 1842500.0,
  "currencyCode": "MMK",
  "isPdfAvailable": true
}
```

### GET `/payslips/{id}`

```json
{
  "id": "ps-2026-03",
  "year": 2026,
  "month": 3,
  "periodLabel": "March 2026",
  "payDate": "2026-03-28",
  "currencyCode": "MMK",
  "paidDays": 31,
  "lopDays": 0,
  "basicSalary": 1500000.0,
  "grossEarnings": 2050000.0,
  "totalDeductions": 207500.0,
  "netPay": 1842500.0,
  "earnings": [{ "code": "BASIC", "label": "Basic Salary", "amount": 1500000.0 }],
  "deductions": [{ "code": "SSB", "label": "Social Security", "amount": 6000.0 }],
  "isPdfAvailable": true
}
```

### GET `/payslips/{id}/pdf`

Returns `application/pdf` bytes (**not** the JSON envelope).
Header: `Content-Disposition: attachment; filename="payslip-2026-03.pdf"`.

---

## 7. Employee profile

### GET `/employees/me`

```json
{
  "userId": "6f1d-...",
  "employeeId": "EMP-00421",
  "fullName": "Aye Chan Moe",
  "email": "aye.chan@example.com",
  "mobileNumber": "+95 9 123 456 789",
  "profilePhotoUrl": "https://cdn.example.com/p/6f1d.jpg",
  "dateOfBirth": "1994-07-21",
  "gender": "Female",
  "nationalId": "12/ABC(N)123456",
  "maritalStatus": "Single",
  "bloodGroup": "O+",
  "address": "No. 24, Bo Aung Kyaw St, Yangon",
  "department": "Finance",
  "designation": "Senior Accountant",
  "employmentType": "Permanent",
  "reportingManagerName": "U Myint Soe",
  "workLocation": "Yangon HQ",
  "dateOfJoining": "2021-02-01",
  "confirmationDate": "2021-08-01",
  "emergencyContact": {
    "name": "Daw Khin Mar",
    "relationship": "Mother",
    "mobileNumber": "+95 9 987 654 321",
    "alternateNumber": null,
    "address": "No. 24, Bo Aung Kyaw St, Yangon"
  }
}
```

### PUT `/employees/me/contact`

```json
{ "mobileNumber": "+95 9 ...", "address": "..." }
```

Only self-editable fields. Employment data is HR-controlled and read-only in the app.

### PUT `/employees/me/emergency-contact`

```json
{
  "name": "...",
  "relationship": "...",
  "mobileNumber": "...",
  "alternateNumber": null,
  "address": "..."
}
```

### POST `/employees/me/photo`

`multipart/form-data`, part `file`, max 2 MB, `jpg|jpeg|png`.
Response `data`: `{ "profilePhotoUrl": "https://..." }`

---

## 8. Notifications

### Enums

`notificationType`: `Announcement` · `LeaveApproval` · `LeaveRejection` · `Attendance` · `Payslip` · `General`

### GET `/notifications?pageNumber=1&pageSize=20&onlyUnread=false`

Paginated:

```json
{
  "id": "n-1",
  "title": "Leave approved",
  "body": "Your annual leave for 2-3 Apr was approved by U Myint Soe.",
  "type": "LeaveApproval",
  "isRead": false,
  "createdAt": "2026-03-13T04:11:00Z",
  "readAt": null,
  "deepLink": "hrapp://leave/lr-5501",
  "imageUrl": null
}
```

### GET `/notifications/unread-count`

```json
{ "count": 3 }
```

### POST `/notifications/{id}/read`

Returns `204`.

### POST `/notifications/read-all`

Returns `204`.

### POST `/devices/register` — push registration

```json
{
  "deviceId": "a1b2c3d4-...",
  "platform": "Android",
  "pushToken": "fcm-token",
  "appVersion": "1.0.0"
}
```

Returns `204`. Called after login once a push provider is wired up. The app currently
ships a **no-op** push service; see `core/services/push_notification_service.dart`.

---

## 9. Open questions for the backend team

1. Is the envelope in §1.1 acceptable, or does the API return bare objects?
2. Are refresh tokens rotated, and is reuse-detection implemented?
3. Does geofencing exist server-side, and what is the error contract when a check-in
   falls outside the allowed radius?
4. Is `GET /payslips/{id}/pdf` a direct stream or a short-lived pre-signed URL?
5. What is the maximum page size the API will honour?
6. Which push provider — FCM, APNs direct, or Azure Notification Hubs?
7. Is multi-tenancy in play (does the client need to send a tenant header)?

---

## 10. Grievances — self-service (REAL API, not a proposal)

Unlike §2–§8, this section describes an API that exists:
`api/src/Apex.Hrms.Api/Endpoints/GrievanceEndpoints.cs` (`MapSelf`), DTOs in
`Apex.Hrms.Application/Grievances/GrievanceModels.cs`, rules in
`Apex.Hrms.Domain/Grievances/GrievanceCase.cs` and
`Apex.Hrms.Application/Grievances/MyGrievanceService.cs`. Only the employee's
own grievances are on mobile; HR's case-team endpoints are not used.

It departs from §1 in three ways, absorbed by
`GrievanceRemoteDataSourceImpl` (no shared code changed):

| Topic | §1 proposal | Grievance API |
|---|---|---|
| Root | `/api/v1` | `/api` (so `API_BASE_URL` must point at `…/api` for these calls) |
| Body | `{ success, data }` envelope | bare object (an envelope is still unwrapped if present) |
| Page | `pageNumber` 1-based; `items, totalCount, totalPages…` | `pageIndex` **0-based**, `pageSize`, `search`; `{ items, total, pageIndex, pageSize }` |
| IDs | string GUID | `int` |
| Errors | `{ message, errors }` | RFC 7807 problem details: `title` generic, human text in `detail`, `errors` per field, `code` |

Enums are their exact texts. Dates are `yyyy-MM-dd`; `…AtUtc` are UTC instants;
`rowVersion` is base64 and every write sends the one last read (stale → `409`
"The record was changed or deleted by someone else. Reload it and try again.").

### Endpoints (paths relative to `/api`)

| Method & path | Body | Answer |
|---|---|---|
| GET `/grievances/options` | — | `{ categories: [{ id, code, name, description, allowsAnonymous, defaultTargetDays }], departments: [{ id, name }] }` (active only) |
| GET `/grievances/my?pageIndex=0&pageSize=20&status=Draft&search=…` | — | page of `MyGrievanceDto` |
| GET `/grievances/my/{id}` | — | `MyGrievanceDetailDto` |
| POST `/grievances/my` (201) | `{ categoryId, subject, description, incidentDate, location, involvedPersonText, involvedDepartmentId, isAnonymous }` | `MyGrievanceDetailDto` (a Draft) |
| PUT `/grievances/my/{id}` | same + `rowVersion` | `MyGrievanceDetailDto` |
| DELETE `/grievances/my/{id}` (204) | `{ rowVersion }` | — |
| POST `/grievances/my/{id}/submit` | `{ rowVersion, declarationAccepted }` | `MyGrievanceDetailDto` (now `Submitted`, with `caseNo`) |
| POST `/grievances/my/{id}/messages` | `{ rowVersion, body, inReplyToId }` | `MyGrievanceDetailDto` |
| POST `/grievances/my/{id}/seen` (204) | — | clears `hasUnreadUpdate` |
| GET `/grievances/my/dashboard`, POST `/grievances/my/{id}/attachments`, GET `/grievances/my/attachments/{id}` | | **not used on mobile yet** |

**`MyGrievanceDto`**: `id, caseNo (null for a draft), categoryId, categoryName,
subject, status, isAnonymous, createdAtUtc, submittedAtUtc,
targetResolutionDate, lastUpdateAtUtc, hasUnreadUpdate, rowVersion`.

**`MyGrievanceDetailDto`**: the above plus `description, incidentDate, location,
involvedPersonText, involvedDepartmentId, involvedDepartmentName,
declarationAcceptedAtUtc, acknowledgedAtUtc, resolvedAtUtc, closedAtUtc,
rejectionReason` (Rejected only), `resolutionSummary` (Resolved/Closed and
shared by HR only), `timeline[] { fromStatus, toStatus, action, actor, reason,
occurredAtUtc }`, `messages[] { id, direction (FromSubmitter|ToSubmitter), kind
(Message|InfoRequest|InfoResponse), body, actor, inReplyToId, createdAtUtc }`,
`attachments[] { id, fileName, contentType, sizeBytes, uploadedBy,
uploadedAtUtc }`. `actor` is "You", "HR team" or "System", never a name.

**Statuses**: `Draft` · `Submitted` · `Acknowledged` · `UnderReview` ·
`Assigned` · `UnderInvestigation` · `AwaitingEmployeeResponse` ·
`ResolutionProposed` · `Resolved` · `Closed` · `Rejected` · `Reopened`.

### Rules (checked in the app first, with the API's wording; the server decides)

* **Draft**: category required ("Choose a category."), subject required and
  ≤ 200 ("Subject is required."), description ≤ 4000, location and person
  involved ≤ 200, incident date not after today ("The incident date cannot be
  in the future."), active category ("Choose an active category."), active
  department ("Choose an active department.").
* **At most 5 open drafts**: "You already have 5 unfinished drafts. Submit or
  delete one before you start another."
* **Submit**: subject 5–200, description 20–4000 ("The description must be 20
  to 4000 characters."), declaration ("Confirm the declaration to submit the
  grievance."); a draft only (`409` otherwise).
* **Anonymity**: only where the category allows it ("This category cannot be
  raised anonymously."), and fixed once the draft exists ("Start a new draft to
  change how you report.").
* **Edit / delete**: drafts only (`409` "The case is Submitted; only a draft
  can be changed or deleted.").
* **Messages**: 1–4000 characters; not on a Draft or a Closed case. While the
  status is `AwaitingEmployeeResponse` the message **is the response**: the
  row version is checked, it is stored as `InfoResponse` (in reply to the
  latest information request) and the case returns to the status it had
  before HR asked. Otherwise it is a plain message and the row version does
  not change.
* **Recorded by HR**: HR can record a grievance on the employee's behalf
  (`POST /grievances/cases`). The employee sees a normal submitted case whose
  first history entry has `actor: "HR team"` and a reason such as "Recorded by
  HR on behalf of the employee (received by phone)."; the detail screen shows
  history reasons for that reason.
