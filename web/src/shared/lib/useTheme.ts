import { useEffect } from 'react';
import { useLocalStorage } from './useLocalStorage';

type Theme = 'light' | 'dark';

/**
 * Giao diện sáng/tối: khách mới (chưa từng chọn) luôn thấy giao diện sáng, không theo chế độ tối của hệ điều hành;
 * người dùng bấm đổi thì ghi nhớ (localStorage "va.theme"). index.html gán sẵn data-theme trước khi React chạy để không nháy tối.
 */
export function useTheme() {
  const [stored, setStored] = useLocalStorage<Theme | null>('va.theme', null);
  const theme: Theme = stored ?? 'light';

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  return { theme, toggle: () => setStored(theme === 'dark' ? 'light' : 'dark') };
}
