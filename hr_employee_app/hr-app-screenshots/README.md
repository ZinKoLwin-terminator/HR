# Screens

21 PNGs, 1170 x 2532 (390 x 844 at 3x — iPhone 14 Pro / Pixel 8 class).

Rendered from the **running app**: the real `HrEmployeeApp`, the real router
and the real widgets, driven through every route. Only three things a
headless renderer cannot provide were substituted — platform storage, device
info and the GPS.

The orange **"Sample data — not from the HR system"** banner appears in every
shot because that is genuinely what the app shows while `USE_MOCK_DATA=true`.
No backend contract has been agreed yet, so the figures are invented. The
banner is not a watermark added for these images; it is part of the UI.

| # | File | Screen |
|---|---|---|
| 01 | `01-splash.png` | Splash, shown while the stored session is read |
| 02 | `02-login.png` | Sign in — employee ID or email, remember me, demo shortcut |
| 03 | `03-forgot-password.png` | Request a reset link |
| 04 | `04-reset-password.png` | Set a new password from an emailed link |
| 05 | `05-dashboard.png` | Home, before check-in |
| 06 | `06-dashboard-checked-in.png` | Home, after check-in — late badge, check-out action |
| 07 | `07-attendance.png` | Punch panel, monthly summary, paginated history |
| 08 | `08-attendance-detail.png` | One day: times, late/early flags, recorded location |
| 09 | `09-leave-balances.png` | Leave → Balances tab |
| 10 | `10-leave-requests.png` | Leave → Requests tab, status filter |
| 11 | `11-apply-leave.png` | Apply for leave — type, duration, dates, attachments |
| 12 | `12-leave-detail-pending.png` | Pending request, withdrawable |
| 13 | `13-leave-detail-approved.png` | Approved request with decision and attachment |
| 14 | `14-payslip-masked.png` | Payslip list, amounts masked (the default) |
| 15 | `15-payslip-revealed.png` | Payslip list, amounts revealed |
| 16 | `16-payslip-detail.png` | Earnings, deductions, net pay |
| 17 | `17-profile.png` | Employee record, HR-controlled fields read-only |
| 18 | `18-edit-profile.png` | The self-editable fields only |
| 19 | `19-change-password.png` | Change password |
| 20 | `20-notifications.png` | Inbox, unread filter, mark all read |
| 21 | `21-notification-detail.png` | One notification with a deep link |

## Regenerating

```bash
flutter test test/screenshots --update-goldens --run-skipped
```

The harness is `test/screenshots/app_screenshots_test.dart`. It is tagged
`screenshots` and skipped by the normal `flutter test` run, so a deliberate
UI change does not fail the build — regenerating is an explicit action.
