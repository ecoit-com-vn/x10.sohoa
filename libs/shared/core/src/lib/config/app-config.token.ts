import { InjectionToken } from '@angular/core';

export interface AppConfig {
  production: boolean;
  apiGatewayUrl: string;
  workflowServiceUrl?: string;
  /** WebSocket URL của EcoScanner trên máy client (localhost) */
  ecoScannerWsUrl?: string;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');
