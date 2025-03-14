// Compatible with NinjaTrader 8.1.4.1
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Indicators.MScalper
{
    [Description("Visualizes order book imbalances to identify potential market pressure points")]
    public class OrderBookImbalanceIndicator : NinjaTrader.NinjaScript.Indicators.Indicator
    {
        #region Variables
        private Dictionary<double, long> bidDepth;
        private Dictionary<double, long> askDepth;
        
        private Series<double> imbalanceSeries;
        private Series<double> bidAskRatioSeries;
        private Series<double> liquidityThresholdSeries;
        
        private double currentBidAskRatio;
        private double currentImbalance;
        private double currentThreshold;
        
        private List<KeyValuePair<double, double>> significantImbalances;
        private DateTime lastUpdateTime;
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "DOM Depth", Description = "Number of DOM levels to analyze", Order = 1, GroupName = "DOM")]
        public int DOMDepth { get; set; }
        
        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "Imbalance Threshold", Description = "Ratio threshold for significant imbalance", Order = 2, GroupName = "DOM")]
        public double ImbalanceThreshold { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, 1000)]
        [Display(Name = "Minimum Volume", Description = "Minimum volume for imbalance calculation", Order = 3, GroupName = "DOM")]
        public int MinimumVolume { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Imbalance Zones", Description = "Draw zones on chart where imbalances occur", Order = 1, GroupName = "Display")]
        public bool ShowImbalanceZones { get; set; }
        
        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Lookback Periods", Description = "Number of bars to analyze for persistent imbalances", Order = 1, GroupName = "Analysis")]
        public int LookbackPeriods { get; set; }
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Visualizes order book imbalances to identify potential market pressure points";
                Name = "OrderBook Imbalance";
                
                // Default settings
                DOMDepth = 5;
                ImbalanceThreshold = 2.0;
                MinimumVolume = 50;
                ShowImbalanceZones = true;
                LookbackPeriods = 5;
                
                // Indicator settings
                IsOverlay = false;
                DisplayInDataBox = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
            }
            else if (State == State.Configure)
            {
                // Initialize collections
                bidDepth = new Dictionary<double, long>();
                askDepth = new Dictionary<double, long>();
                significantImbalances = new List<KeyValuePair<double, double>>();
                
                // Create series for plots
                imbalanceSeries = new Series<double>(this);
                bidAskRatioSeries = new Series<double>(this);
                liquidityThresholdSeries = new Series<double>(this);
                
                // Add plots for visualization
                AddPlot(new Stroke(Brushes.DodgerBlue, 2), PlotStyle.Line, "Imbalance");
                AddPlot(new Stroke(Brushes.Green, 1), PlotStyle.Line, "BidAskRatio");
                AddPlot(new Stroke(Brushes.Red, 1, DashStyleHelper.Dash), PlotStyle.Line, "Threshold");
            }
            else if (State == State.DataLoaded)
            {
                // Initialize values
                currentBidAskRatio = 1.0;
                currentImbalance = 0;
                currentThreshold = ImbalanceThreshold;
                lastUpdateTime = DateTime.MinValue;
            }
        }

        protected override void OnBarUpdate()
        {
            // Skip update for non-primary series
            if (BarsInProgress != 0)
                return;
                
            // Calculate and update current bar imbalance
            CalculateBarImbalance();
            
            // Store values in series for plotting
            imbalanceSeries[0] = currentImbalance;
            bidAskRatioSeries[0] = currentBidAskRatio;
            liquidityThresholdSeries[0] = currentThreshold;
            
            // Set plot colors based on imbalance direction
            if (currentImbalance > 0)
                PlotBrushes[0][0] = Brushes.Green;
            else if (currentImbalance < 0)
                PlotBrushes[0][0] = Brushes.Red;
            else
                PlotBrushes[0][0] = Brushes.Gray;
                
            // Identify persistent imbalances
            if (CurrentBar >= LookbackPeriods)
                IdentifyPersistentImbalances();
                
            // Draw imbalance zones if enabled
            if (ShowImbalanceZones)
                DrawImbalanceZones();
        }

        protected override void OnMarketDepth(MarketDepthEventArgs e)
        {
            // Process market depth updates
            if (e.MarketDataType == MarketDataType.Ask)
            {
                if (e.Operation == Operation.Update || e.Operation == Operation.Add)
                    askDepth[e.Price] = e.Volume;
                else if (e.Operation == Operation.Remove && askDepth.ContainsKey(e.Price))
                    askDepth.Remove(e.Price);
            }
            else if (e.MarketDataType == MarketDataType.Bid)
            {
                if (e.Operation == Operation.Update || e.Operation == Operation.Add)
                    bidDepth[e.Price] = e.Volume;
                else if (e.Operation == Operation.Remove && bidDepth.ContainsKey(e.Price))
                    bidDepth.Remove(e.Price);
            }
            
            // Update imbalance calculation with each significant DOM change
            if (IsTimeToRecalculate(e.Time))
            {
                currentImbalance = CalculateCurrentImbalance();
                // Only recalculate ratio when we have a meaningful update
                if (e.Volume >= MinimumVolume)
                    currentBidAskRatio = CalculateBidAskRatio();
                
                lastUpdateTime = e.Time;
            }
        }

        #region Helper Methods
        private void CalculateBarImbalance()
        {
            // Calculate average imbalance for the current bar
            double bidVolume = bidDepth.Values.Sum();
            double askVolume = askDepth.Values.Sum();
            
            if (bidVolume < MinimumVolume || askVolume < MinimumVolume)
            {
                currentImbalance = 0;
                return;
            }
            
            // Calculate bid/ask ratio
            currentBidAskRatio = bidVolume / askVolume;
            
            // Calculate normalized imbalance (-1 to 1 scale)
            if (currentBidAskRatio > 1.0)
                currentImbalance = (currentBidAskRatio - 1.0) / (ImbalanceThreshold - 1.0);
            else if (currentBidAskRatio < 1.0)
                currentImbalance = -((1.0 / currentBidAskRatio) - 1.0) / (ImbalanceThreshold - 1.0);
            else
                currentImbalance = 0;
                
            // Clamp imbalance to -1.0 to 1.0 range
            currentImbalance = Math.Max(-1.0, Math.Min(1.0, currentImbalance));
        }
        
        private double CalculateCurrentImbalance()
        {
            // Get current bid/ask
            double bid = GetCurrentBid();
            double ask = GetCurrentAsk();
            
            if (bid <= 0 || ask <= 0)
                return 0;
                
            // Get bid volume within threshold of current bid
            double totalBidVolume = CalculateTotalBidVolume(bid);
            
            // Get ask volume within threshold of current ask
            double totalAskVolume = CalculateTotalAskVolume(ask);
            
            if (totalBidVolume < MinimumVolume || totalAskVolume < MinimumVolume)
                return 0;
                
            // Calculate normalized imbalance (-1 to 1 scale)
            double ratio = totalBidVolume / totalAskVolume;
            
            if (ratio > 1.0)
                return (ratio - 1.0) / (ImbalanceThreshold - 1.0);
            else if (ratio < 1.0)
                return -((1.0 / ratio) - 1.0) / (ImbalanceThreshold - 1.0);
            else
                return 0;
        }
        
        private double CalculateBidAskRatio()
        {
            double bid = GetCurrentBid();
            double ask = GetCurrentAsk();
            
            if (bid <= 0 || ask <= 0)
                return 1.0;
                
            double totalBidVolume = CalculateTotalBidVolume(bid);
            double totalAskVolume = CalculateTotalAskVolume(ask);
            
            if (totalBidVolume < MinimumVolume || totalAskVolume < MinimumVolume)
                return 1.0;
                
            return totalBidVolume / totalAskVolume;
        }
        
        private double CalculateTotalBidVolume(double currentBid)
        {
            // Calculate sum of bid volumes within DOM depth
            return bidDepth
                .Where(kv => kv.Key >= currentBid - (DOMDepth * TickSize) && 
                             kv.Key <= currentBid)
                .Sum(kv => kv.Value);
        }
        
        private double CalculateTotalAskVolume(double currentAsk)
        {
            // Calculate sum of ask volumes within DOM depth
            return askDepth
                .Where(kv => kv.Key <= currentAsk + (DOMDepth * TickSize) && 
                             kv.Key >= currentAsk)
                .Sum(kv => kv.Value);
        }
        
        private bool IsTimeToRecalculate(DateTime currentTime)
        {
            // Limit recalculation frequency to reduce CPU usage
            if (lastUpdateTime == DateTime.MinValue)
                return true;
                
            // Recalculate no more than 10 times per second
            return currentTime.Subtract(lastUpdateTime).TotalMilliseconds >= 100;
        }
        
        private void IdentifyPersistentImbalances()
        {
            // Check if we have a persistent imbalance over lookback period
            if (Math.Abs(currentImbalance) < 0.5)
                return;
                
            // Calculate average imbalance over lookback period
            double avgImbalance = 0;
            for (int i = 0; i < LookbackPeriods; i++)
            {
                avgImbalance += Math.Abs(imbalanceSeries[i]);
            }
            avgImbalance /= LookbackPeriods;
            
            // If current imbalance is significantly above average, mark as significant
            if (Math.Abs(currentImbalance) > avgImbalance * 1.5 && 
                Math.Abs(currentImbalance) > 0.7)
            {
                // Add to significant imbalances list
                double price = currentImbalance > 0 ? GetCurrentBid() : GetCurrentAsk();
                significantImbalances.Add(new KeyValuePair<double, double>(price, currentImbalance));
                
                // Trim list if too long
                if (significantImbalances.Count > 10)
                    significantImbalances.RemoveAt(0);
                    
                // Draw marker on chart
                string label = currentImbalance > 0 ? "Bid Imbalance" : "Ask Imbalance";
                Brush brush = currentImbalance > 0 ? Brushes.Green : Brushes.Red;
                
                Draw.Diamond(this, "Imb" + CurrentBar, false, 0, price, brush);
                if (Math.Abs(currentImbalance) > 0.9)
                    Draw.Text(this, "ImbTxt" + CurrentBar, label, 0, price + TickSize * 2);
            }
        }
        
        private void DrawImbalanceZones()
        {
            // Draw zones representing significant imbalances
            foreach (var imbalance in significantImbalances)
            {
                double price = imbalance.Key;
                double value = imbalance.Value;
                
                if (Math.Abs(value) < 0.7)
                    continue;
                    
                // Draw zone with transparency based on imbalance strength
                Brush brush = value > 0 ? 
                    new SolidColorBrush(Color.FromArgb(40, 0, 255, 0)) : 
                    new SolidColorBrush(Color.FromArgb(40, 255, 0, 0));
                    
                // Calculate zone height based on imbalance strength
                double zoneHeight = TickSize * DOMDepth * Math.Abs(value);
                
                // Draw the zone
                if (value > 0)
                {
                    // Support zone below price
                    Draw.Rectangle(this, "Zone" + price, false, 0, price - zoneHeight, 
                        0, price, brush);
                }
                else
                {
                    // Resistance zone above price
                    Draw.Rectangle(this, "Zone" + price, false, 0, price, 
                        0, price + zoneHeight, brush);
                }
                
                // Add price line
                Draw.Line(this, "Line" + price, 0, price, LookbackPeriods, price, 
                    value > 0 ? Brushes.Green : Brushes.Red, 
                    DashStyleHelper.Dot, 1);
            }
        }
        #endregion
    }
}
