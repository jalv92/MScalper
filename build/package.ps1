# MScalper package.ps1
# Script para empaquetar el proyecto para NinjaTrader 8

param (
    [string]$buildType = "Release"
)

# Definir la versión de NinjaTrader objetivo
$ntVersion = "8.1.4.1"
$packageVersion = "0.8.3-beta"

# Directorios
$scriptDir = $PSScriptRoot
$rootDir = (Get-Item $scriptDir).Parent.FullName
$outputDir = Join-Path -Path $rootDir -ChildPath "output"
$packageDir = Join-Path -Path $outputDir -ChildPath "package"
$sourceDir = Join-Path -Path $packageDir -ChildPath "Source"
$configDir = Join-Path -Path $packageDir -ChildPath "Config"
$docsDir = Join-Path -Path $packageDir -ChildPath "Docs"
$coreDir = Join-Path -Path $sourceDir -ChildPath "Core"
$indicatorsDir = Join-Path -Path $sourceDir -ChildPath "Indicators"
$strategyDir = Join-Path -Path $sourceDir -ChildPath "Strategy"
$utilitiesDir = Join-Path -Path $sourceDir -ChildPath "Utilities"

# Crear directorios si no existen
if (!(Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }
if (!(Test-Path $packageDir)) { New-Item -ItemType Directory -Path $packageDir | Out-Null }
if (!(Test-Path $sourceDir)) { New-Item -ItemType Directory -Path $sourceDir | Out-Null }
if (!(Test-Path $configDir)) { New-Item -ItemType Directory -Path $configDir | Out-Null }
if (!(Test-Path $docsDir)) { New-Item -ItemType Directory -Path $docsDir | Out-Null }
if (!(Test-Path $coreDir)) { New-Item -ItemType Directory -Path $coreDir | Out-Null }
if (!(Test-Path $indicatorsDir)) { New-Item -ItemType Directory -Path $indicatorsDir | Out-Null }
if (!(Test-Path $strategyDir)) { New-Item -ItemType Directory -Path $strategyDir | Out-Null }
if (!(Test-Path $utilitiesDir)) { New-Item -ItemType Directory -Path $utilitiesDir | Out-Null }

# Limpiar directorio de salida
Get-ChildItem -Path $packageDir -Recurse | Remove-Item -Force -Recurse

# Crear directorios si no existen (después de limpiar)
if (!(Test-Path $sourceDir)) { New-Item -ItemType Directory -Path $sourceDir | Out-Null }
if (!(Test-Path $configDir)) { New-Item -ItemType Directory -Path $configDir | Out-Null }
if (!(Test-Path $docsDir)) { New-Item -ItemType Directory -Path $docsDir | Out-Null }
if (!(Test-Path $coreDir)) { New-Item -ItemType Directory -Path $coreDir | Out-Null }
if (!(Test-Path $indicatorsDir)) { New-Item -ItemType Directory -Path $indicatorsDir | Out-Null }
if (!(Test-Path $strategyDir)) { New-Item -ItemType Directory -Path $strategyDir | Out-Null }
if (!(Test-Path $utilitiesDir)) { New-Item -ItemType Directory -Path $utilitiesDir | Out-Null }

Write-Host "Copiando y actualizando archivos fuente..."

# Función para actualizar referencias de versión en archivos fuente
function Update-VersionReferences {
    param (
        [string]$filePath,
        [string]$targetVersion
    )

    $content = Get-Content -Path $filePath -Raw
    
    # Patrones para actualizar
    $patterns = @(
        # NinjaTrader.Core
        @{
            Pattern = "NinjaTrader\.Core, Version=\d+\.\d+\.\d+\.\d+"
            Replacement = "NinjaTrader.Core, Version=$targetVersion"
        },
        # NinjaTrader.Client
        @{
            Pattern = "NinjaTrader\.Client, Version=\d+\.\d+\.\d+\.\d+"
            Replacement = "NinjaTrader.Client, Version=$targetVersion"
        },
        # NinjaTrader.Custom
        @{
            Pattern = "NinjaTrader\.Custom, Version=\d+\.\d+\.\d+\.\d+"
            Replacement = "NinjaTrader.Custom, Version=$targetVersion"
        },
        # Espacio de nombres
        @{
            Pattern = "namespace MScalper\.Indicators"
            Replacement = "namespace NinjaTrader.NinjaScript.Indicators.MScalper"
        },
        @{
            Pattern = "namespace MScalper\.Strategy"
            Replacement = "namespace NinjaTrader.NinjaScript.Strategies.MScalper"
        },
        @{
            Pattern = "namespace MScalper\.Core"
            Replacement = "namespace NinjaTrader.Custom.MScalper.Core"
        }
    )

    # Aplicar actualizaciones
    foreach ($p in $patterns) {
        $content = $content -replace $p.Pattern, $p.Replacement
    }

    # Agregar comentario de compatibilidad si no existe
    if (-not ($content -match "Compatible with NinjaTrader")) {
        $header = "// Compatible with NinjaTrader $targetVersion"
        $content = "$header`r`n$content"
    }

    # Guardar archivo actualizado
    Set-Content -Path $filePath -Value $content
}

# Copiar y actualizar archivos fuente
$srcFolder = Join-Path -Path $rootDir -ChildPath "src"
Get-ChildItem -Path $srcFolder -Filter "*.cs" -Recurse | ForEach-Object {
    $relativePath = $_.FullName.Substring($srcFolder.Length + 1)
    $category = $relativePath.Split([IO.Path]::DirectorySeparatorChar)[0]
    
    $destinationFolder = switch ($category) {
        "Core" { $coreDir }
        "Indicators" { $indicatorsDir }
        "Strategy" { $strategyDir }
        "Utilities" { $utilitiesDir }
        default { $sourceDir }
    }
    
    $destinationFile = Join-Path -Path $destinationFolder -ChildPath $_.Name
    Copy-Item -Path $_.FullName -Destination $destinationFile
    
    # Actualizar referencias de versión
    Update-VersionReferences -filePath $destinationFile -targetVersion $ntVersion
}

# Copiar archivos de configuración
Write-Host "Copiando archivos de configuración..."
$configFolder = Join-Path -Path $rootDir -ChildPath "config"
if (Test-Path $configFolder) {
    Get-ChildItem -Path $configFolder -Filter "*.json" | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $configDir
    }
} else {
    Write-Warning "Directorio de configuración no encontrado. Creando estructura básica..."
    $defaultConfig = @{
        Version = $packageVersion
        TargetNTVersion = $ntVersion
        Parameters = @{
            DefaultRiskLevel = "Medio"
            DefaultInstrument = "MNQ"
            EnabledFeatures = @("OrderFlowAnalysis", "AutoPositionSizing")
        }
    } | ConvertTo-Json -Depth 4
    
    Set-Content -Path (Join-Path -Path $configDir -ChildPath "strategy_params.json") -Value $defaultConfig
}

