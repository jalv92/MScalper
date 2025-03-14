// Compatible with NinjaTrader 8.1.4.1
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using Newtonsoft.Json;
using MScalper.Utilities;

namespace NinjaTrader.Custom.MScalper.Core
{
    /// <summary>
    /// Core algorithm logic for order flow-based micro-scalping strategy
    /// Acts as the central coordinator between different system components
    /// </summary>
    public class AlgorithmCore
    {
        #region Private Fields
        private Dictionary<string, object> _parameters;
        private List<OrderFlowPattern> _detectedPatterns;
        private MarketState _currentMarketState;
        private TradingMode _currentTradingMode;
        private DateTime _lastSignalTime;
        private readonly string _configPath;
        private readonly string _algorithmVersion = "1.0.0";
        private bool _isInitialized;
        private readonly object _lockObject = new object();

        // Campos para licencia
        private bool _isLicenseValid = false;
        private DateTime _lastLicenseCheck = DateTime.MinValue;
        private readonly TimeSpan _licenseCheckInterval = TimeSpan.FromHours(1);
        #endregion

        #region Public Properties
        /// <summary>
        /// Current algorithm state for external components to query
        /// </summary>
        public AlgorithmState State { get; private set; }

        /// <summary>
        /// Configuration parameters that have been loaded
        /// </summary>
        public IReadOnlyDictionary<string, object> Parameters => _parameters;

        /// <summary>
        /// Access to current detected patterns
        /// </summary>
        public IReadOnlyList<OrderFlowPattern> DetectedPatterns => _detectedPatterns;

        /// <summary>
        /// Current volatility estimation
        /// </summary>
        public double CurrentVolatility { get; private set; }

        /// <summary>
        /// Algorithm performance metrics
        /// </summary>
        public PerformanceMetrics Performance { get; private set; }
        #endregion

        #region Enums and Classes
        /// <summary>
        /// Algorithm execution state
        /// </summary>
        public enum AlgorithmState
        {
            Uninitialized,
            Ready,
            Running,
            Paused,
            ShuttingDown,
            Error
        }

        /// <summary>
        /// Current market conditions
        /// </summary>
        public enum MarketState
        {
            Unknown,
            Ranging,
            TrendingUp,
            TrendingDown,
            Volatile,
            LowVolatility,
            Opening,
            Closing
        }

        /// <summary>
        /// Algorithm trading mode
        /// </summary>
        public enum TradingMode
        {
            Disabled,
            Aggressive,
            Normal,
            Conservative,
            MonitorOnly
        }

        /// <summary>
        /// Order flow pattern structure
        /// </summary>
        public class OrderFlowPattern
        {
            public string PatternType { get; set; }
            public double Price { get; set; }
            public DateTime DetectionTime { get; set; }
            public double Strength { get; set; }
            public double Probability { get; set; }
            public Dictionary<string, object> AdditionalData { get; set; }
        }

        /// <summary>
        /// Performance metrics for tracking algorithm effectiveness
        /// </summary>
        public class PerformanceMetrics
        {
            public int TotalSignals { get; set; }
            public int ValidSignals { get; set; }
            public int ExecutedTrades { get; set; }
            public int WinningTrades { get; set; }
            public int LosingTrades { get; set; }
            public double WinRate => ExecutedTrades > 0 ? (double)WinningTrades / ExecutedTrades : 0;
            public double AverageWin { get; set; }
            public double AverageLoss { get; set; }
            public double NetProfit { get; set; }
            public double MaxDrawdown { get; set; }
            public double SharpeRatio { get; set; }
            
            public void Reset()
            {
                TotalSignals = 0;
                ValidSignals = 0;
                ExecutedTrades = 0;
                WinningTrades = 0;
                LosingTrades = 0;
                AverageWin = 0;
                AverageLoss = 0;
                NetProfit = 0;
                MaxDrawdown = 0;
                SharpeRatio = 0;
            }
        }
        #endregion

        #region Constructor and Initialization
        /// <summary>
        /// Initializes a new instance of the AlgorithmCore
        /// </summary>
        /// <param name="configPath">Path to configuration files</param>
        public AlgorithmCore(string configPath)
        {
            _configPath = configPath;
            _parameters = new Dictionary<string, object>();
            _detectedPatterns = new List<OrderFlowPattern>();
            _currentMarketState = MarketState.Unknown;
            _currentTradingMode = TradingMode.MonitorOnly;
            _lastSignalTime = DateTime.MinValue;
            State = AlgorithmState.Uninitialized;
            Performance = new PerformanceMetrics();
            _isInitialized = false;
        }

