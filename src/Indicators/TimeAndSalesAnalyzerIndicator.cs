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

namespace OrderFlowScalper.Indicators
{
    [Description("Analyzes Time & Sales data to identify buying/selling pressure and aggressive trades")]
    public class TimeAndSalesAnalyzerIndicator : NinjaTrader.NinjaScript.Indicators.Indicator
    {
        #region Variables
        private List<TransactionData> recentTransactions;
        private Dictionary<DateTime, TransactionSummary> barTransactions;
        
        private Series<double> buyPressureSeries;
        private Series<double> sellPressureSeries;
        private Series<double> largeOrderSeries;
        private Series<double> aggressionIndexSeries;
        
        private DateTime lastProcessedTime;
        private double lastPrice;
        private int transactionCount;
        private double currentAggressionIndex;
        #endregion

        #region Properties
        [NinjaScriptProperty]
        [Range(1, 1000)]
        [Display(Name = "Large Order Threshold", Description = "Volume threshold for large orders", Order = 1, GroupName = "Transaction Analysis")]
        public int LargeOrderThreshold { get; set; }
        
        [NinjaScriptProperty]
        [Range(10, 1000)]
        [Display(Name = "Transaction History", Description = "Number of transactions to keep in memory", Order = 2, GroupName = "Transaction Analysis")]
        public int TransactionHistorySize { get; set; }
        
        [NinjaScriptProperty]
        [Range(0.1, 10.0)]
        [Display(Name = "Aggression Factor", Description = "Multiplier for aggressive trade detection", Order = 3, GroupName = "Transaction Analysis")]
        public double AggressionFactor { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Large Orders", Description = "Highlight large orders on chart", Order = 1, GroupName = "Display")]
        public bool ShowLargeOrders { get; set; }
        
        [NinjaScriptProperty]
        [Display(Name = "Show Aggressive Clusters", Description = "Highlight clusters of aggressive trading", Order = 2, GroupName = "Display")]
        public bool ShowAggressiveClusters { get; set; }
        
        [NinjaScriptProperty]
        [Range(3, 20)]
        [Display(Name = "Cluster Size", Description = "Number of transactions to consider a cluster", Order = 3, GroupName = "Display")]
        public int ClusterSize { get; set; }
        #endregion

        private class TransactionData
        {
            public DateTime Time { get; set; }
            public double Price { get; set; }
            public long Volume { get; set; }
            public bool IsBuyerInitiated { get; set; }
            public bool IsAggressiveOrder { get; set; }
            public bool IsLargeOrder { get; set; }
        }
        
        private class TransactionSummary
        {
            public long BuyVolume { get; set; }
            public long SellVolume { get; set; }
            public int BuyCount { get; set; }
            public int SellCount { get; set; }
            public int AggressiveBuyCount { get; set; }
            public int AggressiveSellCount { get; set; }
            public long LargeOrderVolume { get; set; }
            public double AveragePrice { get; set; }
            public int TransactionCount { get; set; }
            public double MaxPrice { get; set; }
            public double MinPrice { get; set; }
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Analyzes Time & Sales data to identify buying/selling pressure and aggressive trades";
                Name = "Time & Sales Analyzer";
                
                // Default values
                LargeOrderThreshold = 50;
                TransactionHistorySize = 500;
                AggressionFactor = 1.5;
                ShowLargeOrders = true;
                ShowAggressiveClusters = true;
                ClusterSize = 5;
                
                // Indicator settings
                IsOverlay = false;
                DisplayInDataBox = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
            }
            else if (State == State.Configure)
            {
                // Initialize collections
                recentTransactions = new List<TransactionData>();
                barTransactions = new Dictionary<DateTime, TransactionSummary>();
                
                // Create series for plots
                buyPressureSeries = new Series<double>(this);
                sellPressureSeries = new Series<double>(this);
                largeOrderSeries = new Series<double>(this);
                aggressionIndexSeries = new Series<double>(this);
                
                // Add plots
                AddPlot(new Stroke(Brushes.Green, 2), PlotStyle.Line, "BuyPressure");
                AddPlot(new Stroke(Brushes.Red, 2), PlotStyle.Line, "SellPressure");
                AddPlot(new Stroke(Brushes.DarkGoldenrod, 1), PlotStyle.Line, "LargeOrders");
                AddPlot(new Stroke(Brushes.Blue, 1), PlotStyle.Line, "AggressionIndex");
            }
            else if (State == State.DataLoaded)
            {
                // Reset values
                lastProcessedTime = DateTime.MinValue;
                lastPrice = 0;
                transactionCount = 0;
                currentAggressionIndex = 0;
            }
        }

