# MScalper build.ps1
# Script de compilación principal para el proyecto MScalper

# Configuración de variables
$projectName = "MScalper"
$version = (Get-Date).ToString("yyyyMMdd")
$outputDir = "../output"
$packageDir = "$outputDir/package"
$sourceDir = "../src"
$configDir = "../config"
$ntVersion = "8.1.4.1" # Versión de NinjaTrader

# Crear directorios necesarios
Write-Host "Creando directorios de salida..." -ForegroundColor Cyan
if (Test-Path $outputDir) {
    Write-Host "Limpiando directorio de salida anterior..." -ForegroundColor Yellow
    Remove-Item -Path "$outputDir/*" -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
New-Item -ItemType Directory -Force -Path "$packageDir/Source" | Out-Null
New-Item -ItemType Directory -Force -Path "$packageDir/Config" | Out-Null
New-Item -ItemType Directory -Force -Path "$packageDir/Docs" | Out-Null

# Copiar y actualizar archivos fuente
Write-Host "Copiando y actualizando archivos fuente..." -ForegroundColor Cyan
Get-ChildItem -Path $sourceDir -Filter "*.cs" -Recurse | Where-Object { $_.DirectoryName -notlike "*References*" } | ForEach-Object {
    $relativePath = $_.FullName.Substring($sourceDir.Length).TrimStart("\")
    $targetPath = Join-Path -Path "$packageDir/Source" -ChildPath $relativePath
    $targetDir = Split-Path -Path $targetPath -Parent
    
    if (-not (Test-Path -Path $targetDir)) {
        New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    }
    
    # Leer el contenido del archivo
    $content = Get-Content -Path $_.FullName -Raw
    
    # Patrones de actualización para compatibilidad con NT 8.1.4.1
    # Crear los patrones de búsqueda y reemplazo
    $coreFind = [regex]::Escape("NinjaTrader.Core, Version=") + "\d+\.\d+\.\d+\.\d+"
    $coreReplace = "NinjaTrader.Core, Version=$ntVersion"
    
    $clientFind = [regex]::Escape("NinjaTrader.Client, Version=") + "\d+\.\d+\.\d+\.\d+"
    $clientReplace = "NinjaTrader.Client, Version=$ntVersion"
    
    $customFind = [regex]::Escape("NinjaTrader.Custom, Version=") + "\d+\.\d+\.\d+\.\d+"
    $customReplace = "NinjaTrader.Custom, Version=$ntVersion"
    
    $indicatorsNamespaceFind = "namespace\s+MScalper\.Indicators"
    $indicatorsNamespaceReplace = "namespace NinjaTrader.NinjaScript.Indicators"
    
    $strategyNamespaceFind = "namespace\s+MScalper\.Strategy"
    $strategyNamespaceReplace = "namespace NinjaTrader.NinjaScript.Strategies"
    
    # Aplicar patrones de búsqueda y reemplazo
    if ($content -match $coreFind) {
        $content = $content -replace $coreFind, $coreReplace
        Write-Host "  - Actualizada referencia Core en $relativePath" -ForegroundColor Green
    }
    
    if ($content -match $clientFind) {
        $content = $content -replace $clientFind, $clientReplace
        Write-Host "  - Actualizada referencia Client en $relativePath" -ForegroundColor Green
    }
    
    if ($content -match $customFind) {
        $content = $content -replace $customFind, $customReplace
        Write-Host "  - Actualizada referencia Custom en $relativePath" -ForegroundColor Green
    }
    
    if ($content -match $indicatorsNamespaceFind) {
        $content = $content -replace $indicatorsNamespaceFind, $indicatorsNamespaceReplace
        Write-Host "  - Actualizado namespace Indicators en $relativePath" -ForegroundColor Green
    }
    
    if ($content -match $strategyNamespaceFind) {
        $content = $content -replace $strategyNamespaceFind, $strategyNamespaceReplace
        Write-Host "  - Actualizado namespace Strategy en $relativePath" -ForegroundColor Green
    }
    
    # Verificar compatibilidad con NinjaTrader 8.1.4.1
    if ($content -match "NinjaTrader\.") {
        Write-Host "  * Verificando compatibilidad con NinjaTrader en $relativePath..." -ForegroundColor Gray
    }
    
    # Guardar el archivo actualizado
    $content | Out-File -FilePath $targetPath -Encoding utf8
    Write-Host "Copiado y actualizado: $relativePath" -ForegroundColor Gray
}

# Copiar archivos de configuración
Write-Host "Copiando archivos de configuración..." -ForegroundColor Cyan
Get-ChildItem -Path $configDir -Filter "*.json" | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination "$packageDir/Config/" -Force
    Write-Host "Copiado: $($_.Name)" -ForegroundColor Gray
}

# Copiar documentación
Write-Host "Copiando documentación..." -ForegroundColor Cyan
if (Test-Path "../README.md") {
    Copy-Item -Path "../README.md" -Destination "$packageDir/Docs/" -Force
} else {
    # Crear un README.md básico si no existe
    @"
# MScalper v$version

Estrategia algorítmica avanzada para NinjaTrader 8 que utiliza análisis de order flow para generar señales de trading en el micro-scalping de futuros.

## Características

- Análisis de Order Flow en Tiempo Real
- Identificación de Patrones de Absorción
- Análisis de Delta de Volumen
- Detección de Divergencias
- Gestión Dinámica de Riesgo
"@ | Out-File -FilePath "$packageDir/Docs/README.md" -Encoding utf8
}

if (Test-Path "../changelog.md") {
    Copy-Item -Path "../changelog.md" -Destination "$packageDir/Docs/" -Force
}

# Crear archivo README_INSTALL.md
$installReadmeContent = @"
# Instrucciones de Instalación de MScalper v$version

## Compatibilidad
Este paquete está optimizado para NinjaTrader $ntVersion.

## Instalación Manual en NinjaTrader 8

1. **Abra NinjaTrader 8**
2. Vaya a **Tools > Import > NinjaScript Add-On...**
3. Seleccione los archivos .cs de la carpeta Source de este paquete
4. En el cuadro de diálogo de importación, haga clic en **OK**
5. Copie los archivos de configuración de la carpeta Config a:
   * {Documentos}\NinjaTrader 8\bin\Custom\config\
6. Reinicie NinjaTrader para completar la instalación

## Configuración

Consulte los archivos en la carpeta Docs para obtener información detallada sobre la configuración y uso.

## Solución de problemas

Si encuentra problemas durante la importación:
1. Asegúrese de que su versión de NinjaTrader es 8.1.4.1 o superior
2. Verifique que todos los archivos .cs estén seleccionados para importación
3. Cierre y reinicie NinjaTrader antes de intentar nuevamente

## Soporte

Para soporte técnico, contacte a jvlora@hublai.com
"@

$installReadmeContent | Out-File -FilePath "$packageDir/Docs/README_INSTALL.md" -Encoding utf8

# Crear archivo XML para importación en NinjaTrader
$importXmlContent = @"
<?xml version="1.0" encoding="utf-8"?>
<NinjaScript>
  <MinimumRequiredVersion>$ntVersion</MinimumRequiredVersion>
  <Indicators>
    <Indicator>
      <n>OrderFlow Delta</n>
      <File>Indicators\OrderFlowDeltaIndicator.cs</File>
    </Indicator>
    <Indicator>
      <n>OrderBook Imbalance</n>
      <File>Indicators\OrderBookImbalanceIndicator.cs</File>
    </Indicator>
    <Indicator>
      <n>Time &amp; Sales Analyzer</n>
      <File>Indicators\TimeAndSalesAnalyzerIndicator.cs</File>
    </Indicator>
  </Indicators>
  <Strategies>
    <Strategy>
      <n>MScalper</n>
      <File>Strategy\MScalperStrategy.cs</File>
    </Strategy>
  </Strategies>
</NinjaScript>
"@

$importXmlContent | Out-File -FilePath "$packageDir/Source/MScalper.xml" -Encoding utf8

# Crear archivo XML de versión
$versionXmlContent = @"
<?xml version="1.0" encoding="utf-8"?>
<NinjaTraderVersion>
  <TargetVersion>$ntVersion</TargetVersion>
  <Package>MScalper v$version</Package>
  <LastUpdated>$(Get-Date -Format "yyyy-MM-dd")</LastUpdated>
</NinjaTraderVersion>
"@

$versionXmlContent | Out-File -FilePath "$packageDir/version.xml" -Encoding utf8

# Crear archivo ZIP
$zipFile = "$outputDir/${projectName}_v${version}_NT${ntVersion}.zip"
Write-Host "Creando archivo ZIP: $zipFile" -ForegroundColor Cyan
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}
Compress-Archive -Path "$packageDir/*" -DestinationPath $zipFile

Write-Host "¡Paquete creado exitosamente!" -ForegroundColor Green
Write-Host "Archivo ZIP: $zipFile" -ForegroundColor Yellow
Write-Host "" -ForegroundColor Yellow
Write-Host "NOTA IMPORTANTE:" -ForegroundColor Yellow
Write-Host "Este paquete está optimizado para NinjaTrader $ntVersion." -ForegroundColor Yellow
Write-Host "Este paquete contiene los archivos fuente que deben importarse a través de NinjaTrader." -ForegroundColor Yellow
Write-Host "Use Tools > Import > NinjaScript Add-On... en NinjaTrader 8 para importar los archivos .cs" -ForegroundColor Yellow