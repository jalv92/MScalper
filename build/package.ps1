# Script para empaquetar
# Este script creará el paquete ZIP para NinjaTrader 8

$version = Get-Date -Format "yyyyMMdd"
$zipFile = "../output/OrderFlowScalper_v$version.zip"

# Crear estructura para NinjaTrader 8
New-Item -ItemType Directory -Force -Path "../output/package/bin"
New-Item -ItemType Directory -Force -Path "../output/package/config"

# Copiar archivos
Copy-Item "../output/OrderFlowScalper.dll" "../output/package/bin/"
Copy-Item "../config/*" "../output/package/config/"

# Crear archivo de importación específico para NinjaTrader
$importFile = @"
<?xml version="1.0" encoding="utf-8"?>
<NinjaScript>
  <Strategy>
    <Name>OrderFlowScalper</Name>
    <Assembly>OrderFlowScalper.dll</Assembly>
    <ClassName>OrderFlowScalper.Strategy.OrderFlowScalperStrategy</ClassName>
  </Strategy>
</NinjaScript>
"@

$importFile | Out-File -FilePath "../output/package/import.nst" -Encoding utf8

# Crear ZIP
Compress-Archive -Path "../output/package/*" -DestinationPath $zipFile -Force 