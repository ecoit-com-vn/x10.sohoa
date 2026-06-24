import { InjectionToken } from '@angular/core';

export interface AppConfig {
  production: boolean;
  apiGatewayUrl: string;
  workflowServiceUrl?: string;
  /** WebSocket URL của EcoScanner trên máy client (localhost) */
  ecoScannerWsUrl?: string;
  /** Bật log WebSocket EcoScanner trên console (dev) */
  ecoScannerDebug?: boolean;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');
