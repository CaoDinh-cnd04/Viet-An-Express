import type { Publication } from '../api';
import type { MyTrackingConfig } from '../schema';
export interface ConfigLoadResult { config: MyTrackingConfig; warning?: string; publication?: Publication }
export interface ConfigSaveResult { persistent: boolean; warning?: string; publication?: Publication }
export interface ConfigRepository {
  load(userId: string): Promise<ConfigLoadResult>;
  save(userId: string, config: MyTrackingConfig): Promise<ConfigSaveResult>;
}