        /// <summary>
        /// Initializes algorithm components and loads configuration
        /// </summary>
        /// <returns>True if initialization was successful</returns>
        public bool Initialize()
        {
            try
            {
                // Verificar licencia primero
                if (!VerifyLicense())
                {
                    LogMessage("Licencia no válida o expirada. Por favor contacta a Javier Lora email: jvlora@hublai.com", LogLevel.Error);
                    State = AlgorithmState.Error;
                    return false;
                }
                
                // Load configuration
                if (!LoadConfiguration())
                    return false;
                
                // Set initial volatility
                CurrentVolatility = GetParameterValue<double>("InitialVolatility", 0.5);
                
                // Set trading mode based on configuration
                _currentTradingMode = GetParameterValue<bool>("EnableTrading", false) ? 
                    TradingMode.Normal : TradingMode.MonitorOnly;
                
                // Initialize performance metrics
                Performance.Reset();
                
                // Mark as initialized
                _isInitialized = true;
                State = AlgorithmState.Ready;
                
                LogMessage("Algorithm core initialized successfully", LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error initializing algorithm core: {ex.Message}", LogLevel.Error);
                State = AlgorithmState.Error;
                return false;
            }
        }

        /// <summary>
        /// Loads configuration from JSON files
        /// </summary>
        private bool LoadConfiguration()
        {
            try
            {
                string configFile = Path.Combine(_configPath, "strategy_params.json");
                if (!File.Exists(configFile))
                {
                    LogMessage($"Configuration file not found: {configFile}", LogLevel.Error);
                    return false;
                }
                
                string json = File.ReadAllText(configFile);
                _parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                
                // Load risk profiles if available
                string riskFile = Path.Combine(_configPath, "risk_profiles.json");
                if (File.Exists(riskFile))
                {
                    string riskJson = File.ReadAllText(riskFile);
                    var riskProfiles = JsonConvert.DeserializeObject<Dictionary<string, object>>(riskJson);
                    
                    // Merge risk profile parameters
                    string activeProfile = GetParameterValue<string>("ActiveRiskProfile", "default");
                    if (riskProfiles.ContainsKey(activeProfile))
                    {
                        var profileParams = riskProfiles[activeProfile] as Newtonsoft.Json.Linq.JObject;
                        if (profileParams != null)
                        {
                            foreach (var prop in profileParams.Properties())
                            {
                                _parameters[$"Risk.{prop.Name}"] = prop.Value.ToObject<object>();
                            }
                        }
                    }
                }
                
                LogMessage("Configuration loaded successfully", LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading configuration: {ex.Message}", LogLevel.Error);
                return false;
            }
        }
        #endregion

        #region Core Algorithm Methods
        /// <summary>
        /// Starts the algorithm execution
        /// </summary>
        /// <returns>True if started successfully</returns>
        public bool Start()
        {
            if (!_isInitialized)
            {
                LogMessage("Cannot start: Algorithm not initialized", LogLevel.Error);
                return false;
            }
            
            // Verificar licencia antes de iniciar
            if (!VerifyLicense())
            {
                LogMessage("Cannot start: License is not valid", LogLevel.Error);
                State = AlgorithmState.Error;
                return false;
            }
            
            try
            {
                State = AlgorithmState.Running;
                LogMessage("Algorithm started", LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error starting algorithm: {ex.Message}", LogLevel.Error);
                State = AlgorithmState.Error;
                return false;
            }
        }

        /// <summary>
        /// Pauses algorithm execution
        /// </summary>
        public void Pause()
        {
            if (State == AlgorithmState.Running)
            {
                State = AlgorithmState.Paused;
                LogMessage("Algorithm paused", LogLevel.Info);
            }
        }

        /// <summary>
        /// Resumes algorithm execution
        /// </summary>
        public void Resume()
        {
            if (State == AlgorithmState.Paused)
            {
                State = AlgorithmState.Running;
                LogMessage("Algorithm resumed", LogLevel.Info);
            }
        }

        /// <summary>
        /// Stops algorithm execution
        /// </summary>
        public void Stop()
        {
            State = AlgorithmState.ShuttingDown;
            LogMessage("Algorithm stopping", LogLevel.Info);
            
            // Perform any cleanup
            _detectedPatterns.Clear();
            
            State = AlgorithmState.Ready;
        }

        /// <summary>
        /// Updates market state based on current data
        /// </summary>
        /// <param name="currentPrice">Current price</param>
        /// <param name="recentPrices">Recent price history</param>
        /// <param name="currentVolatility">Current volatility estimation</param>
        /// <param name="isSessionStart">Whether this is near session start</param>
        /// <param name="isSessionEnd">Whether this is near session end</param>
        public void UpdateMarketState(double currentPrice, List<double> recentPrices, 
            double currentVolatility, bool isSessionStart, bool isSessionEnd)
        {
            // Ensure we're initialized and running
            if (State != AlgorithmState.Running && State != AlgorithmState.Paused)
                return;
                
            try
            {
                lock (_lockObject)
                {
                    // Update volatility tracking
                    CurrentVolatility = currentVolatility;
                    
                    // Handle session boundaries
                    if (isSessionStart)
                    {
                        _currentMarketState = MarketState.Opening;
                        return;
                    }
                    else if (isSessionEnd)
                    {
                        _currentMarketState = MarketState.Closing;
                        return;
                    }
                    
                    // Need enough price history
                    if (recentPrices == null || recentPrices.Count < 10)
                    {
                        _currentMarketState = MarketState.Unknown;
                        return;
                    }
                    
                    // Check for high volatility
                    double volatilityThreshold = GetParameterValue<double>("VolatilityThreshold", 1.5);
                    if (currentVolatility > volatilityThreshold)
                    {
                        _currentMarketState = MarketState.Volatile;
                        return;
                    }
                    
                    // Check for low volatility
                    double lowVolatilityThreshold = GetParameterValue<double>("LowVolatilityThreshold", 0.3);
                    if (currentVolatility < lowVolatilityThreshold)
                    {
                        _currentMarketState = MarketState.LowVolatility;
                        return;
                    }
                    
                    // Analyze trend using simple linear regression
                    double slope = CalculateTrendSlope(recentPrices);
                    double slopeThreshold = GetParameterValue<double>("TrendSlopeThreshold", 0.5);
                    
                    if (Math.Abs(slope) < slopeThreshold)
                    {
                        _currentMarketState = MarketState.Ranging;
                    }
                    else if (slope > slopeThreshold)
                    {
                        _currentMarketState = MarketState.TrendingUp;
                    }
                    else // slope < -slopeThreshold
                    {
                        _currentMarketState = MarketState.TrendingDown;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating market state: {ex.Message}", LogLevel.Error);
                _currentMarketState = MarketState.Unknown;
            }
        }

        /// <summary>
        /// Analyzes order flow data to detect patterns
        /// </summary>
        /// <param name="marketDepthData">Current market depth data</param>
        /// <param name="timeAndSalesData">Recent time and sales data</param>
        /// <param name="volumeByPrice">Volume distribution by price</param>
        /// <param name="cumulativeDelta">Cumulative delta value</param>
        /// <returns>List of detected patterns</returns>
        public List<OrderFlowPattern> AnalyzeOrderFlow(Dictionary<double, (long BidVolume, long AskVolume)> marketDepthData,
            List<(DateTime Time, double Price, long Volume, bool IsBuy)> timeAndSalesData,
            Dictionary<double, (long BuyVolume, long SellVolume)> volumeByPrice,
            double cumulativeDelta)
        {
            // Ensure we're initialized and running
            if (State != AlgorithmState.Running)
                return new List<OrderFlowPattern>();
                
            try
            {
                lock (_lockObject)
                {
                    // Clear previous patterns
                    _detectedPatterns.Clear();
                    
                    // Analyze order book imbalances
                    AnalyzeOrderBookImbalances(marketDepthData);
                    
                    // Analyze absorption patterns
                    AnalyzeAbsorptionPatterns(volumeByPrice, cumulativeDelta);
                    
                    // Analyze aggressive orders
                    AnalyzeAggressiveOrders(timeAndSalesData);
                    
                    // Return copy of detected patterns
                    return _detectedPatterns.ToList();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error analyzing order flow: {ex.Message}", LogLevel.Error);
                return new List<OrderFlowPattern>();
            }
        }

        /// <summary>
        /// Processes a trading signal and determines if it should be executed
        /// </summary>
        /// <param name="signalType">Type of signal (e.g., Buy, Sell)</param>
        /// <param name="price">Signal price</param>
        /// <param name="strength">Signal strength (0-1)</param>
        /// <param name="additionalData">Additional signal data</param>
        /// <returns>Whether signal should be executed</returns>
        public bool ProcessSignal(string signalType, double price, double strength, 
            Dictionary<string, object> additionalData = null)
        {
            // Ensure we're initialized and running
            if (State != AlgorithmState.Running || _currentTradingMode == TradingMode.Disabled || 
                _currentTradingMode == TradingMode.MonitorOnly)
                return false;
            
            // Verificar licencia periódicamente
            if ((DateTime.Now - _lastLicenseCheck) > _licenseCheckInterval)
            {
                if (!VerifyLicense())
                {
                    LogMessage("Signal rejected: License validation failed", LogLevel.Warning);
                    return false;
                }
            }
                
            try
            {
                lock (_lockObject)
                {
                    // Track signal for metrics
                    Performance.TotalSignals++;
                    
                    // Check minimum time between signals
                    double signalCooldownSeconds = GetParameterValue<double>("SignalCooldownSeconds", 30);
                    if (_lastSignalTime != DateTime.MinValue && 
                        (DateTime.Now - _lastSignalTime).TotalSeconds < signalCooldownSeconds)
                    {
                        LogMessage($"Signal rejected: Cooldown period not elapsed ({signalCooldownSeconds}s)", LogLevel.Info);
                        return false;
                    }
                    
                    // Check signal strength threshold based on trading mode
                    double strengthThreshold = 0.6; // Default threshold
                    
                    switch (_currentTradingMode)
                    {
                        case TradingMode.Aggressive:
                            strengthThreshold = GetParameterValue<double>("AggressiveSignalThreshold", 0.4);
                            break;
                        case TradingMode.Normal:
                            strengthThreshold = GetParameterValue<double>("NormalSignalThreshold", 0.6);
                            break;
                        case TradingMode.Conservative:
                            strengthThreshold = GetParameterValue<double>("ConservativeSignalThreshold", 0.8);
                            break;
                    }
                    
                    // Check if signal is strong enough
                    if (strength < strengthThreshold)
                    {
                        LogMessage($"Signal rejected: Strength too low ({strength} < {strengthThreshold})", LogLevel.Info);
                        return false;
                    }
                    
                    // Validate signal against market state
                    bool isValidForMarketState = ValidateSignalForMarketState(signalType, _currentMarketState);
                    if (!isValidForMarketState)
                    {
                        LogMessage($"Signal rejected: Not valid for current market state ({_currentMarketState})", LogLevel.Info);
                        return false;
                    }
                    
                    // Signal passes validation
                    Performance.ValidSignals++;
                    _lastSignalTime = DateTime.Now;
                    
                    LogMessage($"Signal accepted: {signalType} at {price} with strength {strength}", LogLevel.Info);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing signal: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Updates trading mode based on performance and market conditions
        /// </summary>
        public void UpdateTradingMode()
        {
            // Don't change mode if disabled
            if (_currentTradingMode == TradingMode.Disabled)
                return;
                
            try
            {
                bool adaptiveMode = GetParameterValue<bool>("AdaptiveTradingMode", true);
                if (!adaptiveMode)
                    return;
                    
                TradingMode newMode = _currentTradingMode;
                
                // Adapt based on market state
                switch (_currentMarketState)
                {
                    case MarketState.Volatile:
                        newMode = TradingMode.Conservative;
                        break;
                    case MarketState.LowVolatility:
                        newMode = TradingMode.Aggressive;
                        break;
                    case MarketState.Ranging:
                        newMode = TradingMode.Normal;
                        break;
                    case MarketState.Opening:
                    case MarketState.Closing:
                        newMode = TradingMode.Conservative;
                        break;
                }
                
                // Also consider recent performance
                if (Performance.ExecutedTrades >= 10)
                {
                    if (Performance.WinRate < 0.4)
                    {
                        // Poor performance - be more conservative
                        newMode = newMode == TradingMode.Aggressive ? TradingMode.Normal :
                                 newMode == TradingMode.Normal ? TradingMode.Conservative :
                                 TradingMode.Conservative;
                    }
                    else if (Performance.WinRate > 0.7)
                    {
                        // Great performance - be more aggressive
                        newMode = newMode == TradingMode.Conservative ? TradingMode.Normal :
                                 newMode == TradingMode.Normal ? TradingMode.Aggressive :
                                 TradingMode.Aggressive;
                    }
                }
                
                // Apply changes if different
                if (newMode != _currentTradingMode)
                {
                    LogMessage($"Trading mode changed: {_currentTradingMode} -> {newMode}", LogLevel.Info);
                    _currentTradingMode = newMode;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating trading mode: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Updates performance metrics with trade result
        /// </summary>
        /// <param name="isWin">Whether trade was profitable</param>
        /// <param name="profitLoss">Profit/loss amount</param>
        public void UpdatePerformance(bool isWin, double profitLoss)
        {
            try
            {
                lock (_lockObject)
                {
                    Performance.ExecutedTrades++;
                    
                    if (isWin)
                    {
                        Performance.WinningTrades++;
                        
                        // Update average win
                        Performance.AverageWin = 
                            (Performance.AverageWin * (Performance.WinningTrades - 1) + profitLoss) / 
                            Performance.WinningTrades;
                    }
                    else
                    {
                        Performance.LosingTrades++;
                        
                        // Update average loss (profitLoss is negative)
                        Performance.AverageLoss = 
                            (Performance.AverageLoss * (Performance.LosingTrades - 1) + profitLoss) / 
                            Performance.LosingTrades;
                    }
                    
                    // Update net profit
                    Performance.NetProfit += profitLoss;
                    
                    // Update max drawdown if applicable
                    if (profitLoss < 0 && Math.Abs(profitLoss) > Performance.MaxDrawdown)
                    {
                        Performance.MaxDrawdown = Math.Abs(profitLoss);
                    }
                    
                    // Recalculate Sharpe ratio (simplified)
                    if (Performance.ExecutedTrades >= 10)
                    {
                        double avgReturn = Performance.NetProfit / Performance.ExecutedTrades;
                        double stdDev = Math.Sqrt(
                            (Performance.WinningTrades * Math.Pow(Performance.AverageWin - avgReturn, 2) +
                             Performance.LosingTrades * Math.Pow(Performance.AverageLoss - avgReturn, 2)) /
                            Performance.ExecutedTrades);
                        
                        if (stdDev > 0)
                            Performance.SharpeRatio = avgReturn / stdDev;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error updating performance: {ex.Message}", LogLevel.Error);
            }
        }
        #endregion

        #region License Verification
        /// <summary>
        /// Verifies that the license is valid
        /// </summary>
        /// <returns>True if license is valid</returns>
        private bool VerifyLicense()
        {
            try
            {
                // Solo verificar la licencia a intervalos regulares después de la primera vez
                if (_isLicenseValid && (DateTime.Now - _lastLicenseCheck) < _licenseCheckInterval)
                    return true;
                
                // Inicializar y verificar licencia
                var licenseManager = LicenseManager.Instance;
                bool initialized = licenseManager.Initialize();
                _isLicenseValid = initialized && licenseManager.IsLicenseValid();
                _lastLicenseCheck = DateTime.Now;
                
                if (_isLicenseValid)
                {
                    // Verificar si la licencia está a punto de expirar (menos de 3 días)
                    int remainingDays = licenseManager.GetRemainingDays();
                    if (remainingDays >= 0 && remainingDays <= 3)
                    {
                        LogMessage($"ADVERTENCIA: Su licencia expira en {remainingDays} días. Por favor contacta a Javier Lora email: jvlora@hublai.com", LogLevel.Warning);
                    }
                    
                    // Registrar información de la licencia
                    var licenseType = licenseManager.GetLicenseType();
                    LogMessage($"Licencia válida. Tipo: {licenseType}, Expira: {licenseManager.GetExpirationDate():yyyy-MM-dd}", LogLevel.Info);
                }
                else
                {
                    LogMessage("Licencia no válida o expirada. Por favor contacta a Javier Lora email: jvlora@hublai.com", LogLevel.Warning);
                }
                
                return _isLicenseValid;
            }
            catch (Exception ex)
            {
                LogMessage($"Error verificando licencia: {ex.Message}", LogLevel.Error);
                return false;
            }
        }
        #endregion

        #region Order Flow Analysis Methods
        /// <summary>
        /// Analyzes order book to detect imbalances
        /// </summary>
        private void AnalyzeOrderBookImbalances(Dictionary<double, (long BidVolume, long AskVolume)> marketDepthData)
        {
            // Skip if no data
            if (marketDepthData == null || marketDepthData.Count == 0)
                return;
                
            // Get imbalance threshold from config
            double imbalanceThreshold = GetParameterValue<double>("OrderBookImbalanceThreshold", 2.0);
            
            // Analyze each price level
            foreach (var kvp in marketDepthData)
            {
                double price = kvp.Key;
                long bidVolume = kvp.Value.BidVolume;
                long askVolume = kvp.Value.AskVolume;
                
                // Skip levels with insufficient volume
                long minVolume = GetParameterValue<long>("MinimumVolumeThreshold", 10);
                if (bidVolume < minVolume && askVolume < minVolume)
                    continue;
                    
                // Calculate imbalance ratio
                double ratio = 0;
                string patternType = "";
                
                if (bidVolume > askVolume && askVolume > 0)
                {
                    ratio = (double)bidVolume / askVolume;
                    patternType = "BidImbalance";
                }
                else if (askVolume > bidVolume && bidVolume > 0)
                {
                    ratio = (double)askVolume / bidVolume;
                    patternType = "AskImbalance";
                }
                
                // Check if significant imbalance
                if (ratio >= imbalanceThreshold)
                {
                    // Calculate pattern strength (0-1)
                    double normalizedRatio = Math.Min(ratio / (imbalanceThreshold * 2), 1.0);
                    
                    // Create pattern
                    _detectedPatterns.Add(new OrderFlowPattern
                    {
                        PatternType = patternType,
                        Price = price,
                        DetectionTime = DateTime.Now,
                        Strength = normalizedRatio,
                        Probability = CalculateProbability(patternType, normalizedRatio),
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "BidVolume", bidVolume },
                            { "AskVolume", askVolume },
                            { "Ratio", ratio }
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Analyzes volume distribution to detect absorption patterns
        /// </summary>
        private void AnalyzeAbsorptionPatterns(Dictionary<double, (long BuyVolume, long SellVolume)> volumeByPrice, 
            double cumulativeDelta)
        {
            // Skip if no data
            if (volumeByPrice == null || volumeByPrice.Count == 0)
                return;
                
            // Get absorption threshold from config
            double absorptionThreshold = GetParameterValue<double>("AbsorptionThreshold", 2.0);
            
            // Analyze each price level
            foreach (var kvp in volumeByPrice)
            {
                double price = kvp.Key;
                long buyVolume = kvp.Value.BuyVolume;
                long sellVolume = kvp.Value.SellVolume;
                
                // Skip levels with insufficient volume
                long minVolume = GetParameterValue<long>("MinimumVolumeThreshold", 50);
                if (buyVolume + sellVolume < minVolume)
                    continue;
                    
                // Check for buy absorption (high sell volume but price isn't dropping)
                if (sellVolume > buyVolume * absorptionThreshold && cumulativeDelta > 0)
                {
                    // Calculate pattern strength (0-1)
                    double volumeRatio = (double)sellVolume / (buyVolume + sellVolume);
                    double normalizedStrength = Math.Min(volumeRatio * 1.5, 1.0);
                    
                    // Create pattern
                    _detectedPatterns.Add(new OrderFlowPattern
                    {
                        PatternType = "BuyAbsorption",
                        Price = price,
                        DetectionTime = DateTime.Now,
                        Strength = normalizedStrength,
                        Probability = CalculateProbability("BuyAbsorption", normalizedStrength),
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "BuyVolume", buyVolume },
                            { "SellVolume", sellVolume },
                            { "CumulativeDelta", cumulativeDelta }
                        }
                    });
                }
                
                // Check for sell absorption (high buy volume but price isn't rising)
                if (buyVolume > sellVolume * absorptionThreshold && cumulativeDelta < 0)
                {
                    // Calculate pattern strength (0-1)
                    double volumeRatio = (double)buyVolume / (buyVolume + sellVolume);
                    double normalizedStrength = Math.Min(volumeRatio * 1.5, 1.0);
                    
                    // Create pattern
                    _detectedPatterns.Add(new OrderFlowPattern
                    {
                        PatternType = "SellAbsorption",
                        Price = price,
                        DetectionTime = DateTime.Now,
                        Strength = normalizedStrength,
                        Probability = CalculateProbability("SellAbsorption", normalizedStrength),
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "BuyVolume", buyVolume },
                            { "SellVolume", sellVolume },
                            { "CumulativeDelta", cumulativeDelta }
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Analyzes time and sales data to detect aggressive order patterns
        /// </summary>
        private void AnalyzeAggressiveOrders(List<(DateTime Time, double Price, long Volume, bool IsBuy)> timeAndSalesData)
        {
            // Skip if insufficient data
            if (timeAndSalesData == null || timeAndSalesData.Count < 5)
                return;
                
            // Get configuration parameters
            int aggressiveOrderCount = GetParameterValue<int>("AggressiveOrderCountThreshold", 3);
            int clusterTimeWindowMs = GetParameterValue<int>("AggressiveOrderTimeWindowMs", 500);
            long largeOrderThreshold = GetParameterValue<long>("LargeOrderThreshold", 20);
            
            // Look for clusters of aggressive orders
            List<(DateTime Time, double Price, long Volume, bool IsBuy)> buyCluster = new List<(DateTime, double, long, bool)>();
            List<(DateTime Time, double Price, long Volume, bool IsBuy)> sellCluster = new List<(DateTime, double, long, bool)>();
            
            // Process each transaction
            foreach (var transaction in timeAndSalesData)
            {
                // Check for large order
                if (transaction.Volume >= largeOrderThreshold)
                {
                    string patternType = transaction.IsBuy ? "LargeBuyOrder" : "LargeSellOrder";
                    double normalizedSize = Math.Min((double)transaction.Volume / (largeOrderThreshold * 2), 1.0);
                    
                    _detectedPatterns.Add(new OrderFlowPattern
                    {
                        PatternType = patternType,
                        Price = transaction.Price,
                        DetectionTime = transaction.Time,
                        Strength = normalizedSize,
                        Probability = CalculateProbability(patternType, normalizedSize),
                        AdditionalData = new Dictionary<string, object>
                        {
                            { "Volume", transaction.Volume }
                        }
                    });
                }
                
                // Update clusters
                if (transaction.IsBuy)
                {
                    // Remove old transactions from cluster
                    buyCluster = buyCluster
                        .Where(t => (transaction.Time - t.Time).TotalMilliseconds <= clusterTimeWindowMs)
                        .ToList();
                        
                    // Add new transaction
                    buyCluster.Add(transaction);
                    
                    // Check if we have a cluster
                    if (buyCluster.Count >= aggressiveOrderCount)
                    {
                        double avgPrice = buyCluster.Average(t => t.Price);
                        long totalVolume = buyCluster.Sum(t => t.Volume);
                        double normalizedStrength = Math.Min((double)buyCluster.Count / (aggressiveOrderCount * 1.5), 1.0);
                        
                        _detectedPatterns.Add(new OrderFlowPattern
                        {
                            PatternType = "BuyOrderCluster",
                            Price = avgPrice,
                            DetectionTime = transaction.Time,
                            Strength = normalizedStrength,
                            Probability = CalculateProbability("BuyOrderCluster", normalizedStrength),
                            AdditionalData = new Dictionary<string, object>
                            {
                                { "Count", buyCluster.Count },
                                { "TotalVolume", totalVolume }
                            }
                        });
                    }
                }
                else // Sell transaction
                {
                    // Remove old transactions from cluster
                    sellCluster = sellCluster
                        .Where(t => (transaction.Time - t.Time).TotalMilliseconds <= clusterTimeWindowMs)
                        .ToList();
                        
                    // Add new transaction
                    sellCluster.Add(transaction);
                    
                    // Check if we have a cluster
                    if (sellCluster.Count >= aggressiveOrderCount)
                    {
                        double avgPrice = sellCluster.Average(t => t.Price);
                        long totalVolume = sellCluster.Sum(t => t.Volume);
                        double normalizedStrength = Math.Min((double)sellCluster.Count / (aggressiveOrderCount * 1.5), 1.0);
                        
                        _detectedPatterns.Add(new OrderFlowPattern
                        {
                            PatternType = "SellOrderCluster",
                            Price = avgPrice,
                            DetectionTime = transaction.Time,
                            Strength = normalizedStrength,
                            Probability = CalculateProbability("SellOrderCluster", normalizedStrength),
                            AdditionalData = new Dictionary<string, object>
                            {
                                { "Count", sellCluster.Count },
                                { "TotalVolume", totalVolume }
                            }
                        });
                    }
                }
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Calculate trend slope from price series
        /// </summary>
        private double CalculateTrendSlope(List<double> prices)
        {
            // Need at least 2 points for a slope
            if (prices == null || prices.Count < 2)
                return 0;
                
            // Simple linear regression
            int n = prices.Count;
            int[] x = Enumerable.Range(0, n).ToArray();
            double sumX = x.Sum();
            double sumY = prices.Sum();
            double sumXY = x.Zip(prices, (xi, yi) => xi * yi).Sum();
            double sumX2 = x.Select(xi => xi * xi).Sum();
            
            // Calculate slope
            double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            
            // Normalize by price level
            double avgPrice = prices.Average();
            return slope / avgPrice * 100; // As percentage
        }

        /// <summary>
        /// Calculate signal probability based on pattern type and strength
        /// </summary>
        private double CalculateProbability(string patternType, double strength)
        {
            // Base probability from strength
            double baseProbability = strength * 0.7;
            
            // Adjust based on pattern type
            switch (patternType)
            {
                case "BuyAbsorption":
                case "SellAbsorption":
                    // Absorption patterns tend to be more reliable
                    baseProbability *= 1.2;
                    break;
                case "BidImbalance":
                case "AskImbalance":
                    // DOM imbalances can be less reliable
                    baseProbability *= 0.9;
                    break;
                case "BuyOrderCluster":
                case "SellOrderCluster":
                    // Clusters reliability depends on market conditions
                    baseProbability *= _currentMarketState == MarketState.Ranging ? 1.1 : 0.95;
                    break;
                case "LargeBuyOrder":
                case "LargeSellOrder":
                    // Large orders can be significant
                    baseProbability *= 1.05;
                    break;
            }
            
            // Adjust based on market state
            if (patternType.StartsWith("Buy") && _currentMarketState == MarketState.TrendingDown)
                baseProbability *= 0.8; // Buy signals less reliable in downtrend
            else if (patternType.StartsWith("Buy") && _currentMarketState == MarketState.TrendingUp)
                baseProbability *= 1.1; // Buy signals more reliable in uptrend
            else if (patternType.StartsWith("Sell") && _currentMarketState == MarketState.TrendingUp)
                baseProbability *= 0.8; // Sell signals less reliable in uptrend
            else if (patternType.StartsWith("Sell") && _currentMarketState == MarketState.TrendingDown)
                baseProbability *= 1.1; // Sell signals more reliable in downtrend
                
            // Clamp to 0-1 range
            return Math.Max(0, Math.Min(1, baseProbability));
        }

        /// <summary>
        /// Validate if signal is appropriate for current market state
        /// </summary>
        private bool ValidateSignalForMarketState(string signalType, MarketState marketState)
        {
            // Default validation logic based on signal type and market state
            bool isBuySignal = signalType.StartsWith("Buy") || signalType.Contains("Long");
            bool isSellSignal = signalType.StartsWith("Sell") || signalType.Contains("Short");
            
            // Validate against market state
            switch (marketState)
            {
                case MarketState.Volatile:
                    // Be more conservative in volatile markets
                    return (isBuySignal && signalType.Contains("Absorption")) ||
                           (isSellSignal && signalType.Contains("Absorption"));
                           
                case MarketState.TrendingUp:
                    // Favor buy signals in uptrend
                    return isBuySignal || (isSellSignal && signalType.Contains("Absorption"));
                    
                case MarketState.TrendingDown:
                    // Favor sell signals in downtrend
                    return isSellSignal || (isBuySignal && signalType.Contains("Absorption"));
                    
                case MarketState.Ranging:
                    // Both signals valid in ranging markets
                    return true;
                    
                case MarketState.Opening:
                    // Be cautious during opening
                    return signalType.Contains("Absorption") || signalType.Contains("Cluster");
                    
                case MarketState.Closing:
                    // Limit signals during closing
                    return false;
                    
                case MarketState.LowVolatility:
                    // Favor order flow patterns in low volatility
                    return signalType.Contains("Imbalance") || signalType.Contains("Cluster");
                    
                default: // Unknown
                    return false;
            }
        }

        /// <summary>
        /// Gets parameter value from configuration with default fallback
        /// </summary>
        public T GetParameterValue<T>(string key, T defaultValue)
        {
            if (_parameters.ContainsKey(key))
            {
                try
                {
                    if (_parameters[key] is Newtonsoft.Json.Linq.JToken token)
                    {
                        return token.ToObject<T>();
                    }
                    
                    return (T)Convert.ChangeType(_parameters[key], typeof(T));
                }
                catch
                {
                    LogMessage($"Error converting parameter {key} to type {typeof(T).Name}", LogLevel.Warning);
                    return defaultValue;
                }
            }
            
            return defaultValue;
        }

        /// <summary>
        /// Log message with level
        /// </summary>
        private void LogMessage(string message, LogLevel level)
        {
            // Simple console logging for now
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] AlgorithmCore: {message}");
            
            // In a real implementation, this would integrate with a proper logging system
        }

        /// <summary>
        /// Log levels for messages
        /// </summary>
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }
        #endregion
    }
}
