using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MScalper.Core
{
    /// <summary>
    /// Manages risk parameters and position sizing for the trading strategy
    /// </summary>
    public class RiskManagement
    {
        #region Private Fields
        private readonly Dictionary<string, RiskProfile> _riskProfiles;
        private RiskProfile _activeProfile;
        private readonly TradingStatistics _statistics;
        private readonly object _lockObject = new object();
        private double _currentBalance;
        private double _initialBalance;
        private double _maxDrawdown;
        private double _peakBalance;
        private double _currentVolatility;
        #endregion

        #region Public Properties
        /// <summary>
        /// Current account balance
        /// </summary>
        public double CurrentBalance
        {
            get { return _currentBalance; }
            set
            {
                lock (_lockObject)
                {
                    _currentBalance = value;
                    
                    // Update peak balance if applicable
                    if (_currentBalance > _peakBalance)
                        _peakBalance = _currentBalance;
                        
                    // Update max drawdown if applicable
                    double currentDrawdown = (_peakBalance - _currentBalance) / _peakBalance;
                    if (currentDrawdown > _maxDrawdown)
                        _maxDrawdown = currentDrawdown;
                }
            }
        }
        
        /// <summary>
        /// Maximum historical drawdown (as percentage)
        /// </summary>
        public double MaxDrawdown => _maxDrawdown;
        
        /// <summary>
        /// List of available risk profiles
        /// </summary>
        public IReadOnlyDictionary<string, RiskProfile> RiskProfiles => _riskProfiles;
        
        /// <summary>
        /// Currently active risk profile
        /// </summary>
        public RiskProfile ActiveProfile => _activeProfile;
        
        /// <summary>
        /// Trading statistics
        /// </summary>
        public TradingStatistics Statistics => _statistics;
        
        /// <summary>
        /// Current market volatility estimate
        /// </summary>
        public double CurrentVolatility
        {
            get { return _currentVolatility; }
            set { _currentVolatility = value; }
        }
        #endregion

        #region Classes
        /// <summary>
        /// Represents a risk management profile
        /// </summary>
        public class RiskProfile
        {
            /// <summary>
            /// Name of the profile
            /// </summary>
            public string Name { get; set; }
            
            /// <summary>
            /// Maximum percentage of account to risk per trade
            /// </summary>
            public double MaxRiskPerTrade { get; set; }
            
            /// <summary>
            /// Maximum percentage of account to risk across all open positions
            /// </summary>
            public double MaxTotalRisk { get; set; }
            
            /// <summary>
            /// Maximum number of concurrent trades
            /// </summary>
            public int MaxConcurrentTrades { get; set; }
            
            /// <summary>
            /// Default position size (contracts/shares)
            /// </summary>
            public int DefaultPositionSize { get; set; }
            
            /// <summary>
            /// Minimum position size (contracts/shares)
            /// </summary>
            public int MinPositionSize { get; set; }
            
            /// <summary>
            /// Maximum position size (contracts/shares)
            /// </summary>
            public int MaxPositionSize { get; set; }
            
            /// <summary>
            /// Default stop loss distance (ticks)
            /// </summary>
            public int DefaultStopLossTicks { get; set; }
            
            /// <summary>
            /// Minimum profit target ratio (relative to stop loss)
            /// </summary>
            public double MinProfitTargetRatio { get; set; }
            
            /// <summary>
            /// Whether to use dynamic position sizing
            /// </summary>
            public bool UseDynamicPositionSizing { get; set; }
            
            /// <summary>
            /// Whether to adjust risk based on volatility
            /// </summary>
            public bool AdjustForVolatility { get; set; }
            
            /// <summary>
            /// Whether to reduce risk after losses
            /// </summary>
            public bool ReduceRiskAfterLosses { get; set; }
            
            /// <summary>
            /// Maximum consecutive losses before reducing risk
            /// </summary>
            public int MaxConsecutiveLosses { get; set; }
            
            /// <summary>
            /// Amount to reduce risk by after max consecutive losses (percentage)
            /// </summary>
            public double RiskReductionFactor { get; set; }
            
            /// <summary>
            /// Symbol-specific risk overrides
            /// </summary>
            public Dictionary<string, SymbolRiskSettings> SymbolSettings { get; set; }
        }
        
        /// <summary>
        /// Symbol-specific risk settings
        /// </summary>
        public class SymbolRiskSettings
        {
            /// <summary>
            /// Symbol identifier
            /// </summary>
            public string Symbol { get; set; }
            
            /// <summary>
            /// Override for max risk per trade
            /// </summary>
            public double? MaxRiskPerTrade { get; set; }
            
            /// <summary>
            /// Override for default position size
            /// </summary>
            public int? DefaultPositionSize { get; set; }
            
            /// <summary>
            /// Override for stop loss distance
            /// </summary>
            public int? DefaultStopLossTicks { get; set; }
            
            /// <summary>
            /// Override for profit target ratio
            /// </summary>
            public double? MinProfitTargetRatio { get; set; }
            
            /// <summary>
            /// Contract/share value in account currency
            /// </summary>
            public double ContractValue { get; set; }
            
            /// <summary>
            /// Tick value in account currency
            /// </summary>
            public double TickValue { get; set; }
            
            /// <summary>
            /// Typical volatility (standard deviation of returns)
            /// </summary>
            public double TypicalVolatility { get; set; }
        }
        
        /// <summary>
        /// Trading statistics for risk management
        /// </summary>
        public class TradingStatistics
        {
            /// <summary>
            /// Total number of trades
            /// </summary>
            public int TotalTrades { get; set; }
            
            /// <summary>
            /// Number of winning trades
            /// </summary>
            public int WinningTrades { get; set; }
            
            /// <summary>
            /// Number of losing trades
            /// </summary>
            public int LosingTrades { get; set; }
            
            /// <summary>
            /// Win rate (percentage)
            /// </summary>
            public double WinRate => TotalTrades > 0 ? (double)WinningTrades / TotalTrades * 100 : 0;
            
            /// <summary>
            /// Total profit/loss
            /// </summary>
            public double TotalProfitLoss { get; set; }
            
            /// <summary>
            /// Average win amount
            /// </summary>
            public double AverageWin { get; set; }
            
            /// <summary>
            /// Average loss amount
            /// </summary>
            public double AverageLoss { get; set; }
            
            /// <summary>
            /// Profit factor (gross profit / gross loss)
            /// </summary>
            public double ProfitFactor => AverageLoss != 0 ? (AverageWin * WinningTrades) / (Math.Abs(AverageLoss) * LosingTrades) : 0;
            
            /// <summary>
            /// Number of consecutive wins
            /// </summary>
            public int ConsecutiveWins { get; set; }
            
            /// <summary>
            /// Number of consecutive losses
            /// </summary>
            public int ConsecutiveLosses { get; set; }
            
            /// <summary>
            /// Maximum consecutive wins
            /// </summary>
            public int MaxConsecutiveWins { get; set; }
            
            /// <summary>
            /// Maximum consecutive losses
            /// </summary>
            public int MaxConsecutiveLosses { get; set; }
            
            /// <summary>
            /// List of recent trade results (true for win, false for loss)
            /// </summary>
            public List<bool> RecentResults { get; set; }
            
            /// <summary>
            /// Average R multiple (profit/loss relative to initial risk)
            /// </summary>
            public double AverageRMultiple { get; set; }
            
            /// <summary>
            /// Initializes a new statistics object
            /// </summary>
            public TradingStatistics()
            {
                RecentResults = new List<bool>();
                Reset();
            }
            
            /// <summary>
            /// Resets statistics to initial values
            /// </summary>
            public void Reset()
            {
                TotalTrades = 0;
                WinningTrades = 0;
                LosingTrades = 0;
                TotalProfitLoss = 0;
                AverageWin = 0;
                AverageLoss = 0;
                ConsecutiveWins = 0;
                ConsecutiveLosses = 0;
                MaxConsecutiveWins = 0;
                MaxConsecutiveLosses = 0;
                RecentResults.Clear();
                AverageRMultiple = 0;
            }
            
            /// <summary>
            /// Updates statistics with new trade result
            /// </summary>
            /// <param name="isWin">Whether trade was profitable</param>
            /// <param name="profitLoss">Profit/loss amount</param>
            /// <param name="rMultiple">R multiple for the trade</param>
            public void UpdateStats(bool isWin, double profitLoss, double rMultiple)
            {
                TotalTrades++;
                TotalProfitLoss += profitLoss;
                
                // Add to recent results
                RecentResults.Add(isWin);
                if (RecentResults.Count > 20)
                    RecentResults.RemoveAt(0);
                
                if (isWin)
                {
                    WinningTrades++;
                    
                    // Update average win
                    AverageWin = ((AverageWin * (WinningTrades - 1)) + profitLoss) / WinningTrades;
                    
                    // Update consecutive counts
                    ConsecutiveWins++;
                    ConsecutiveLosses = 0;
                    
                    // Update max consecutive wins
                    if (ConsecutiveWins > MaxConsecutiveWins)
                        MaxConsecutiveWins = ConsecutiveWins;
                }
                else
                {
                    LosingTrades++;
                    
                    // Update average loss
                    AverageLoss = ((AverageLoss * (LosingTrades - 1)) + profitLoss) / LosingTrades;
                    
                    // Update consecutive counts
                    ConsecutiveLosses++;
                    ConsecutiveWins = 0;
                    
                    // Update max consecutive losses
                    if (ConsecutiveLosses > MaxConsecutiveLosses)
                        MaxConsecutiveLosses = ConsecutiveLosses;
                }
                
                // Update R multiple
                AverageRMultiple = ((AverageRMultiple * (TotalTrades - 1)) + rMultiple) / TotalTrades;
            }
        }
        
        /// <summary>
        /// Position sizing calculation result
        /// </summary>
        public class PositionSizeResult
        {
            /// <summary>
            /// Calculated position size in contracts/shares
            /// </summary>
            public int Size { get; set; }
            
            /// <summary>
            /// Actual risk amount in account currency
            /// </summary>
            public double RiskAmount { get; set; }
            
            /// <summary>
            /// Risk percentage of account
            /// </summary>
            public double RiskPercentage { get; set; }
            
            /// <summary>
            /// Stop loss level
            /// </summary>
            public double StopLossLevel { get; set; }
            
            /// <summary>
            /// Profit target level
            /// </summary>
            public double ProfitTargetLevel { get; set; }
            
            /// <summary>
            /// Risk/reward ratio
            /// </summary>
            public double RiskRewardRatio { get; set; }
            
            /// <summary>
            /// Expected value based on win rate and R/R ratio
            /// </summary>
            public double ExpectedValue { get; set; }
        }
        #endregion

        #region Constructor and Initialization
        /// <summary>
        /// Initializes a new instance of the RiskManagement class
        /// </summary>
        /// <param name="initialBalance">Initial account balance</param>
        public RiskManagement(double initialBalance)
        {
            _riskProfiles = new Dictionary<string, RiskProfile>();
            _statistics = new TradingStatistics();
            _currentBalance = initialBalance;
            _initialBalance = initialBalance;
            _peakBalance = initialBalance;
            _maxDrawdown = 0;
            _currentVolatility = 1.0; // Base volatility
            
            // Initialize default risk profiles
            InitializeDefaultProfiles();
            
            // Set default active profile
            _activeProfile = _riskProfiles["Normal"];
        }
        
        /// <summary>
        /// Initializes default risk profiles
        /// </summary>
        private void InitializeDefaultProfiles()
        {
            // Conservative profile
            var conservative = new RiskProfile
            {
                Name = "Conservative",
                MaxRiskPerTrade = 0.5, // 0.5% per trade
                MaxTotalRisk = 2.0,    // 2% maximum total risk
                MaxConcurrentTrades = 2,
                DefaultPositionSize = 1,
                MinPositionSize = 1,
                MaxPositionSize = 2,
                DefaultStopLossTicks = 5,
                MinProfitTargetRatio = 2.0, // 2:1 reward:risk
                UseDynamicPositionSizing = true,
                AdjustForVolatility = true,
                ReduceRiskAfterLosses = true,
                MaxConsecutiveLosses = 2,
                RiskReductionFactor = 0.5, // Reduce by 50%
                SymbolSettings = new Dictionary<string, SymbolRiskSettings>()
            };
            
            // Normal profile
            var normal = new RiskProfile
            {
                Name = "Normal",
                MaxRiskPerTrade = 1.0, // 1% per trade
                MaxTotalRisk = 4.0,    // 4% maximum total risk
                MaxConcurrentTrades = 3,
                DefaultPositionSize = 1,
                MinPositionSize = 1,
                MaxPositionSize = 3,
                DefaultStopLossTicks = 5,
                MinProfitTargetRatio = 1.5, // 1.5:1 reward:risk
                UseDynamicPositionSizing = true,
                AdjustForVolatility = true,
                ReduceRiskAfterLosses = true,
                MaxConsecutiveLosses = 3,
                RiskReductionFactor = 0.5, // Reduce by 50%
                SymbolSettings = new Dictionary<string, SymbolRiskSettings>()
            };
            
            // Aggressive profile
            var aggressive = new RiskProfile
            {
                Name = "Aggressive",
                MaxRiskPerTrade = 2.0, // 2% per trade
                MaxTotalRisk = 6.0,    // 6% maximum total risk
                MaxConcurrentTrades = 4,
                DefaultPositionSize = 2,
                MinPositionSize = 1,
                MaxPositionSize = 5,
                DefaultStopLossTicks = 4,
                MinProfitTargetRatio = 1.2, // 1.2:1 reward:risk
                UseDynamicPositionSizing = true,
                AdjustForVolatility = true,
                ReduceRiskAfterLosses = true,
                MaxConsecutiveLosses = 3,
                RiskReductionFactor = 0.7, // Reduce by 30%
                SymbolSettings = new Dictionary<string, SymbolRiskSettings>()
            };
            
            // Add sample symbol settings
            var nqSettings = new SymbolRiskSettings
            {
                Symbol = "NQ",
                ContractValue = 20, // Each point = $20
                TickValue = 5, // Each tick = $5
                TypicalVolatility = 0.0015
            };
            
            var mnqSettings = new SymbolRiskSettings
            {
                Symbol = "MNQ",
                ContractValue = 2, // Each point = $2
                TickValue = 0.5, // Each tick = $0.5
                TypicalVolatility = 0.0015
            };
            
            // Add symbol settings to profiles
            conservative.SymbolSettings["NQ"] = nqSettings;
            conservative.SymbolSettings["MNQ"] = mnqSettings;
            
            normal.SymbolSettings["NQ"] = nqSettings;
            normal.SymbolSettings["MNQ"] = mnqSettings;
            
            aggressive.SymbolSettings["NQ"] = nqSettings;
            aggressive.SymbolSettings["MNQ"] = mnqSettings;
            
            // Add profiles to dictionary
            _riskProfiles["Conservative"] = conservative;
            _riskProfiles["Normal"] = normal;
            _riskProfiles["Aggressive"] = aggressive;
        }
        #endregion

        #region Risk Management Methods
        /// <summary>
        /// Sets the active risk profile
        /// </summary>
        /// <param name="profileName">Name of profile to activate</param>
        /// <returns>True if profile was found and activated</returns>
        public bool SetActiveProfile(string profileName)
        {
            lock (_lockObject)
            {
                if (_riskProfiles.ContainsKey(profileName))
                {
                    _activeProfile = _riskProfiles[profileName];
                    return true;
                }
                return false;
            }
        }
        
        /// <summary>
        /// Creates a new risk profile
        /// </summary>
        /// <param name="profile">Risk profile to add</param>
        /// <returns>True if successfully added</returns>
        public bool AddRiskProfile(RiskProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.Name))
                return false;
                
            lock (_lockObject)
            {
                if (_riskProfiles.ContainsKey(profile.Name))
                    return false;
                    
                _riskProfiles[profile.Name] = profile;
                return true;
            }
        }
        
        /// <summary>
        /// Updates an existing risk profile
        /// </summary>
        /// <param name="profile">Risk profile with updates</param>
        /// <returns>True if successfully updated</returns>
        public bool UpdateRiskProfile(RiskProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.Name))
                return false;
                
            lock (_lockObject)
            {
                if (!_riskProfiles.ContainsKey(profile.Name))
                    return false;
                    
                _riskProfiles[profile.Name] = profile;
                
                // If this is the active profile, update reference
                if (_activeProfile.Name == profile.Name)
                    _activeProfile = profile;
                    
                return true;
            }
        }
        
        /// <summary>
        /// Calculates position size based on risk parameters
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="entryPrice">Entry price</param>
        /// <param name="stopLossPrice">Initial stop loss price</param>
        /// <param name="signalQuality">Quality of entry signal (0-1)</param>
        /// <returns>Position sizing result</returns>
        public PositionSizeResult CalculatePositionSize(string symbol, double entryPrice, 
            double stopLossPrice, double signalQuality = 0.7)
        {
            lock (_lockObject)
            {
                // Get symbol settings
                SymbolRiskSettings symbolSettings = null;
                if (_activeProfile.SymbolSettings.ContainsKey(symbol))
                {
                    symbolSettings = _activeProfile.SymbolSettings[symbol];
                }
                else
                {
                    // No specific settings for this symbol
                    return new PositionSizeResult
                    {
                        Size = _activeProfile.DefaultPositionSize,
                        RiskAmount = 0,
                        RiskPercentage = 0,
                        StopLossLevel = stopLossPrice,
                        ProfitTargetLevel = entryPrice,
                        RiskRewardRatio = 1,
                        ExpectedValue = 0
                    };
                }
                
                // Calculate risk per point
                double tickSize = symbolSettings.TickValue > 0 ? 
                    symbolSettings.TickValue : 1;
                    
                // Calculate stop distance in ticks
                double stopDistancePoints = Math.Abs(entryPrice - stopLossPrice);
                double stopDistanceTicks = stopDistancePoints / tickSize;
                
                // Use default stop if none provided
                if (stopDistanceTicks <= 0)
                {
                    int stopTicks = symbolSettings.DefaultStopLossTicks ?? _activeProfile.DefaultStopLossTicks;
                    stopDistanceTicks = stopTicks;
                    stopLossPrice = entryPrice > stopLossPrice ? 
                        entryPrice - (stopTicks * tickSize) : 
                        entryPrice + (stopTicks * tickSize);
                }
                
                // Calculate risk amount per contract
                double riskPerContract = stopDistanceTicks * symbolSettings.TickValue;
                
                // Calculate max risk amount based on account balance
                double maxRiskPercentage = symbolSettings.MaxRiskPerTrade ?? _activeProfile.MaxRiskPerTrade;
                
                // Adjust risk based on consecutive losses
                if (_activeProfile.ReduceRiskAfterLosses && 
                    _statistics.ConsecutiveLosses >= _activeProfile.MaxConsecutiveLosses)
                {
                    maxRiskPercentage *= _activeProfile.RiskReductionFactor;
                }
                
                // Adjust risk based on signal quality
                maxRiskPercentage *= Math.Max(0.5, signalQuality);
                
                // Adjust risk based on volatility
                if (_activeProfile.AdjustForVolatility && symbolSettings.TypicalVolatility > 0)
                {
                    double volatilityRatio = _currentVolatility / symbolSettings.TypicalVolatility;
                    
                    // Reduce risk in high volatility, increase in low volatility
                    if (volatilityRatio > 1.2)
                    {
                        maxRiskPercentage /= Math.Min(2.0, volatilityRatio);
                    }
                    else if (volatilityRatio < 0.8)
                    {
                        maxRiskPercentage *= Math.Min(1.5, 1 / volatilityRatio);
                    }
                }
                
                double maxRiskAmount = _currentBalance * (maxRiskPercentage / 100);
                
                // Calculate position size
                int positionSize = (int)Math.Floor(maxRiskAmount / riskPerContract);
                
                // Apply position size limits
                positionSize = Math.Max(_activeProfile.MinPositionSize, 
                             Math.Min(_activeProfile.MaxPositionSize, positionSize));
                
                // Calculate actual risk amount and percentage
                double actualRiskAmount = positionSize * riskPerContract;
                double actualRiskPercentage = (actualRiskAmount / _currentBalance) * 100;
                
                // Calculate profit target
                double minTargetRatio = symbolSettings.MinProfitTargetRatio ?? _activeProfile.MinProfitTargetRatio;
                double targetDistance = stopDistancePoints * minTargetRatio;
                double profitTargetLevel = entryPrice > stopLossPrice ? 
                    entryPrice + targetDistance : 
                    entryPrice - targetDistance;
                
                // Calculate risk/reward ratio
                double riskRewardRatio = minTargetRatio;
                
                // Calculate expected value (simplified)
                double expectedWinRate = _statistics.TotalTrades > 10 ? 
                    _statistics.WinRate / 100 : 0.5; // Default to 50% if not enough history
                double expectedValue = (expectedWinRate * minTargetRatio) - (1 - expectedWinRate);
                
                return new PositionSizeResult
                {
                    Size = positionSize,
                    RiskAmount = actualRiskAmount,
                    RiskPercentage = actualRiskPercentage,
                    StopLossLevel = stopLossPrice,
                    ProfitTargetLevel = profitTargetLevel,
                    RiskRewardRatio = riskRewardRatio,
                    ExpectedValue = expectedValue
                };
            }
        }
        
        /// <summary>
        /// Updates trading statistics with trade results
        /// </summary>
        /// <param name="isWin">Whether trade was profitable</param>
        /// <param name="profitLoss">Profit/loss amount</param>
        /// <param name="initialRisk">Initial risk amount for the trade</param>
        public void UpdateTradeResult(bool isWin, double profitLoss, double initialRisk)
        {
            lock (_lockObject)
            {
                // Update balance
                _currentBalance += profitLoss;
                
                // Calculate R multiple (profit/loss relative to initial risk)
                double rMultiple = initialRisk != 0 ? 
                    profitLoss / Math.Abs(initialRisk) : 
                    0;
                
                // Update statistics
                _statistics.UpdateStats(isWin, profitLoss, rMultiple);
                
                // Adapt risk parameters based on performance if needed
                AdaptRiskParameters();
            }
        }
        
        /// <summary>
        /// Checks if maximum risk limit would be exceeded by a new trade
        /// </summary>
        /// <param name="newTradeRisk">Risk amount for new trade</param>
        /// <param name="currentOpenRisk">Current risk from open positions</param>
        /// <returns>True if risk limit would be exceeded</returns>
        public bool WouldExceedRiskLimits(double newTradeRisk, double currentOpenRisk)
        {
            lock (_lockObject)
            {
                // Calculate total risk percentage
                double totalRiskAmount = newTradeRisk + currentOpenRisk;
                double totalRiskPercentage = (totalRiskAmount / _currentBalance) * 100;
                
                // Check against max total risk
                return totalRiskPercentage > _activeProfile.MaxTotalRisk;
            }
        }
        
        /// <summary>
        /// Checks if maximum concurrent trades limit would be exceeded
        /// </summary>
        /// <param name="currentOpenTrades">Current number of open trades</param>
        /// <returns>True if trade limit would be exceeded</returns>
        public bool WouldExceedTradeLimit(int currentOpenTrades)
        {
            return (currentOpenTrades + 1) > _activeProfile.MaxConcurrentTrades;
        }
        
        /// <summary>
        /// Gets risk metrics for reporting
        /// </summary>
        /// <returns>Dictionary of risk metrics</returns>
        public Dictionary<string, object> GetRiskMetrics()
        {
            lock (_lockObject)
            {
                return new Dictionary<string, object>
                {
                    { "CurrentBalance", _currentBalance },
                    { "InitialBalance", _initialBalance },
                    { "PeakBalance", _peakBalance },
                    { "CurrentDrawdown", (_peakBalance - _currentBalance) / _peakBalance * 100 },
                    { "MaxDrawdown", _maxDrawdown * 100 },
                    { "TotalReturn", (_currentBalance - _initialBalance) / _initialBalance * 100 },
                    { "WinRate", _statistics.WinRate },
                    { "ProfitFactor", _statistics.ProfitFactor },
                    { "AverageWin", _statistics.AverageWin },
                    { "AverageLoss", _statistics.AverageLoss },
                    { "ConsecutiveWins", _statistics.ConsecutiveWins },
                    { "ConsecutiveLosses", _statistics.ConsecutiveLosses },
                    { "MaxConsecutiveWins", _statistics.MaxConsecutiveWins },
                    { "MaxConsecutiveLosses", _statistics.MaxConsecutiveLosses },
                    { "AverageRMultiple", _statistics.AverageRMultiple },
                    { "CurrentRiskProfile", _activeProfile.Name },
                    { "CurrentVolatility", _currentVolatility }
                };
            }
        }
        
        /// <summary>
        /// Validates whether a trade meets risk management criteria
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="potentialLoss">Potential loss amount</param>
        /// <param name="riskRewardRatio">Risk/reward ratio</param>
        /// <param name="signalQuality">Signal quality score (0-1)</param>
        /// <returns>True if trade meets criteria</returns>
        public bool ValidateTrade(string symbol, double potentialLoss, double riskRewardRatio, double signalQuality)
        {
            lock (_lockObject)
            {
                // Check basic risk parameters
                double riskPercentage = Math.Abs(potentialLoss) / _currentBalance * 100;
                double maxRiskPerTrade = _activeProfile.MaxRiskPerTrade;
                
                if (_activeProfile.SymbolSettings.ContainsKey(symbol) && 
                    _activeProfile.SymbolSettings[symbol].MaxRiskPerTrade.HasValue)
                {
                    maxRiskPerTrade = _activeProfile.SymbolSettings[symbol].MaxRiskPerTrade.Value;
                }
                
                // Check risk percentage
                if (riskPercentage > maxRiskPerTrade)
                {
                    return false; // Exceeds maximum risk per trade
                }
                
                // Get minimum target ratio
                double minTargetRatio = _activeProfile.MinProfitTargetRatio;
                
                if (_activeProfile.SymbolSettings.ContainsKey(symbol) && 
                    _activeProfile.SymbolSettings[symbol].MinProfitTargetRatio.HasValue)
                {
                    minTargetRatio = _activeProfile.SymbolSettings[symbol].MinProfitTargetRatio.Value;
                }
                
                // Check risk/reward ratio
                if (riskRewardRatio < minTargetRatio)
                {
                    return false; // Risk/reward ratio too low
                }
                
                // Consider recent performance
                if (_activeProfile.ReduceRiskAfterLosses &&
                    _statistics.ConsecutiveLosses >= _activeProfile.MaxConsecutiveLosses)
                {
                    // Require higher quality signals after consecutive losses
                    if (signalQuality < 0.7)
                    {
                        return false; // Signal quality too low after losses
                    }
                    
                    // Require better risk/reward after losses
                    if (riskRewardRatio < minTargetRatio * 1.5)
                    {
                        return false; // Need better R:R after losses
                    }
                }
                
                return true; // Trade meets risk criteria
            }
        }
        
        /// <summary>
        /// Calculates optimal profit target and stop loss levels based on risk profile
        /// </summary>
        /// <param name="symbol">Trading symbol</param>
        /// <param name="entryPrice">Entry price</param>
        /// <param name="direction">Trade direction (1 for long, -1 for short)</param>
        /// <param name="volatility">Current volatility (optional)</param>
        /// <returns>Tuple with (stopLossPrice, profitTargetPrice)</returns>
        public (double StopLoss, double Target) CalculateExitLevels(string symbol, double entryPrice, 
            int direction, double? volatility = null)
        {
            lock (_lockObject)
            {
                // Get symbol settings
                int stopLossTicks = _activeProfile.DefaultStopLossTicks;
                double tickSize = 1;
                double minTargetRatio = _activeProfile.MinProfitTargetRatio;
                
                if (_activeProfile.SymbolSettings.ContainsKey(symbol))
                {
                    var settings = _activeProfile.SymbolSettings[symbol];
                    
                    if (settings.DefaultStopLossTicks.HasValue)
                        stopLossTicks = settings.DefaultStopLossTicks.Value;
                        
                    tickSize = settings.TickValue;
                    
                    if (settings.MinProfitTargetRatio.HasValue)
                        minTargetRatio = settings.MinProfitTargetRatio.Value;
                }
                
                // Adjust for volatility if enabled
                if (_activeProfile.AdjustForVolatility)
                {
                    double currentVol = volatility ?? _currentVolatility;
                    
                    if (_activeProfile.SymbolSettings.ContainsKey(symbol))
                    {
                        double typicalVol = _activeProfile.SymbolSettings[symbol].TypicalVolatility;
                        if (typicalVol > 0)
                        {
                            double ratio = currentVol / typicalVol;
                            
                            // Increase stop distance in high volatility
                            if (ratio > 1.2)
                            {
                                stopLossTicks = (int)(stopLossTicks * Math.Min(2.0, ratio));
                            }
                        }
                    }
                }
                
                // Calculate stop loss distance
                double stopDistance = stopLossTicks * tickSize;
                
                // Calculate stop and target levels
                double stopLossPrice = direction > 0 ? 
                    entryPrice - stopDistance : 
                    entryPrice + stopDistance;
                    
                double targetDistance = stopDistance * minTargetRatio;
                double targetPrice = direction > 0 ? 
                    entryPrice + targetDistance : 
                    entryPrice - targetDistance;
                    
                return (stopLossPrice, targetPrice);
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Adapts risk parameters based on recent performance
        /// </summary>
        private void AdaptRiskParameters()
        {
            // Only adapt if we have enough trade history
            if (_statistics.TotalTrades < 10)
                return;
                
            // Check if we need to reduce risk due to consecutive losses
            if (_activeProfile.ReduceRiskAfterLosses && 
                _statistics.ConsecutiveLosses >= _activeProfile.MaxConsecutiveLosses)
            {
                // Already at maximum consecutive losses, no need to adapt further
                return;
            }
            
            // Check if we had good performance and can consider increasing risk
            if (_statistics.ConsecutiveWins >= 5 && _statistics.WinRate > 60)
            {
                // Consider moving to more aggressive profile
                if (_activeProfile.Name == "Conservative")
                {
                    SetActiveProfile("Normal");
                }
                else if (_activeProfile.Name == "Normal" && _statistics.WinRate > 70)
                {
                    SetActiveProfile("Aggressive");
                }
            }
            
            // Check if we should become more conservative due to poor performance
            if (_statistics.ConsecutiveLosses >= 3 || 
                (_statistics.TotalTrades > 20 && _statistics.WinRate < 40))
            {
                // Consider moving to more conservative profile
                if (_activeProfile.Name == "Aggressive")
                {
                    SetActiveProfile("Normal");
                }
                else if (_activeProfile.Name == "Normal")
                {
                    SetActiveProfile("Conservative");
                }
            }
            
            // Check drawdown and take protective action if needed
            double currentDrawdown = (_peakBalance - _currentBalance) / _peakBalance;
            if (currentDrawdown > 0.1) // 10% drawdown
            {
                // Significant drawdown, become more conservative
                SetActiveProfile("Conservative");
            }
        }
        #endregion
    }
}