# Việt An Express

Portal khách hàng gồm frontend React/TypeScript trong `web/` và backend .NET 10 trong `backend/`.

- Repository: https://github.com/CaoDinh-cnd04/VietanExpress
- Triển khai: hosting Windows IIS qua FTP — build bằng `backend/scripts/publish-ftp.ps1`, cấu hình máy chủ theo [HUONG_DAN_IIS.md](backend/scripts/iis/HUONG_DAN_IIS.md).
- Frontend, kiến trúc module và cách chạy: [web/README.md](web/README.md).
- Backend, database và API: [backend/README.md](backend/README.md), [hợp đồng API](web/docs/API_CONTRACT.md).
- CI kiểm tra web và backend khi push lên `main`.

Mini App được phát triển riêng tại máy local và được loại khỏi repository này bằng `.gitignore`.
