// Shapes returned by the Shore Desk API (see src/Desk.Api/Endpoints).

export type Level = 'None' | 'View' | 'Edit' | 'Manage'
export type DepartmentLimit = 'All' | 'Own' | 'Specific'

export interface ModuleInfo {
  key: string
  name: string
  group: string
  phase: number
  description: string
  departmentScoped: boolean
  sensitive: boolean
}

export interface ModuleAccess extends ModuleInfo {
  level: Level
  hotelId: number | null
  departmentId: number | null
  source: 'Admin' | 'Person' | 'Department' | 'None'
}

export interface Me {
  id: number | null
  name: string
  email: string | null
  isAdmin: boolean
  departmentId: number | null
  hotelId: number | null
  modules: ModuleAccess[]
}

export interface Hotel { id: number; code: string; name: string; address: string | null; isActive: boolean }
export interface Department { id: number; name: string; isActive: boolean }
export interface LocationSpot { id: number; name: string; sortOrder: number; isActive: boolean }
export interface Area { id: number; hotelId: number | null; name: string; sortOrder: number; isActive: boolean; locations: LocationSpot[] }

export interface ListInfo { key: string; name: string; description: string }
export interface ListItem { id: number; listKey: string; name: string; sortOrder: number; isActive: boolean }

export interface Lookups {
  hotels: Hotel[]; departments: Department[]; areas: Area[]; modules: ModuleInfo[]
  lists: ListInfo[]; listItems: ListItem[]
}

export interface Person { id: number; name: string; departmentId: number | null; hotelId: number | null }

export interface AdminUser {
  id: number
  entraObjectId: string
  name: string
  email: string | null
  jobTitle: string | null
  departmentId: number | null
  hotelId: number | null
  lastSeenUtc: string
  firstSignInUtc: string | null
  blockedUtc: string | null
  removedUtc: string | null
  personalSettings: number
}

export interface Grant { level: Level; hotelId: number | null; departmentLimit: DepartmentLimit; departmentId: number | null }

export interface ModuleRow { module: string; personal: Grant | null; departmentDefault: Grant | null; effective: ModuleAccess }
export interface PersonAccess { person: AdminUser; departmentName: string | null; modules: ModuleRow[] }
export interface DepartmentRow { module: string; grant: Grant | null }

export interface ActivityEntry {
  id: number
  utc: string
  userId: number | null
  userName: string
  action: string
  module: string | null
  entityType: string | null
  entityId: string | null
  details: string | null
  ipAddress: string | null
}
export interface ActivityPage { total: number; items: ActivityEntry[] }

export interface SignInPerson {
  id: number; name: string; email: string | null; role: string | null; firstSignInUtc: string | null
  lastSignInUtc: string | null; signIns: number; lastIp: string | null
}
export interface SignInReport { people: SignInPerson[]; events: ActivityPage }

export interface HealthCheck { area: string; name: string; status: 'Ok' | 'Warning' | 'Problem' | 'Info'; message: string; detail: string | null }
export interface HealthReport { checkedUtc: string; checks: HealthCheck[] }

// --- Incident reports (see src/Desk.Core/Incidents) ---

export type IncidentForm = 'Incident' | 'GuestInjury' | 'EmployeeInjury' | 'PropertyMissingDamaged' | 'HighLevel'
export type IncidentStatus = 'Draft' | 'AwaitingReview' | 'Approved' | 'Voided'
export type Shift = 'Morning' | 'Afternoon' | 'Overnight'

