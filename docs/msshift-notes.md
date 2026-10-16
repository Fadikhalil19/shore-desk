# MSShift walkthrough notes (read 2026-10-16, read-only; field names and lists only, no incident content)
See [[it-safety-app-plan]]. Field names/lists only, no incident content.


Modules in MSShift: Event (dispatch/quick log), Incident, Keys, Visitors, Lockers, Parking, Patrol, Handover
(Morning/Afternoon/Overnight), On Duty, Lost & Found (+ ForgotMyItem guest shipping), Packages (in/out), Admin,
Support, Devices, Training, Images. Home: weather, latest incidents, today's handover, contacts.

## Incident module
Menu: Report New Incident | Today's Incidents | Quick Incident Search | Detailed Incident Search
Report forms (5): Incident Report (IR), Guest Accident/Illness (GAI), Employee Accident/Illness (EAI),
Property Missing/Damaged (MD), High Level Incident Report (HI). Also "Mobile Reports" pending completion.

Report Type (category, 55): Alleged Bug Bites, Blackout/Power Outage, Bugs in Room, Burglary, Chemical Fumes, Chemical Spill,
Cyber Security Breach, Damaged Property, Defrauding an Innkeeper - Arrest, Drill, Earthquake, Elevator Entrapment,
Employee opened guest door with key card, Employee Violation - Altercation / Key Control / Other / Traffic-Parking /
Unauthorized Entry, Eviction - Non-Registered Guest / Registered Guest, Fire, Fire/Smoke Detector Alarm, Flood, Gasoline Leak,
Guest Altercation, Guest Harassing/Abuse to Staff/Other, Guest Incident, Guest Intoxication, Guest Traffic/Parking Violation,
In house Guest not properly checked in, Informational, Informational - No Crime, Missing Money, Missing Person - Adult / Child,
Natural Gas Leak, Noise Disturbance, Possible Bed Bugs (OVH / SHO / SMH; one duplicate "OVH- Possible Bed Bugs"),
Possible Missing Friend of Guest, Property Missing / Damaged Property, Safety Hazard, Scam/Fraud, Smoking in Guest Room,
Smoking in Unauthorized Area, Suspicious Odor, Trespassing (Warned - No Arrest), Trespassing - Arrest, Unknown Substance,
Unpaid/Defrauding an Innkeeper, Vandalism, Welfare check, Welfare Check In Room
Status: Pending, Closed, Locked, Deleted
Dates: Incident date, Reported date, Written date. Shift reported: Morning / Afternoon / Overnight
Location Category (21): BPT, Guest Room, IDF, Lost and Found, OVH, Remote, Server Room, SH A Building 1st-4th Floor,
SH B Building 1st-4th Floor, SH Garage, SH Hallway, SH Mezzanine, SH Parking Structure, SH Property, SMH, Type Location
+ Location Detail (free text) + Subject Room #
Subjects: Main subject vs Non subject (other people involved); type Guest / Non Guest / Employee / Hotel Property;
first/last name, department, associate (user), key name. Lock interrogation: key used by employee / guest / any lock result.
Departments (21): AAA Parking-Valet, Accounting, Admin, Admin-Ownership, Finance, Human Resources, IT, OVH-Ocean View Hotel,
Reservation, Revenue Management, Safety, Sales, SHO-Concierge, SHO-Engineering, SHO-F&B Kitchen, SHO-F&B Mini Bar,
SHO-F&B Restaurant, SHO-Guest Services, SHO-Housekeeping, SHO-Laundry, SMM-Santa Monica Hotel
Reporting parties: Reported By, Written By, Reviewed By, Forward To, Witnessed By; type Guest / Non Guest / Employee

## Admin (read only)
Admin menu: Category, Users, Schedule, Attendance, Reports, Recap, Labels.
Category tabs: Event, Location, Incident, Accident, Vehicle, Found, D, Keys, Visit, Pkg 1/2, Pkg Storage, Carr, Post, CO, Patrol, Color, P.
- Event categories (daily log, ~90): Administrative, Alarm Activation, Call Off, Cybersecurity, Desk Duty, Door Lock Assist,
  Elevator, Employee Assist, Escort, Eviction, Field Contact, Fire Command Station, Fire Drill, Fire Extinguisher, Fire Watch,
  Guest Assist, Guest Warning, Hard Key, Hazards, Investigation, Key Concern-Employee, Lock Interrogation, Lockout, Lost and Found,
  Medical, Meeting, Monthly Inventory, Officer, Orange Safety Card, Packages & Mail, Parking, Safe Assist, Safety, Vendor,
  Water Leak/Plumbing, Wellness Check, Wheel Chair ... (several near-duplicates to clean up)
