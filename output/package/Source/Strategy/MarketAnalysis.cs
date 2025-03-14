// Compatible with NinjaTrader 8.1.4.1
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;

namespace OrderFlowScalper.Strategy
{
    public class MarketAnalysis
    {
        #region Enums and Result Structures
        public enum ImbalanceType
        {
            None,
            BuyerDominant,
            SellerDominant
        }
        
        public enum AbsorptionType
        {
            None,
            BuyAbsorptionAtSupport,
            SellAbsorptionAtResistance
        }
        
        public struct ImbalanceResult
        {
            public bool HasSignificantImbalance;
            public ImbalanceType ImbalanceType;
            public double ImbalanceRatio;
            public double Price;
        }
        #endregion
        
        #region Variables
        private readonly int _domDepth;
        private readonly int _absorptionThreshold;
        private readonly double _imbalanceThreshold;
        
        private Dictionary<double, long> _bidDepth = new Dictionary<double, long>();
        private Dictionary<double, long> _askDepth = new Dictionary<double, long>();
        
        private List<double> _recentSupportLevels = new List<double>();
        private List<double> _recentResistanceLevels = new List<double>();
        
        private const int MaxSupportResistanceHistory = 10;
        private const double SupportResistanceProximityFactor = 0.1; // 10% proximity to consider level hit
        
        // Track key price levels
        private double _lastHigh = 0;
        private double _lastLow = double.MaxValue;
        private double _lastSignificantHigh = 0;
        private double _lastSignificantLow = double.MaxValue;
        #endregion
        
        #region Constructor
        public MarketAnalysis(int domDepth, int absorptionThreshold, double imbalanceThreshold)
        {
            _domDepth = domDepth;
            _absorptionThreshold = absorptionThreshold;
            _imbalanceThreshold = imbalanceThreshold;
        }
        #endregion
        
        #region Public Methods
        /// <summary>
        /// Process market depth update and detect imbalances
        /// </summary>
        public ImbalanceResult ProcessMarketDepth(MarketDepthEventArgs e, double currentBid, double currentAsk)
        {
            // Update DOM tracking
            UpdateDOMFromEvent(e);
            
            // Initialize default result
            ImbalanceResult result = new ImbalanceResult
            {
                HasSignificantImbalance = false,
                ImbalanceType = ImbalanceType.None,
                ImbalanceRatio = 1.0,
                Price = e.Price
            };
            
            // Calculate total volume at bid and ask within specified depth
            long totalBidVolume = CalculateTotalBidVolume(currentBid);
            long totalAskVolume = CalculateTotalAskVolume(currentAsk);
            
            // Skip if either volume is too low for reliable analysis
            if (totalBidVolume < _absorptionThreshold || totalAskVolume < _absorptionThreshold)
                return result;
            
            // Calculate imbalance ratio
            double bidAskRatio = (double)totalBidVolume / totalAskVolume;
            double askBidRatio = (double)totalAskVolume / totalBidVolume;
            
            // Check for significant imbalance
            if (bidAskRatio >= _imbalanceThreshold)
            {
                result.HasSignificantImbalance = true;
                result.ImbalanceType = ImbalanceType.BuyerDominant;
                result.ImbalanceRatio = bidAskRatio;
            }
            else if (askBidRatio >= _imbalanceThreshold)
            {
                result.HasSignificantImbalance = true;
                result.ImbalanceType = ImbalanceType.SellerDominant;
                result.ImbalanceRatio = askBidRatio;
            }
            
            // If significant imbalance detected, check if it's at a key price level
            if (result.HasSignificantImbalance)
            {
                UpdateKeyPriceLevels(e.Price);
            }
            
            return result;
        }
        
        /// <summary>
        /// Update DOM info with new market data
        /// </summary>
        public void UpdateDOM(MarketDataType dataType, double price, long volume)
        {
            if (dataType == MarketDataType.Bid)
            {
                if (volume == 0 && _bidDepth.ContainsKey(price))
                    _bidDepth.Remove(price);
                else
                    _bidDepth[price] = volume;
            }
            else if (dataType == MarketDataType.Ask)
            {
                if (volume == 0 && _askDepth.ContainsKey(price))
                    _askDepth.Remove(price);
                else
                    _askDepth[price] = volume;
            }
        }
        
        /// <summary>
        /// Detect absorption patterns by analyzing volume at price
        /// </summary>
        public bool DetectAbsorption(double price, Dictionary<double, long> buyVolume, 
            Dictionary<double, long> sellVolume, double cumulativeDelta)
        {
            // Check if price has sufficient volume for analysis
            if (!buyVolume.ContainsKey(price) || !sellVolume.ContainsKey(price))
                return false;
                
            long buys = buyVolume[price];
            long sells = sellVolume[price];
            
            // Skip if volume is too low for reliable analysis
            if (buys + sells < _absorptionThreshold)
                return false;
            
            // Calculate ratio between opposite volume and price movement
            double volumeRatio = Math.Max(buys, sells) / (double)Math.Min(buys, sells);
            
            // Check for absorption pattern - high volume but minimal price movement
            bool isAbsorption = volumeRatio >= _imbalanceThreshold && 
                                Math.Abs(cumulativeDelta) > _absorptionThreshold;
            
            // If absorption detected, update support/resistance levels
            if (isAbsorption)
            {
                if (buys > sells && cumulativeDelta < 0)
                {
                    // Buyers absorbing selling pressure - potential support
                    AddSupportLevel(price);
                }
                else if (sells > buys && cumulativeDelta > 0)
                {
                    // Sellers absorbing buying pressure - potential resistance
                    AddResistanceLevel(price);
                }
            }
            
            return isAbsorption;
        }
        