export interface Address { street?: string; city?: string; state?: string; zip?: string; country?: string }
export interface PersonDescription {
  race?: string; height?: string; weight?: string; eyes?: string; hair?: string; clothing?: string
  licenseNumber?: string; licenseState?: string; marks?: string
}
export interface PersonEntry {
  type?: string; firstName?: string; lastName?: string; userId?: number | null; departmentId?: number | null
  position?: string; employeeNumber?: string; dateOfHire?: string; room?: string; arrivalDate?: string
  departureDate?: string; company?: string; phone?: string; email?: string; gender?: string; dateOfBirth?: string
  hotelProperty?: string; home?: Address; work?: Address; description?: PersonDescription
}
export interface VehicleEntry {
  circumstanceId?: number | null; otherCircumstance?: string; make?: string; model?: string; year?: string
  colorId?: number | null; plate?: string; plateState?: string; vin?: string; valetTicket?: string
  operatorName?: string; operatorDateOfBirth?: string; operatorLicense?: string; operatorPhone?: string
  ownerName?: string; ownerAddress?: string; ownerPhone?: string; insuranceCompany?: string
  rentalCompany?: string; damage?: string; description?: string
}
export interface LockEntry { usedByType?: string; keyName?: string; usedLocal?: string; departmentId?: number | null; userId?: number | null; name?: string }
export interface MediaEntry { pictureCount?: number | null; picturesTakenLocal?: string; takenBy?: string; videoLocal?: string; videoDescription?: string }
export interface EvidenceEntry { statusId?: number | null; items?: string; storedLocation?: string; date?: string; disposition?: string }
export interface ClaimEntry { reportedLocal?: string; claimNumber?: string }
export interface PoliceEntry { calledLocal?: string; agency?: string; officerName?: string; shield?: string; caseNumber?: string }
export interface MedicalEntry {
  detail?: string; injuryTypeId?: number | null; otherInjury?: string; bodyPartId?: number | null
  bodyPartLocationId?: number | null; emsCalled?: boolean; emsArrivedLocal?: string; emsName?: string; emsNumber?: string
  hospital?: string; refusedMedicalAttention?: boolean; contributingFactorId?: number | null; otherFactor?: string
  consumedLocal?: string; symptoms?: string; safetyShoes?: boolean | null; safetyShoesBrand?: string; safetyShoesPurchased?: string
}
export interface PropertyItem {
  categoryId?: number | null; detail?: string; description?: string; colorId?: number | null
  brand?: string; modelSerial?: string; claimValue?: number | null
}
export interface IncidentDetails {
  reportedBy?: PersonEntry | null; subjects: PersonEntry[]; victims: PersonEntry[]; witnesses: PersonEntry[]
  vehicle?: VehicleEntry | null; lockInterrogation: LockEntry[]; media?: MediaEntry | null
  evidence?: EvidenceEntry | null; claim?: ClaimEntry | null; police?: PoliceEntry | null
  medical?: MedicalEntry | null; propertyItems: PropertyItem[]; writtenByOther?: string; supervisorOther?: string
}

export interface IncidentSave {
  form: IncidentForm; hotelId: number; typeId: number | null; kind: string | null; otherType: string | null
  heading: string; occurredLocal: string | null; reportedLocal: string | null; shift: Shift | null
  areaId: number | null; locationId: number | null; locationDetail: string | null; room: string | null
  narrative: string | null; followUp: string | null; details: IncidentDetails
  supervisorId: number | null; distributedTo: string | null
}

export interface Attachment {
  id: number; fileName: string; contentType: string; sizeBytes: number; caption: string | null
  uploadedBy: string; uploadedUtc: string; inline: boolean
}

export interface Incident extends IncidentSave {
  id: number; number: string; formName: string; status: IncidentStatus; typeName: string | null
  areaName: string | null; locationName: string | null; writtenById: number; writtenByName: string
  supervisorName: string | null; createdUtc: string; updatedUtc: string; submittedUtc: string | null
  reviewedByName: string | null; reviewedUtc: string | null; reviewNote: string | null
  voidedUtc: string | null; voidReason: string | null; forHandover: boolean; attachments: Attachment[]; missing: string[]
  can: { edit: boolean; submit: boolean; review: boolean; reopen: boolean; void: boolean; history: boolean; flag: boolean }
}

export interface IncidentRow {
  id: number; number: string; form: IncidentForm; status: IncidentStatus; hotelCode: string; typeName: string | null
  kind: string | null; heading: string; occurredLocal: string | null; shift: Shift | null; areaName: string | null
  locationName: string | null; writtenByName: string; supervisorName: string | null; attachments: number
}
export interface IncidentPage { total: number; items: IncidentRow[] }
export interface HistoryEntry { utc: string; userName: string; action: string; details: string | null }

// --- Event log (see src/Desk.Core/Events) ---

export type Priority = 'Low' | 'Medium' | 'High' | 'Highest'

export interface EventSave {
  hotelId: number; categoryId: number | null; detail: string | null; areaId: number | null; locationId: number | null
  locationDetail: string | null; room: string | null; requestedByType: string | null; requestedByName: string | null
  requestedByUserId: number | null; requestedByDepartmentId: number | null; requestedLocal: string | null
  relatesToType: string | null; relatesToName: string | null; relatesToRoom: string | null; priority: Priority
  assignedDepartmentId: number | null; assignedToId: number | null; startedLocal: string | null
  completedLocal: string | null; narrative: string | null
}

export interface SecurityEvent extends EventSave {
  id: number; categoryName: string; areaName: string | null; locationName: string | null; assignedToName: string | null
  shift: Shift; createdByName: string; createdUtc: string; updatedUtc: string; voidedUtc: string | null
  voidReason: string | null; forHandover: boolean; can: { edit: boolean; complete: boolean; void: boolean; flag: boolean }
}

export interface EventRow {
  id: number; hotelCode: string; categoryName: string; detail: string | null; areaName: string | null
  locationName: string | null; room: string | null; priority: Priority; startedLocal: string; completedLocal: string | null
  shift: Shift; assignedToName: string | null; createdByName: string; narrative: string | null; voided: boolean
}
export interface EventPage { total: number; items: EventRow[] }

