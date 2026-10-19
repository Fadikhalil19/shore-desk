# Report pick-lists (phase 2) and how they were cleaned up from MSShift

Seeded by src/Desk.Core/Data/SeedLists.cs. Admins change them in Settings > Report lists; nothing is deleted, only
switched off. Never insert or remove names in SeedLists.cs (ids are numbered in order) - change lists in the app.

## Incident types (52, from MSShift's 55)
- "Possible Bed Bugs" for OVH / SHO / SMH (+ a duplicate "OVH- Possible Bed Bugs") -> one "Possible Bed Bugs";
  the hotel is a field on the report.
- Spelling/case tidied ("Employee opened guest door with key card" -> "Employee Opened Guest Door with Key Card",
  "In house Guest not properly checked in" -> "In-House Guest Not Properly Checked In",
  "Trespassing (Warned - No Arrest)" -> "Trespassing - Warned, No Arrest").
- Kept both "Informational" and "Informational - No Crime" - ask the Director of Safety whether one is enough.

## High Level types (25)
As in MSShift; "Employee Violation-Drugs/Harassment/Liquor" became three entries.

## TO CHECK against MSShift (only part of these lists was read on 2026-10-16)
- Injury types: MSShift has 67, many free-typed duplicates. Seeded a clean starter set of 16 - compare.
- Contributing factors: MSShift has 65; 17 were noted. "Fall or Slip" -> "Slip/Fall". Add the rest that are real.
- Colors: MSShift has 28; seeded 24 common colors.
- Vehicle circumstances: MSShift list was partly read; seeded 8.
- Body parts (24) and body part side/area (11) match MSShift.
- Property categories (30) and evidence status (4) match MSShift.
