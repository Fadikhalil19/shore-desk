# Starting lists (phase 1) and how they were cleaned up from MSShift

Seeded by src/Desk.Core/Data/SeedData.cs. Admins change them in Settings; nothing is deleted, only switched off.

## Hotels
SHO Shore Hotel (1515 Ocean Ave, Santa Monica, CA), SMH Santa Monica Hotel, OVH Ocean View Hotel.
SMH and OVH addresses are blank - Fadi to fill in (Settings > Hotels); they print on incident report headers.

## Departments (19, from MSShift's 21)
Accounting, Admin, Concierge, Engineering, F&B Kitchen, F&B Mini Bar, F&B Restaurant, Finance, Guest Services,
Housekeeping, Human Resources, IT, Laundry, Ownership, Parking and Valet, Reservations, Revenue Management, Safety, Sales.
- "SHO-Engineering", "SHO-Housekeeping" etc. -> one department each; the hotel is set on the person.
- "OVH-Ocean View Hotel" and "SMM-Santa Monica Hotel" were hotels listed as departments -> dropped (they are hotels).
- "Admin-Ownership" -> Ownership. "AAA Parking-Valet" -> Parking and Valet (AAA is probably the valet company; check).
- Phase 4 import maps the old MSShift names onto these.

## Location areas (21, from MSShift's 21 "Location Category")
SHO: A Building 1st-4th Floor, B Building 1st-4th Floor, Garage, Hallway, Mezzanine, Parking Structure,
Property (grounds), BPT. SMH: Santa Monica Hotel. OVH: Ocean View Hotel.
Every hotel: Guest Room, IDF, Server Room, Lost and Found, Remote.
- "SH " prefix dropped (the hotel is a field). "Type Location" dropped (looked like a placeholder).
- To check with the Director of Safety: are "Garage" and "Parking Structure" the same place? What is "Remote"?
- The spots inside each area (e.g. "SH A Building 1st Floor" had 33, OVH 30, IDF 9) were not copied yet:
  add them in Settings > Locations or bring them with the MSShift import.
