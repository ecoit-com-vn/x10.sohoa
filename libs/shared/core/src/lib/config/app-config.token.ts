import { InjectionToken } from '@angular/core';

export interface AppConfig {
  production: boolean;
  apiGatewayUrl: string;
  workflowServiceUrl?: string;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');