        protected override void OnBarUpdate()
        {
            // Process for main price series only
            if (BarsInProgress != 0)
                return;
                
            // Get all transactions for this bar
            DateTime barTime = Time[0];
            
            if (!barTransactions.ContainsKey(barTime))
            {
                // No transactions for this bar yet
                buyPressureSeries[0] = 0;
                sellPressureSeries[0] = 0;
                largeOrderSeries[0] = 0;
                aggressionIndexSeries[0] = 0;
                return;
            }
            
            // Calculate metrics from transactions
            TransactionSummary summary = barTransactions[barTime];
            
            // Calculate buying pressure (0-100 scale)
            double buyPressure = 0;
            if (summary.TransactionCount > 0)
            {
                buyPressure = (double)summary.BuyCount / summary.TransactionCount * 100;
                
                // Adjust for aggressive buys
                if (summary.AggressiveBuyCount > 0)
                    buyPressure *= (1 + (double)summary.AggressiveBuyCount / summary.BuyCount);
            }
            
            // Calculate selling pressure (0-100 scale)
            double sellPressure = 0;
            if (summary.TransactionCount > 0)
            {
                sellPressure = (double)summary.SellCount / summary.TransactionCount * 100;
                
                // Adjust for aggressive sells
                if (summary.AggressiveSellCount > 0)
                    sellPressure *= (1 + (double)summary.AggressiveSellCount / summary.SellCount);
            }
            
            // Calculate large order influence
            double largeOrderInfluence = 0;
            if (summary.TransactionCount > 0)
            {
                double totalVolume = summary.BuyVolume + summary.SellVolume;
                if (totalVolume > 0)
                    largeOrderInfluence = (double)summary.LargeOrderVolume / totalVolume * 100;
            }
            
            // Set values for plotting
            buyPressureSeries[0] = buyPressure;
            sellPressureSeries[0] = sellPressure;
            largeOrderSeries[0] = largeOrderInfluence;
            aggressionIndexSeries[0] = currentAggressionIndex;
            
            // Handle visualization
            if (ShowAggressiveClusters && CurrentBar > 0)
            {
                DetectAggressiveClusters(barTime);
            }
        }

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            // Process only last trades (executed transactions)
            if (e.MarketDataType != MarketDataType.Last)
                return;
                
            // Get the data
            double price = e.Price;
            long volume = e.Volume;
            DateTime time = e.Time;
            
            // Determine if buyer or seller initiated
            bool isBuyerInitiated = false;
            
            // If we have bid/ask data, use it
            if (GetCurrentAsk() > 0 && GetCurrentBid() > 0)
            {
                double bid = GetCurrentBid();
                double ask = GetCurrentAsk();
                
                isBuyerInitiated = price >= ask; // At or above ask is buyer initiated
                bool isSellerInitiated = price <= bid; // At or below bid is seller initiated
                
                if (!isBuyerInitiated && !isSellerInitiated)
                {
                    // Trade in the spread, use price direction
                    if (lastPrice > 0)
                        isBuyerInitiated = price > lastPrice;
                }
            }
            else
            {
                // No bid/ask data, use price direction
                if (lastPrice > 0)
                    isBuyerInitiated = price > lastPrice;
            }
            
