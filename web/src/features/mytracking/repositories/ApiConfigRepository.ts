import type { MyTrackingResponse, Publication, SaveMyTrackingRequest } from '../api';
import { emptyConfig, myTrackingSchema, parseConfig, type MyTrackingConfig } from '../schema';
import type { ConfigLoadResult, ConfigRepository, ConfigSaveResult } from './ConfigRepository';
/** Transport HTTP (myTrackingApi) — tách ra để test không gọi mạng. */
export interface ConfigApi { load(): Promise<MyTrackingResponse>; save(body: SaveMyTrackingRequest): Promise<MyTrackingResponse> }
const toPublication = ({ slug, published, updatedAt }: MyTrackingResponse): Publication => ({ slug, published, updatedAt });
/** Lưu cấu hình trên server (TCustomer_MyTracking + TCustomer_MyTracking_Banner) theo tài khoản đang đăng nhập — userId chỉ để khớp interface. */
export class ApiConfigRepository implements ConfigRepository {
  constructor(private readonly api: ConfigApi) {}
  async load(): Promise<ConfigLoadResult> {
    const res = await this.api.load();
    return { config: res.config ? parseConfig(res.config) : emptyConfig(), publication: toPublication(res) };
  }
  async save(_userId: string, config: MyTrackingConfig, publication?: Publication): Promise<ConfigSaveResult> {
    if (!publication) throw new Error('Thiếu đường dẫn MyTracking');
    const res = await this.api.save({ slug: publication.slug, published: publication.published, config: myTrackingSchema.parse(config) });
    return { persistent: true, publication: toPublication(res) };
  }
}