- Incident: Incident Report types (55, as above); Accident/Illness CONTRIBUTING FACTORS (65: Fall or Slip, Trip/Fall, Slippery floor,
  Uneven Concrete, Lack of PPE, Unsafe Work Practice, Unsafe Working Condition, Push/pull/lift, Struck by Object, Chemical,
  Inhalation, Ingestion, Medical Condition, Weather, Weapon, Motor Vehicle Collision, Needle/Sharp, ...);
  High Level Incident types (25): Assault, Assault With Weapon, BOLO, Bomb Threat, Burglary, Computer room Breach,
  Death/Suicide/Murder, Defrauding Innkeeper-Arrest, Domestic Assault, Employee Violation-Drugs/Harassment/Liquor, Fire Alarm,
  Fraud, Guest Harassing/Abuse, Identity Theft/Fraud, Phone call impersonation, Robbery, Sexual Assault, Sexual Offense,
  SMS impersonation, System Breach, Theft (over $950), Threats/Harassment, Trespassing (Arrest)
- Accident: Injury Type (67, many free-typed duplicates), Body Part (24: Ankle..Wrist), Body Part Location (11: Back, Both, Face,
  Front, Head-Top, Left, Left Dominant, Lower, Right, Right Dominant, Upper)
- Location: 2 levels, area (20) -> detail spots (e.g. SH A Building 1st Floor 33, OVH 30, IDF 9).
Users: by Department (21); level Associate / Super / Dir / Admin; work shift; employee #; users inherit department permissions.
Permissions: module (Event Log: Events/Incident/Keys/Visitors/Visitors Schedule/Handover/On Duty/Sick Log/Patrol/
  Employee Lockers/Employee Parking; Lost & Found: Lost/Found/Todays/Storage/Disposal/Ship; Packages: In/Out/Todays/Storage/Request)
  x department or user x Read / Write / Admin.
Reports: Shift Summary (shift times e.g. Afternoon 13:00-21:00, associates on duty, incidents/events/keys/found/lost counts),
  Events, By Officer, Incidents (by shift/date/officer, chart by type), Qtr Incidents, Keys, Visitors, Patrols, Lost & Found,
  Schedule, Incoming/Outgoing Packages. Volume: 4 incidents Oct 1-16 2026.
NOT SEEN YET: the report forms themselves (narrative, actions taken, photos, signatures, police/EMS numbers) - opening
  "Report New Incident" may create a record; Fadi to open one himself or get a blank PDF / data export from MSShift.

## FORM: Incident Report (IR) - read 2026-10-16 (blank form URL .../IncidentReport/IncidentReport/IncidentReport; opening does NOT create a record; only "Save Report" does)
Header: hotel name + address. Sections:
- Heading: Incident Type* (55 list), Other Type, Incident Heading* (short title)
- Time & Location: Incident Date & Time*, Incident Location* (area list), Location Detail* (spot list per area / free text)
- Reported to Security By: Reported Date & Time*, Reported By Type* (Guest/Non Guest/Employee), First/Last name, Room#,
  Dept, Associate (user)
- Subject: Subject Type* (Guest/Non Guest/Employee/Hotel Property/N/A), F/L name, Department, Associate, Hotel Property,
  Room #, Arrival Date, Departure Date, Group/Company, Email
- Subject Address: Home street*/city*/state*/zip*/country*/phone*; Work street/city/state/zip/country/phone (address autocomplete)
- Incident Narrative - Statement of Facts* (char counter); Investigation/Follow Up (char counter)
- Yes/No switches that open sub-blocks:
  Witnesses -> 2 witnesses: name, phone, address
  Vehicle -> circumstance (list: Abandoned, Hit and Run, Stolen, Towed, Vehicle Break-in, Damaged by Guest/Associate, ...),
     other circumstances, operator name/DOB/DL/phone, owner name/address/phone, make, model, year, color, plate/state, VIN,
     insurance co, rental co, valet ticket #, nature & extent of damage, description
  Lock Interrogation -> up to 4 rows: used by type (Guest/Employee), key name, date & time used, department, associate, name
  Pictures/Video -> qty, date & time, taken by dept/associate/other; video date & time, description
  Evidence -> status (In Storage / Sent to Claims / Sent to Attorney / Destroyed)
  Reported to Claims -> reported date & time, claim #
- Written By* (user) / other name; Supervisor* (reviewer list: supervisors) / other name; Report Distributed To (list)
- Button: Save Report. No file/photo upload seen on the form (photos may be via "Images" module).

