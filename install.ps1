#Requires -RunAsAdministrator
<#
.SYNOPSIS
    PokeyApp kurulum scripti.
    Uygulamayı kopyalar, Start Menu kısayolu oluşturur ve Windows Firewall kurallarını ekler.

.USAGE
    PowerShell'i Yönetici olarak aç ve çalıştır:
    .\install.ps1

    Kaldırmak için:
    .\install.ps1 -Uninstall
#>

param(
    [switch]$Uninstall
)

$AppName    = "PokeyApp"
$ExeName    = "PokeyApp.exe"
$InstallDir = "$env:LOCALAPPDATA\$AppName"
$StartMenu  = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\$AppName.lnk"
$TcpPort    = 14191
$UdpPort    = 14190

if ($Uninstall) {
    Write-Host "PokeyApp kaldırılıyor..." -ForegroundColor Yellow

    # Süreci durdur
    Get-Process -Name "PokeyApp" -ErrorAction SilentlyContinue | Stop-Process -Force

    # Dosyaları kaldır
    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
        Write-Host "Uygulama dosyaları silindi: $InstallDir" -ForegroundColor Green
    }

    # Kısayolu kaldır
    if (Test-Path $StartMenu) {
        Remove-Item $StartMenu -Force
        Write-Host "Kısayol silindi" -ForegroundColor Green
    }

    # Firewall kurallarını kaldır
    Remove-NetFirewallRule -DisplayName "PokeyApp TCP" -ErrorAction SilentlyContinue
    Remove-NetFirewallRule -DisplayName "PokeyApp UDP" -ErrorAction SilentlyContinue
    Write-Host "Firewall kuralları kaldırıldı" -ForegroundColor Green

    Write-Host "`nPokeyApp başarıyla kaldırıldı." -ForegroundColor Cyan
    exit
}

# --- Kurulum ---
Write-Host "PokeyApp kuruluyor..." -ForegroundColor Cyan

# Kaynak dizini kontrolü (publish klasörü)
$SourceDir = Join-Path $PSScriptRoot "publish"
if (-not (Test-Path "$SourceDir\$ExeName")) {
    Write-Host "HATA: publish\$ExeName bulunamadi." -ForegroundColor Red
    Write-Host "Önce çalıştırın: dotnet publish src/PokeyApp/PokeyApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/" -ForegroundColor Yellow
    exit 1
}

# Kurulum dizini oluştur
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

# Tüm publish içeriğini kopyala (WPF native DLL'ler dahil)
Copy-Item "$SourceDir\*" -Destination $InstallDir -Recurse -Force
Write-Host "Uygulama kopyalandı: $InstallDir" -ForegroundColor Green

# Start Menu kısayolu oluştur
$Shell = New-Object -ComObject WScript.Shell
$Shortcut = $Shell.CreateShortcut($StartMenu)
$Shortcut.TargetPath = "$InstallDir\$ExeName"
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = "PokeyApp - LAN Dürt Uygulaması"
$Shortcut.Save()
Write-Host "Start Menu kısayolu oluşturuldu" -ForegroundColor Green

# Windows Firewall kuralları
$existingTcp = Get-NetFirewallRule -DisplayName "PokeyApp TCP" -ErrorAction SilentlyContinue
if ($existingTcp) {
    Remove-NetFirewallRule -DisplayName "PokeyApp TCP"
}
New-NetFirewallRule `
    -DisplayName "PokeyApp TCP" `
    -Direction Inbound `
    -Protocol TCP `
    -LocalPort $TcpPort `
    -Action Allow `
    -Profile Private,Domain `
    -Description "PokeyApp dürt mesajları (TCP)" | Out-Null

$existingUdp = Get-NetFirewallRule -DisplayName "PokeyApp UDP" -ErrorAction SilentlyContinue
if ($existingUdp) {
    Remove-NetFirewallRule -DisplayName "PokeyApp UDP"
}
New-NetFirewallRule `
    -DisplayName "PokeyApp UDP" `
    -Direction Inbound `
    -Protocol UDP `
    -LocalPort $UdpPort `
    -Action Allow `
    -Profile Private,Domain `
    -Description "PokeyApp peer keşfi (UDP)" | Out-Null

Write-Host "Firewall kuralları eklendi (TCP $TcpPort, UDP $UdpPort)" -ForegroundColor Green

Write-Host "`nKurulum tamamlandı!" -ForegroundColor Cyan
Write-Host "Başlatmak için: $InstallDir\$ExeName" -ForegroundColor White
Write-Host "veya Start Menu'den 'PokeyApp' arayın." -ForegroundColor White
