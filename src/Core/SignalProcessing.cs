using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OrderFlowScalper.Core
{
    /// <summary>
    /// Processes, filters, and validates trading signals based on order flow analysis
    /// </summary>
    public class SignalProcessing
    {
        #region Private Fields
        private readonly Dictionary<string, List<SignalEvent>> _signalHistory;
        private readonly Dictionary<string, double> _signalWeights;
        private readonly object _lockObject = new object();
        private readonly int _signalHistoryLength;
        private readonly int _minimumSignalsForValidation;
        #endregion

        #region Public Properties
        /// <summary>
        /// Weight for different signal types in combined analysis
        /// </summary>
        public IReadOnlyDictionary<string, double> SignalWeights => _signalWeights;
        
        /// <summary>
        /// Recent signal history
        /// </summary>
        public IReadOnlyDictionary<string, List<SignalEvent>> SignalHistory => _signalHistory;
        
        /// <summary>
        /// Quality threshold for signal validation
        /// </summary>
        public double QualityThreshold { get; set; }
        
        /// <summary>
        /// Time window for evaluating multiple signals (milliseconds)
        /// </summary>
        public int ConfluenceTimeWindowMs { get; set; }
        #endregion

        #region Signal Classes
        /// <summary>
        /// Represents a trading signal event
        /// </summary>
        public class SignalEvent
        {
            /// <summary>
            /// Signal type identifier
            /// </summary>
            public string Type { get; set; }
            
            /// <summary>
            /// Direction of the signal (e.g., Buy, Sell)
            /// </summary>
            public SignalDirection Direction { get; set; }
            
            /// <summary>
            /// Time the signal was generated
            /// </summary>
            public DateTime Time { get; set; }
            
            /// <summary>
            /// Price level associated with the signal
            /// </summary>
            public double Price { get; set; }
            
            /// <summary>
            /// Signal strength (0-1)
            /// </summary>
            public double Strength { get; set; }
            
            /// <summary>
            /// Signal quality metric (0-1)
            /// </summary>
            public double Quality { get; set; }
            
            /// <summary>
            /// Additional properties for the signal
            /// </summary>
            public Dictionary<string, object> Properties { get; set; }
            
            /// <summary>
            /// Whether signal has been validated
            /// </summary>
            public bool IsValidated { get; set; }
            
            /// <summary>
            /// Whether signal has been executed
            /// </summary>
            public bool IsExecuted { get; set; }
            
            /// <summary>
            /// Outcome after execution (if known)
            /// </summary>
            public SignalOutcome Outcome { get; set; }
        }
        
        /// <summary>
        /// Represents a consolidated signal from multiple sources
        /// </summary>
        public class ConsolidatedSignal
        {
            /// <summary>
            /// Direction of the signal
            /// </summary>
            public SignalDirection Direction { get; set; }
            
            /// <summary>
            /// Timestamp of consolidated signal
            /// </summary>
            public DateTime Time { get; set; }
            
            /// <summary>
            /// Suggested price for execution
            /// </summary>
            public double Price { get; set; }
            
            /// <summary>
            /// Combined strength of all signals
            /// </summary>
            public double Strength { get; set; }
            
            /// <summary>
            /// Quality assessment of the signal
            /// </summary>
            public double Quality { get; set; }
            
            /// <summary>
            /// Probability of successful outcome
            /// </summary>
            public double Probability { get; set; }
            
            /// <summary>
            /// Component signals that contribute to this consolidated signal
            /// </summary>
            public List<SignalEvent> ComponentSignals { get; set; }
            
            /// <summary>
            /// Market context at time of signal
            /// </summary>
            public Dictionary<string, object> MarketContext { get; set; }
        }
        
        /// <summary>
        /// Represents the outcome of a signal after execution
        /// </summary>
        public class SignalOutcomeInfo
        {
            /// <summary>
            /// Original signal event
            /// </summary>
            public SignalEvent Signal { get; set; }
            
            /// <summary>
            /// Outcome type
            /// </summary>
            public SignalOutcome Outcome { get; set; }
            
            /// <summary>
            /// Profit/loss result (if applicable)
            /// </summary>
            public double ProfitLoss { get; set; }
            
            /// <summary>
            /// Maximum adverse excursion
            /// </summary>
            public double MaxAdverseExcursion { get; set; }
            
            /// <summary>
            /// Maximum favorable excursion
            /// </summary>
            public double MaxFavorableExcursion { get; set; }
            
            /// <summary>
            /// Time from signal to outcome
            /// </summary>
            public TimeSpan Duration { get; set; }
            
            /// <summary>
            /// Additional outcome data
            /// </summary>
            public Dictionary<string, object> OutcomeData { get; set; }
        }
        #endregion

        #region Enums
        /// <summary>
        /// Trading signal directions
        /// </summary>
        public enum SignalDirection
        {
            None,
            Buy,
            Sell
        }
        
        /// <summary>
        /// Possible outcomes of signals
        /// </summary>
        public enum SignalOutcome
        {
            Unknown,
            Win,
            Loss,
            Breakeven,
            Filtered,
            Expired,
            Canceled
        }
        
        /// <summary>
        /// Types of market context
        /// </summary>
        public enum MarketContextType
        {
            Ranging,
            Trending,
            Volatile,
            Opening,
            Closing,
            Consolidating,
            Breaking
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the SignalProcessing class
        /// </summary>
        /// <param name="signalHistoryLength">Number of signals to keep in history</param>
        /// <param name="minimumSignalsForValidation">Minimum signals required for validation</param>
        public SignalProcessing(int signalHistoryLength = 100, int minimumSignalsForValidation = 10)
        {
            _signalHistory = new Dictionary<string, List<SignalEvent>>();
            _signalWeights = new Dictionary<string, double>();
            _signalHistoryLength = signalHistoryLength;
            _minimumSignalsForValidation = minimumSignalsForValidation;
            
            // Default configurations
            QualityThreshold = 0.6;
            ConfluenceTimeWindowMs = 500;
            
            // Initialize default signal weights
            InitializeDefaultWeights();
        }
        
        /// <summary>
        /// Set default weights for different signal types
        /// </summary>
        private void InitializeDefaultWeights()
        {
            // Order flow patterns
            _signalWeights["AbsorptionPattern"] = 1.0;
            _signalWeights["StackingPattern"] = 0.8;
            _signalWeights["ExhaustionPattern"] = 0.9;
            
            // DOM signals
            _signalWeights["DOMImbalance"] = 0.7;
            _signalWeights["LimitOrderCluster"] = 0.6;
            _signalWeights["PullbackReaction"] = 0.8;
            
            // Volume signals
            _signalWeights["DeltaDivergence"] = 0.9;
            _signalWeights["VolumeSpike"] = 0.7;
            _signalWeights["VWAPBreakout"] = 0.75;
            
            // Tape reading signals
            _signalWeights["AggressiveOrder"] = 0.85;
            _signalWeights["OrderFlowShift"] = 0.9;
            _signalWeights["LargeOrderImpact"] = 0.8;
        }
        #endregion

        #region Signal Processing Methods
        /// <summary>
        /// Processes a new signal and adds it to history
        /// </summary>
        /// <param name="type">Signal type</param>
        /// <param name="direction">Signal direction</param>
        /// <param name="price">Signal price</param>
        /// <param name="strength">Signal strength</param>
        /// <param name="properties">Additional properties</param>
        /// <returns>Processed signal event</returns>
        public SignalEvent ProcessSignal(string type, SignalDirection direction, double price,
            double strength, Dictionary<string, object> properties = null)
        {
            lock (_lockObject)
            {
                // Calculate quality based on signal type, strength, and historical performance
                double quality = CalculateSignalQuality(type, direction, strength);
                
                // Create signal event
                var signal = new SignalEvent
                {
                    Type = type,
                    Direction = direction,
                    Time = DateTime.Now,
                    Price = price,
                    Strength = strength,
                    Quality = quality,
                    Properties = properties ?? new Dictionary<string, object>(),
                    IsValidated = false,
                    IsExecuted = false,
                    Outcome = SignalOutcome.Unknown
                };
                
                // Add to history
                if (!_signalHistory.ContainsKey(type))
                    _signalHistory[type] = new List<SignalEvent>();
                    
                _signalHistory[type].Add(signal);
                
                // Trim history if needed
                if (_signalHistory[type].Count > _signalHistoryLength)
                    _signalHistory[type].RemoveAt(0);
                
                return signal;
            }
        }
        
        /// <summary>
        /// Validates a signal based on quality and history
        /// </summary>
        /// <param name="signal">Signal to validate</param>
        /// <param name="marketContext">Current market context</param>
        /// <returns>Whether signal is validated</returns>
        public bool ValidateSignal(SignalEvent signal, Dictionary<string, object> marketContext = null)
        {
            if (signal == null)
                return false;
                
            lock (_lockObject)
            {
                // Check quality threshold
                if (signal.Quality < QualityThreshold)
                    return false;
                    
                // Apply contextual validation based on market conditions
                if (marketContext != null && marketContext.Count > 0)
                {
                    // Check market context compatibility with signal
                    if (!ValidateMarketContext(signal, marketContext))
                        return false;
                }
                
                // Check for similar signals in history
                if (_signalHistory.ContainsKey(signal.Type) && _signalHistory[signal.Type].Count >= _minimumSignalsForValidation)
                {
                    var similarSignals = _signalHistory[signal.Type]
                        .Where(s => s.Direction == signal.Direction && s.IsExecuted && s.Outcome != SignalOutcome.Unknown)
                        .ToList();
                        
                    if (similarSignals.Count >= 5)
                    {
                        // Calculate win rate for this signal type
                        double winRate = similarSignals.Count(s => s.Outcome == SignalOutcome.Win) / (double)similarSignals.Count;
                        
                        // Reject signal if historical performance is poor
                        if (winRate < 0.4)
                            return false;
                    }
                }
                
                // Mark as validated
                signal.IsValidated = true;
                return true;
            }
        }
        
        /// <summary>
        /// Consolidates multiple signals within a time window into a single signal
        /// </summary>
        /// <param name="recentSignals">Recent signals to consolidate</param>
        /// <param name="marketContext">Current market context</param>
        /// <returns>Consolidated signal if valid, null otherwise</returns>
        public ConsolidatedSignal ConsolidateSignals(List<SignalEvent> recentSignals, 
            Dictionary<string, object> marketContext = null)
        {
            if (recentSignals == null || recentSignals.Count == 0)
                return null;
                
            lock (_lockObject)
            {
                // Group signals by direction
                var buySignals = recentSignals.Where(s => s.Direction == SignalDirection.Buy).ToList();
                var sellSignals = recentSignals.Where(s => s.Direction == SignalDirection.Sell).ToList();
                
                // Find dominant direction
                SignalDirection dominantDirection = SignalDirection.None;
                List<SignalEvent> dominantSignals;
                
                // Calculate weighted strength for each direction
                double buyStrength = CalculateWeightedStrength(buySignals);
                double sellStrength = CalculateWeightedStrength(sellSignals);
                
                if (buyStrength > sellStrength)
                {
                    dominantDirection = SignalDirection.Buy;
                    dominantSignals = buySignals;
                }
                else if (sellStrength > buyStrength)
                {
                    dominantDirection = SignalDirection.Sell;
                    dominantSignals = sellSignals;
                }
                else
                {
                    // No clear direction
                    return null;
                }
                
                // Need at least 2 signals in dominant direction for confluence
                if (dominantSignals.Count < 2)
                    return null;
                    
                // Create consolidated signal
                var consolidated = new ConsolidatedSignal
                {
                    Direction = dominantDirection,
                    Time = dominantSignals.Max(s => s.Time),
                    // Use price from strongest signal
                    Price = dominantSignals.OrderByDescending(s => s.Strength).First().Price,
                    Strength = dominantDirection == SignalDirection.Buy ? buyStrength : sellStrength,
                    Quality = dominantSignals.Average(s => s.Quality),
                    ComponentSignals = dominantSignals,
                    MarketContext = marketContext ?? new Dictionary<string, object>()
                };
                
                // Calculate probability based on strength, quality, and market context
                consolidated.Probability = CalculateSignalProbability(consolidated);
                
                return consolidated;
            }
        }
        
        /// <summary>
        /// Records the outcome of an executed signal
        /// </summary>
        /// <param name="signal">The signal</param>
        /// <param name="outcome">The outcome</param>
        /// <param name="profitLoss">Profit/loss amount</param>
        /// <param name="additionalData">Additional data about the outcome</param>
        /// <returns>Detailed outcome information</returns>
        public SignalOutcomeInfo RecordSignalOutcome(SignalEvent signal, SignalOutcome outcome, 
            double profitLoss, Dictionary<string, object> additionalData = null)
        {
            if (signal == null)
                return null;
                
            lock (_lockObject)
            {
                // Update signal
                signal.IsExecuted = true;
                signal.Outcome = outcome;
                
                if (additionalData != null)
                {
                    foreach (var kvp in additionalData)
                    {
                        signal.Properties[$"Outcome.{kvp.Key}"] = kvp.Value;
                    }
                }
                
                // Create outcome info
                var outcomeInfo = new SignalOutcomeInfo
                {
                    Signal = signal,
                    Outcome = outcome,
                    ProfitLoss = profitLoss,
                    OutcomeData = additionalData ?? new Dictionary<string, object>()
                };
                
                // Extract MAE/MFE if available
                if (additionalData != null)
                {
                    if (additionalData.ContainsKey("MaxAdverseExcursion"))
                        outcomeInfo.MaxAdverseExcursion = Convert.ToDouble(additionalData["MaxAdverseExcursion"]);
                        
                    if (additionalData.ContainsKey("MaxFavorableExcursion"))
                        outcomeInfo.MaxFavorableExcursion = Convert.ToDouble(additionalData["MaxFavorableExcursion"]);
                        
                    if (additionalData.ContainsKey("Duration"))
                        outcomeInfo.Duration = TimeSpan.FromSeconds(Convert.ToDouble(additionalData["Duration"]));
                }
                
                return outcomeInfo;
            }
        }
        
        /// <summary>
        /// Updates signal weights based on historical performance
        /// </summary>
        public void UpdateSignalWeights()
        {
            lock (_lockObject)
            {
                // For each signal type
                foreach (var type in _signalHistory.Keys)
                {
                    var signals = _signalHistory[type]
                        .Where(s => s.IsExecuted && s.Outcome != SignalOutcome.Unknown)
                        .ToList();
                        
                    if (signals.Count >= _minimumSignalsForValidation)
                    {
                        // Calculate win rate
                        double winRate = signals.Count(s => s.Outcome == SignalOutcome.Win) / (double)signals.Count;
                        
                        // Calculate average profit
                        double avgProfit = signals
                            .Where(s => s.Properties.ContainsKey("Outcome.ProfitLoss"))
                            .Average(s => Convert.ToDouble(s.Properties["Outcome.ProfitLoss"]));
                            
                        // Update weight based on performance
                        if (_signalWeights.ContainsKey(type))
                        {
                            double currentWeight = _signalWeights[type];
                            
                            // Adjust weight: increase if win rate > 0.6, decrease if < 0.4
                            if (winRate > 0.6)
                                _signalWeights[type] = Math.Min(1.0, currentWeight * 1.1);
                            else if (winRate < 0.4)
                                _signalWeights[type] = Math.Max(0.1, currentWeight * 0.9);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets recent signals within a specific time window
        /// </summary>
        /// <param name="timeWindowMs">Time window in milliseconds</param>
        /// <returns>List of recent signals</returns>
        public List<SignalEvent> GetRecentSignals(int timeWindowMs)
        {
            lock (_lockObject)
            {
                DateTime cutoff = DateTime.Now.AddMilliseconds(-timeWindowMs);
                
                return _signalHistory.Values
                    .SelectMany(signals => signals)
                    .Where(s => s.Time >= cutoff)
                    .OrderByDescending(s => s.Time)
                    .ToList();
            }
        }
        
        /// <summary>
        /// Identifies conflicting signals that should cancel each other
        /// </summary>
        /// <param name="signals">List of signals to analyze</param>
        /// <returns>List of signals after resolving conflicts</returns>
        public List<SignalEvent> ResolveConflictingSignals(List<SignalEvent> signals)
        {
            if (signals == null || signals.Count <= 1)
                return signals;
                
            lock (_lockObject)
            {
                // Group signals by direction
                var buySignals = signals.Where(s => s.Direction == SignalDirection.Buy).ToList();
                var sellSignals = signals.Where(s => s.Direction == SignalDirection.Sell).ToList();
                
                // If no conflicting directions, return all
                if (buySignals.Count == 0 || sellSignals.Count == 0)
                    return signals;
                    
                // Calculate strength for each direction
                double buyStrength = CalculateWeightedStrength(buySignals);
                double sellStrength = CalculateWeightedStrength(sellSignals);
                
                // If strengths are close, signals conflict and should cancel
                if (Math.Abs(buyStrength - sellStrength) / Math.Max(buyStrength, sellStrength) < 0.2)
                {
                    // Signals too close in strength, conflict
                    return new List<SignalEvent>();
                }
                
                // Return signals from dominant direction
                return buyStrength > sellStrength ? buySignals : sellSignals;
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Calculates signal quality based on type, direction, and strength
        /// </summary>
        private double CalculateSignalQuality(string type, SignalDirection direction, double strength)
        {
            // Base quality from strength
            double quality = strength * 0.7;
            
            // Adjust based on historical performance if available
            if (_signalHistory.ContainsKey(type) && _signalHistory[type].Count >= _minimumSignalsForValidation)
            {
                var similarSignals = _signalHistory[type]
                    .Where(s => s.Direction == direction && s.IsExecuted && s.Outcome != SignalOutcome.Unknown)
                    .ToList();
                    
                if (similarSignals.Count >= 5)
                {
                    // Calculate win rate
                    double winRate = similarSignals.Count(s => s.Outcome == SignalOutcome.Win) / (double)similarSignals.Count;
                    
                    // Adjust quality based on historical win rate
                    quality = quality * 0.6 + winRate * 0.4;
                }
            }
            
            // Adjust based on signal type weight if available
            if (_signalWeights.ContainsKey(type))
            {
                quality *= _signalWeights[type];
            }
            
            // Clamp to 0-1 range
            return Math.Max(0, Math.Min(1, quality));
        }
        
        /// <summary>
        /// Validates signal against market context
        /// </summary>
        private bool ValidateMarketContext(SignalEvent signal, Dictionary<string, object> marketContext)
        {
            // Extract market state if available
            string marketState = "";
            if (marketContext.ContainsKey("MarketState"))
            {
                marketState = marketContext["MarketState"].ToString();
            }
            
            // Validate based on market state
            switch (marketState)
            {
                case "Trending":
                    // In trending markets, favor signals aligned with trend
                    if (marketContext.ContainsKey("TrendDirection"))
                    {
                        string trendDirection = marketContext["TrendDirection"].ToString();
                        
                        if (trendDirection == "Up" && signal.Direction == SignalDirection.Sell)
                            return signal.Strength > 0.8; // Higher threshold for counter-trend
                            
                        if (trendDirection == "Down" && signal.Direction == SignalDirection.Buy)
                            return signal.Strength > 0.8; // Higher threshold for counter-trend
                    }
                    break;
                    
                case "Ranging":
                    // In ranging markets, mean reversion signals are more reliable
                    if (signal.Type.Contains("Exhaustion") || signal.Type.Contains("Absorption"))
                        return true;
                    break;
                    
                case "Volatile":
                    // In volatile markets, be more selective
                    return signal.Quality > 0.7 && 
                           (signal.Type.Contains("Absorption") || signal.Type.Contains("LargeOrder"));
                           
                case "Opening":
                case "Closing":
                    // During session boundaries, be very selective
                    return signal.Quality > 0.8;
            }
            
            // Default validation
            return true;
        }
        
        /// <summary>
        /// Calculates weighted strength of a group of signals
        /// </summary>
        private double CalculateWeightedStrength(List<SignalEvent> signals)
        {
            if (signals == null || signals.Count == 0)
                return 0;
                
            double weightedSum = 0;
            double weightSum = 0;
            
            foreach (var signal in signals)
            {
                double weight = 1.0;
                
                // Use signal type weight if available
                if (_signalWeights.ContainsKey(signal.Type))
                {
                    weight = _signalWeights[signal.Type];
                }
                
                weightedSum += signal.Strength * weight;
                weightSum += weight;
            }
            
            return weightSum > 0 ? weightedSum / weightSum : 0;
        }
        
        /// <summary>
        /// Calculates probability of success for a consolidated signal
        /// </summary>
        private double CalculateSignalProbability(ConsolidatedSignal signal)
        {
            // Base probability from strength and quality
            double baseProbability = (signal.Strength * 0.6) + (signal.Quality * 0.4);
            
            // Adjust based on component signal types
            foreach (var component in signal.ComponentSignals)
            {
                if (component.Type.Contains("Absorption"))
                    baseProbability *= 1.1; // Absorption patterns increase probability
                else if (component.Type.Contains("Exhaustion"))
                    baseProbability *= 1.05; // Exhaustion patterns increase probability
            }
            
            // Adjust based on market context
            if (signal.MarketContext.ContainsKey("MarketState"))
            {
                string state = signal.MarketContext["MarketState"].ToString();
                
                switch (state)
                {
                    case "Trending":
                        // Check if signal aligns with trend
                        if (signal.MarketContext.ContainsKey("TrendDirection"))
                        {
                            string trendDirection = signal.MarketContext["TrendDirection"].ToString();
                            
                            if ((trendDirection == "Up" && signal.Direction == SignalDirection.Buy) ||
                                (trendDirection == "Down" && signal.Direction == SignalDirection.Sell))
                            {
                                baseProbability *= 1.1; // Trend-aligned signals more probable
                            }
                            else
                            {
                                baseProbability *= 0.9; // Counter-trend signals less probable
                            }
                        }
                        break;
                        
                    case "Ranging":
                        // Mean reversion signals more reliable in ranging markets
                        if (signal.ComponentSignals.Any(s => s.Type.Contains("Exhaustion") || 
                                                        s.Type.Contains("Absorption")))
                        {
                            baseProbability *= 1.1;
                        }
                        break;
                        
                    case "Volatile":
                        baseProbability *= 0.9; // Reduce probability in volatile markets
                        break;
                }
            }
            
            // Adjust based on confluence strength
            int signalCount = signal.ComponentSignals.Count;
            if (signalCount >= 3)
                baseProbability *= 1.1; // Multiple confirming signals increase probability
                
            // Clamp to range 0-1
            return Math.Max(0, Math.Min(1, baseProbability));
        }
        #endregion
    }
}