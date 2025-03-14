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
4. Seleccione el archivo MScalper.xml dentro de la carpeta Source
5. Siga las instrucciones en pantalla para completar la importación

## Método 2: Importación Manual

Si el método anterior no funciona, puede importar los archivos manualmente:

1. Abra NinjaTrader 8
2. Vaya a Herramientas (Tools) > Editar NinjaScript (Edit NinjaScript) > Editar... (Edit...)
3. En el explorador de soluciones, haga clic derecho en la carpeta donde desea importar (por ejemplo, 'Indicators' o 'Strategies')
4. Seleccione 'Importar...' (Import...)
5. Navegue hasta la carpeta Source/[Categoría] correspondiente y seleccione los archivos .cs
6. Compile la solución

## Método 3: Importación Manual de Archivos Individuales

Si los métodos anteriores no funcionan, puede importar cada archivo individualmente:

1. Abra NinjaTrader 8
2. Vaya a Herramientas (Tools) > Editar NinjaScript (Edit NinjaScript) > Editar... (Edit...)
3. En el explorador de soluciones, haga clic derecho en 'Estrategias' (Strategies)
4. Seleccione 'Importar...' (Import...)
5. Navegue hasta la carpeta Source/Strategy y seleccione los archivos .cs
6. Repita el proceso con los indicadores y otros archivos
7. Compile la solución

## Configuración

1. Copie los archivos de la carpeta Config a [Directorio de NinjaTrader 8]/bin/Custom/MScalper/
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