## Other 4 forms (blank-form URLs under /d/EventLog/IncidentReport/: LiabilityIncident/LiabilityIncident,
## AidedAssociate/AidedReport, MissingDamage/MissingDamageProperty, Arrest/Arrest_Report; *_View = view existing)
Common to all: heading, date/time, location+detail, reported-by, subject, home/work address, narrative*, investigation/
follow-up, witnesses(2), vehicle block, pictures/video, evidence (item, location stored, date, disposition), claims (date, claim #),
written by*, supervisor*, distributed to. All share the Save button only.
- Guest Accident/Illness (GAI): type Accident/Illness; detail Incident Only/Medical; injury type (68), other injury, body part*,
  body part location*; subject type Guest/Non Guest/Pedestrian, gender, DOB+age, room, arrival/departure;
  EMS? (respond time, EMS name, EMS #, hospital) + RMA (refused medical attention) Y/N; Police? (date/time, officer name,
  shield #, case #); Contributing factors? (main factor list 66, other, consumed date, symptoms description). No lock block.
- Employee Accident/Illness (EAI): as GAI but subject = employee: department*, employee (user)*, position*, date of hire,
  DOB/age, employee #; reported by dept/name; + Safety shoes? (brand, date purchased). (Workers' comp relevant.)
- Property Missing/Damaged (MD): type Missing/Damaged Property; up to 4 items: category (30: Alcohol, Appliance, Art, Baby,
  Beauty/Hygiene, Bedding, Books, Cell Phone, Clothing, Computer, Dishware, Dog & Cat, Electronics, Glasses, Jewelry, Luggage,
  Medication, Misc, Money, Musical Equipment, Office Supplies, Passport, Personal Items, Shoes, Sport, Toys, Weapons, Vehicle,
  Hotel Property, Other), detail, description, primary color (28), brand, model/serial, claim value $; total claim value;
  police; lock interrogation (4 persons); etc.
- High Level (HI): report type (25 list); SUBJECT detail: gender, race, DOB/age, height, weight, eyes, hair, clothing worn,
  DL # + state, scars/marks/tattoos, email, address; VICTIM? same description fields + address; hotel property involved?
  (4 items as MD); police; witnesses; vehicle; pictures; evidence; claims.
Design note: build ONE incident record with a form type + optional sections, not 5 separate tables.

## Other modules (read 2026-10-16)
- EVENT LOG (daily dispatch log): tabs Report New Event / Today's Events / Quick + Detailed Search / Task Reminder.
  Fields: category* (91) -> detail*, location type* -> location detail*; Requested by* (Guest/Non Guest/Employee/Visitor/N/A)
  + request time*, dept, associate, name; Relates to (same types, room #, name); Assigned to: event start*, event complete,
  priority* (Low/Medium/High/Highest), department*, on-duty employee*; narrative. Push notification toggle.
  Event status shown in reports (open/complete).
- KEYS: Assign New Key / Today's Keys / Keys Alert (out > 10 hrs) / Quick Key Search / Shift Inventory / Inventory Search.
  Key list (Officer Ring 1/2, Supervisor Ring 3, Master Cabinets 1-4, SSM, DOS, 001...), key barcode scan, assigned to type
  (Employee/Contractor), dept, associate (+ barcode), date/time, status (Back In/Signed Out/Missing/Broken),
  SIGNATURE PAD capture, narrative.
- VISITORS: New Visitor / Today's / Alert (in > 8 hrs) / Quick Search / ID Badge / ID Setup / Schedule a Visit / Today's
  Schedule / Quick Schedule Search / Kiosk Log. Fields: photo (webcam, scan ID), company (add new), visitor name, type
  (Visitor/Contractor/Owner/Member), status (Check In/Out), barcode, time in/out/expiry, area visiting + detail,
  badge color + #, phone, email, profile notes, "in company of" up to 5 more people with badges; narrative. Prints badges.
- LOCKERS: Assign New Locker / Mens / Womens / Admin. Locker list, lock code, lock serial, status Vacant/Occupied,
  date, dept, associate, assigned by.
- PARKING: Assign New Parking / Employee Parking / Motorcycle Parking. Space list (shows holder), vehicle make/model/year/
  color/plate/state, status Vacant/Occupied, assign-to type, dept, associate, parking sticker #.
- PATROL: Today's Patrols / Quick Patrol Search / Quick Problem Search; by shift/day; checkpoints via beacons/NFC tags +
  mobile app (none reported recently - ask if used).
- HANDOVER: per shift + date: general comments & notifications, and items "marked" for handover from each module
  (incidents, events, found, lost, keys, visitors, call outs, packages in/out); Handover Comments tab; printable.
- ON DUTY: add employee to shift (employee, radio #, post, start, end); today's roster by shift with counts;
  Call Out Log (sick calls) + search.
- LOST & FOUND: Found / Lost / Today's / Ship / Matches / Storage / Disposal. Found item: category (30) -> detail,
  description, location found, registered guest, color, brand, model/serial, found by (type, dept, associate), wants item?,
  received by, stored in, purge date, narrative. ForgotMyItem = guest pays + ships item (external service, inquiries).
- PACKAGES: Incoming / Outgoing / Today's / Ship / Request / Storage / Reports. Incoming: date received*, carrier*, package
  type*, tracking #*, total pkgs*, weight, handling fee; recipient (name*, type Guest/..., room, company, arrival/departure,
  convention name/date); sender; notified (type, to, dept, associate); received by, location stored*; comments.
- ADMIN extras: Schedule, Attendance, Recap, Labels (barcode/badge labels), Users Report, Users Login (login history).
- Home page: weather, latest incidents, today's handover, training videos, contacts directory.
