import type { MyTrackingResponse, Publication, SaveMyTrackingRequest } from '../api';
import { emptyConfig, myTrackingSchema, parseConfig, type MyTrackingConfig } from '../schema';
import type { ConfigLoadResult, ConfigRepository, ConfigSaveResult } from './ConfigRepository';
/** Transport HTTP (myTrackingApi) — tách ra để test không gọi mạng. */
export interface ConfigApi { load(): Promise<MyTrackingResponse>; save(body: SaveMyTrackingRequest): Promise<MyTrackingResponse> }
const toPublication = ({ customerCode }: MyTrackingResponse): Publication => ({ customerCode });
/** Lưu cấu hình trên server (TCustomer_MyTracking + TCustomer_MyTracking_Banner) theo tài khoản đang đăng nhập — userId chỉ để khớp interface. */
export class ApiConfigRepository implements ConfigRepository {
  constructor(private readonly api: ConfigApi) {}
  async load(): Promise<ConfigLoadResult> {
    const res = await this.api.load();
    const config = res.config ? parseConfig(res.config) : emptyConfig();
    // Chưa có cấu hình: tên công ty theo hồ sơ khách (cấu hình đã có thì backend trả tên khách sửa, trống thì theo hồ sơ).
    if (!config.brand.companyName && res.companyName) config.brand.companyName = res.companyName;
    return { config, publication: toPublication(res) };
  }
  async save(_userId: string, config: MyTrackingConfig): Promise<ConfigSaveResult> {
    const res = await this.api.save({ config: myTrackingSchema.parse(config) });
    return { persistent: true, publication: toPublication(res) };
  }
}
