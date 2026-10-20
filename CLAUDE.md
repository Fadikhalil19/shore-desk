# Shore Desk app (desk.shorehotelapps.com) - project brief

Claude builds the code; Fadi Khalil (fadikhalil@shorehotel.com) reviews, deploys and owns it. Fadi is not a developer.
Started 2026-10-16 from the contract-app session. Plan approved 2026-10-16 ("start phase 1"). PHASE 1 CODE BUILT 2026-10-16
(see "Phase 1 status" below); LIVE 2026-10-19 (Fadi only). PHASE 2 STARTED 2026-10-19.

## Source docs (Claude Docs; read with the Claude Docs connector, never web-fetch)
- PLAN (the spec, approve before building): https://claude.ai/code/artifact/03f4bee4-f9c8-4f8f-aaa9-05fa92900d11
- MSShift walkthrough notes (fields, lists, forms, permissions, reports): docs/msshift-notes.md in this folder

## What we are building
One in-house web app that replaces ALL of MSShift (hotel security SaaS, https://msshift-security-2.com) and adds IT
and inventory. Fadi's decisions (2026-10-16):
- Address desk.shorehotelapps.com (not it./ops.). Code folder C:\Desk_app (this folder), data folder C:\DeskData.
- Replace the WHOLE MSShift package: incident reports (5 forms: Incident, Guest Accident/Illness, Employee
  Accident/Illness, Property Missing/Damaged, High Level), event log, keys, visitors, lockers, parking, patrol,
  handover, on duty (+ call-out log), lost & found (+ shipping), packages, safety reports. Copy fields/steps only -
  never MSShift code, branding or look. Clean up duplicate/misspelled list entries.
- IT: tickets opened by email to a new shared mailbox (itsupport@), IT security incidents, IT equipment inventory.
- Department inventory: each department (engineering/maintenance, safety, housekeeping, any other) keeps its own stock.
- ACCESS "as we choose": admin screen in the app, per person x module x level (None/View/Edit/Manage), limited by
  department (and hotel if useful), department defaults, separate level for High Level + employee injury reports.
  Entra only gates sign-in: groups DESK-Users + DESK-Admins. Disable/Remove like the contract app.
- Officers use WORK CELL PHONES (iPhone/Android unknown) -> phone-first web app, no app store.
- Patrol uses TAGS, used depending on the situation: each NFC tag holds a URL (desk.shorehotelapps.com/p/<checkpoint>)
  so it works on iPhone and Android with no app. Web NFC is Android-Chrome-only - don't rely on it.
- Lost-item shipping: guest may pay or not -> track paid / no charge / waiting; NEVER take or store card data.
- Hardware (scanner, signature pad, badge printer, kiosk): unknown - design to need none (USB scanner = keyboard,
  finger signatures, browser printing).
- MSShift data export: Fadi decided 2026-10-20 NOT to ask MSShift; "export as much as you can by yourself" = explicit
  OK to read records via Chrome (Fadi signed in; slow; may go against MSShift terms - he was told). Check MSShift's own
  report/export screens first (ask before each download). Extracted data ONLY under C:\DeskData\import\msshift (locked),
  never in Git or chat. Read-only in MSShift: never create, edit or delete anything there.
- MSShift contract: Fadi thinks it was recently renewed; find renewal/notice date, add it to the contract app.

## Build order (from the plan)
1 Foundation (sign-in, access screen, departments/hotels/locations, activity log, backup, Cloudflare route)
2 Safety core (incident reports, event log, handover, on duty, reports) -> gate: 2-3 officers test in both apps
3 Desk tools (keys, visitors, lockers, parking, lost & found, packages, patrol)
4 Switch over (import MSShift history, run side by side, cancel MSShift at its notice date)
5 IT (tickets, IT incidents, IT equipment) - can move earlier if urgent
6 Department inventory

## Copy the contract app's proven setup (C:\Contract_management - read it, never change it from here)
- Stack: ASP.NET Core (net10) minimal APIs + EF Core + SQL Server Express + React/TypeScript (Vite) + xUnit tests on a
  temp SQL DB. Separate scheduler Windows service for emails/alerts. Look at its CLAUDE.md, scripts\Publish.ps1,
  scripts\Install-Services.ps1, scripts\Backup-App.ps1, src\Contracts.Api\Auth (sign-in, SessionCheck, CSRF header),
  Endpoints\AdminEndpoints.cs (Disable/Remove), Services\ActivityLogger.cs, HealthReport.cs.
- Sign-in = server-side OIDC auth-code + PKCE (Microsoft.Identity.Web), cookie, no tokens in the browser, default-deny.
- Every view/edit/download goes to an activity log. Nothing hard-deleted; destructive deletes need type-the-name.
- Pre-live: all outgoing email redirected to Fadi until he says live.
- Desk app gets its OWN: repo (private GitHub, ask Fadi to create), DB "Desk", services ShoreDeskWeb/ShoreDeskScheduler
  on its own port, Entra app registration, Cloudflare Access app, shared mailboxes (desk@ out, itsupport@ in).

## Environment (shared server)
- Server SHO-CONTRACT, 10.1.22.4, Windows Server 2019, WORKGROUP, 23 GB RAM, ~380 GB free on C:. Claude's shell is NOT
  admin: admin steps go to Fadi one at a time. SQL Server 2025 Express localhost\SQLEXPRESS (Windows auth, Fadi sysadmin;
  sqlcmd needs -S localhost\SQLEXPRESS -E -C). .NET SDK 10, Node 24, Git.
- Cloudflare: tunnel "contracts-server" (service cloudflared) already runs here; add a published-application route
  desk.shorehotelapps.com -> https://localhost:<port> (TLS verify off for self-signed). Zero Trust free = 50 seats
  TOTAL across both apps. Zone shorehotelapps.com is on Fadi's Cloudflare; shorehotel.com DNS stays at GoDaddy.
- M365 tenant "Shore Hotel" c7319400-c050-406d-b027-2eb54f8b6f90, Entra ID Free (no group app assignment -> app
  enforces groups itself). Company domains: shorehotel.com, oceanviewsantamonica.com, santamonicahotel.com.
- Hotels: SHO (Shore Hotel, 1515 Ocean Ave, Santa Monica), SMH (Santa Monica Hotel), OVH (Ocean View Hotel).
- Chrome on this server has the Claude extension (deviceId e32fafa0...). Never type passwords; Fadi signs in himself.
- Hotelsheet (hotelsheet.shorehotelapps.com) is unrelated - don't touch.

## How Fadi likes to work
- Plain language, explain the why. One step at a time for anything he clicks or types; wait for his OK.
- Long material goes in a Claude Doc, not the terminal. Recommendation with the reason, not a survey.
- Do it yourself when you can; only hand him steps needing admin or a dashboard login.
- After each change: commit + push without being asked. Never commit data or secrets (they live under C:\DeskData).
- Risky operations: say what could go wrong, keep a rollback, verify afterwards. Label prices/limits from memory
  "approximate". Never paste secrets into chat. PowerShell 5.1: keep scripts ASCII, write files UTF-8.
- Incident data is sensitive (injuries, DOB, addresses): test with made-up data, never real MSShift records in Git.

## Phase 1 status (built 2026-10-16)
- Layout: src/Desk.Core (EF model, migrations in Data/Migrations, Access/Modules.cs = the 18 module keys incl. 2
  sensitive ones, Access/AccessRules.cs = the access rules), src/Desk.Api (minimal APIs, Auth/, Endpoints/),
  src/Desk.Scheduler (heartbeat + first sign-in email, Outbox mode), src/Desk.Web (React; npm run build -> Desk.Api/wwwroot),
  tests/Desk.Api.Tests (xUnit; ApiTests run the real app on a temp SQL DB DeskTest_* with a header-based test sign-in).
  Run `dotnet test Desk.slnx`. EF: `dotnet ef ... --project src/Desk.Core --startup-project src/Desk.Api`
  (DesignTimeFactory, so ef never needs the secret).
- ACCESS RULES: DESK-Admins = Manage everything. Otherwise per module: a personal setting (UserAccess row) REPLACES the
  department default (DepartmentAccess row) for that module; none -> no access. Limits: hotel (null = all) and, for
  department-split modules (it-tickets, inventory), All / Own / Specific department. New module endpoints use
  .RequireModule(Modules.X, AccessLevel.Y) and check EffectiveAccess.Allows(level, hotelId, departmentId) per record.
- DB "Desk" CREATED 2026-10-16 on localhost\SQLEXPRESS (migration Initial applied by Claude's account). Seeded lists +
  cleanup notes: docs/phase1-lists.md. C:\DeskData\{files,secrets,keys,status,outbox,backup} created.
- Port 5444 (contract app 5443). Cookie "desk", 12 h. Cert CN=desk.shorehotelapps.com (made by Install-Services).
- Entra groups DESK-Users + DESK-Admins CREATED 2026-10-16 (Security, assigned; Fadi only member of Admins); their IDs are in
  appsettings.json App:Groups. Entra app "Shore Desk" REGISTERED 2026-10-16 (single tenant, Web redirects /signin-oidc +
  /signout-callback-oidc on desk.shorehotelapps.com, groups claim = Security groups, client ID in AzureAd:ClientId).
  Client secret "Shore Desk server" saved 2026-10-16 by Fadi (Set-EntraSecret.ps1 -> C:\DeskData\secrets).
  SECRET EXPIRES 2028-10-15 - sign-in breaks then; renew before (add to contract app reminders).
- Scripts: Publish.ps1 (tests, web build, `ef database update`, publish to C:\DeskApp, restart), Install-Services.ps1
  (ADMIN, once: creates local svc-desk, ACLs, cert, SQL login with db_datareader/writer on Desk only, services
  ShoreDeskWeb/ShoreDeskScheduler, operator start/stop rights), Backup-App.ps1 + Register-BackupTask.ps1 (task
  "Shore Desk Backup" 2:30 am -> \\10.1.11.243\d$\Backup\shoreapps\desk, 14 days; failure email only once a desk
  mailer exists, else App health shows it). Separate from the contract app's backup on purpose.
- Scheduler RedirectAllTo = fadikhalil@ (pre-live rule). Mode Outbox -> C:\DeskData\outbox until desk@ + mailer app exist.
- Security note for the contract app (not changed from here): EF SqlServer pulls System.Security.Cryptography.Pkcs
  9.0.11 (known high-severity advisory); Desk pins 10.0.13 in Desk.Core.csproj. Contract app probably needs the same.
- GitHub remote connected and pushed 2026-10-16: https://github.com/Fadikhalil19/shore-desk.
- Services ShoreDeskWeb + ShoreDeskScheduler INSTALLED + running 2026-10-16 (health 200, heartbeat ok).
- Cloudflare DONE 2026-10-16: Access app "Shore Desk" (Entra login only, instant auth, 24 h) with reusable policy
  "Desk users (allow list)" = emails, only fadikhalil@ so far (add officers there; seats shared with contract app);
  tunnel contracts-server route desk.shorehotelapps.com -> https://localhost:5444, TLS verify off.
- FIRST SIGN-IN OK 2026-10-19: Fadi via desk.shorehotelapps.com -> Cloudflare -> Entra -> app shows "(admin)".
- Backup task "Shore Desk Backup" REGISTERED 2026-10-19 (runs as Fadi); test run OK to the share.
- PHASE 1 COMPLETE 2026-10-19. Leftovers: location spots,
  SMH/OVH addresses.
- C:\DeskData and C:\DeskApp do NOT inherit C:\ (default C:\ lets every signed-in account modify subfolders):
  only SYSTEM, Administrators, Fadi (+ svc-desk / SQL service where needed). DeskData locked 2026-10-16.
- Gotcha: Git Bash heredocs on this server mangle backslashes - write JSON/PS1 with the Write tool.

## Phase 2 status (started 2026-10-19)
- INCIDENT REPORTS built 2026-10-19: one IncidentReport record for all 5 forms (Desk.Core/Incidents). Searchable/report
  fields are columns; optional sections (people, vehicle, police, injury, items, ...) are IncidentDetails JSON in
  DetailsJson, re-validated on save (list ids, user/department ids, sizes). IncidentRules = module per form, shift
  times (5-13 / 13-21 / 21-5), what is missing before submit, who may edit/submit/review/reopen/void.
- Flow: Draft -> AwaitingReview (to the chosen supervisor = Manage in that module+hotel) -> Approved (locked).
  Return needs a note; reopen needs a reason; void needs reason + typed number. A non-admin cannot approve own report.
  Others' reports answer 404. Number = YYYY-NNNN per Pacific year.
- Files: C:\DeskData\files\incidents\<id>\<random name>; allow-list of types in Services/FileStore.cs; removal hides.
- Pick-lists: ListItems table (ListKeys in Desk.Core/Data/ListItems.cs), Settings > Report lists. Notes and lists still
  to check against MSShift: docs/phase2-lists.md.
- AppUser.IsAdmin = copy of DESK-Admins at last sign-in (lets admins be listed as supervisors).
- Web: src/Desk.Web/src/pages/incidents (IncidentList, IncidentEdit, IncidentView, fields, meta). Print view prints the
  hotel address header.
- EVENT LOG built 2026-10-19: SecurityEvent + EventReminder (Desk.Core/Events, EventRules), /api/events, web pages/events.
  Open until CompletedLocal set; creator/assignee/manager edit, anyone with Edit completes, manager voids (reason).
  Reminders: daily ones move to next day when done. Event categories = list "event-category" (38, appended to SeedLists).
- Shared helpers: Endpoints/Shared.cs (PacificNow, Clean, Bad); CurrentUser.LevelAtAsync(module, hotelId).
- HANDOVER built 2026-10-19: Desk.Core/Handover (HandoverNote, HandoverRead, HandoverRules: ShiftOf/Window/Previous;
  Overnight 21-05 belongs to the date it started). /api/handover?hotelId&date&shift: notes + incidents/events with
  ForHandover in that shift window (each list only if the person may see that module) + open events + read marks.
  Flag: POST /api/incidents/{id}/handover and /api/events/{id}/handover {flag} (Edit level). Web pages/Handover.tsx.
  Later modules (keys, found items, call-outs) add their own ForHandover lists here.
- ON DUTY built 2026-10-19: Desk.Core/OnDuty (DutyAssignment, CallOut, OnDutyRules: reasons list, times must fit
  the shift). /api/onduty (roster per hotel+shift, copy another shift's roster, call-outs + 30-day search).
  Call-outs show on the handover (for people with onduty View). Web pages/OnDuty.tsx; shift helpers src/shifts.ts.
- SAFETY REPORTS built 2026-10-20: /api/reports (shift summary default = the shift that just ended; incident and
  event statistics by type/form/shift/officer/month/location/status; CSV export for Excel, formula-guarded, no
  narratives). Every figure only counts what the person may see (IncidentEndpoints.VisibleTo shared with search).
  Web pages/SafetyReports.tsx (single-series bar lists, stat tiles, quarter presets, print).
- PHASE 2 CODE COMPLETE 2026-10-20 (100 tests). Gate before phase 3 (2-3 officers test alongside MSShift) POSTPONED
  by Fadi 2026-10-20: "start phase 3, I won't test anything yet". Officer testing still needed before switch-over.

## Phase 3 status (started 2026-10-20)
- Order: keys, visitors, lockers, parking, lost & found (+ shipping), packages, patrol.
- KEYS built 2026-10-20: Desk.Core/Keys (HotelKey, KeyCheckout with PNG finger signatures in the DB, KeyStatusChange,
  KeyCount + lines, KeyRules: 10 h alert, status changes). /api/keys; web pages/Keys.tsx (scan box = USB scanner types
  + Enter, board, history, shift count, key list for managers). Reusable src/SignaturePad.tsx. Keys not In -> handover.
  Key list is empty: add keys (or bring them from MSShift).
- VISITORS built 2026-10-20: Desk.Core/Visitors (Visit with companions JSON + badge photo under files/visitors/<id>,
  ScheduledVisit; 8 h alert). /api/visitors; web pages/Visitors.tsx (on property / today / booked / search, check-in form,
  visit page with photo from camera, printable badge /visitors/:id/badge). Visitors still in -> handover.
- MSSHIFT READ-OUT: tools/msshift-export/incidents.js (in-page script, counts-only progress, one JSON download per form);
  files go to C:\DeskData\import\msshift (moved out of Downloads at once). Importer not written yet (phase 4).
