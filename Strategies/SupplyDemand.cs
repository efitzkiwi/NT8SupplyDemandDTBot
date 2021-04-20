#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using System.Windows.Media.Converters;
#endregion



// TODO TO DO:
/*
 * Exit half short position if short falls below zone and then rallies and touches same zone
 * Exit half long position if long goes above zone then falls and touches same zone
 * Fix the damn exit-on-close bug with OCO2
 */


//https://ninjatrader.com/support/helpGuides/nt8/?working_with_automated_strateg.htm
//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{

    public class PositionManager
    {
        public int Type { get; set; }
        public int Quantity { get; set; }
        public int BarCount { get; set; } // absolute bar count

    }
    public class SupplyDemand : Strategy
    {
        NinjaTrader.Cbi.Account ThisAcc;
        private AdvancedSRZones ASRZ;
        private RelativeVolumeNT8 RVOL;
        private RSI rSI1m;
        private RSI rSI30m;
        private RSI rSI10m;
        private LinRegSlope LRSDaily;
        private LinRegSlope LRS5p;
        private SMA sma5m;
        private SMA sma6m;
        private SMA sma20m;
        private SMA sma50m;
        private LinRegSlope LRS;
        public List<PositionManager> PPManager;



        double orderMultiplier = 1;
        bool debugPrint = false;

        // Unmanaged orders
        // We will never be long if we are short
        Order longOrder;
        Order shortOrder;
        Order stopOrder;
        Order limitOrder;
        Order stopOrderHalf;
        Order limitOrderHalf;
        string longName = "Long";
        string shortName = "Short";
        string longNameExit = "Long Exit on Close";
        string shortNameExit = "Short Exit on Close";
        string longStopName = "Long Stop";
        string longLimitName = "Long Limit";
        string shortStopName = "Short Stop";
        string shortLimitName = "Short Limit";
        string longStopNameHalf = "Long Stop Half";
        string longLimitNameHalf = "Long Limit Half";
        string shortStopNameHalf = "Short Stop Half";
        string shortLimitNameHalf = "Short Limit Half";
        string ocoString;
        string ocoStringHalf;
        int shareQuantity;
        double stopPrice;
        double limitPrice;
        double stopPriceHalf;
        double limitPriceHalf;
        double entryPrice;
        string orderMask;
        double longRSIStopPercent = 0.01;
        double longRSILimitPercent = 0.045;
        string currentOrderClassification = "";
        bool hasLongRSIOrderBeenAdjusted = false;
        bool hasGenericProtectionOrderBeenAdjusted = false;
        bool hasPriorityOrderBeenAdjusted = false;
        bool useSmaLineAsLongStopLoss = false;
        bool CancelOCOAndExitLongOrder = false;
        bool CancelOCOAndExitShortOrder = false;
        ZONE orderZone;
        bool NewOCO = false;
        int count = 0;
        private SessionIterator sessIter;
        DateTime sessBegin;
        DateTime sessEnd;
        int sessionEndSeconds = 120;
        bool GoShortAfterExit = false;
        bool GoLongAfterExit = false;
        bool cfa = false;
        double[] IsGap = { 0.0, 0.0, 0.0 }; //   Gap type(0=none1=up2=down) , last price , this price
        double PriceAdjustedDailySMASlope = 0;

        // Protection order variables
        /*
		double overBoughtRsiHighPriorityStopPercent = 0.0036;
		double overBoughtRsiLowPriorityStopPercent = 0.0034;
		double overBoughtRsiGenericStopPercent = 0.004;
		double genericLongOrderProtectionPercentThreshold = 0.012;
		double genericShortOrderProtectionPercentThreshold = 0.0075;
		double overBoughtRsiPercentAdjustmentThreshold = 72;
		double overSoldRsiPercentAdjustmentThreshold = 30;
		*/





        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Accurate supply/demand zones for intraday reversals.";
                Name = "SupplyDemand";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = false;
                //ExitOnSessionCloseSeconds = 30;
                IsUnmanaged = true;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 0;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 20;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;

                // Indicator vars
                AreaStrengthMultiplier = 1500;
                TimeThreshold = 45; // Minutes
                ProxyStrengthMultiplier = 500;
                NewZoneStrength = 60;
                ZoneTimeoutStrength = -1;
                NewZoneTopMultiplier = 0.0045;
                NewZoneBottomMultiplier = 0.008;
                ResZoneColor = Brushes.Red;
                SupZoneColor = Brushes.Green;
                AmountToBuy = 30000; // $$$
                LongStrengthThreshold = 55;
                ShortStrengthThreshold = 50;
                UseZoneStrengthOrderMultiplier = true;
                ZoneStrengthOrderScale = 100;
                BreakStrengthMultiplier = -2200;
                UseVolAccumulation = true;
                ShortStopLossPercent = 0.6;
                LongStopLossPercent = 1.3;
                ShortProfitPercent = 1.5;
                LongProfitPercent = 1.7;
                TradeDelay = 0; //minutes
                DelayExitMinutes = 0;
                Expiration = 60;
                MaxMergeCount = 1;
                MergeThreshold = 0.007;
                ScaleHalf = true;
                PercentOfAccForPosition = 50;

                overBoughtRsiHighPriorityStopPercent = 0.0036;
                overBoughtRsiLowPriorityStopPercent = 0.0034;
                overBoughtRsiGenericStopPercent = 0.004;
                genericLongOrderProtectionPercentThreshold = 0.012;
                genericShortOrderProtectionPercentThreshold = 0.0075;
                overBoughtRsiPercentAdjustmentThreshold = 72;
                overSoldRsiPercentAdjustmentThreshold = 30;

            }

            else if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Minute, 30); //BarsInProgress = 1
                AddDataSeries(BarsPeriodType.Day, 1); //BarsInProgress = 2
                AddDataSeries(BarsPeriodType.Minute, 10); //BarsInProgress = 3
            }

            else if (State == State.DataLoaded)
            {
                PPManager = new List<PositionManager>();
                ASRZ = AdvancedSRZones(AreaStrengthMultiplier, TimeThreshold, ProxyStrengthMultiplier, NewZoneStrength, ZoneTimeoutStrength, NewZoneTopMultiplier, NewZoneBottomMultiplier, ResZoneColor, SupZoneColor, BreakStrengthMultiplier, UseVolAccumulation, Expiration, MaxMergeCount, MergeThreshold);
                AddChartIndicator(ASRZ);
                RVOL = RelativeVolumeNT8(60, 2);
                AddChartIndicator(RVOL);
                rSI1m = RSI(14, 3);
                AddChartIndicator(rSI1m);
                rSI30m = RSI(BarsArray[1], 14, 7);
                rSI10m = RSI(BarsArray[3], 14, 7);
                LRS = LinRegSlope(50);
                LRS5p = LinRegSlope(5);
                sma5m = SMA(5);
                sma6m = SMA(6);
                sma20m = SMA(20);
                sma50m = SMA(50);
                LRSDaily = LinRegSlope(BarsArray[2], 20);
                sessIter = new SessionIterator(Bars);

            }
        }

        public int GetShareQuantity()
        {
            lock (Account.All)
            {
                ThisAcc = Account.All.FirstOrDefault(a => a.Name == "Sim101");
            }
            shareQuantity = (int)Math.Floor((ThisAcc.Get(AccountItem.CashValue, Currency.UsDollar) * (PercentOfAccForPosition / 100)) / Bars.GetClose(CurrentBar));
            if (ScaleHalf && shareQuantity % 2 != 0)
            {
                shareQuantity = shareQuantity - 1;
            }
            if (debugPrint) Print("Returning position quantity " + shareQuantity + "($ " + Bars.GetClose(CurrentBar) * shareQuantity + " )" + " for account size " + ThisAcc.Get(AccountItem.CashValue, Currency.UsDollar));
            return shareQuantity;
        }


        protected override void OnExecutionUpdate(Cbi.Execution execution, string executionId, double price, int quantity,
            Cbi.MarketPosition marketPosition, string orderId, DateTime time)
        {
            // if the long entry filled, place a profit target and stop loss to protect the order
            if (longOrder != null && execution.Order == longOrder && NewOCO == true && longOrder.OrderAction == OrderAction.Buy)
            {
                if (debugPrint) Print("Long order filled at " + longOrder.AverageFillPrice);

                double i = 1.01;
                while (stopPrice >= GetCurrentAsk())
                {
                    stopPrice /= i;
                    i *= i;
                }

                if (ScaleHalf)
                {
                    i = 1.01;
                    while (stopPriceHalf >= GetCurrentAsk())
                    {
                        stopPriceHalf /= i;
                        i *= i;
                    }
                    string temp = DateTime.Now.ToString("hhmmssffff");
                    ocoString = "LongOCO{1}" + " P: " + execution.OrderId + " T: " + temp;
                    limitOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, execution.Quantity / 2, limitPrice, 0, ocoString, longLimitName);
                    stopOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, execution.Quantity / 2, 0, stopPrice, ocoString, longStopName);
                    ocoStringHalf = "LongOCO{2}" + " P: " + execution.OrderId + " T: " + temp;
                    limitOrderHalf = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, execution.Quantity / 2, limitPriceHalf, 0, ocoStringHalf, longLimitNameHalf);
                    stopOrderHalf = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, execution.Quantity / 2, 0, stopPriceHalf, ocoStringHalf, longStopNameHalf);
                    if (debugPrint) Print("Limit order " + ocoString + " submitted at " + limitPrice);
                    if (debugPrint) Print("Stop order " + ocoString + " submitted at " + stopPrice);
                    if (debugPrint) Print("Limit order " + ocoStringHalf + " submitted at " + limitPriceHalf);
                    if (debugPrint) Print("Stop order " + ocoStringHalf + " submitted at " + stopPriceHalf);
                }
                else
                {
                    ocoString = "LongOCO{1}" + " P: " + execution.OrderId + " T: " + DateTime.Now.ToString("hhmmssffff");
                    limitOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, execution.Quantity, limitPrice, 0, ocoString, longLimitName);
                    stopOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, execution.Quantity, 0, stopPrice, ocoString, longStopName);
                    if (debugPrint) Print("Limit order " + ocoString + " submitted at " + limitPrice);
                    if (debugPrint) Print("Stop order " + ocoString + " submitted at " + stopPrice);
                }


            }

            // reverse the order types and prices for a short
            else if (shortOrder != null && execution.Order == shortOrder && NewOCO == true && shortOrder.OrderAction == OrderAction.SellShort)
            {
                if (debugPrint) Print("Short order filled at " + shortOrder.AverageFillPrice);


                double i = 1.01;
                while (stopPrice <= GetCurrentAsk())
                {
                    stopPrice *= i;
                    i *= i;
                }


                if (ScaleHalf)
                {
                    i = 1.01;
                    while (stopPriceHalf <= GetCurrentAsk())
                    {
                        stopPriceHalf *= i;
                        i *= i;
                    }
                    string temp = DateTime.Now.ToString("hhmmssffff");
                    ocoString = "ShortOCO{1}" + " P: " + execution.OrderId + " T: " + temp;
                    limitOrder = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, execution.Quantity / 2, limitPrice, 0, ocoString, shortLimitName);
                    stopOrder = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, execution.Quantity / 2, 0, stopPrice, ocoString, shortStopName);
                    ocoStringHalf = "ShortOCO{2}" + " P: " + execution.OrderId + " T: " + temp;
                    limitOrderHalf = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, execution.Quantity / 2, limitPriceHalf, 0, ocoStringHalf, shortLimitNameHalf);
                    stopOrderHalf = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, execution.Quantity / 2, 0, stopPriceHalf, ocoStringHalf, shortStopNameHalf);
                    if (debugPrint) Print("Limit order " + ocoString + " submitted at " + limitPrice);
                    if (debugPrint) Print("Stop order " + ocoString + " submitted at " + stopPrice);
                    if (debugPrint) Print("Limit order " + ocoStringHalf + " submitted at " + limitPriceHalf);
                    if (debugPrint) Print("Stop order " + ocoStringHalf + " submitted at " + stopPriceHalf);
                }
                else
                {
                    ocoString = "ShortOCO{1}" + " P: " + execution.OrderId + " T: " + DateTime.Now.ToString("hhmmssffff");
                    limitOrder = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, execution.Quantity, limitPrice, 0, ocoString, shortLimitName);
                    stopOrder = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, execution.Quantity, 0, stopPrice, ocoString, shortStopName);
                    if (debugPrint) Print("Limit order " + ocoString + " submitted at " + limitPrice);
                    if (debugPrint) Print("Stop order " + ocoString + " submitted at " + stopPrice);
                }


            }



            // when the long profit or stop fills, set the long entry to null to allow a new entry and reset everything
            else if (limitOrder != null && execution.Name == longLimitName)
            {
                if (debugPrint) Print("Long limit order " + limitOrder.Oco + " executed at " + execution.Price);
                limitOrder = null;
                count++;
            }
            else if (stopOrder != null && execution.Name == longStopName)
            {
                if (debugPrint) Print("Long stop order " + stopOrder.Oco + " executed at " + execution.Price);
                stopOrder = null;
                count++;
            }
            else if (limitOrder != null && execution.Name == shortLimitName)
            {
                if (debugPrint) Print("Short limit order " + limitOrder.Oco + " executed at " + execution.Price);
                limitOrder = null;
                count++;
            }
            else if (stopOrder != null && execution.Name == shortStopName)
            {
                if (debugPrint) Print("Short stop order " + stopOrder.Oco + " executed at " + execution.Price);
                stopOrder = null;
                count++;
            }

            // Halves
            else if (limitOrderHalf != null && execution.Name == longLimitNameHalf)
            {
                if (debugPrint) Print("Long limit order " + limitOrderHalf.Oco + " executed at " + execution.Price);
                limitOrderHalf = null;
                count++;
            }
            else if (stopOrderHalf != null && execution.Name == longStopNameHalf)
            {
                if (debugPrint) Print("Long stop order " + stopOrderHalf.Oco + " executed at " + execution.Price);
                stopOrderHalf = null;
                count++;
            }
            else if (limitOrderHalf != null && execution.Name == shortLimitNameHalf)
            {
                if (debugPrint) Print("Short limit order " + limitOrderHalf.Oco + " executed at " + execution.Price);
                limitOrderHalf = null;
                count++;
            }
            else if (stopOrderHalf != null && execution.Name == shortStopNameHalf)
            {
                if (debugPrint) Print("Short stop order " + stopOrderHalf.Oco + " executed at " + execution.Price);
                stopOrderHalf = null;
                count++;
            }

            if (ScaleHalf && count == 2)
            {
                currentOrderClassification = "";
                hasLongRSIOrderBeenAdjusted = false;
                hasGenericProtectionOrderBeenAdjusted = false;
                hasPriorityOrderBeenAdjusted = false;
                useSmaLineAsLongStopLoss = false;
                count = 0;
                if (GoLongAfterExit)
                {
                    GoLongZone();
                    GoLongAfterExit = false;
                }
                else if (GoShortAfterExit)
                {
                    GoShortZone();
                    GoShortAfterExit = false;
                }
            }
            else if (ScaleHalf == false && count == 1)
            {
                currentOrderClassification = "";
                hasLongRSIOrderBeenAdjusted = false;
                hasGenericProtectionOrderBeenAdjusted = false;
                hasPriorityOrderBeenAdjusted = false;
                useSmaLineAsLongStopLoss = false;
                count = 0;
                if (GoLongAfterExit)
                {
                    GoLongZone();
                    GoLongAfterExit = false;
                }
                else if (GoShortAfterExit)
                {
                    GoShortZone();
                    GoShortAfterExit = false;
                }
            }



        }

        protected override void OnOrderUpdate(Cbi.Order order, double limitPrice, double stopPrice,
            int quantity, int filled, double averageFillPrice,
            Cbi.OrderState orderState, DateTime time, Cbi.ErrorCode error, string comment)
        {

            if (stopOrder != null && order == stopOrder && stopOrder.OrderState == OrderState.Cancelled)
            {
                if (debugPrint) Print("Stop " + stopOrder.Oco + " cancelled");
                stopOrder = null;
            }

            if (limitOrder != null && order == limitOrder && limitOrder.OrderState == OrderState.Cancelled)
            {
                if (debugPrint) Print("Limit " + limitOrder.Oco + " cancelled");
                limitOrder = null;
            }

            if (stopOrder != null && order == stopOrder && stopOrder.OrderState == OrderState.Accepted)
            {
                if (debugPrint) Print("Stop " + stopOrder.Oco + " set " + stopPrice);

            }
            if (limitOrder != null && order == limitOrder && limitOrder.OrderState == OrderState.Accepted)
            {
                if (debugPrint) Print("Limit " + limitOrder.Oco + " set " + limitPrice);
            }

            if (stopOrder != null && order == stopOrder && stopOrder.OrderState == OrderState.ChangePending)
            {
                if (debugPrint) Print("Stop change" + stopOrder.Oco + " submitted " + stopPrice);
            }

            if (limitOrder != null && order == limitOrder && limitOrder.OrderState == OrderState.ChangePending)
            {
                if (debugPrint) Print("Limit change " + limitOrder.Oco + " submitted " + limitPrice);
            }
            // Halves

            if (stopOrderHalf != null && order == stopOrderHalf && stopOrderHalf.OrderState == OrderState.Cancelled)
            {
                if (debugPrint) Print("Stop " + stopOrderHalf.Oco + " cancelled");
                stopOrderHalf = null;
            }

            if (limitOrderHalf != null && order == limitOrderHalf && limitOrderHalf.OrderState == OrderState.Cancelled)
            {
                if (debugPrint) Print("Limit " + limitOrderHalf.Oco + " cancelled");
                limitOrderHalf = null;
            }

            if (stopOrderHalf != null && order == stopOrderHalf && stopOrderHalf.OrderState == OrderState.Accepted)
            {
                if (debugPrint) Print("Stop " + stopOrderHalf.Oco + " set " + stopPriceHalf);

            }
            if (limitOrderHalf != null && order == limitOrderHalf && limitOrderHalf.OrderState == OrderState.Accepted)
            {
                if (debugPrint) Print("Limit " + limitOrderHalf.Oco + " set " + limitPriceHalf);
            }

            if (stopOrderHalf != null && order == stopOrderHalf && stopOrderHalf.OrderState == OrderState.ChangePending)
            {
                if (debugPrint) Print("Stop change" + stopOrderHalf.Oco + " submitted " + stopPriceHalf);
            }

            if (limitOrderHalf != null && order == limitOrderHalf && limitOrderHalf.OrderState == OrderState.ChangePending)
            {
                if (debugPrint) Print("Limit change " + limitOrderHalf.Oco + " submitted " + limitPriceHalf);
            }


            // when both orders are cancelled set to null for a new entry
            if ((longOrder != null && longOrder.OrderState == OrderState.Cancelled && shortOrder != null && shortOrder.OrderState == OrderState.Cancelled))
            {
                longOrder = null;
                shortOrder = null;
            }
        }

        public void CancelAndFlattenAll()
        {
            // SOMETHING TO REMEMBER: IF THESE DON'T FILL AT EOD FOR SOME REASON, WE HAVE A PROBLEM!!!
            if (Position.MarketPosition == MarketPosition.Flat) return;
            if (ScaleHalf)
            {
                if (limitOrder != null)
                {
                    ChangeOrder(limitOrder, limitOrder.Quantity, Close[0], 0);
                }
                if (limitOrderHalf != null)
                {
                    ChangeOrder(limitOrderHalf, limitOrderHalf.Quantity, Close[0], 0);
                }
            }
            else
            {
                if (limitOrder != null)
                {
                    ChangeOrder(limitOrder, limitOrder.Quantity, Close[0], 0);
                }
            }
            cfa = true;

        }

        public void GoShortZone()
        {

            stopPrice = Close[0] + (Close[0] * (ShortStopLossPercent / 100) * orderMultiplier);
            limitPrice = Close[0] - (Close[0] * (ShortProfitPercent / 100) * orderMultiplier);
            if (ScaleHalf)
            {
                stopPriceHalf = Close[0] + (Close[0] * (ShortStopLossPercent / 200) * orderMultiplier);
                limitPriceHalf = Close[0] - (Close[0] * (ShortProfitPercent / 200) * orderMultiplier);
            }
            if (stopPrice <= limitPrice || stopPrice <= Close[0])
            {
                if (debugPrint) Print("Short side OCO prices are conflicting, order not submitted");
            }
            else
            {
                orderZone = ASRZ.GetZone(Close[0]);
                NewOCO = true;
                if ((sessEnd - Time[0]).TotalSeconds > sessionEndSeconds) shortOrder = SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Market, shareQuantity, 0, Close[0], ocoString, shortName);
                currentOrderClassification = "Short ZONE";
            }
        }

        public void GoLongZone()
        {
            stopPrice = Close[0] - (Close[0] * (LongStopLossPercent / 100) * orderMultiplier);
            limitPrice = Close[0] + (Close[0] * (LongProfitPercent / 100) * orderMultiplier);
            if (ScaleHalf)
            {
                stopPriceHalf = Close[0] - (Close[0] * (LongStopLossPercent / 200) * orderMultiplier);
                limitPriceHalf = Close[0] + (Close[0] * (LongProfitPercent / 200) * orderMultiplier);
            }
            if (stopPrice >= limitPrice || stopPrice >= Close[0])
            {
                if (debugPrint) Print("Long side OCO prices are conflicting, order not submitted");
            }
            else
            {
                orderZone = ASRZ.GetZone(Close[0]);
                NewOCO = true;
                if ((sessEnd - Time[0]).TotalSeconds > sessionEndSeconds) longOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, shareQuantity, 0, Close[0], ocoString, longName);
                currentOrderClassification = "Long ZONE";
            }

        }


        public void TryShortSMACross()
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                shareQuantity = GetShareQuantity();
                stopPrice = Close[0] + (Close[0] * (0.095 / 100) * orderMultiplier);
                limitPrice = Close[0] - (Close[0] * (0.24 / 100) * orderMultiplier);
                if (ScaleHalf)
                {
                    stopPriceHalf = Close[0] + (Close[0] * (0.06 / 100) * orderMultiplier);
                    limitPriceHalf = Close[0] - (Close[0] * (0.085 / 100) * orderMultiplier);
                }
                if (stopPrice <= limitPrice || stopPrice <= Close[0] || stopPriceHalf <= limitPriceHalf || stopPriceHalf <= Close[0])
                {
                    if (debugPrint) Print("Short SMA cross OCO prices are conflicting, order not submitted");
                }
                else
                {
                    if (debugPrint) Print(limitPrice + " " + limitPriceHalf + " " + stopPrice + " " + stopPriceHalf + " " + Close[0]);
                    orderZone = ASRZ.GetZone(Close[0]);
                    NewOCO = true;
                    //useSmaLineAsLongStopLoss = true;
                    if ((sessEnd - Time[0]).TotalSeconds > sessionEndSeconds) shortOrder = SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Market, shareQuantity, 0, Close[0], ocoString, shortName);
                    currentOrderClassification = "Short SMA Cross";
                }
            }

        }
        public void TryLongSMACross()
        {
            
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                shareQuantity = GetShareQuantity();
                
                stopPrice = Close[0] - (Close[0] * (0.09 / 100) * orderMultiplier);
                limitPrice = Close[0] + (Close[0] * (0.1 / 100) * orderMultiplier);
                if (ScaleHalf)
                {
                    stopPriceHalf = Close[0] - (Close[0] * (0.045 / 100) * orderMultiplier);
                    limitPriceHalf = Close[0] + (Close[0] * (0.09 / 100) * orderMultiplier);
                }
                if (stopPrice >= limitPrice || stopPrice >= Close[0] || stopPriceHalf >= limitPriceHalf || stopPriceHalf >= Close[0])
                {
                    if (debugPrint) Print("Long SMA cross OCO prices are conflicting, order not submitted");
                }
                else
                {
                    if (debugPrint) Print(limitPrice + " " + limitPriceHalf + " " + stopPrice + " " + stopPriceHalf + " " + Close[0]);
                    orderZone = ASRZ.GetZone(Close[0]);
                    NewOCO = true;
                    useSmaLineAsLongStopLoss = true;
                    if ((sessEnd - Time[0]).TotalSeconds > sessionEndSeconds) longOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, shareQuantity, 0, Close[0], ocoString, longName);
                    currentOrderClassification = "Long SMA Cross";
                }
            }
            
        }


        public void TryOversoldRSIEntry30m()
        {
            if (CurrentBars[1] > rSI30m.Period - 1)
            {
                // TODO : oversold RSI profit and stop should be more bullish

                if (rSI30m[0] < 26 && Position.MarketPosition == MarketPosition.Flat)
                {
                    shareQuantity = GetShareQuantity();
                    double tempRVol = 0;
                    int rsiLookBackTime = 30;
                    if (CurrentBar > rsiLookBackTime)
                    {
                        for (int i = 0; i < rsiLookBackTime; i++)
                        {
                            tempRVol += RVOL[i];
                        }
                    }
                    stopPrice = Close[0] - (Close[0] * longRSIStopPercent);
                    limitPrice = Close[0] + (Close[0] * longRSILimitPercent);
                    if (ScaleHalf)
                    {
                        stopPriceHalf = Close[0] - (Close[0] * longRSIStopPercent / 2);
                        limitPriceHalf = Close[0] + (Close[0] * longRSILimitPercent / 2);
                    }
                    NewOCO = true;
                    if ((sessEnd - Time[0]).TotalSeconds > sessionEndSeconds) longOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, shareQuantity, 0, Close[0], ocoString, "Long");
                    currentOrderClassification = "Long RSI";
                }
                // not shorting overbought RSI because the stocks are generally BULLISH and we'd die.
            }
        }

        public void Try10mRSIProfitProtection()
        {
            // If the 10m RSI is oversold 
            if (CurrentBars[3] > rSI10m.Period - 1)
            {
                
                if (longOrder != null && currentOrderClassification == "Long ZONE" && limitOrder != null && stopOrder != null && hasPriorityOrderBeenAdjusted == false)
                {
                    if (rSI10m[0] > overBoughtRsiPercentAdjustmentThreshold && Close[0] > Open[0])
                    {
                        if (ScaleHalf)
                        {
                            if (stopOrderHalf != null)
                            {
                                stopPriceHalf = (Open[0] - Open[0] * overBoughtRsiHighPriorityStopPercent); // overbought RSI protection high
                                ChangeOrder(stopOrderHalf, longOrder.Quantity / 2, 0, stopPriceHalf);
                            }
                            stopPrice = Open[0] - Open[0] * overBoughtRsiLowPriorityStopPercent; // overbought RSI protection low
                            ChangeOrder(stopOrder, longOrder.Quantity / 2, 0, stopPrice);
                        }
                        else
                        {
                            stopPrice = Open[0] - Open[0] * overBoughtRsiGenericStopPercent; // overbought RSI protection generic
                            ChangeOrder(stopOrder, longOrder.Quantity, 0, stopPrice);
                        }
                        if (debugPrint) Print("Adjusting long OCO, 10m RSI overbought protection");
                        hasPriorityOrderBeenAdjusted = true;
                    }
                }
                

                if (shortOrder != null && currentOrderClassification == "Short ZONE" && limitOrder != null && stopOrder != null && hasPriorityOrderBeenAdjusted == false)
                {
                    if (rSI10m[0] < overSoldRsiPercentAdjustmentThreshold && Close[0] < Open[0])
                    {
                        if (ScaleHalf)
                        {
                            if (stopOrderHalf != null)
                            {
                                stopPriceHalf = (Open[0] + Open[0] * overBoughtRsiHighPriorityStopPercent);
                                ChangeOrder(stopOrderHalf, shortOrder.Quantity / 2, 0, stopPriceHalf);
                            }
                            stopPrice = Open[0] + Open[0] * overBoughtRsiLowPriorityStopPercent;
                            ChangeOrder(stopOrder, shortOrder.Quantity / 2, 0, stopPrice);
                        }
                        else
                        {
                            stopPrice = Open[0] + Open[0] * overBoughtRsiGenericStopPercent;
                            ChangeOrder(stopOrder, shortOrder.Quantity, 0, stopPrice);
                        }
                        if (debugPrint) Print("Adjusting short OCO, 10m RSI oversold protection");
                        hasPriorityOrderBeenAdjusted = true;
                    }
                }
            }
        }

        public void TryZoneOrders()
        {
            if (UseZoneStrengthOrderMultiplier)
            {
                orderMultiplier = Math.Floor(ZoneStrengthOrderScale * Math.Log(ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1 - TradeDelay].Strength + 1)) / 100;
            }
            //Print(Position.Quantity + " " + Position.MarketPosition);
            double LST = LongStrengthThreshold + (LongStrengthThreshold * (-PriceAdjustedDailySMASlope));
            double STT = ShortStrengthThreshold + (ShortStrengthThreshold * (-PriceAdjustedDailySMASlope));

            //Print(LST + " L | S " + STT);
            if (ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1 - TradeDelay].Strength > LST && ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1 - TradeDelay].Direction < 0 && ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1 - TradeDelay].Inflection == "U")
            {
                //Print((Bars.GetTime(CurrentBar) - Bars.GetTime(CurrentBar - BarsSinceEntryExecution())).Minutes);
                if (Position.MarketPosition == MarketPosition.Short && currentOrderClassification == "Short ZONE" && (Bars.GetTime(CurrentBar) - Bars.GetTime(CurrentBar - BarsSinceEntryExecution(0, "", 0))).Minutes >= DelayExitMinutes)
                {

                    // Cancel the short stop and short limit orders so we can exit the short order
                    //CancelOCO();
                    NewOCO = false;
                    //shortOrder = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, shortOrder.Quantity, 0, Close[0], ocoString, "Short Exit");
                    GoLongAfterExit = true;
                    if (ScaleHalf)
                    {
                        if (limitOrder != null)
                            ChangeOrder(limitOrder, limitOrder.Quantity, Close[0], 0);
                        if (limitOrderHalf != null)
                            ChangeOrder(limitOrderHalf, limitOrderHalf.Quantity, Close[0], 0);
                    }
                    else
                    {
                        ChangeOrder(limitOrder, shortOrder.Quantity, Close[0], 0);
                    }

                    // Make request to exit 
                }
                // 75% -> 25% entry strategy
                else if (Position.MarketPosition == MarketPosition.Flat)
                {
                    shareQuantity = GetShareQuantity();
                    GoLongZone();
                }

            }
            else if (ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1 - TradeDelay].Strength > STT && ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1 - TradeDelay].Direction > 0 && ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1 - TradeDelay].Inflection == "D")
            {
                //Print((Bars.GetTime(CurrentBar) - Bars.GetTime(CurrentBar - BarsSinceEntryExecution())).Minutes);
                //ExitLong();
                if (Position.MarketPosition == MarketPosition.Long && currentOrderClassification == "Long ZONE" && (Bars.GetTime(CurrentBar) - Bars.GetTime(CurrentBar - BarsSinceEntryExecution(0, "", 0))).Minutes >= DelayExitMinutes)
                {
                    //Print("Exit long");
                    //CancelOCO();
                    NewOCO = false;
                    //longOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, longOrder.Quantity, 0, Close[0], ocoString, "Long Exit");
                    GoShortAfterExit = true;
                    if (ScaleHalf)
                    {
                        if (limitOrder != null)
                            ChangeOrder(limitOrder, limitOrder.Quantity, Close[0], 0);
                        if (limitOrderHalf != null)
                            ChangeOrder(limitOrderHalf, limitOrderHalf.Quantity, Close[0], 0);
                    }
                    else
                    {
                        ChangeOrder(limitOrder, longOrder.Quantity, Close[0], 0);
                    }
                }
                else if (Position.MarketPosition == MarketPosition.Flat)
                {
                    shareQuantity = GetShareQuantity();
                    GoShortZone();
                }
                else
                {
                    //Print("A position is already open");
                }
            }
        }


        public void TryUseSMAAsStopLoss(int period, int halfPeriod, string orderType="Long")
        {
            if (useSmaLineAsLongStopLoss && CurrentBar > sma20m.Period - 1 && CurrentBar > sma50m.Period - 1 && CurrentBar > sma5m.Period - 1 && CurrentBar > sma6m.Period - 1)
            {

                Order refOrder = longOrder;
                if (orderType=="Short") refOrder = shortOrder;
                if (refOrder == null) return;
                switch (period)
                {
                    case 5:
                        stopPrice = sma5m[0];
                        break;

                    case 6:
                        stopPrice = sma6m[0];
                        break;

                    case 20:
                        stopPrice = sma20m[0];
                        break;

                    case 50:
                        stopPrice = sma50m[0];
                        break;

                    default:
                        stopPrice = sma20m[0];
                        break;
                }
                if (stopPrice >= Close[0])
                {
                    stopPrice = (Close[0] + refOrder.AverageFillPrice) / 2;
                }
                if (ScaleHalf)
                {
                    switch (halfPeriod)
                    {
                        case 5:
                            stopPriceHalf = sma5m[0];
                            break;

                        case 6:
                            stopPriceHalf = sma6m[0];
                            break;

                        case 20:
                            stopPriceHalf = sma20m[0];
                            break;

                        case 50:
                            stopPriceHalf = sma50m[0];
                            break;

                        default:
                            stopPriceHalf = sma20m[0];
                            break;
                    }
                    if (stopPriceHalf >= Close[0])
                    {
                        stopPriceHalf = (Close[0] + refOrder.AverageFillPrice) / 2;
                    }
                    ChangeOrder(stopOrderHalf, refOrder.Quantity / 2, 0, stopPriceHalf);
                    ChangeOrder(stopOrder, refOrder.Quantity / 2, 0, stopPrice);
                }
                else
                {
                    ChangeOrder(stopOrder, refOrder.Quantity, 0, stopPrice);
                }
                //ChangeOrder(stopOrder, longOrder.Quantity, 0, stopPrice);
            }
        }

        public void TryOCOProfitProtection()
        {
            if (longOrder != null && currentOrderClassification == "Long RSI" && limitOrder != null && stopOrder != null && hasLongRSIOrderBeenAdjusted == false)
            {
                if (Close[0] > ((longOrder.AverageFillPrice + (longOrder.AverageFillPrice * longRSILimitPercent)) + longOrder.AverageFillPrice) / 2)
                {
                    if (ScaleHalf)
                    {
                        if (stopOrderHalf != null)
                        {
                            stopPriceHalf = (longOrder.AverageFillPrice);
                            ChangeOrder(stopOrderHalf, longOrder.Quantity / 2, 0, stopPriceHalf);
                        }
                        stopPrice = (Close[0] + longOrder.AverageFillPrice) / 2;
                        ChangeOrder(stopOrder, longOrder.Quantity / 2, 0, stopPrice);
                    }
                    else
                    {
                        stopPrice = (Close[0] + longOrder.AverageFillPrice) / 2;
                        ChangeOrder(stopOrder, longOrder.Quantity, 0, stopPrice);
                    }
                    if (debugPrint) Print("Adjusting long RSI OCO");
                    hasLongRSIOrderBeenAdjusted = true;
                }
            }

            if (longOrder != null && currentOrderClassification == "Long ZONE" && limitOrder != null && stopOrder != null && useSmaLineAsLongStopLoss == false && CurrentBar > sma20m.Period - 1 && CurrentBar > sma50m.Period - 1)
            {
                if (Close[0] > longOrder.AverageFillPrice + longOrder.AverageFillPrice * genericLongOrderProtectionPercentThreshold)
                {
                    useSmaLineAsLongStopLoss = true;
                    TryUseSMAAsStopLoss(20, 50);
                    if (debugPrint) Print("Adjusting long overbought protection SMA20 OCO");
                }
            }
        }

        public void UpdateDayEvents()
        {
            // Session iterator stuff
            if (IsFirstTickOfBar && Bars.IsFirstBarOfSession)
            {

                if (sessIter != null && sessIter.GetNextSession(Time[0], true))
                {
                    sessBegin = sessIter.ActualSessionBegin;
                    sessEnd = sessIter.ActualSessionEnd;
                    if (debugPrint) Print("Session begin: " + sessBegin + " end: " + sessEnd);
                    cfa = false;
                }
                IsGap[0] = IsGap[1] = IsGap[2] = 0;
                if (Low[0] > High[1])
                {
                    Draw.Rectangle(this, "Gap up " + CurrentBar, 0, Low[0], 1, High[1], Brushes.SpringGreen);
                    IsGap[0] = 1;
                    IsGap[1] = High[1];
                    IsGap[2] = Low[0];
                }

                else if (High[0] < Low[1])
                {
                    Draw.Rectangle(this, "Gap down" + CurrentBar, 0, High[0], 1, Low[1], Brushes.IndianRed);
                    IsGap[0] = 2;
                    IsGap[1] = Low[1];
                    IsGap[2] = High[0];
                }
            }
        }



        protected override void OnBarUpdate()
        {

            RVOL.Update();
            ASRZ.Update();
            rSI1m.Update();
            LRS.Update();


            if (CurrentBars[0] < BarsRequiredToTrade)
                return;






            if (BarsInProgress == 3)
            {

                rSI10m.Update();
                // If the 10m RSI is oversold 
                Try10mRSIProfitProtection();
            }



            if (BarsInProgress == 2)
            {
                if (CurrentBars[2] > LRSDaily.Period - 1)
                {
                    LRSDaily.Update();
                    PriceAdjustedDailySMASlope = (LRSDaily[0] / Close[0]) * 10;
                    //Print(PriceAdjustedDailySMASlope); // price-adjusted slope
                }
            }

            if (BarsInProgress == 1)
            {



                rSI30m.Update();
                TryOversoldRSIEntry30m();

            }


            if (BarsInProgress == 0)
            {

                // Update gaps, current session start, etc
                UpdateDayEvents();

                // Cancel and flatten orders at the end of day
                if ((sessEnd - Time[0]).TotalSeconds <= sessionEndSeconds && Position.MarketPosition != MarketPosition.Flat && cfa == false)
                {
                    if (debugPrint) Print((sessEnd - Time[0]).TotalSeconds + " until session end.");
                    CancelAndFlattenAll();
                }

                if (CurrentBar >= 0)
                {


                    if (ASRZ.LINKLIST.Count > 0 + TradeDelay)
                    {

                        // If the order was designated to use an SMA as a stoploss, this function updates the order

                        
                        if (currentOrderClassification == "Long SMA Cross") TryUseSMAAsStopLoss(6, 6);
                        else if (currentOrderClassification == "Short SMA Cross") TryUseSMAAsStopLoss(6, 6, "Short");
                        else TryUseSMAAsStopLoss(20, 50);
                        

                        // Generic order OCO exit handling to make sure we keep some profits on the table
                        // Checking longs and shorts if they need to be exited
                        // Manage open RSI orders
                        // If RSI limit order goes above certain percent, raise the stop to breakeven
                        TryOCOProfitProtection();

                        /*
						// Generic profit protection order, lowest priority order adjustment
						if (longOrder != null && currentOrderClassification == "Long ZONE" && limitOrder != null && stopOrder != null && hasGenericProtectionOrderBeenAdjusted == false)
						{
							if (Close[0] > longOrder.AverageFillPrice + longOrder.AverageFillPrice * genericLongOrderProtectionPercentThreshold)
							{

								if (ScaleHalf)
								{
									if (stopOrderHalf != null)
									{
										stopPriceHalf = (longOrder.AverageFillPrice);
										ChangeOrder(stopOrderHalf, longOrder.Quantity / 2, 0, stopPriceHalf);
									}
									stopPrice = (Close[0] + longOrder.AverageFillPrice) / 2;
									ChangeOrder(stopOrder, longOrder.Quantity / 2, 0, stopPrice);
								}
								else
								{
									stopPrice = (Close[0] + longOrder.AverageFillPrice) / 2;
									ChangeOrder(stopOrder, longOrder.Quantity, 0, stopPrice);
								}
								if (debugPrint) Print("Adjusting long overbought protection RSI OCO");
								hasGenericProtectionOrderBeenAdjusted = true;
							}

						}
						*/
                        /*
						// Short side
						if (shortOrder != null && currentOrderClassification == "Short ZONE" && limitOrder != null && stopOrder != null && hasGenericProtectionOrderBeenAdjusted == false)
						{
							if (Close[0] < shortOrder.AverageFillPrice - shortOrder.AverageFillPrice * genericShortOrderProtectionPercentThreshold)
							{
								if (ScaleHalf)
								{
									if (stopOrderHalf !=null)
									{
										stopPriceHalf = (shortOrder.AverageFillPrice);
										ChangeOrder(stopOrderHalf, shortOrder.Quantity / 2, 0, stopPriceHalf);
									}
									stopPrice = (Close[0] - shortOrder.AverageFillPrice) / 2;
									ChangeOrder(stopOrder, shortOrder.Quantity / 2, 0, stopPrice);
								}
								else
								{
									stopPrice = (Close[0] - shortOrder.AverageFillPrice) / 2;
									ChangeOrder(stopOrder, shortOrder.Quantity, 0, stopPrice);
								}
								if (debugPrint) Print("Adjusting short oversold protection RSI OCO");
								hasGenericProtectionOrderBeenAdjusted = true;
							}

						}
						*/





                        /*
						else if (longOrder != null && currentOrderClassification == "Long ZONE" && limitOrder != null && stopOrder != null && hasOrderBeenAdjusted == false)
						{
							//Print("a");
							if (Close[0] > ((longOrder.AverageFillPrice + (longOrder.AverageFillPrice * LongProfitPercent / 100)) + longOrder.AverageFillPrice) / 2)
							{
								//Print("b");
								ChangeOrder(stopOrder, longOrder.Quantity, 0, (Close[0] + longOrder.AverageFillPrice) / 2);
								hasOrderBeenAdjusted = true;
							}
							else if (Close[0] < orderZone.ZoneBottomY)
							{
								ChangeOrder(limitOrder, longOrder.Quantity, orderZone.ZoneBottomY, 0);
								hasOrderBeenAdjusted = true;
							}

						}

						else if (shortOrder != null && currentOrderClassification == "Short ZONE" && limitOrder != null && stopOrder != null && hasOrderBeenAdjusted == false)
						{
							//Print("a");
							if (Close[0] < ((shortOrder.AverageFillPrice - (shortOrder.AverageFillPrice * ShortProfitPercent / 100)) + shortOrder.AverageFillPrice) / 2)
							{
								//Print("b");
								ChangeOrder(stopOrder, shortOrder.Quantity, 0, (Close[0] + shortOrder.AverageFillPrice) / 2);
								hasOrderBeenAdjusted = true;
							}
							else if (Close[0] > orderZone.ZoneTopY)
							{
								ChangeOrder(limitOrder, shortOrder.Quantity, orderZone.ZoneTopY, 0);
								hasOrderBeenAdjusted = true;
							}
						}
						*/

                        //Print(ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1].NumFullTradingDays);
                        //Print("Zones above: " + ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1].ZonesAbove + " Zones below: " + ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1].ZonesBelow);
                        //Print(ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1].Direction);
                        //Print(IsLong);
                        //Print(ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1].Strength + " " + ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1].Direction);

                        //Print(PositionCount);
                        //if (Position.MarketPosition == MarketPosition.Long && Position.Quantity < EntriesPerDirection &&   ( Bars.GetClose(CurrentBar) >= )   ) EnterLong(shareQuantity*(1/4), "Support entry")




                        if (CurrentBar > sma20m.Period - 1 && CurrentBar > sma50m.Period - 1 && CurrentBar > sma5m.Period - 1 && CurrentBar > sma6m.Period)
                        {
                            if (sma20m[1] > sma50m[1] && sma20m[0] < sma50m[0] && rSI1m[0] > 30)
                            {
                                TryShortSMACross();
                            }
                            else if (sma5m[1] < sma20m[1] && sma5m[0] > sma20m[0] && rSI1m[0] < 85)
                            {
                                TryLongSMACross();
                            }
                        }

                        TryZoneOrders();

                        /*
						if (ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1].NumFullTradingDays > 30 && ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1].ZonesAbove == 0 && LRS[0] > 0 && Position.MarketPosition == MarketPosition.Flat)
						{
							shareQuantity = GetShareQuantity();
							GoLong();
						}
						else if (ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1].NumFullTradingDays > 30 && ASRZ.LINKLIST[ASRZ.LINKLIST.Count - 1].ZonesBelow == 0 && LRS[0] < 0 && Position.MarketPosition == MarketPosition.Flat)
						{
							shareQuantity = GetShareQuantity();
							GoShort();
						}
						*/
                    }
                }

            }

        }

        #region properties
        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "Area strength threshold multiplier", Description = "Multiplies the minimum threshold required for zones to gain strength", Order = 1, GroupName = "Parameters")]
        public int AreaStrengthMultiplier
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Time Threshold", Description = "Amount of time in minutes the bot uses as a limit for adjusting integral zone strength", Order = 2, GroupName = "Parameters")]
        public int TimeThreshold
        { get; set; }

        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "Proxy strength multiplier", Description = "Proximity of pivots in Ticks to determine where S/R level will be", Order = 3, GroupName = "Parameters")]
        public int ProxyStrengthMultiplier
        { get; set; }

        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "New zone strength", Description = "Default pre-computed strength of a new zone. Equation is 5 * Sqrt ( input ) ", Order = 4, GroupName = "Parameters")]
        public int NewZoneStrength
        { get; set; }

        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "Zone timeout strength", Description = "Pre-computed value added to a zone that fails to go above the strength threshold. Equation is - 5 * Sqrt ( (abs)input ) ", Order = 5, GroupName = "Parameters")]
        public int ZoneTimeoutStrength
        { get; set; }

        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "New zone top multiplier", Description = "Multiplier amount to extend the top of a new zone upon creation", Order = 6, GroupName = "Parameters")]
        public double NewZoneTopMultiplier
        { get; set; }


        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "New zone bottom multiplier", Description = "Multiplier amount to extend the bottom of a new zone upon creation", Order = 7, GroupName = "Parameters")]
        public double NewZoneBottomMultiplier
        { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Resistance Zone Color", Order = 8, GroupName = "Parameters")]
        public Brush ResZoneColor
        { get; set; }
        [Browsable(false)]
        public string ResColorSerializable
        {
            get { return Serialize.BrushToString(ResZoneColor); }
            set { ResZoneColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Support Zone Color", Order = 9, GroupName = "Parameters")]
        public Brush SupZoneColor
        { get; set; }

        [Browsable(false)]
        public string SupColorSerializable
        {
            get { return Serialize.BrushToString(SupZoneColor); }
            set { SupZoneColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "$$$ amount to buy", Description = "Amount in $$$ to buy for ONE full order", Order = 10, GroupName = "Parameters")]
        public double AmountToBuy
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Long zone strength", Description = "The cumulative strength of the current bar a zone is in that triggers a long order", Order = 11, GroupName = "Parameters")]
        public double LongStrengthThreshold
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Short zone strength", Description = "The cumulative strength of the current bar a zone is in that triggers a short order", Order = 12, GroupName = "Parameters")]
        public double ShortStrengthThreshold
        { get; set; }


        [NinjaScriptProperty]
        [Display(Name = "Zone strength order multiplier", Description = "Adjusts stop loss and profit taking based on percieved zone strength upon entry condition. ", Order = 13, GroupName = "Parameters")]
        public bool UseZoneStrengthOrderMultiplier
        { get; set; }

        [NinjaScriptProperty]
        [Range(50, 200)]
        [Display(Name = "Zone strength order scale", Description = "Adjusts the zone scale equation. Higher = wider stop losses and limit sells", Order = 14, GroupName = "Parameters")]
        public int ZoneStrengthOrderScale
        { get; set; }

        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "Break strength multiplier", Description = "Multiplier for threshold strength applied if a zone is broken", Order = 15, GroupName = "Parameters")]
        public double BreakStrengthMultiplier
        { get; set; }


        [NinjaScriptProperty]
        [Display(Name = "Use volume accumulation", Description = "(Suggest TRUE) Factors in relative volume into zone strength calculation", Order = 16, GroupName = "Parameters")]
        public bool UseVolAccumulation
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Short stop loss %", Description = "Short stop loss %", Order = 17, GroupName = "Parameters")]
        public double ShortStopLossPercent
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Long stop loss %", Description = "Long stop loss %", Order = 18, GroupName = "Parameters")]
        public double LongStopLossPercent
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Short profit target %", Description = "Short profit target %", Order = 19, GroupName = "Parameters")]
        public double ShortProfitPercent
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Long profit target %", Description = "Long profit target %", Order = 20, GroupName = "Parameters")]
        public double LongProfitPercent
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Trade delay", Description = "Delay each trade condition by X minutes", Order = 21, GroupName = "Parameters")]
        public int TradeDelay
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Delay exit (minutes)", Description = "Prevents rapid, unnecessary orders from firing in a tight area of zones", Order = 22, GroupName = "Parameters")]
        public int DelayExitMinutes
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Zone expiration", Description = "Number of days it takes for a bar to expire. -1 means they never expire", Order = 23, GroupName = "Parameters")]
        public int Expiration
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Max merge count", Description = "Maximum number of times a zone can be merged", Order = 24, GroupName = "Parameters")]
        public int MaxMergeCount
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Merge threshold", Description = "Merge threshold required for two zones to combine", Order = 25, GroupName = "Parameters")]
        public double MergeThreshold
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Scale half", Description = "Scale out orders half at a time", Order = 26, GroupName = "Parameters")]
        public bool ScaleHalf
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "% acc to use for pos", Description = "% acc to use for pos", Order = 27, GroupName = "Parameters")]
        public double PercentOfAccForPosition
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "overBoughtRsiHighPriorityStopPercent", Description = "overBoughtRsiHighPriorityStopPercent", Order = 28, GroupName = "Parameters")]
        public double overBoughtRsiHighPriorityStopPercent
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "overBoughtRsiLowPriorityStopPercent", Description = "overBoughtRsiLowPriorityStopPercent", Order = 29, GroupName = "Parameters")]
        public double overBoughtRsiLowPriorityStopPercent
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "overBoughtRsiGenericStopPercent", Description = "overBoughtRsiGenericStopPercent", Order = 30, GroupName = "Parameters")]
        public double overBoughtRsiGenericStopPercent
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "genericLongOrderProtectionPercentThreshold", Description = "genericLongOrderProtectionPercentThreshold", Order = 31, GroupName = "Parameters")]
        public double genericLongOrderProtectionPercentThreshold
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "genericShortOrderProtectionPercentThreshold", Description = "genericShortOrderProtectionPercentThreshold", Order = 32, GroupName = "Parameters")]
        public double genericShortOrderProtectionPercentThreshold
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "overBoughtRsiPercentAdjustmentThreshold", Description = "overBoughtRsiPercentAdjustmentThreshold", Order = 33, GroupName = "Parameters")]
        public double overBoughtRsiPercentAdjustmentThreshold
        { get; set; }


        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "overSoldRsiPercentAdjustmentThreshold", Description = "overSoldRsiPercentAdjustmentThreshold", Order = 34, GroupName = "Parameters")]
        public double overSoldRsiPercentAdjustmentThreshold
        { get; set; }





        #endregion


    }
}