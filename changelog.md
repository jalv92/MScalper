# Changelog
# MScalper - Changelog y Plan de Desarrollo

## [0.8.3-beta] - 2025-03-13 16:30
### Cambiado
- Optimizado para compatibilidad con NinjaTrader 8.1.4.1
- Actualizado script de empaquetado para especificar la versión mínima de NinjaTrader
- Mejorado sistema de actualización de referencias en archivos fuente

## [0.8.2-beta] - 2025-03-13 14:45
### Cambiado
- Modificado el proceso de compilación y empaquetado para generar un paquete con archivos fuente
- Actualización de scripts build.ps1 y package.ps1 para mejorar la compatibilidad con NinjaTrader 8
- Añadido archivo MScalper.xml para facilitar la importación en NinjaTrader

## [0.8.1-beta] - 2025-03-14 15:30
### Cambiado
- Renombrado del proyecto de "OrderFlowScalper" a "MScalper"
- Actualización de todas las referencias en el código y documentación

## Visión General del Proyecto
MScalper es una estrategia algorítmica avanzada para NinjaTrader 8 que utiliza análisis de order flow para generar señales de trading en el micro-scalping de futuros (especialmente NQ y MNQ). El sistema identifica desequilibrios en el flujo de órdenes, patrones de absorción y otros indicadores de la microestructura del mercado para ejecutar operaciones con alta probabilidad de éxito.

## Estado Actual: v0.8.3-beta (Desarrollo en Progreso)
Este proyecto está actualmente en fase de desarrollo activo. Los componentes principales se han implementado siguiendo una arquitectura modular que permite la extensión, prueba y mantenimiento eficiente.

---

## Componentes Implementados

### Core
- [x] `AlgorithmCore.cs` - Cerebro central del sistema que coordina los componentes, detecta patrones y evalúa el mercado
- [x] `SignalProcessing.cs` - Procesamiento, validación y consolidación de señales de trading
- [x] `RiskManagement.cs` - Gestión del riesgo, dimensionamiento de posiciones y administración de perfiles de riesgo

### Indicadores
- [x] `OrderFlowDeltaIndicator.cs` - Visualiza el delta acumulativo del volumen y divergencias
- [x] `OrderBookImbalanceIndicator.cs` - Detecta y visualiza desequilibrios en el libro de órdenes
- [x] `TimeAndSalesAnalyzerIndicator.cs` - Analiza patrones en Time & Sales para identificar órdenes agresivas

### Estrategia
- [x] `MScalperStrategy.cs` - Estrategia principal compatible con NinjaScript
- [x] `MarketAnalysis.cs` - Componentes de análisis de mercado para la estrategia

### Utilidades
- [x] `ConfigManager.cs` - Gestión centralizada de configuración
- [x] `Logger.cs` - Sistema de registro para depuración y seguimiento
- [x] `LicenseManager.cs` - Sistema de gestión y verificación de licencias

### Construcción y Compilación
- [x] `build.ps1` - Script PowerShell para empaquetado de archivos fuente
- [x] `package.ps1` - Script PowerShell para empaquetado y distribución

---

## Componentes Pendientes

### Integración
- [ ] Optimización de rendimiento con conjuntos de datos grandes
- [ ] Mejoras en la detección de patrones de order flow

### Documentación
- [ ] `README.md` - Documentación principal del proyecto
- [ ] `INSTALL.md` - Instrucciones de instalación 
- [ ] `CONFIGURATION.md` - Guía de configuración y parámetros
- [ ] Comentarios XML completos para generación de documentación
- [ ] Manual del usuario con ejemplos prácticos

### Interfaz de Usuario
- [ ] Panel de control personalizado para NinjaTrader
- [ ] Visualizaciones avanzadas de datos de order flow

---

## Tareas Pendientes

### Fase 1: Finalización del Núcleo (Completado)
- [x] Implementar componentes básicos (Core, Indicators, Strategy, Utilities)
- [x] Implementar scripts de compilación y empaquetado
- [x] Implementar sistema de licencias con protección por MID
- [x] Crear flujo de trabajo de compilación/empaquetado

### Fase 2: Integración y Pruebas (Actual)
- [ ] Configurar entorno de pruebas en NinjaTrader 8
- [ ] Realizar pruebas con datos de mercado grabados
- [ ] Verificar funcionamiento del sistema de licencias
- [ ] Corrección de errores y optimización de rendimiento
- [ ] Pruebas con distintos instrumentos y timeframes

### Fase 3: Refinamiento y Mejoras (Pendiente)
- [ ] Calibración de parámetros para diferentes instrumentos
- [ ] Implementación de funciones avanzadas (adaptabilidad, auto-optimización)
- [ ] Creación de interfaz gráfica para configuración y monitoreo
- [ ] Documentación completa y manual de usuario

### Fase 4: Distribución y Mantenimiento (Pendiente)
- [ ] Creación de sistema de registro y activación en línea
- [ ] Empaquetado final para distribución
- [ ] Preparación para actualizaciones periódicas
- [ ] Soporte para múltiples instrumentos

---

## Notas Técnicas Importantes

