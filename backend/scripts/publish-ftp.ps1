<#
.SYNOPSIS
  Build web + backend thành 2 thư mục để đẩy lên máy chủ IIS qua FTP (Total Commander...).

.DESCRIPTION
  Kết quả trong <repo>/publish:
    frontend/         giao diện (React build tĩnh + web.config URL Rewrite) — site http://beta.vietanexpress.com.vn
    backend/          API .NET 10 — site riêng http://api.vietanexpress.com.vn (route /api/v1/...)
    app_offline.htm   đặt vào backend/ trên máy chủ khi cập nhật để IIS nhả file .dll
    HUONG_DAN_IIS.md  hướng dẫn cho người quản trị máy chủ (cấu hình một lần)

  Cập nhật backend: đẩy app_offline.htm vào backend/ → ghi đè nội dung backend/ → xóa app_offline.htm.
  Cập nhật frontend: ghi đè nội dung frontend/ (không cần dừng gì).
  Bí mật (connection string, Jwt:Secret...) nằm trong appsettings.Production.json do quản trị tạo trên máy chủ;
  script không sinh file đó nên lần đẩy sau không ghi đè.

.PARAMETER ApiBaseUrl
  Địa chỉ API giao diện gọi tới (ghi vào bản build). Backend ở tên miền riêng → địa chỉ đầy đủ;
  backend là Application /api cùng tên miền với giao diện → "/api/v1".
#>
param(
    [string]$ApiBaseUrl = 'http://api.vietanexpress.com.vn/api/v1'
)
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
    Write-Host "   API: $ApiBaseUrl"
    $env:VITE_API_BASE_URL = $ApiBaseUrl
    npm.cmd run build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Remove-Item Env:VITE_API_BASE_URL -ErrorAction SilentlyContinue
    Pop-Location
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

Write-Host '== Publish backend'
# Build vào thư mục tạm riêng: không đụng bin/Release của backend đang chạy trên máy (file .exe / .dll bị khóa).
$buildDir = Join-Path ([IO.Path]::GetTempPath()) 'vietan-publish-build'
# Self-contained win-x64: kèm sẵn runtime .NET 10, máy chủ không cần cài .NET 10 (chỉ cần module ASP.NET Core của IIS).
dotnet publish (Join-Path $repo 'backend/src/VietAnExpress.API') -c Release -o $backend --artifacts-path $buildDir -r win-x64 --self-contained true
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
