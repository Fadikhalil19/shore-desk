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

export interface Lookups { hotels: Hotel[]; departments: Department[]; areas: Area[]; modules: ModuleInfo[] }

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