        /// <summary>
        /// Classify absorption pattern based on price location
        /// </summary>
        public AbsorptionType ClassifyAbsorption(double price, Dictionary<double, long> buyVolume, 
            Dictionary<double, long> sellVolume)
        {
            // Check if price has sufficient volume for analysis
            if (!buyVolume.ContainsKey(price) || !sellVolume.ContainsKey(price))
                return AbsorptionType.None;
                
            long buys = buyVolume[price];
            long sells = sellVolume[price];
            
            // Check if this price is near a support level
            bool nearSupport = IsNearSupportLevel(price);
            
            // Check if this price is near a resistance level
            bool nearResistance = IsNearResistanceLevel(price);
            
            // Classify absorption pattern
            if (buys > sells && nearSupport)
            {
                return AbsorptionType.BuyAbsorptionAtSupport;
            }
            else if (sells > buys && nearResistance)
            {
                return AbsorptionType.SellAbsorptionAtResistance;
            }
            
            return AbsorptionType.None;
        }
        
        /// <summary>
        /// Confirm buy signal based on historical imbalances
        /// </summary>
        public bool ConfirmBuySignal(List<double> recentImbalances, double currentImbalance)
        {
            // Need some history to confirm signal
            if (recentImbalances.Count < 3)
                return false;
                
            // Check if current imbalance is stronger than recent ones
            double avgRecentImbalance = recentImbalances.Take(recentImbalances.Count - 1).Average();
            
            // Check if we have price near support level
            bool nearSupport = IsNearSupportLevel(_lastLow);
            
            // Confirm signal if imbalance is strong and increasing, and price is near support
            return currentImbalance > avgRecentImbalance * 1.2 && nearSupport;
        }
        
        /// <summary>
        /// Confirm sell signal based on historical imbalances
        /// </summary>
        public bool ConfirmSellSignal(List<double> recentImbalances, double currentImbalance)
        {
            // Need some history to confirm signal
            if (recentImbalances.Count < 3)
                return false;
                
            // Check if current imbalance is stronger than recent ones
            double avgRecentImbalance = recentImbalances.Take(recentImbalances.Count - 1).Average();
            
            // Check if we have price near resistance level
            bool nearResistance = IsNearResistanceLevel(_lastHigh);
            
            // Confirm signal if imbalance is strong and increasing, and price is near resistance
            return currentImbalance > avgRecentImbalance * 1.2 && nearResistance;
        }
        #endregion
        
        #region Private Helper Methods
        private void UpdateDOMFromEvent(MarketDepthEventArgs e)
        {
            if (e.MarketDataType == MarketDataType.Ask)
            {
                if (e.Operation == Operation.Update || e.Operation == Operation.Add)
                    _askDepth[e.Price] = e.Volume;
                else if (e.Operation == Operation.Remove && _askDepth.ContainsKey(e.Price))
                    _askDepth.Remove(e.Price);
            }
            else if (e.MarketDataType == MarketDataType.Bid)
            {
                if (e.Operation == Operation.Update || e.Operation == Operation.Add)
                    _bidDepth[e.Price] = e.Volume;
                else if (e.Operation == Operation.Remove && _bidDepth.ContainsKey(e.Price))
                    _bidDepth.Remove(e.Price);
            }
        }
        
        private long CalculateTotalBidVolume(double currentBid)
        {
            return _bidDepth
                .Where(kv => kv.Key >= currentBid - (_domDepth * 0.25) && kv.Key <= currentBid)
                .Sum(kv => kv.Value);
        }
        
        private long CalculateTotalAskVolume(double currentAsk)
        {
            return _askDepth
                .Where(kv => kv.Key <= currentAsk + (_domDepth * 0.25) && kv.Key >= currentAsk)
                .Sum(kv => kv.Value);
        }
        
        private void UpdateKeyPriceLevels(double price)
        {
            // Update high/low tracking
            if (price > _lastHigh)
            {
                _lastHigh = price;
                // If price makes a new high with significant margin, update significant high
                if (price > _lastSignificantHigh * 1.005)
                    _lastSignificantHigh = price;
            }
            
            if (price < _lastLow)
            {
                _lastLow = price;
                // If price makes a new low with significant margin, update significant low
                if (_lastSignificantLow == double.MaxValue || price < _lastSignificantLow * 0.995)
                    _lastSignificantLow = price;
            }
        }
        
        private void AddSupportLevel(double price)
        {
            // Only add if not too close to existing support
            if (!_recentSupportLevels.Any(p => Math.Abs(p - price) / price < 0.001))
            {
                _recentSupportLevels.Add(price);
                if (_recentSupportLevels.Count > MaxSupportResistanceHistory)
                    _recentSupportLevels.RemoveAt(0);
            }
        }
        
        private void AddResistanceLevel(double price)
        {
            // Only add if not too close to existing resistance
            if (!_recentResistanceLevels.Any(p => Math.Abs(p - price) / price < 0.001))
            {
                _recentResistanceLevels.Add(price);
                if (_recentResistanceLevels.Count > MaxSupportResistanceHistory)
                    _recentResistanceLevels.RemoveAt(0);
            }
        }
        
        private bool IsNearSupportLevel(double price)
        {
            // Check if price is near any recent support level
            double proximityThreshold = price * SupportResistanceProximityFactor;
            return _recentSupportLevels.Any(s => Math.Abs(s - price) <= proximityThreshold);
        }
        
        private bool IsNearResistanceLevel(double price)
        {
            // Check if price is near any recent resistance level
            double proximityThreshold = price * SupportResistanceProximityFactor;
            return _recentResistanceLevels.Any(r => Math.Abs(r - price) <= proximityThreshold);
        }
        #endregion
    }
}
