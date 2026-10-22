<#
.SYNOPSIS
  Chép khóa API / mật khẩu từ dotnet user-secrets (máy dev) vào web.config backend để đưa lên máy chủ IIS.

.DESCRIPTION
  Thêm biến môi trường (dấu ":" đổi thành "__") vào khối <environmentVariables> của:
    - backend/scripts/iis/backend.web.config  (bản mẫu publish-ftp.ps1 chép sang, đã gitignore)
    - publish/backend/web.config               (bản đang chờ đẩy FTP, nếu có)
  Khóa đã có trong file thì giữ nguyên. Không chép ConnectionStrings:Default (máy dev khác máy chủ).
  Không in giá trị ra màn hình.
#>
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$keys = 'GeoNames:Username', 'Geoapify:ApiKey', 'Email:UserName', 'Email:Password', 'Email:InboxAddress',
        'Ecommerce:TokenEncryptionKey', 'Identity:Seed:AdminPassword'

$secrets = @{}
Push-Location (Join-Path $repo 'backend/src/VietAnExpress.API')
try {
    foreach ($line in (dotnet user-secrets list)) {
        $i = $line.IndexOf(' = ')
        if ($i -gt 0) { $secrets[$line.Substring(0, $i).Trim()] = $line.Substring($i + 3) }
    }
} finally { Pop-Location }

$files = @((Join-Path $PSScriptRoot 'backend.web.config'), (Join-Path $repo 'publish/backend/web.config')) | Where-Object { Test-Path $_ }
foreach ($file in $files) {
    [xml]$xml = Get-Content $file -Raw -Encoding UTF8
    $vars = $xml.SelectSingleNode('//aspNetCore/environmentVariables')
    if (-not $vars) { Write-Warning "$file không có <environmentVariables>"; continue }
    $added = @()
    foreach ($key in $keys) {
        $name = $key.Replace(':', '__')
        if (-not $secrets.ContainsKey($key)) { Write-Warning "user-secrets không có $key"; continue }
        if ($vars.SelectSingleNode("environmentVariable[@name='$name']")) { continue }
        $node = $xml.CreateElement('environmentVariable')
        $node.SetAttribute('name', $name)
        $node.SetAttribute('value', $secrets[$key])
        [void]$vars.AppendChild($node)
        $added += $name
    }
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding($true)
    $writer = [System.Xml.XmlWriter]::Create($file, $settings)
    try { $xml.Save($writer) } finally { $writer.Dispose() }
    Write-Host "$file"
    Write-Host ('  thêm: ' + $(if ($added) { $added -join ', ' } else { '(không có khóa mới)' }))
}
if (-not ((Get-Content (Join-Path $PSScriptRoot 'backend.web.config') -Raw) -match 'ConnectionStrings__Default')) {
    Write-Warning 'backend.web.config chưa có ConnectionStrings__Default — copy dòng đó từ publish\backend\web.config sang, không thì lần publish sau mất kết nối DB.'
}
