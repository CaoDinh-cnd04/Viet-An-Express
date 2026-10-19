<#
.SYNOPSIS
  Build web + backend thành 2 thư mục để đẩy lên máy chủ IIS qua FTP (Total Commander...).

.DESCRIPTION
  Kết quả trong <repo>/publish:
    frontend/         giao diện (React build tĩnh + web.config URL Rewrite) — gốc site IIS
    backend/          API .NET 10 — IIS Application gắn ở /api
    app_offline.htm   đặt vào backend/ trên máy chủ khi cập nhật để IIS nhả file .dll
    HUONG_DAN_IIS.md  hướng dẫn cho người quản trị máy chủ (cấu hình một lần)

  Cập nhật backend: đẩy app_offline.htm vào backend/ → ghi đè nội dung backend/ → xóa app_offline.htm.
  Cập nhật frontend: ghi đè nội dung frontend/ (không cần dừng gì).
  Bí mật (connection string, Jwt:Secret...) nằm trong appsettings.Production.json do quản trị tạo trên máy chủ;
  script không sinh file đó nên lần đẩy sau không ghi đè.
#>
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$web = Join-Path $repo 'web'
$iis = Join-Path $PSScriptRoot 'iis'
$out = Join-Path $repo 'publish'
$frontend = Join-Path $out 'frontend'
$backend = Join-Path $out 'backend'

Write-Host '== Build giao diện (web/)'
Push-Location $web
try {
    # Không dùng npm ci: nó xóa node_modules, hỏng `npm run dev` đang chạy. Chỉ cài khi chưa có.
    if (-not (Test-Path 'node_modules')) {
        npm.cmd install
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    npm.cmd run build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally { Pop-Location }

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

Write-Host '== Publish backend'
# Build vào thư mục tạm riêng: không đụng bin/Release của backend đang chạy trên máy (file .exe / .dll bị khóa).
$buildDir = Join-Path ([IO.Path]::GetTempPath()) 'vietan-publish-build'
dotnet publish (Join-Path $repo 'backend/src/VietAnExpress.API') -c Release -o $backend --artifacts-path $buildDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
# File cấu hình máy dev không đưa lên máy chủ.
Get-ChildItem $backend -Filter 'appsettings.Development*.json' | Remove-Item -Force
# web.config có chuỗi kết nối SQL + Jwt secret thật của máy chủ (file cục bộ, không lên git) — có thì thay bản mặc định.
$serverConfig = Join-Path $iis 'backend.web.config'
if (Test-Path $serverConfig) { Copy-Item $serverConfig (Join-Path $backend 'web.config') -Force }
else { Write-Warning 'Chưa có backend/scripts/iis/backend.web.config — web.config backend không có chuỗi kết nối, máy chủ phải tự cấu hình.' }

Write-Host '== Chép giao diện'
Copy-Item (Join-Path $web 'dist') $frontend -Recurse
Copy-Item (Join-Path $iis 'frontend.web.config') (Join-Path $frontend 'web.config')

Copy-Item (Join-Path $iis 'app_offline.htm') $out
Copy-Item (Join-Path $iis 'HUONG_DAN_IIS.md') $out

Write-Host ''
Write-Host "Xong: $out"
Write-Host '  frontend\ -> thư mục gốc site trên máy chủ'
Write-Host '  backend\  -> thư mục backend (IIS Application /api). Đẩy app_offline.htm vào trước, xong thì xóa.'
