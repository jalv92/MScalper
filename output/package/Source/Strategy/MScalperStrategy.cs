// Compatible with NinjaTrader 8.1.4.1
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Core;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using MScalper.Utilities;

namespace NinjaTrader.NinjaScript.Strategies.MScalper
{
    [Description("Strategy for micro-scalping based on order flow analysis")]
    public class MScalperStrategy : NinjaTrader.NinjaScript.Strategies.Strategy
    {
        #region Variables
        private MarketAnalysis marketAnalysis;
        private DateTime lastTradeTime = DateTime.MinValue;
        private Dictionary<double, long> buyVolume = new Dictionary<double, long>();
        private Dictionary<double, long> sellVolume = new Dictionary<double, long>();
        private Dictionary<double, long> deltaVolume = new Dictionary<double, long>();
        private double cumulativeDelta = 0;
        private List<double> recentImbalances = new List<double>();
        private const int MaxImbalanceHistory = 20;
        private double currentImbalance = 0;

        #region Variables de Licencia
        private bool isLicenseValid = false;
        private DateTime lastLicenseCheck = DateTime.MinValue;
        private TimeSpan licenseCheckInterval = TimeSpan.FromHours(1);
        private string licenseMessage = string.Empty;
        private bool showedLicenseWarning = false;
        #endregion
        #endregion

