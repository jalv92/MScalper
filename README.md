# MScalper

![Versión](https://img.shields.io/badge/versión-0.8.1--beta-blue)
![NinjaTrader](https://img.shields.io/badge/NinjaTrader-8.0+-green)
![Estado](https://img.shields.io/badge/estado-beta-orange)

<p align="center">
  <img src="docs/images/logo.png" alt="MScalper Logo" width="200"/>
</p>

## 📊 Sistema Avanzado de Micro-Scalping basado en Order Flow

MScalper es una estrategia algorítmica avanzada para NinjaTrader 8 que utiliza análisis de order flow para generar señales de trading en el micro-scalping de futuros. El sistema identifica desequilibrios en el flujo de órdenes, patrones de absorción y otros indicadores de la microestructura del mercado para ejecutar operaciones con alta probabilidad de éxito.

---

## ✨ Características Principales

- **Análisis de Order Flow en Tiempo Real**: Detección instantánea de desequilibrios y patrones en el DOM.
- **Identificación de Patrones de Absorción**: Reconoce cuando el mercado está absorbiendo presión vendedora o compradora.
- **Análisis de Delta de Volumen**: Calcula y visualiza delta de volumen acumulativo para identificar fortaleza direccional.
- **Detección de Divergencias**: Identifica divergencias entre precio y delta de volumen.
- **Gestión Dinámica de Riesgo**: Adapta parámetros de riesgo según condiciones de mercado.
- **Interfaz Visual**: Muestra zonas de absorción, imbalances y otros patrones directamente en el gráfico.
- **Perfiles de Riesgo Configurables**: Adapta la estrategia a diferentes estilos de trading.

---

## 💻 Requisitos del Sistema

- **NinjaTrader 8.0.23.1** o superior
- **.NET Framework 4.8**
- **Windows 10/11**
- **Suscripción a datos con profundidad de mercado (Level II)** - Fundamental para análisis de order flow
- **Procesador** i5 o superior (recomendado)
- **RAM** 8GB o superior (recomendado)

---

## 🚀 Instalación

### Método Automático

1. **Descargue** el archivo ZIP más reciente de la [sección de releases](https://github.com/yourusername/MScalper/releases)
2. **Cierre** NinjaTrader si está abierto
3. **Ejecute** el instalador y siga las instrucciones

### Método Manual

1. **Cierre** NinjaTrader si está abierto
2. **Descomprima** el archivo descargado
3. **Copie** los archivos de la carpeta `bin` a la ruta `[DocumentsFolder]\NinjaTrader 8\bin\Custom`
4. **Copie** los archivos de la carpeta `config` a la ruta `[DocumentsFolder]\NinjaTrader 8\bin\Custom\config`
5. **Inicie** NinjaTrader 8
6. **Importe** el archivo `import.nst` a través de `Herramientas > Importar > NinjaScript Add-On...`

---

## ⚙️ Configuración

### Parámetros Principales

| Parámetro | Descripción | Valor Recomendado |
|-----------|-------------|-------------------|
| **Volume Threshold** | Volumen mínimo para considerar significativo | 10-25 |
| **Imbalance Ratio** | Proporción de desequilibrio para generar señal | 1.5-3.0 |
| **DOM Levels** | Niveles del DOM a analizar | 5-10 |
| **Absorption Threshold** | Umbral para detectar patrones de absorción | 5-10 |
| **Profit Target** | Objetivo de ganancia en ticks | 2-5 |
| **Stop Loss** | Stop loss en ticks | 2-4 |

### Archivos de Configuración

- **strategy_params.json**: Configuración general de la estrategia
- **risk_profiles.json**: Perfiles de riesgo (Conservador, Moderado, Agresivo)

Para modificar estos archivos, utilice un editor de texto y ajuste según sus preferencias.

---

## 📈 Uso Básico

### Agregar Indicadores al Gráfico

1. Abra un gráfico del instrumento deseado (recomendado: NQ, MNQ)
2. Haga clic derecho y seleccione **Indicadores...**
3. Busque y agregue:
   - **OrderFlow Delta**
   - **OrderBook Imbalance**
   - **Time & Sales Analyzer**

### Configurar la Estrategia

1. Vaya a **Estrategias > Nuevo...**
2. Seleccione **MScalper**
3. Configure los parámetros según su instrumento y estilo de trading
4. Elija **Tamaño de la posición** y **Activar**

### Interpretación de Señales

- **Barras Verdes en Delta**: Predominio de compras
- **Barras Rojas en Delta**: Predominio de ventas
- **Triángulos**: Zonas de absorción significativa
- **Diamantes**: Divergencias entre delta y precio

---

## 📖 Guía de Patrones Detectados

### 1. Imbalance (Desequilibrio en DOM)
Un desequilibrio significativo entre volumen comprador y vendedor en el DOM.
- **Señal Alcista**: Mayor volumen comprador que vendedor
- **Señal Bajista**: Mayor volumen vendedor que comprador

### 2. Absorption (Absorción)
Un nivel de precio absorbe órdenes significativas sin movimiento de precio.
- **Señal Alcista**: Absorción de ventas sin caída de precio
- **Señal Bajista**: Absorción de compras sin subida de precio

### 3. Delta Divergence (Divergencia)
Divergencia entre el movimiento del precio y el delta acumulativo.
- **Señal Alcista**: Precio hace mínimos más bajos pero delta hace mínimos más altos
- **Señal Bajista**: Precio hace máximos más altos pero delta hace máximos más bajos

### 4. Aggressive Order Clusters (Grupos de Órdenes Agresivas)
Concentración de órdenes agresivas en un corto período de tiempo.
- **Señal Alcista**: Grupo de órdenes de compra agresivas
- **Señal Bajista**: Grupo de órdenes de venta agresivas

---

## ⚠️ Solución de Problemas Comunes

### La Estrategia No Aparece en NinjaTrader

1. Verifique que los archivos estén copiados en la ubicación correcta
2. Asegúrese de haber importado el archivo `import.nst`
3. Reinicie NinjaTrader por completo

### Errores de Referencia

Si aparecen errores relacionados con referencias faltantes, asegúrese de tener instalada la versión correcta de NinjaTrader 8 y .NET Framework 4.8.

### Rendimiento Lento

- Reduzca la cantidad de datos históricos cargados
- Cierre otras aplicaciones que consuman muchos recursos
- Considere reducir el parámetro `DOM Levels`

### Licencia No Válida

Si recibe mensajes sobre licencia no válida:
1. Verifique su conexión a internet
2. Reinicie NinjaTrader
3. Contacte a soporte en jvlora@hublai.com

---

## 💰 Versión de Prueba y Licenciamiento

MScalper está disponible con una versión de prueba gratuita de 14 días. Después de este período, se requiere una licencia válida para continuar utilizando el software.

### Opciones de Licencia

- **Licencia Básica**: $XXX.XX - Funcionalidades principales
- **Licencia Pro**: $XXX.XX - Funcionalidades completas con actualizaciones por 1 año
- **Licencia Enterprise**: $XXX.XX - Licencia para múltiples terminales y soporte prioritario

Para adquirir una licencia: [Contactar a Javier Lora](mailto:jvlora@hublai.com)

---

## 📞 Soporte y Contacto

Para soporte técnico, preguntas o sugerencias:

- **Email**: jvlora@hublai.com
- **Sitio Web**: www.hublai.com/](https://hublai.com/)

---

## 📸 Capturas de Pantalla

<p align="center">
  <img src="docs/images/screenshot1.png" alt="Interfaz Principal" width="45%"/>
  <img src="docs/images/screenshot2.png" alt="Detección de Patrones" width="45%"/>
</p>

<p align="center">
  <img src="docs/images/screenshot3.png" alt="Análisis de Delta" width="45%"/>
  <img src="docs/images/screenshot4.png" alt="Configuración" width="45%"/>
</p>

---

## 📜 Aviso Legal

El trading implica riesgos significativos. MScalper es una herramienta de análisis y no garantiza resultados. Utilícelo bajo su propia responsabilidad. Realice siempre su propio análisis y gestión de riesgo.

---

© 2025 MScalper. Todos los derechos reservados.