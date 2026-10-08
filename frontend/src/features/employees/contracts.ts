export type Master = { id: string; name: string; isActive: boolean; isTerminal?: boolean };
export type EmployeeRow = {
  employeeId: string;
  employeeNumber: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  preferredName?: string | null;
  isActive: boolean;
  department?: string | null;
  designation?: string | null;
  employmentStatus?: string | null;
  hireDate?: string | null;
};
export type Employment = {
  employmentRecordId: string;
  departmentId: string | null;
  designationId: string | null;
  employmentTypeId: string | null;
  employmentStatusId: string | null;
  locationId: string | null;
  hiringSourceId: string | null;
  reportingToEmployeeId: string | null;
  hireDate: string;
  startDate: string | null;
  endDate: string | null;
  isCurrent: boolean;
  department?: string | null;
  designation?: string | null;
  employmentType?: string | null;
  employmentStatus?: string | null;
  location?: string | null;
  reportingToName?: string | null;
};
export type Employee = EmployeeRow & {
  dateOfBirth: string | null;
  gender: string | null;
  maritalStatus: string | null;
  nationality: string | null;
  profilePhoto: string | null;
  createdAt: string;
  updatedAt: string;
  currentEmployment: Employment | null;
  contacts: {
    employeeContactId: string;
    workEmail: string | null;
    personalEmail: string | null;
    mobile: string | null;
    phone: string | null;
    workPhone: string | null;
    isPrimary: boolean;
  }[];
  addresses: {
    employeeAddressId: string;
    addressType: string;
    addressLine1: string;
    addressLine2: string | null;
    city: string | null;
    stateProvince: string | null;
    country: string | null;
    postalCode: string | null;
    isPrimary: boolean;
  }[];
  emergencyContacts: {
    emergencyContactId: string;
    name: string;
    relationship: string;
    mobile: string | null;
    phone: string | null;
    email: string | null;
    isPrimary: boolean;
  }[];
  teacherProfile: {
    teacherCode: string;
    teachingLevel: string | null;
    specialization: string | null;
    yearsOfExperience: number | null;
    teachingStatus: string;
  } | null;
};
export type EmployeePage = {
  items: EmployeeRow[];
  page: number;
  pageSize: number;
  totalCount: number;
};
export type AccountLifecycle = {
  employeeId: string;
  linkedUserId: string | null;
  currentEmploymentStatus: string | null;
  hasCurrentEmployment: boolean;
  accountLinked: boolean;
  accountStatus: string | null;
  linkedAccountVersion: string | null;
  currentEmploymentRecordId: string | null;
  requiresOffboardingDecision: boolean;
};
export const masterNames = [
  'departments',
  'designations',
  'employment-types',
  'employment-statuses',
  'locations',
  'hiring-sources',
  'genders',
  'marital-statuses',
  'nationalities',
] as const;
export type MasterName = (typeof masterNames)[number];
export type Masters = Record<MasterName, Master[]>;
export function employeeName(employee: EmployeeRow) {
  return [employee.firstName, employee.middleName, employee.lastName].filter(Boolean).join(' ');
}
