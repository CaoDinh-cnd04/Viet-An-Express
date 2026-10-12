<#
.SYNOPSIS
  Tạo migration cho 1 module, rồi đổi class sinh ra thành internal (quy tắc: mọi class trong module là internal).

.EXAMPLE
  ./scripts/add-migration.ps1 -Module Shipments -Name AddShipmentEta
#>
param(
    [Parameter(Mandatory)][ValidateSet('Shipments', 'Ecommerce', 'Identity')][string]$Module,
    [Parameter(Mandatory)][string]$Name
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "src/Modules/$Module/VietAnExpress.$Module"

dotnet ef migrations add $Name `
    --project $project `
    --startup-project (Join-Path $root 'src/VietAnExpress.API') `
    --context "${Module}DbContext" `
    --configuration Release `
    --output-dir Infrastructure/Migrations
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-ChildItem (Join-Path $project 'Infrastructure/Migrations') -Filter *.cs | ForEach-Object {
    $text = Get-Content $_.FullName -Raw
    $fixed = $text -replace '(?m)^    public partial class ', '    internal partial class '
    if ($fixed -ne $text) { Set-Content $_.FullName $fixed -NoNewline -Encoding utf8 }
}
Write-Host "Đã tạo migration $Name cho module $Module (class đã chuyển sang internal)."