### Estructura de Compilación
El proyecto está diseñado para importarse como archivos fuente en NinjaTrader 8. La estructura de paquete es:
```
MScalper_version.zip
├── Source/
│   ├── Core/              # Componentes del núcleo
│   ├── Indicators/        # Indicadores técnicos
│   ├── Strategy/          # Componentes de estrategias
│   ├── Utilities/         # Utilidades comunes
│   └── MScalper.xml       # Archivo de configuración para importación
├── Config/
│   ├── strategy_params.json      # Parámetros base
│   └── risk_profiles.json        # Configuraciones de riesgo
└── Docs/
    ├── README.md                 # Documentación general
    ├── changelog.md              # Historial de cambios
    └── README_INSTALL.md         # Instrucciones de instalación
```

### Compatibilidad con NinjaTrader
- Compatible con NinjaTrader 8.1.4.1 y versiones superiores
- Optimizado para el uso en las últimas versiones de la plataforma
- Incluye especificación de versión mínima en los metadatos de importación

### Sistema de Licencias
El sistema implementa una protección basada en ID de máquina (MID) con:
- Acceso permanente para el creador (MID: C6CF79C74B4AA01E152615AB23C6C728)
- Período de prueba de 14 días para nuevas instalaciones
- Verificación periódica durante la ejecución
- Mensajes de expiración próxima con datos de contacto

### Requisitos de NinjaTrader 8
- NinjaTrader 8.1.4.1 o superior
- .NET Framework 4.8
- Suscripción a datos que proporcione Market Depth (Level II)
- Acceso a datos históricos de futuros para backtesting

### Dependencias Externas
- Newtonsoft.Json para procesamiento de archivos de configuración
- System.Management para acceso a hardware (sistema de licencias)

---

## Progreso de Desarrollo

### 2025-03-13 16:30
- Optimizado para compatibilidad con NinjaTrader 8.1.4.1
- Actualizado script de empaquetado para especificar la versión mínima de NinjaTrader
- Mejorado sistema de actualización de referencias en archivos fuente

### 2025-03-13 14:45
- Modificado el proceso de compilación y empaquetado para generar un paquete con archivos fuente
- Actualización de scripts build.ps1 y package.ps1 para mejorar la compatibilidad con NinjaTrader 8
- Añadido archivo MScalper.xml para facilitar la importación en NinjaTrader

### 2025-03-14 15:30
- Renombrado del proyecto de "OrderFlowScalper" a "MScalper"
- Actualización de todas las referencias en el código y documentación

### 2025-03-14
- Implementados scripts de compilación y empaquetado (build.ps1, package.ps1)
- Implementado sistema de licencias completo (LicenseManager.cs)
- Integración del sistema de licencias con el núcleo y la estrategia
- Correcciones y mejoras en los scripts de compilación

### 2025-03-13
- Implementadas clases core: AlgorithmCore, SignalProcessing, RiskManagement
- Implementados los indicadores para análisis de order flow
- Implementada la estrategia principal y componentes de análisis de mercado
- Implementadas utilidades de configuración y logging

### Próximos Pasos (Prioridad)
1. Probar el sistema completo en NinjaTrader 8
2. Verificar funcionamiento correcto del sistema de licencias
3. Crear documentación de usuario detallada
4. Optimizar detección de patrones para mejorar precisión

---

## Contacto y Contribuciones
Para contactar al desarrollador:
- Email: jvlora@hublai.com
- Creador: Javier Lora

---

Última actualización: 2025-03-13 16:30

Todos los cambios notables en este proyecto serán documentados en este archivo.

## [0.8.3-beta] - 2025-03-13 16:30

### Cambiado
- Optimizado para compatibilidad con NinjaTrader 8.1.4.1
- Actualizado script de empaquetado para especificar la versión mínima de NinjaTrader
- Mejorado sistema de actualización de referencias en archivos fuente

## [0.8.2-beta] - 2025-03-13 14:45

### Cambiado
- Modificado el proceso de compilación y empaquetado para generar un paquete con archivos fuente
- Actualización de scripts build.ps1 y package.ps1 para mejorar la compatibilidad con NinjaTrader 8
- Añadido archivo MScalper.xml para facilitar la importación en NinjaTrader

## [0.8.1-beta] - 2025-03-14 15:30

### Cambiado
- Renombrado del proyecto de "OrderFlowScalper" a "MScalper"
- Actualización de todas las referencias en el código y documentación

## [0.8.0-beta] - 2025-03-14

### Añadido
- Sistema completo de gestión de licencias
- Protección por ID de máquina (MID)
- Scripts de compilación y empaquetado
- Integración del sistema de licencias con componentes principales

### Mejorado
- Configuración de la compilación para incluir todas las dependencias
- Mensajes de error y advertencia en la interfaz de usuario
- Verificación periódica de licencia durante la ejecución

### Corregido
- Problemas en script de compilación (build.ps1)
- Estructuración y organización del código

## [0.5.0-alpha] - 2025-03-13

### Añadido
- Estructura inicial del proyecto
- Archivos base para la estrategia de micro-scalping
- Configuración básica de compilación y empaquetado
- Archivos de configuración para parámetros y perfiles de riesgo