# Copiar documentación
Write-Host "Copiando documentación..."
$readmePath = Join-Path -Path $rootDir -ChildPath "README.md"
$changelogPath = Join-Path -Path $rootDir -ChildPath "changelog.md"

# Crear README.md simple si no existe
if (-not (Test-Path $readmePath)) {
    $readmeContent = @"
# MScalper

MScalper es una estrategia algorítmica avanzada para NinjaTrader 8 que utiliza análisis de order flow para generar señales de trading en el micro-scalping de futuros (especialmente NQ y MNQ).

## Versión: $packageVersion

Este paquete está optimizado para NinjaTrader $ntVersion.

## Instalación

Consulte el archivo README_INSTALL.md para instrucciones detalladas de instalación.

## Licencia

Este software requiere una licencia válida para su uso. Contacte al desarrollador para más información.
"@
    Set-Content -Path $readmePath -Value $readmeContent
}

if (Test-Path $readmePath) { Copy-Item -Path $readmePath -Destination $docsDir }
if (Test-Path $changelogPath) { Copy-Item -Path $changelogPath -Destination $docsDir }

# Crear archivo de instrucciones de instalación
$installReadmePath = Join-Path -Path $docsDir -ChildPath "README_INSTALL.md"
$installReadmeContent = @"
# Instrucciones de Instalación de MScalper