            // Determine if this is an aggressive order
            bool isAggressiveOrder = false;
            if (lastProcessedTime != DateTime.MinValue)
            {
                // Time between transactions (milliseconds)
                double timeDiff = time.Subtract(lastProcessedTime).TotalMilliseconds;
                
                // If transactions are coming in very rapidly, may indicate aggression
                if (timeDiff < 500) // Less than 500ms between trades
                    isAggressiveOrder = true;
                    
                // If price is moving rapidly, may indicate aggression
                if (lastPrice > 0)
                {
                    double priceChange = Math.Abs(price - lastPrice);
                    if (priceChange > TickSize * 2) // Moving more than 2 ticks
                        isAggressiveOrder = true;
                }
            }
            
            // Determine if this is a large order
            bool isLargeOrder = volume >= LargeOrderThreshold;
            
            // Create transaction data
            TransactionData transaction = new TransactionData
            {
                Time = time,
                Price = price,
                Volume = volume,
                IsBuyerInitiated = isBuyerInitiated,
                IsAggressiveOrder = isAggressiveOrder,
                IsLargeOrder = isLargeOrder
            };
            
            // Add to recent transactions list
            recentTransactions.Add(transaction);
            if (recentTransactions.Count > TransactionHistorySize)
                recentTransactions.RemoveAt(0);
                
            // Update bar transactions summary
            DateTime barTime = Time[0];
            if (!barTransactions.ContainsKey(barTime))
            {
                barTransactions[barTime] = new TransactionSummary
                {
                    BuyVolume = 0,
                    SellVolume = 0,
                    BuyCount = 0,
                    SellCount = 0,
                    AggressiveBuyCount = 0,
                    AggressiveSellCount = 0,
                    LargeOrderVolume = 0,
                    AveragePrice = 0,
                    TransactionCount = 0,
                    MaxPrice = double.MinValue,
                    MinPrice = double.MaxValue
                };
            }
            
            TransactionSummary summary = barTransactions[barTime];
            
            // Update summary
            summary.TransactionCount++;
            
            if (isBuyerInitiated)
            {
                summary.BuyVolume += volume;
                summary.BuyCount++;
                
                if (isAggressiveOrder)
                    summary.AggressiveBuyCount++;
            }
            else
            {
                summary.SellVolume += volume;
                summary.SellCount++;
                
                if (isAggressiveOrder)
                    summary.AggressiveSellCount++;
            }
            
            if (isLargeOrder)
                summary.LargeOrderVolume += volume;
                
            // Update price stats
            summary.MaxPrice = Math.Max(summary.MaxPrice, price);
            summary.MinPrice = Math.Min(summary.MinPrice, price);
            
            // Update average price
            summary.AveragePrice = ((summary.AveragePrice * (summary.TransactionCount - 1)) + price) / summary.TransactionCount;
            
            // Update aggression index
            UpdateAggressionIndex();
            
            // Update tracking variables
            lastProcessedTime = time;
            lastPrice = price;
            transactionCount++;
            
            // Handle large order visualization
            if (ShowLargeOrders && isLargeOrder)
                HighlightLargeOrder(transaction);
        }

        #region Helper Methods
        private void UpdateAggressionIndex()
        {
            // Need at least a few transactions
            if (recentTransactions.Count < 5)
                return;
                
            // Get most recent transactions
            var lastTrans = recentTransactions.Skip(Math.Max(0, recentTransactions.Count - 10)).ToList();
            
            // Calculate aggression metrics
            int aggressiveCount = lastTrans.Count(t => t.IsAggressiveOrder);
            long aggressiveVolume = lastTrans.Where(t => t.IsAggressiveOrder).Sum(t => t.Volume);
            
            double totalVolume = lastTrans.Sum(t => t.Volume);
            
            // Calculate normalized aggression index (0-100)
            double aggressiveRatio = (double)aggressiveCount / lastTrans.Count;
            double volumeRatio = totalVolume > 0 ? (double)aggressiveVolume / totalVolume : 0;
            
            // Combine metrics with configurable weight
            currentAggressionIndex = (aggressiveRatio * 0.7 + volumeRatio * 0.3) * 100;
        }
        
