<#
.SYNOPSIS
  Áp migration của mọi module vào database (theo ConnectionStrings:Default trong user-secrets / biến môi trường).
  Khi dev có thể không cần: API tự migrate lúc khởi động nếu Database:MigrateOnStartup = true.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($module in @('Shipments', 'Ecommerce', 'Identity')) {  # Customers chỉ đọc dbo.TCustomer — không có migration
    Write-Host "== $module"
    dotnet ef database update `
        --project (Join-Path $root "src/Modules/$module/VietAnExpress.$module") `
        --startup-project (Join-Path $root 'src/VietAnExpress.API') `
        --context "${module}DbContext"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
