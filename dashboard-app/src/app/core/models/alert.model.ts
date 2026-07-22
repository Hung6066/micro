export interface Alert {
  name: string;
  status: 'firing' | 'resolved';
  severity: 'critical' | 'warning' | 'info';
  summary: string;
  service: string;
  instance: string;
  startsAt: Date;
  endsAt?: Date;
  generatorUrl: string;
  isSilenced: boolean;
}

export type AlertSeverity = Alert['severity'];
