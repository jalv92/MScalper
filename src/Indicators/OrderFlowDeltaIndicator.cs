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

namespace MScalper.Indicators
{
    [Description("Visualizes the cumulative delta volume to identify buying/selling pressure")]
    public class OrderFlowDeltaIndicator : NinjaTrader.NinjaScript.Indicators.Indicator
    {
        #region Variables
        private double cumulativeDelta;
        private double previousBarDelta;
        private Dictionary<double, long> buyVolume;
        private Dictionary<double, long> sellVolume;
        private Dictionary<double, long> deltaVolume;
        private List<double> deltaHistory;

        private Series<double> deltaSeries;
        private Series<double> cumulativeDeltaSeries;
        private Series<double> impulseLineSeries;

        private DateTime lastUpdateTime;
        private bool sessionNewDay;
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Plot Style", Description = "Visual style for delta display", Order = 1, GroupName = "Display")]
        public DeltaPlotStyle PlotStyle { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Delta Threshold", Description = "Highlight bars with delta exceeding this threshold", Order = 2, GroupName = "Thresholds")]
        public int DeltaThreshold { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Reset on New Session", Description = "Reset cumulative delta at session start", Order = 1, GroupName = "Sessions")]
        public bool ResetOnNewSession { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Divergence Detection", Description = "Enable price-delta divergence detection", Order = 1, GroupName = "Analysis")]
        public bool EnableDivergenceDetection { get; set; }

        [NinjaScriptProperty]
        [Range(1, 50)]
        [Display(Name = "Divergence Lookback", Description = "Bars to look back for divergence", Order = 2, GroupName = "Analysis")]
        public int DivergenceLookback { get; set; }
        #endregion

        /// <summary>
        /// Defines display styles for delta visualization
        /// </summary>
        public enum DeltaPlotStyle
        {
            Histogram,
            Line,
            DualColorBars
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Visualizes the cumulative delta volume to identify buying/selling pressure";
                Name = "OrderFlow Delta";
                
                // Default settings
                PlotStyle = DeltaPlotStyle.Histogram;
                DeltaThreshold = 500;
                ResetOnNewSession = true;
                EnableDivergenceDetection = true;
                DivergenceLookback = 10;
                
                // Indicator settings
                IsOverlay = false;
                PaintPriceMarkers = false;
                DisplayInDataBox = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
            }
            else if (State == State.Configure)
            {
                // Create series for different plots
                deltaSeries = new Series<double>(this);
                cumulativeDeltaSeries = new Series<double>(this);
                impulseLineSeries = new Series<double>(this);
                
                // Add plot for delta visualization
                AddPlot(new Stroke(Brushes.DodgerBlue, 2), PlotStyle == DeltaPlotStyle.Line ? 
                    PlotStyle.Line : PlotStyle.Histogram, "Delta");
                
                // Add plot for cumulative delta
                AddPlot(new Stroke(Brushes.DarkGreen, 2), PlotStyle.Line, "CumDelta");
                
                // Add plot for impulse line (zero line)
                AddPlot(new Stroke(Brushes.Gray, 1, DashStyleHelper.Dash), PlotStyle.Line, "Impulse");
                
                // Initialize collections
                buyVolume = new Dictionary<double, long>();
                sellVolume = new Dictionary<double, long>();
                deltaVolume = new Dictionary<double, long>();
                deltaHistory = new List<double>();
            }
            else if (State == State.DataLoaded)
            {
                // Reset variables
                cumulativeDelta = 0;
                previousBarDelta = 0;
                sessionNewDay = false;
                lastUpdateTime = DateTime.MinValue;
            }
        }

        protected override void OnBarUpdate()
        {
            // Skip update on non-primary bars
            if (BarsInProgress != 0)
                return;

            // Check for session reset
            if (ResetOnNewSession && IsNewSession())
            {
                cumulativeDelta = 0;
                previousBarDelta = 0;
                buyVolume.Clear();
                sellVolume.Clear();
                deltaVolume.Clear();
                sessionNewDay = true;
            }

            // Calculate bar delta from stored tick data or estimate from OHLC
            double barDelta = CalculateBarDelta();
            
            // Update cumulative delta
            cumulativeDelta += barDelta;
            
            // Store deltas
            deltaSeries[0] = barDelta;
            cumulativeDeltaSeries[0] = cumulativeDelta;
            impulseLineSeries[0] = 0; // Zero line
            
            // Update delta history for divergence detection
            if (CurrentBar >= 1)
            {
                deltaHistory.Add(barDelta);
                if (deltaHistory.Count > DivergenceLookback * 2)
                    deltaHistory.RemoveAt(0);
            }
            
            // Set plot colors based on delta value
            if (barDelta > 0)
                PlotBrushes[0][0] = Brushes.Green;
            else if (barDelta < 0)
                PlotBrushes[0][0] = Brushes.Red;
            else
                PlotBrushes[0][0] = Brushes.Gray;
            
            // Set cumulative delta color
            if (cumulativeDelta > 0)
                PlotBrushes[1][0] = Brushes.Green;
            else if (cumulativeDelta < 0)
                PlotBrushes[1][0] = Brushes.Red;
            else
                PlotBrushes[1][0] = Brushes.Gray;
                
            // Set dynamic opacity based on delta magnitude
            if (Math.Abs(barDelta) > DeltaThreshold)
            {
                double opacity = Math.Min(1.0, Math.Abs(barDelta) / (DeltaThreshold * 2.0));
                PlotBrushes[0][0] = new SolidColorBrush(
                    Color.FromArgb(
                        (byte)(opacity * 255), 
                        ((SolidColorBrush)PlotBrushes[0][0]).Color.R,
                        ((SolidColorBrush)PlotBrushes[0][0]).Color.G,
                        ((SolidColorBrush)PlotBrushes[0][0]).Color.B));
            }
                
            // Detect divergence if enabled
            if (EnableDivergenceDetection && CurrentBar >= DivergenceLookback)
                DetectDivergence();
                
            // Store previous delta for next calculation
            previousBarDelta = barDelta;
        }

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            // Process market data for delta calculation
            if (e.MarketDataType == MarketDataType.Last)
            {
                double price = e.Price;
                long volume = e.Volume;
                
                // Determine if buy or sell
                bool isBuy = false;
                
                // If we have bid/ask data, use it to determine direction
                if (GetCurrentAsk() > 0 && GetCurrentBid() > 0)
                {
                    double bid = GetCurrentBid();
                    double ask = GetCurrentAsk();
                    
                    isBuy = price >= ask; // Trade at or above ask is buyer initiated
                    bool isSell = price <= bid; // Trade at or below bid is seller initiated
                    
                    if (!isBuy && !isSell)
                    {
                        // Trade inside the spread, use previous price direction heuristic
                        if (LastPrice < price)
                            isBuy = true;
                    }
                }
                else
                {
                    // No bid/ask data, use tick direction heuristic
                    if (lastUpdateTime != DateTime.MinValue)
                    {
                        if (LastPrice < price)
                            isBuy = true;
                    }
                }
                
                // Update volume tracking
                if (!buyVolume.ContainsKey(price))
                    buyVolume[price] = 0;
                    
                if (!sellVolume.ContainsKey(price))
                    sellVolume[price] = 0;
                    
                if (!deltaVolume.ContainsKey(price))
                    deltaVolume[price] = 0;
                
                if (isBuy)
                {
                    buyVolume[price] += volume;
                    deltaVolume[price] += volume;
                }
                else
                {
                    sellVolume[price] += volume;
                    deltaVolume[price] -= volume;
                }
                
                // Update tracking variables
                lastUpdateTime = e.Time;
            }
        }

        #region Helper Methods
        private double CalculateBarDelta()
        {
            // Calculate delta from stored tick data
            double total = 0;
            
            // If we have tick data, use it
            if (deltaVolume.Count > 0)
            {
                foreach (var delta in deltaVolume.Values)
                    total += delta;
                    
                // Reset for next bar
                deltaVolume.Clear();
                buyVolume.Clear();
                sellVolume.Clear();
                
                return total;
            }
            
            // Fallback: Estimate from OHLC and volume
            double barRange = High[0] - Low[0];
            if (barRange <= 0)
                return 0;
                
            double closePosition = (Close[0] - Low[0]) / barRange;
            double estimatedDelta = Volume[0] * (closePosition * 2 - 1);
            
            return estimatedDelta;
        }
        
        private bool IsNewSession()
        {
            bool isNewSession = false;
            
            // NinjaTrader has built-in IsNewSession logic we can use
            if (Bars.IsFirstBarOfSession)
                isNewSession = true;
                
            // Additional check for new day
            if (!sessionNewDay && Time[0].Date != (CurrentBar > 0 ? Time[1].Date : DateTime.MinValue.Date))
            {
                isNewSession = true;
                sessionNewDay = true;
            }
            
            return isNewSession;
        }
        
        private void DetectDivergence()
        {
            if (deltaHistory.Count < DivergenceLookback)
                return;
                
            // Check for bullish divergence: lower lows in price, higher lows in delta
            bool bullishDivergence = false;
            if (Low[0] < Low[DivergenceLookback] && deltaHistory.Last() > deltaHistory[deltaHistory.Count - DivergenceLookback - 1])
                bullishDivergence = true;
                
            // Check for bearish divergence: higher highs in price, lower highs in delta
            bool bearishDivergence = false;
            if (High[0] > High[DivergenceLookback] && deltaHistory.Last() < deltaHistory[deltaHistory.Count - DivergenceLookback - 1])
                bearishDivergence = true;
                
            // Draw markers for divergence
            if (bullishDivergence)
            {
                Draw.Diamond(this, "BullDiv" + CurrentBar, false, 0, Low[0] - TickSize * 5, Brushes.LimeGreen);
                Draw.Text(this, "BullDivTxt" + CurrentBar, "Bull Div", 0, Low[0] - TickSize * 10);
            }
                
            if (bearishDivergence)
            {
                Draw.Diamond(this, "BearDiv" + CurrentBar, false, 0, High[0] + TickSize * 5, Brushes.Crimson);
                Draw.Text(this, "BearDivTxt" + CurrentBar, "Bear Div", 0, High[0] + TickSize * 10);
            }
        }
        #endregion
    }
}