export interface Reminder {
  id: number; hotelId: number; title: string; notes: string | null; dueLocal: string; repeatsDaily: boolean
  assignedDepartmentId: number | null; lastDoneBy: string | null; lastDoneUtc: string | null; overdue: boolean
}

// --- Handover ---

export interface HandoverNote { id: number; text: string; by: string; createdUtc: string; updatedUtc: string; canEdit: boolean }
export interface HandoverIncident { id: number; number: string; form: IncidentForm; heading: string; type: string | null; occurredLocal: string | null; status: IncidentStatus }
export interface HandoverEvent { id: number; category: string; detail: string | null; startedLocal: string; completedLocal: string | null; assignedTo: string | null; narrative: string | null }
export interface Handover {
  hotelId: number; shiftDate: string; shift: Shift; startLocal: string; endLocal: string
  previousDate: string; previousShift: Shift; currentDate: string; currentShift: Shift
  notes: HandoverNote[]; incidents: HandoverIncident[] | null; events: HandoverEvent[] | null; openEvents: HandoverEvent[] | null
  callOuts: CallOut[] | null; keysOut: HotelKey[] | null; visitorsIn: Visit[] | null
  foundItems: FoundRow[] | null; lostReports: LostRow[] | null; packages: Package[] | null
  reads: { name: string; readUtc: string }[]; readByMe: boolean; canWrite: boolean
}

// --- On duty ---

export interface DutyAssignment {
  id: number; userId: number | null; name: string; departmentId: number | null; radio: string | null; post: string | null
  startLocal: string; endLocal: string
}
export interface CallOut {
  id: number; hotelId: number; shiftDate: string; shift: Shift; userId: number | null; name: string; departmentId: number | null
  calledLocal: string; reason: string; notes: string | null; coveredBy: string | null; takenBy: string
}
export interface Roster {
  hotelId: number; shiftDate: string; shift: Shift; startLocal: string; endLocal: string
  previousDate: string; previousShift: Shift; currentDate: string; currentShift: Shift
  roster: DutyAssignment[]; callOuts: CallOut[]; canWrite: boolean
}

// --- Safety reports ---

export interface Count { key: string; value: number }
export interface IncidentStats {
  from: string; to: string; total: number; byType: Count[]; byForm: Count[]; byShift: Count[]; byOfficer: Count[]
  byMonth: Count[]; byLocation: Count[]; byStatus: Count[]; byHotel: Count[]
}
export interface EventStats {
  from: string; to: string; total: number; open: number; averageMinutesToComplete: number | null
  byCategory: Count[]; byShift: Count[]; byOfficer: Count[]; byPriority: Count[]; byMonth: Count[]
}
export interface ShiftSummary {
  hotelId: number; shiftDate: string; shift: Shift; startLocal: string; endLocal: string; previousDate: string; previousShift: Shift
  onDuty: DutyAssignment[] | null; callOuts: CallOut[] | null
  incidents: { id: number; number: string; form: string; heading: string; type: string | null; occurredLocal: string | null; status: IncidentStatus; writtenBy: string }[] | null
  eventsTotal: number | null; eventsOpen: number | null; eventsByCategory: Count[] | null; handoverNotes: string[] | null
}

// --- Keys ---

export type KeyStatus = 'In' | 'SignedOut' | 'Missing' | 'Broken'
export interface KeyHolder {
  checkoutId: number; toType: string; name: string; company: string | null; departmentId: number | null
  outLocal: string; dueBackLocal: string | null; outBy: string; overdue: boolean; hoursOut: number
}
export interface HotelKey {
  id: number; hotelId: number; name: string; barcode: string | null; description: string | null; status: KeyStatus
  isActive: boolean; sortOrder: number; holder: KeyHolder | null
}
export interface KeyHistoryRow {
  kind: string; keyId: number; keyName: string; atLocal: string; who: string | null; company: string | null; by: string
  note: string | null; checkoutId: number | null; hasSignature: boolean; backLocal: string | null; backBy: string | null; hasBackSignature: boolean
}
export interface KeyCount {
  id: number; hotelId: number; shiftDate: string; shift: Shift; countedBy: string; countedUtc: string; notes: string | null
  keys: number; onHand: number; lines: { keyId: number; keyName: string; expectedStatus: KeyStatus; onHand: boolean; note: string | null }[] | null
}

// --- Visitors ---