        #region Parameters
        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Volume Threshold", Description = "Minimum volume for significant order flow", Order = 1, GroupName = "Order Flow Parameters")]
        public int VolumeThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, 10.0)]
        [Display(Name = "Imbalance Ratio", Description = "Minimum ratio between buy/sell volumes for signal", Order = 2, GroupName = "Order Flow Parameters")]
        public double ImbalanceRatio { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "DOM Levels", Description = "Number of DOM levels to analyze", Order = 3, GroupName = "Order Flow Parameters")]
        public int DOMDepth { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Absorption Threshold", Description = "Volume threshold for absorption detection", Order = 4, GroupName = "Order Flow Parameters")]
        public int AbsorptionThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(0.5, 10)]
        [Display(Name = "Profit Target (ticks)", Description = "Profit target in ticks", Order = 1, GroupName = "Trade Parameters")]
        public double ProfitTargetTicks { get; set; }

        [NinjaScriptProperty]
        [Range(0.5, 10)]
        [Display(Name = "Stop Loss (ticks)", Description = "Stop loss in ticks", Order = 2, GroupName = "Trade Parameters")]
        public double StopLossTicks { get; set; }

        [NinjaScriptProperty]
        [Range(10, 300)]
        [Display(Name = "Trade Cooldown (seconds)", Description = "Minimum time between trades", Order = 3, GroupName = "Trade Parameters")]
        public int TradeCooldownSeconds { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Debug Logging", Description = "Enable detailed debug logging", Order = 1, GroupName = "Debug Options")]
        public bool EnableDebugLogging { get; set; }
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Strategy for micro-scalping based on order flow analysis";
                Name = "OrderFlowScalper";
                
                // Default parameter values
                VolumeThreshold = 10;
                ImbalanceRatio = 2.0;
                DOMDepth = 5;
                AbsorptionThreshold = 5;
                ProfitTargetTicks = 2;
                StopLossTicks = 2;
                TradeCooldownSeconds = 30;
                EnableDebugLogging = true;
                
                // Strategy settings
                IsAutoStarEnabled = false;
                IsFillLimitOnTouch = false;
                IsInstantiatedOnEachOptimizationIteration = true;
                
                // Time-based settings
                StartTime = DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture);
                EndTime = DateTime.Parse("16:00", System.Globalization.CultureInfo.InvariantCulture);
                
                // Order fill settings
                DefaultQuantity = 1;
                IncludeCommission = true;
                Slippage = 1;
                IncludeTradeHistoryInBacktest = true;
                
                // This enables processing of Market Depth data
                IsUnmanaged = false;
                IsMarketDataSubscribed = true;
            }
            else if (State == State.Configure)
            {
                // Add required data
                AddDataSeries(BarsPeriodType.Tick, 1);
                
                // Initialize market analysis
                marketAnalysis = new MarketAnalysis(DOMDepth, AbsorptionThreshold, ImbalanceRatio);
            }
            else if (State == State.DataLoaded)
            {
                // Validations and initializations after data is loaded
                buyVolume.Clear();
                sellVolume.Clear();
                deltaVolume.Clear();
                cumulativeDelta = 0;
                recentImbalances.Clear();
                
                // Verificar licencia
                VerifyLicense();
            }
            else if (State == State.Terminated)
            {
                // Limpiar recursos cuando se termina la estrategia
            }
        }

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            // Process market data for order flow analysis
            if (e.MarketDataType == MarketDataType.Last)
            {
                // Process executed trade
                ProcessTrade(e.Price, e.Volume, e.Time);
            }
            else if (e.MarketDataType == MarketDataType.Ask || e.MarketDataType == MarketDataType.Bid)
            {
                // Process DOM updates
                ProcessDOMUpdate(e.MarketDataType, e.Price, e.Volume);
            }
        }

        protected override void OnMarketDepth(MarketDepthEventArgs e)
        {
            // Forward to market analysis for DOM processing
            var imbalanceResult = marketAnalysis.ProcessMarketDepth(e, GetCurrentBid(), GetCurrentAsk());
            if (imbalanceResult.HasSignificantImbalance)
            {
                currentImbalance = imbalanceResult.ImbalanceRatio;
                UpdateImbalanceHistory(currentImbalance);
                
                // Log detected imbalance
                if (EnableDebugLogging)
                {
                    Log(string.Format("Imbalance detected: {0:F2} at price {1} type: {2}", 
                        currentImbalance, 
                        imbalanceResult.Price,
                        imbalanceResult.ImbalanceType));
                }
                
                // Check for trade signals
                EvaluateTradeSignals(imbalanceResult);
            }
        }

        protected override void OnBarUpdate()
        {
            // Verificar licencia periódicamente durante la ejecución
            if ((DateTime.Now - lastLicenseCheck) > licenseCheckInterval)
            {
                if (!VerifyLicense())
                {
                    // Si la licencia no es válida, permitir funcionalidad limitada
                    // Por ejemplo, solo mostrar información pero no ejecutar operaciones
                }
            }
            
            // Not using bar updates for this strategy as it's based on order flow
            // But we can use it for session management
            if (BarsInProgress != 0) 
                return;
                
            // Check for session boundaries
            if (ToTime(Time[0]) < ToTime(StartTime) || ToTime(Time[0]) > ToTime(EndTime))
            {
                // Outside trading hours, flatten position if any
                if (Position.MarketPosition != MarketPosition.Flat)
                {
                    if (EnableDebugLogging)
                        Log("Session end - flattening position");
                        
                    Flatten();
                }
            }
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, 
            double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            // Handle execution updates
            if (Position.MarketPosition != MarketPosition.Flat)
            {
                double stopPrice = 0;
                double targetPrice = 0;
                
                if (Position.MarketPosition == MarketPosition.Long)
                {
                    stopPrice = Position.AveragePrice - (StopLossTicks * TickSize);
                    targetPrice = Position.AveragePrice + (ProfitTargetTicks * TickSize);
                    
                    ExitLongStopMarket(0, true, Position.Quantity, stopPrice, "Stop Loss", "StopLoss");
                    ExitLongLimit(0, true, Position.Quantity, targetPrice, "Profit Target", "ProfitTarget");
                }
                else if (Position.MarketPosition == MarketPosition.Short)
                {
                    stopPrice = Position.AveragePrice + (StopLossTicks * TickSize);
                    targetPrice = Position.AveragePrice - (ProfitTargetTicks * TickSize);
                    
                    ExitShortStopMarket(0, true, Position.Quantity, stopPrice, "Stop Loss", "StopLoss");
                    ExitShortLimit(0, true, Position.Quantity, targetPrice, "Profit Target", "ProfitTarget");
                }
                
                if (EnableDebugLogging)
                {
                    Log(string.Format("Position: {0} at {1} - Stop: {2} Target: {3}", 
                        Position.MarketPosition, Position.AveragePrice, stopPrice, targetPrice));
                }
            }
        }
        
        private void ProcessTrade(double price, long volume, DateTime time)
        {
            // Determine if trade was buyer or seller initiated
            double currentBid = GetCurrentBid();
            double currentAsk = GetCurrentAsk();
            
            bool isBuyerInitiated = price >= currentAsk;
            bool isSellerInitiated = price <= currentBid;
            
            // Update volume tracking
            if (!buyVolume.ContainsKey(price))
                buyVolume.Add(price, 0);
                
            if (!sellVolume.ContainsKey(price))
                sellVolume.Add(price, 0);
                
            if (!deltaVolume.ContainsKey(price))
                deltaVolume.Add(price, 0);
            
            if (isBuyerInitiated)
            {
                buyVolume[price] += volume;
                deltaVolume[price] += volume;
                cumulativeDelta += volume;
            }
            else if (isSellerInitiated)
            {
                sellVolume[price] += volume;
                deltaVolume[price] -= volume;
                cumulativeDelta -= volume;
            }
            
            // Analyze absorption pattern
            if (marketAnalysis.DetectAbsorption(price, buyVolume, sellVolume, cumulativeDelta))
            {
                if (EnableDebugLogging)
                    Log(string.Format("Absorption detected at price {0}", price));
                
                EvaluateAbsorptionSignal(price, time);
            }
        }

        private void ProcessDOMUpdate(MarketDataType dataType, double price, long volume)
        {
            // Forward to market analysis
            marketAnalysis.UpdateDOM(dataType, price, volume);
        }

        private void UpdateImbalanceHistory(double imbalance)
        {
            recentImbalances.Add(imbalance);
            if (recentImbalances.Count > MaxImbalanceHistory)
                recentImbalances.RemoveAt(0);
        }

        private void EvaluateTradeSignals(MarketAnalysis.ImbalanceResult imbalanceResult)
        {
            // Only proceed if we're outside the trade cooldown period
            if (lastTradeTime != DateTime.MinValue && 
                DateTime.Now.Subtract(lastTradeTime).TotalSeconds < TradeCooldownSeconds)
                return;
            
            // Check for entry conditions
            if (imbalanceResult.ImbalanceType == MarketAnalysis.ImbalanceType.BuyerDominant && 
                Position.MarketPosition != MarketPosition.Long)
            {
                // Potential long entry
                if (marketAnalysis.ConfirmBuySignal(recentImbalances, imbalanceResult.ImbalanceRatio))
                {
                    EnterLong();
                    lastTradeTime = DateTime.Now;
                    
                    if (EnableDebugLogging)
                        Log("LONG ENTRY: Buy imbalance confirmed");
                }
            }
            else if (imbalanceResult.ImbalanceType == MarketAnalysis.ImbalanceType.SellerDominant && 
                     Position.MarketPosition != MarketPosition.Short)
            {
                // Potential short entry
                if (marketAnalysis.ConfirmSellSignal(recentImbalances, imbalanceResult.ImbalanceRatio))
                {
                    EnterShort();
                    lastTradeTime = DateTime.Now;
                    
                    if (EnableDebugLogging)
                        Log("SHORT ENTRY: Sell imbalance confirmed");
                }
            }
        }

        private void EvaluateAbsorptionSignal(double price, DateTime time)
        {
            // Only proceed if we're outside the trade cooldown period
            if (lastTradeTime != DateTime.MinValue && 
                time.Subtract(lastTradeTime).TotalSeconds < TradeCooldownSeconds)
                return;
            
            // Check for entry based on absorption patterns
            MarketAnalysis.AbsorptionType absorptionType = 
                marketAnalysis.ClassifyAbsorption(price, buyVolume, sellVolume);
            
            if (absorptionType == MarketAnalysis.AbsorptionType.BuyAbsorptionAtSupport && 
                Position.MarketPosition != MarketPosition.Long)
            {
                EnterLong();
                lastTradeTime = time;
                
                if (EnableDebugLogging)
                    Log("LONG ENTRY: Buy absorption at support detected");
            }
            else if (absorptionType == MarketAnalysis.AbsorptionType.SellAbsorptionAtResistance && 
                     Position.MarketPosition != MarketPosition.Short)
            {
                EnterShort();
                lastTradeTime = time;
                
                if (EnableDebugLogging)
                    Log("SHORT ENTRY: Sell absorption at resistance detected");
            }
        }

        #region Métodos sobrescritos para verificación de licencia
        private new void EnterLong()
        {
            // Verificar licencia antes de ejecutar operación
            if (!isLicenseValid && !VerifyLicense())
            {
                if (EnableDebugLogging)
                    Log("Operación rechazada: Licencia no válida");
                return;
            }
            
            // Ejecutar entrada original
            base.EnterLong();
        }

        private new void EnterShort()
        {
            // Verificar licencia antes de ejecutar operación
            if (!isLicenseValid && !VerifyLicense())
            {
                if (EnableDebugLogging)
                    Log("Operación rechazada: Licencia no válida");
                return;
            }
            
            // Ejecutar entrada original
            base.EnterShort();
        }
        #endregion

        #region Métodos de Licencia
        /// <summary>
        /// Verifica que la licencia sea válida
        /// </summary>
        /// <returns>True si la licencia es válida</returns>
        private bool VerifyLicense()
        {
            try
            {
                // Solo verificar la licencia periódicamente después de la primera verificación
                if (isLicenseValid && (DateTime.Now - lastLicenseCheck) < licenseCheckInterval)
                    return true;
                
                // Inicializar y verificar licencia
                var licenseManager = LicenseManager.Instance;
                bool initialized = licenseManager.Initialize();
                isLicenseValid = initialized && licenseManager.IsLicenseValid();
                lastLicenseCheck = DateTime.Now;
                
                if (isLicenseValid)
                {
                    // Verificar si la licencia está a punto de expirar (menos de 3 días)
                    int remainingDays = licenseManager.GetRemainingDays();
                    if (remainingDays >= 0 && remainingDays <= 3)
                    {
                        licenseMessage = $"ADVERTENCIA: Su periodo de prueba expira en {remainingDays} días. Por favor contacta a Javier Lora email: jvlora@hublai.com";
                        if (!showedLicenseWarning)
                        {
                            // Mostrar advertencia en UI de NinjaTrader (solo una vez)
                            ShowLicenseWarning(licenseMessage);
                            showedLicenseWarning = true;
                        }
                        if (EnableDebugLogging)
                            Log(licenseMessage);
                    }
                    else if (licenseManager.GetLicenseType() == LicenseManager.LicenseType.Trial)
                    {
                        licenseMessage = $"Versión de prueba activa. Días restantes: {remainingDays}";
                        if (EnableDebugLogging)
                            Log(licenseMessage);
                    }
                }
                else
                {
                    licenseMessage = "Licencia no válida o expirada. Por favor contacta a Javier Lora email: jvlora@hublai.com";
                    ShowLicenseError(licenseMessage);
                    Log(licenseMessage);
                }
                
                return isLicenseValid;
            }
            catch (Exception ex)
            {
                Log($"Error verificando licencia: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Muestra un mensaje de advertencia de licencia en la UI de NinjaTrader
        /// </summary>
        /// <param name="message">Mensaje de advertencia</param>
        private void ShowLicenseWarning(string message)
        {
            try
            {
                // NinjaTrader puede ejecutar esto en un hilo no UI, por lo que debemos asegurarnos
                // de mostrar el mensaje en el hilo UI
                NinjaTrader.Gui.Tools.NTMessageBoxSimple.Show(
                    NinjaTrader.Core.Globals.MainWindow,
                    message,
                    "OrderFlowScalper - Advertencia de Licencia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Log($"Error mostrando advertencia de licencia: {ex.Message}");
            }
        }

        /// <summary>
        /// Muestra un mensaje de error de licencia en la UI de NinjaTrader
        /// </summary>
        /// <param name="message">Mensaje de error</param>
        private void ShowLicenseError(string message)
        {
            try
            {
                NinjaTrader.Gui.Tools.NTMessageBoxSimple.Show(
                    NinjaTrader.Core.Globals.MainWindow,
                    message,
                    "OrderFlowScalper - Error de Licencia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Log($"Error mostrando error de licencia: {ex.Message}");
            }
        }
        #endregion

        #region Helper Methods
        private double GetCurrentBid()
        {
            return GetCurrentBid(0);
        }
        
        private double GetCurrentAsk()
        {
            return GetCurrentAsk(0);
        }
        #endregion
    }
}
