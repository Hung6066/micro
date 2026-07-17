// @ts-nocheck
export interface ScheduleAppointmentRequest {
  patientId: string;
  providerId: string;
  scheduledDate: string;
  startTime: string;
  durationMinutes: number;
  typeCode: string;
  reason?: string;
  location?: string;
}

export interface Appointment {
  id: string;
  patientId: string;
  providerId: string;
  scheduledDate: string;
  startTime: string;
  endTime: string;
  status: string;
  statusName?: string;
  type: string;
  typeName?: string;
  reason?: string;
  notes?: string;
  location?: string;
  createdAt: string;
  updatedAt?: string;
  checkedInAt?: string;
  checkedOutAt?: string;
  cancelledAt?: string;
  cancellationReason?: string;
}