        private void DetectAggressiveClusters(DateTime barTime)
        {
            // Need enough transactions
            if (recentTransactions.Count < ClusterSize)
                return;
                
            // Get transactions for the current bar
            var barTrans = recentTransactions
                .Where(t => ToDay(t.Time) == ToDay(barTime) && 
                           ToTime(t.Time) >= ToTime(barTime) && 
                           ToTime(t.Time) < ToTime(barTime) + 60000) // Within this bar
                .ToList();
                
            if (barTrans.Count < ClusterSize)
                return;
                
            // Look for clusters of aggressive trades in same direction
            bool foundBuyCluster = false;
            bool foundSellCluster = false;
            
            // Check for buy clusters
            int consecutiveBuys = 0;
            int aggressiveBuys = 0;
            
            foreach (var trans in barTrans)
            {
                if (trans.IsBuyerInitiated)
                {
                    consecutiveBuys++;
                    if (trans.IsAggressiveOrder)
                        aggressiveBuys++;
                }
                else
                {
                    // Reset on sell
                    if (consecutiveBuys >= ClusterSize && aggressiveBuys >= ClusterSize / 2)
                        foundBuyCluster = true;
                        
                    consecutiveBuys = 0;
                    aggressiveBuys = 0;
                }
            }
            
            // Check final state
            if (consecutiveBuys >= ClusterSize && aggressiveBuys >= ClusterSize / 2)
                foundBuyCluster = true;
                
            // Check for sell clusters
            int consecutiveSells = 0;
            int aggressiveSells = 0;
            
            foreach (var trans in barTrans)
            {
                if (!trans.IsBuyerInitiated)
                {
                    consecutiveSells++;
                    if (trans.IsAggressiveOrder)
                        aggressiveSells++;
                }
                else
                {
                    // Reset on buy
                    if (consecutiveSells >= ClusterSize && aggressiveSells >= ClusterSize / 2)
                        foundSellCluster = true;
                        
                    consecutiveSells = 0;
                    aggressiveSells = 0;
                }
            }
            
            // Check final state
            if (consecutiveSells >= ClusterSize && aggressiveSells >= ClusterSize / 2)
                foundSellCluster = true;
                
            // Highlight clusters on chart
            if (foundBuyCluster)
            {
                Draw.ArrowUp(this, "BuyCluster" + CurrentBar, false, 0, Low[0] - TickSize * 5, Brushes.Green);
                Draw.Text(this, "BuyClusterTxt" + CurrentBar, "Buy Cluster", 0, Low[0] - TickSize * 10);
            }
            
            if (foundSellCluster)
            {
                Draw.ArrowDown(this, "SellCluster" + CurrentBar, false, 0, High[0] + TickSize * 5, Brushes.Red);
                Draw.Text(this, "SellClusterTxt" + CurrentBar, "Sell Cluster", 0, High[0] + TickSize * 10);
            }
        }
        
        private void HighlightLargeOrder(TransactionData transaction)
        {
            // Draw marker for large order
            Brush brush = transaction.IsBuyerInitiated ? Brushes.Green : Brushes.Red;
            string label = string.Format("{0} @ {1}", transaction.Volume, transaction.Price);
            
            // Draw different markers based on order direction
            if (transaction.IsBuyerInitiated)
            {
                Draw.TriangleUp(this, "LargeOrder" + transactionCount, false, 0, transaction.Price - TickSize * 2, brush);
            }
            else
            {
                Draw.TriangleDown(this, "LargeOrder" + transactionCount, false, 0, transaction.Price + TickSize * 2, brush);
            }
            
            // Add label for very large orders
            if (transaction.Volume >= LargeOrderThreshold * 2)
            {
                Draw.Text(this, "LargeOrderTxt" + transactionCount, label, 0, 
                    transaction.IsBuyerInitiated ? transaction.Price - TickSize * 4 : transaction.Price + TickSize * 4, 
                    brush);
            }
        }
        #endregion
    }
}