export interface Companion { name: string; badgeNumber: string | null }
export interface Visit {
  id: number; hotelId: number; visitorName: string; company: string | null; visitorType: string; phone: string | null; email: string | null
  areaId: number | null; areaName: string | null; areaDetail: string | null; hostName: string | null; hostUserId: number | null
  purpose: string | null; badgeColor: string | null; badgeNumber: string | null; inLocal: string; expectedOutLocal: string | null
  outLocal: string | null; notes: string | null; companions: Companion[]; hasPhoto: boolean; checkedInBy: string; checkedOutBy: string | null
  overdue: boolean; hoursIn: number; voidedUtc: string | null; voidReason: string | null
}
export interface ScheduledVisit {
  id: number; hotelId: number; visitorName: string; company: string | null; visitorType: string; expectedLocal: string
  hostName: string | null; purpose: string | null; notes: string | null; bookedBy: string; visitId: number | null
}

// --- Lockers and parking ---

export interface SpaceHolder {
  assignmentId: number; userId: number | null; name: string; departmentId: number | null; fromDate: string
  vehicle: string | null; plate: string | null; stickerNumber: string | null; notes: string | null
}
export interface Space {
  id: number; hotelId: number; section: string; number: string; lockCode: string | null; lockSerial: string | null
  notes: string | null; isActive: boolean; holder: SpaceHolder | null
}
export interface SpaceHistoryRow {
  assignmentId: number; spaceId: number; section: string; number: string; name: string; fromDate: string; toDate: string | null
  vehicle: string | null; plate: string | null; stickerNumber: string | null; assignedBy: string; releasedBy: string | null
}

// --- Lost and found ---

export type FoundStatus = 'InStorage' | 'ReturnedToOwner' | 'Shipped' | 'Disposed'
export type LostStatus = 'Open' | 'Matched' | 'Closed'
export type PaymentStatus = 'Waiting' | 'Paid' | 'NoCharge'
export interface FoundRow {
  id: number; number: string; hotelId: number; category: string | null; detail: string | null; color: string | null; brand: string | null
  foundLocal: string; area: string | null; room: string | null; storageLocation: string | null; purgeDate: string; status: FoundStatus
  highValue: boolean; hasPhoto: boolean; forHandover: boolean; shipmentPayment: PaymentStatus | null
}
export interface Shipment {
  id: number; foundItemId: number; itemNumber: string; itemName: string | null; recipientName: string; street: string | null; city: string | null
  state: string | null; zip: string | null; country: string | null; phone: string | null; email: string | null; carrier: string | null
  trackingNumber: string | null; cost: number | null; payment: PaymentStatus; paymentNote: string | null; shippedLocal: string | null
  cancelledUtc: string | null; createdBy: string; createdUtc: string
}
export interface LfMatch { id: number; number: string; score: number; summary: string; when: string }
export interface FoundItem {
  item: FoundRow; description: string | null; modelSerial: string | null; categoryId: number | null; colorId: number | null; areaId: number | null
  locationDetail: string | null; foundByType: string | null; foundByName: string | null; foundByUserId: number | null
  foundByDepartmentId: number | null; ownerName: string | null; ownerContact: string | null; notes: string | null; closedLocal: string | null
  closedBy: string | null; returnedToName: string | null; hasReturnSignature: boolean; disposalMethod: string | null; closeNote: string | null
  createdBy: string; shipments: Shipment[]; matches: LfMatch[]; canEdit: boolean; canDispose: boolean
}
export interface LostRow {
  id: number; number: string; hotelId: number; reportedLocal: string; ownerName: string; room: string | null; category: string | null
  color: string | null; brand: string | null; description: string | null; status: LostStatus; foundItemId: number | null; forHandover: boolean
}
export interface LostReport {
  report: LostRow; phone: string | null; email: string | null; arrivalDate: string | null; departureDate: string | null
  categoryId: number | null; colorId: number | null; lostWhere: string | null; lostFrom: string | null; lostTo: string | null
  notes: string | null; createdBy: string; matches: LfMatch[]
}

// --- Packages ---

export type PackageDirection = 'Incoming' | 'Outgoing'
export type PackageStatus = 'Waiting' | 'PickedUp' | 'Delivered' | 'ReturnedToSender' | 'Shipped'
export interface Package {
  id: number; number: string; hotelId: number; direction: PackageDirection; receivedLocal: string; carrier: string; packageType: string
  trackingNumber: string | null; count: number; weight: string | null; handlingFee: number | null; personType: string; personName: string
  personUserId: number | null; room: string | null; company: string | null; arrivalDate: string | null; departureDate: string | null
  groupName: string | null; otherParty: string | null; storageLocation: string | null; notifiedLocal: string | null; notifiedHow: string | null
  notifiedBy: string | null; status: PackageStatus; closedLocal: string | null; closedToName: string | null; hasSignature: boolean
  closedBy: string | null; notes: string | null; forHandover: boolean; createdBy: string; daysWaiting: number; old: boolean
}