Este documento proporciona instrucciones detalladas para instalar MScalper en NinjaTrader 8.1.4.1.

## Requisitos Previos

- NinjaTrader 8.1.4.1 o superior
- .NET Framework 4.8
- Suscripción a datos que proporcione Market Depth (Level II)

## Método 1: Importación Directa (Recomendado)

1. Abra NinjaTrader 8
2. Vaya a Herramientas (Tools) > Importar (Import) > NinjaScript Add-On...
3. Navegue hasta la ubicación donde descomprimió el paquete MScalper
4. Seleccione el archivo `MScalper.xml` dentro de la carpeta Source
5. Siga las instrucciones en pantalla para completar la importación

## Método 2: Importación Manual

Si el método anterior no funciona, puede importar los archivos manualmente:

1. Abra NinjaTrader 8
2. Vaya a Herramientas (Tools) > Editar NinjaScript (Edit NinjaScript) > Editar... (Edit...)
3. En el explorador de soluciones, haga clic derecho en la carpeta donde desea importar (por ejemplo, 'Indicators' o 'Strategies')
4. Seleccione 'Importar...' (Import...)
5. Navegue hasta la carpeta Source/[Categoría] correspondiente y seleccione los archivos .cs
6. Compile la solución

## Configuración

1. Copie los archivos de la carpeta `Config` a `[Directorio de NinjaTrader 8]/bin/Custom/MScalper/`
2. Reinicie NinjaTrader 8

## Verificación de Instalación

1. Abra un gráfico en NinjaTrader 8
2. Haga clic derecho > Indicators...
3. Busque los indicadores en la categoría 'MScalper'
4. Si puede ver los indicadores, la instalación fue exitosa

## Solución de Problemas

Si encuentra algún problema durante la instalación, verifique:

1. Que está utilizando NinjaTrader 8.1.4.1 o superior
2. Que todos los archivos fueron copiados correctamente
3. Que no hay errores de compilación en NinjaTrader

Para soporte adicional, contacte al desarrollador en jvlora@hublai.com.
"@
Set-Content -Path $installReadmePath -Value $installReadmeContent

# Crear archivo XML para importación
$xmlPath = Join-Path -Path $sourceDir -ChildPath "MScalper.xml"
$xmlContent = @"
<?xml version="1.0" encoding="utf-8" ?>
<NinjaTrader>
  <AddOnDescriptor>
    <MinimumRequiredVersion>$ntVersion</MinimumRequiredVersion>
    <Author>Javier Lora</Author>
    <DisplayName>MScalper - Order Flow Trading Strategy</DisplayName>
    <Description>
      MScalper es una estrategia algorítmica avanzada para NinjaTrader 8 que utiliza análisis de order flow para generar señales de trading en el micro-scalping de futuros (especialmente NQ y MNQ).
    </Description>
    <Version>$packageVersion</Version>
    <Category>Strategy</Category>
    <AddOnAssembly>
      <Name>MScalper</Name>
      <Files>
        <File>
          <Path>Indicators\MScalper\OrderFlowDeltaIndicator.cs</Path>
          <Type>IndicatorSource</Type>
        </File>
        <File>
          <Path>Indicators\MScalper\OrderBookImbalanceIndicator.cs</Path>
          <Type>IndicatorSource</Type>
        </File>
        <File>
          <Path>Indicators\MScalper\TimeAndSalesAnalyzerIndicator.cs</Path>
          <Type>IndicatorSource</Type>
        </File>
        <File>
          <Path>Strategies\MScalper\MScalperStrategy.cs</Path>
          <Type>StrategySource</Type>
        </File>
        <File>
          <Path>Custom\MScalper\Core\AlgorithmCore.cs</Path>
          <Type>CSharpSource</Type>
        </File>
        <File>
          <Path>Custom\MScalper\Core\SignalProcessing.cs</Path>
          <Type>CSharpSource</Type>
        </File>
        <File>
          <Path>Custom\MScalper\Core\RiskManagement.cs</Path>
          <Type>CSharpSource</Type>
        </File>
        <File>
          <Path>Custom\MScalper\Core\MarketAnalysis.cs</Path>
          <Type>CSharpSource</Type>
        </File>
        <File>
          <Path>Custom\MScalper\Core\ConfigManager.cs</Path>
          <Type>CSharpSource</Type>
        </File>
        <File>
          <Path>Custom\MScalper\Core\Logger.cs</Path>
          <Type>CSharpSource</Type>
        </File>
        <File>
          <Path>Custom\MScalper\Core\LicenseManager.cs</Path>
          <Type>CSharpSource</Type>
        </File>
      </Files>
    </AddOnAssembly>
  </AddOnDescriptor>
