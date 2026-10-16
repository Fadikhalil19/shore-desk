# Shore Desk app (desk.shorehotelapps.com) - project brief

Claude builds the code; Fadi Khalil (fadikhalil@shorehotel.com) reviews, deploys and owns it. Fadi is not a developer.
Started 2026-10-16 from the contract-app session. NOTHING BUILT YET - next step = Fadi approves the plan, then Phase 1.

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
- MSShift data export: Fadi will ask support (support@msshift-ssw.com) "in time". Fallback = read records via Chrome
  only with Fadi's explicit OK (slow, may break MSShift terms).
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
