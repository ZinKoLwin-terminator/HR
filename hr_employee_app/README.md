# HR Employee App

An HR mobile application for employees: attendance, leave, payslips, profile,
notifications and their own grievances. Flutter, Clean Architecture,
feature-first.

> **The app currently runs on local fixtures, not a backend.** No API
> contract was supplied, so `docs/api_contracts.md` proposes one and the app
> ships with `USE_MOCK_DATA=true`. Every screen that can show fixture data
> carries a visible **"Sample data"** banner, and `AppConfig` forces mock mode
> off for production builds. See [Mock mode](#mock-mode).

---

## Requirements

| Tool | Version |
|---|---|
| Flutter | 3.50.0 (stable) |
| Dart | 3.14.0 |
| Android | compileSdk per Flutter default, minSdk 23+ |
| iOS | 13.0+ |

Building an APK or an iOS app additionally needs the Android SDK + JDK 17, or
Xcode. Neither is required to run the analyzer or the test suite.

## Getting started

```bash
flutter pub get

# Freezed and json_serializable output is NOT committed - generate it first.
dart run build_runner build --delete-conflicting-outputs

flutter test
flutter run
```

If you change any file containing `part '*.freezed.dart'` or `part '*.g.dart'`,
re-run `build_runner`. While iterating, leave it watching:

```bash
dart run build_runner watch
```

## Running against an environment

No endpoint is hardcoded. Configuration comes from `--dart-define`, read once
in `lib/app/config/app_config.dart`.

```bash
# Fixtures, no backend needed (the default)
flutter run

# Local ASP.NET Core API (10.0.2.2 is the Android emulator's host alias)
flutter run \
  --dart-define=APP_ENV=development \
  --dart-define=USE_MOCK_DATA=false \
  --dart-define=API_BASE_URL=http://10.0.2.2:5000/api/v1

# Staging
flutter run \
  --dart-define=APP_ENV=staging \
  --dart-define=USE_MOCK_DATA=false \
  --dart-define=API_BASE_URL=https://hr-api-staging.example.com/api/v1

# Production release
flutter build apk --release \
  --dart-define=APP_ENV=production \
  --dart-define=API_BASE_URL=https://hr-api.example.com/api/v1
```

| Define | Values | Default |
|---|---|---|
| `APP_ENV` | `development` · `staging` · `production` | `development` |
| `API_BASE_URL` | full API root | per-environment placeholder |
| `USE_MOCK_DATA` | `true` · `false` | `true` (forced to `false` in production) |
| `ATTENDANCE_LOCATION_POLICY` | `disabled` · `optional` · `required` | `optional` |

`AppConfig.validate()` runs at start-up and throws if a production build has
no `API_BASE_URL`, or if it is not HTTPS. A misconfigured release fails
immediately rather than at the first request.

## Mock mode

`USE_MOCK_DATA=true` swaps every `*RemoteDataSourceImpl` for a
`*MockDataSource` backed by `lib/shared/helpers/mock_fixtures.dart`. The
fixtures are a single shared store, so the screens agree with each other:
checking in on the dashboard changes the attendance screen, and applying for
leave changes the balance. Grievances are the one exception: they share no
records with other screens, so their fixtures live in the feature
(`lib/features/grievance/data/datasources/grievance_mock_store.dart`).

Mock sign-in accepts **`EMP-00421` / `Password1`**; anything else returns the
same validation error the API would, so the failure path is exercised too.

Three things keep this honest:

* every screen that can show fixtures renders `MockDataBanner`;
* `AppConfig` forces `useMockData` to `false` when `APP_ENV=production`;
* mock failures mirror real ones - a second check-in returns `409`, an
  overlapping leave request returns `422`.

To remove mock mode entirely once the API is live: delete
`lib/shared/helpers/mock_*.dart`, the `*MockDataSource` classes, and the
branch in each feature's `*RemoteDataSourceProvider`.

## Screens

`screenshots/` holds a PNG of all 21 screens, with an index in
`screenshots/README.md`. They are rendered from the running app rather than
mocked up:

```bash
flutter test test/screenshots --update-goldens --run-skipped
```

The harness drives the real `HrEmployeeApp` and the real router through every
route, substituting only what a headless renderer cannot provide — platform
storage, device info and the GPS. The "Sample data" banner in every shot is
part of the UI in mock mode, not an overlay added to the images.

## Device mockup (browser preview)

`web/mockup.html` shows the running app inside as many phone frames as you
want, side by side. It is a plain page served next to the app, so it does not
depend on the Chrome DevTools device toolbar — which emulates one device per
tab and changes between Chrome releases.

```bash
flutter run -d web-server --web-port 5000
# then open http://localhost:5000/mockup.html in Chrome
```

`flutter run -d chrome --web-port 5000` works too; it opens the app itself in
one tab, and the mockup page is a second tab at the same address.

Each frame is an `<iframe>` pointing at the app, so what you see is the real
build and the page never needs rebuilding when the app changes. From the bar
you can add any preset or a custom size, rotate or remove a frame, scale
everything to fit the window, and pad the screen the way a notch and a home
bar do on hardware. The layout is remembered per browser.

After a hot restart (`R` in the terminal), press **R** on the page — or
*Reload all* — to pull the new build into the frames; the dev server only
pushes into the tab it launched. Note that every frame shares one origin, so
they also share stored session state: signing in on one and reloading another
signs that one in too.

### What the browser build is for

It is a layout and interaction preview, not a shipping target. The three
things a browser genuinely cannot do are isolated behind conditional imports
(`lib/core/platform/`, `payslip_document_store_web.dart`) so the device code
is untouched:

| Behaviour | Device | Browser |
|---|---|---|
| `Platform.operatingSystem` | `android` / `ios` | `web` |
| Payslip PDF download + open | App-private storage, handed to a viewer | Declines with a message — an unencrypted payslip must not land in a shared computer's Downloads folder |
| Profile photo size check | Checked before upload | Skipped: a browser cannot stat a path |

## Architecture

Clean Architecture, three layers per feature, dependencies pointing inwards.

```
presentation  ──▶  domain  ◀──  data
   widgets          entities      models
   controllers      use cases     data sources
   providers        repository    repository impls
                    contracts
```

`domain` depends on nothing but Dart. It defines the repository *interfaces*;
`data` implements them. That inversion is what lets a use case be unit-tested
with a fake and the data layer be swapped for fixtures without the domain
noticing.

### Data flow

```
Widget
  └─ watches a Riverpod controller          (AsyncNotifier / Notifier)
       └─ calls a use case                   (business rules)
            └─ calls a repository contract   (domain interface)
                 └─ repository impl          (data layer)
                      ├─ remote data source  → ApiClient → Dio
                      └─ local data source   → secure storage / prefs
```

Errors travel the opposite way, converting exactly twice:

```
DioException ──▶ AppException ──▶ Failure ──▶ UI state
             (data source)   (repository)  (controller)
```

`ErrorMapper` owns both hops. Exceptions never cross the repository boundary:
every repository method returns `Result<T>`, so a caller cannot forget that it
might fail. Controllers turn a `Failure` into `AsyncValue.error` by throwing it
(`Result.valueOrThrow`), which is the one place that conversion happens.

### Rules this codebase keeps

* a widget never calls an API, a repository or a data source;
* `domain/` imports no Flutter package;
* `core/` never imports `features/`;
* every endpoint lives in `ApiEndpoints`, never inline;
* every colour, radius and gap comes from the theme, never a literal.

### Notable decisions

| Decision | Why |
|---|---|
| `Result<T>` rather than exceptions | Failure is visible in the signature; callers cannot silently ignore it |
| A hand-rolled `Result` rather than a functional-programming package | One obvious way to express "succeeded or failed", no extra dependency |
| `SubmissionState` for forms rather than `AsyncValue` | A form has four states; *idle* is not "loaded with no data" |
| Login has its own controller, separate from the session | A failed sign-in must not flip the provider the router watches |
| `PaginatedListNotifier` base class | Four screens page identically; one concurrency guard, not four |
| Mock data sources behind a config flag | The UI could be built before the API existed, without faking success |
| Geofencing left to the server | A client-side check is bypassable, and reporting "verified" locally would be a lie |
| Salary amounts masked by default | Payslips get read in public |
| `material_ui` package | Flutter 3.50 moved Material out of the framework; `package:flutter/material.dart` is deprecated |

## Project layout

```
lib/
  main.dart                  entry point; resolves async deps, injects overrides
  app/                       app widget, router, theme, build config
  core/                      infrastructure shared by every feature
    network/                 Dio client, interceptors, endpoints
    errors/                  Failure, Result, ErrorMapper, RepositoryGuard
    storage/                 secure storage, preferences, token storage
    services/                location, file picker, push, device info
    widgets/                 the reusable component library
    providers/               DI roots and PaginatedListNotifier
  features/
    auth/ dashboard/ attendance/ leave/ payslip/ profile/ notifications/
    grievance/
      data/                  models, data sources, repository impls
      domain/                entities, repository contracts, use cases
      presentation/          providers, controllers, screens, widgets
  shared/                    cross-feature models, helpers, the tab shell

docs/api_contracts.md        the proposed API contract
test/                        unit, provider and widget tests
```

## Features

| Module | What it does |
|---|---|
| **Auth** | Sign in with employee ID or email, auto-login, forgot/reset/change password, silent token refresh, session-expiry handling |
| **Dashboard** | Profile headline, today's attendance with check-in/out, leave balances, quick actions, recent notifications - one aggregate request |
| **Attendance** | Check in/out with optional or mandatory location, monthly summary, paginated history, per-day detail |
| **Leave** | Balances, apply with attachments and upload progress, paginated requests with status filter, detail, withdraw |
| **Payslip** | Monthly list, full earnings/deductions breakdown, PDF download to app-private storage, masked amounts |
| **Profile** | Full employee record, self-editable contact and emergency contact, photo upload, change password |
| **Notifications** | Inbox with unread filter, mark one/all read, deep links into leave, attendance and payslips |
| **Grievances** | The employee's own grievances only (HR's case screens stay on the web): list with status filter and unread dot, new/edit draft with category rules and an honest anonymity explanation, submit with a declaration, detail with status history (HR's reasons included), message thread and "Respond" when HR asks a question, delete a draft. Opened from Profile → My grievances. Written against the real API, see `docs/api_contracts.md` §10 |

## Security

* tokens live in `flutter_secure_storage` (Android Keystore / iOS Keychain),
  never in `SharedPreferences`;
* `AppLogger` redacts tokens, passwords, salary figures, national IDs and
  coordinates; `AuthTokens.toString()` and the payslip models withhold their
  own values so they cannot leak through interpolation;
* refresh is **single-flight** - ten simultaneous 401s produce one refresh -
  and a request is replayed at most once;
* a refresh that fails because the network is down does *not* sign the user
  out; only an explicit server rejection does;
* production builds must use HTTPS, enforced at start-up;
* sign-out clears tokens, the cached identity, downloaded payslip PDFs and the
  push token - a shared device keeps nothing;
* payslip PDFs go to the app-private support directory, never to Documents or
  external storage;
* only foreground location is requested; `ACCESS_BACKGROUND_LOCATION` is
  deliberately absent.

## Testing

```bash
flutter test                      # everything
flutter test test/core            # one directory
flutter test --coverage
```

123 tests covering validators, the error-mapping contract, use cases
(including every branch of the attendance location policy), the auth
repository's token and session handling, the login controller's provider
wiring, the router's access-control policy, and the login screen as a widget.

`mocktail` is used rather than `mockito`, so tests need no code generation.

`test/app_router_test.dart` doubles as a compile check: the router imports
every screen, so a type error anywhere in the presentation layer fails there.
This matters because `analysis_options.yaml` excludes generated
`.freezed.dart` files from analysis, so `flutter analyze` alone would not
catch a problem inside one.

## Code quality

```bash
dart format lib test
flutter analyze
dart fix --apply
```

`flutter analyze` is currently clean: **0 errors, 0 warnings, 0 infos**.

## Connecting the real API

1. Agree `docs/api_contracts.md` with the backend team and answer the open
   questions in §9.
2. Adjust the models under `features/*/data/models/` to match the agreed
   shapes, then re-run `build_runner`.
3. Run with `--dart-define=USE_MOCK_DATA=false` and a real `API_BASE_URL`.
4. Delete the mock data sources and `lib/shared/helpers/mock_*.dart`.

## Wiring up push notifications

The architecture is ready; no provider is chosen (contract question 6).
`PushNotificationService` is the interface and `NoopPushNotificationService`
is what ships. To enable push:

1. add the provider package, e.g. `firebase_messaging`;
2. implement `PushNotificationService` against it;
3. swap the implementation in `pushNotificationServiceProvider` - the only
   line that changes;
4. `AuthController.registerForPushNotifications()` is already called after
   sign-in and already clears the token on sign-out.

## Known gaps

* **No backend.** The contract is a proposal; mock mode is on by default.
* **Attendance location policy is a build-time flag.** It is tenant
  configuration and belongs on the server (contract question 3).
* **Leave attachments cannot be downloaded** from the detail screen. The
  contract exposes `downloadUrl` but the endpoint is unconfirmed, and an
  action that might fail is worse than one that is plainly absent.
* **Grievance files are web-only.** The mobile detail lists a case's shared
  files but cannot add or download them yet (`POST /grievances/my/{id}/attachments`
  and `GET /grievances/my/attachments/{id}` are not wired).
* **No localisation.** Strings are inline English. The app is structured so a
  `flutter_localizations` pass is mechanical.
* **Launcher icons and the logo are placeholders.** `AppLogo` draws the mark
  in code; point it at an asset when branding is supplied.
* **Android/iOS builds are unverified.** The toolchains are not installed on
  the machine this was developed on; the analyzer and the full test suite
  both pass.
