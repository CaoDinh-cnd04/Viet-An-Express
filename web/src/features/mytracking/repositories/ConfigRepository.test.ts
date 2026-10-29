import { describe, expect, it } from 'vitest';
import { emptyConfig } from '../schema';
import { ApiConfigRepository } from './ApiConfigRepository';
describe('ConfigRepository', () => {
  it('adapter API đọc / ghi cấu hình kèm mã khách', async () => {
    const saved: unknown[] = [];
    const repo = new ApiConfigRepository({
      load: async () => ({ customerCode: 'SPVN', companyName: 'CÔNG TY SPV', config: null }),
      save: async body => { saved.push(body); return { customerCode: 'SPVN', config: body.config }; }
    });
    const loaded = await repo.load();
    expect(loaded.config).toEqual({ ...emptyConfig(), brand: { ...emptyConfig().brand, companyName: 'CÔNG TY SPV' } });
    expect(loaded.publication).toEqual({ customerCode: 'SPVN' });
    const result = await repo.save('A', { ...emptyConfig(), title: 'Shop' });
    expect(result.publication).toEqual({ customerCode: 'SPVN' });
    expect(saved).toEqual([{ config: { ...emptyConfig(), title: 'Shop' } }]);
  });
  it('adapter API đọc cấu hình đã lưu, bỏ dữ liệu sai', async () => {
    const repo = new ApiConfigRepository({ load: async () => ({ config: { title: 'Đã lưu', images: 'sai' } }), save: async () => { throw new Error('không gọi'); } });
    expect((await repo.load()).config.title).toBe('Đã lưu');
  });
});