</NinjaTrader>
"@
Set-Content -Path $xmlPath -Value $xmlContent

# Crear archivo de versión
$versionXmlPath = Join-Path -Path $packageDir -ChildPath "version.xml"
$versionXmlContent = @"
<?xml version="1.0" encoding="utf-8" ?>
<MScalper>
  <Version>$packageVersion</Version>
  <TargetNTVersion>$ntVersion</TargetNTVersion>
  <BuildDate>$(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</BuildDate>
  <Package>
    <Name>MScalper</Name>
    <Description>MScalper Order Flow Trading Strategy</Description>
    <Author>Javier Lora</Author>
    <Contact>jvlora@hublai.com</Contact>
  </Package>
</MScalper>
"@
Set-Content -Path $versionXmlPath -Value $versionXmlContent

# Crear archivo ZIP
$zipFileName = "MScalper-$packageVersion.zip"
$zipFilePath = Join-Path -Path $outputDir -ChildPath $zipFileName

Write-Host "Creando archivo ZIP: $zipFilePath"
if (Test-Path $zipFilePath) {
    Remove-Item -Path $zipFilePath -Force
}

# Comprimir
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($packageDir, $zipFilePath)

Write-Host "¡Paquete creado con éxito en: $zipFilePath!" -ForegroundColor Green
Write-Host "Este paquete está optimizado para NinjaTrader versión $ntVersion" -ForegroundColor Cyan

# Preguntar si desea instalar directamente en NinjaTrader
$ntDocumentsPath = Join-Path -Path $env:USERPROFILE -ChildPath "Documents\NinjaTrader 8"
if (Test-Path $ntDocumentsPath) {
    Write-Host "`n¿Desea instalar el paquete directamente en NinjaTrader 8? (S/N)" -ForegroundColor Yellow
    $response = Read-Host
    if ($response -eq "S" -or $response -eq "s") {
        # Copiar a carpeta de importación de NinjaTrader
        $ntImportPath = Join-Path -Path $ntDocumentsPath -ChildPath "import"
        if (!(Test-Path $ntImportPath)) {
            New-Item -ItemType Directory -Path $ntImportPath | Out-Null
        }
        $ntZipPath = Join-Path -Path $ntImportPath -ChildPath $zipFileName
        Copy-Item -Path $zipFilePath -Destination $ntZipPath -Force
        
        # Descomprimir en carpeta de importación
        $ntExtractPath = Join-Path -Path $ntImportPath -ChildPath "MScalper"
        if (Test-Path $ntExtractPath) {
            Remove-Item -Path $ntExtractPath -Recurse -Force
        }
        New-Item -ItemType Directory -Path $ntExtractPath | Out-Null
        [System.IO.Compression.ZipFile]::ExtractToDirectory($ntZipPath, $ntExtractPath)
        
        # Copiar archivo XML para importación
        $ntAddOnPath = Join-Path -Path $ntDocumentsPath -ChildPath "import\AddOns"
        if (!(Test-Path $ntAddOnPath)) {
            New-Item -ItemType Directory -Path $ntAddOnPath | Out-Null
        }
        Copy-Item -Path $xmlPath -Destination $ntAddOnPath -Force
        
        Write-Host "Paquete instalado en NinjaTrader 8. Abra NinjaTrader y vaya a Tools > Import > NinjaScript Add-On..." -ForegroundColor Green
    }
}

Write-Host "Proceso completado." -ForegroundColor Green