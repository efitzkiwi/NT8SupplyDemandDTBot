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
using System.Runtime.InteropServices.WindowsRuntime;
using NinjaTrader.NinjaScript.MarketAnalyzerColumns;
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
    public class Orderz
    {
        public string Name { get; set; }
        public double Thres { get; set; }
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
        private string[] longNames =
        {
            "Long ZONE", "Long RSI", "Long SMA CROSS", "Long INFLECTION", "Long ZERO-RES"
        };
        private string[] shortNames =
        {
            "Short ZONE", "Short RSI", "Short SMA CROSS", "Short INFLECTION"
        };


        double orderMultiplier = 1;
        bool debugPrint = false;
        bool debugDraw = false;

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
        int smaSLSLOW = 50;
        int smaSLFAST = 20;
        string currentOrderClassification = "";
        bool hasLongRSIOrderBeenAdjusted = false;
        bool hasGenericProtectionOrderBeenAdjusted = false;
        bool hasPriorityOrderBeenAdjusted = false;
        bool useSmaLineAsLongStopLoss = false;
        bool usingProfitMaximization = false;
        bool usingProfitProtection = false;
        bool CancelOCOAndExitLongOrder = false;
        bool CancelOCOAndExitShortOrder = false;
        bool ProtectOverlapFromCancelDelayToEnter = false;
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

        double longRSIProfitMaximizationThreshold = 0.009;
        double longSMACROSSProfitMaximizationThreshold = 0.011;
        double longZONEProfitMaximizationThreshold = 0.012;
        double longINFLECTIONProfitMaximizationThreshold = 0.012;

        double shortRSIProfitMaximizationThreshold = 0.009;
        double shortSMACROSSProfitMaximizationThreshold = 0.011;
        double shortZONEProfitMaximizationThreshold = 0.012;
        double shortINFLECTIONProfitMaximizationThreshold = 0.01;

        List<Orderz> OrderManager = new List<Orderz>();




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
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelCloseIgnoreRejects;
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
                //longZONEProfitMaximizationThreshold = 0.012;
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
                ASRZ = AdvancedSRZones(AreaStrengthMultiplier, TimeThreshold, ProxyStrengthMultiplier, NewZoneStrength, ZoneTimeoutStrength, NewZoneTopMultiplier, NewZoneBottomMultiplier, ResZoneColor, SupZoneColor, BreakStrengthMultiplier, UseVolAccumulation, Expiration, MaxMergeCount, MergeThreshold);
                AddChartIndicator(ASRZ);
                RVOL = RelativeVolumeNT8(60, 2, 30);
                AddChartIndicator(RVOL);
                rSI1m = RSI(14, 3);
                AddChartIndicator(rSI1m);
                rSI30m = RSI(BarsArray[1], 14, 3);
                rSI10m = RSI(BarsArray[3], 14, 3);
                LRS = LinRegSlope(50);
                LRS5p = LinRegSlope(5);
                sma5m = SMA(5);
                sma6m = SMA(6);
                sma20m = SMA(20);
                sma50m = SMA(50);
                LRSDaily = LinRegSlope(BarsArray[2], 20);
                sessIter = new SessionIterator(Bars);
                // add new types of orders here
                OrderManager.Add(new Orderz
                {
                    Name = "Long ZONE",
                    Thres = longZONEProfitMaximizationThreshold
                });
                OrderManager.Add(new Orderz
                {
                    Name = "Long RSI",
                    Thres = longRSIProfitMaximizationThreshold
                });
                OrderManager.Add(new Orderz
                {
                    Name = "Long SMA CROSS",
                    Thres = longSMACROSSProfitMaximizationThreshold
                });
                OrderManager.Add(new Orderz
                {
                    Name = "Long INFLECTION",
                    Thres = longINFLECTIONProfitMaximizationThreshold
                });
                OrderManager.Add(new Orderz
                {
                    Name = "Short ZONE",
                    Thres = shortZONEProfitMaximizationThreshold
                });
                OrderManager.Add(new Orderz
                {
                    Name = "Short RSI",
                    Thres = shortRSIProfitMaximizationThreshold
                });
                OrderManager.Add(new Orderz
                {
                    Name = "Short SMA CROSS",
                    Thres = shortSMACROSSProfitMaximizationThreshold
                });
                OrderManager.Add(new Orderz
                {
                    Name = "Short INFLECTION",
                    Thres = shortINFLECTIONProfitMaximizationThreshold
                });
                OrderManager.Add(new Orderz
                {
                    Name = "Long ZERO-RES",
                    Thres = shortINFLECTIONProfitMaximizationThreshold
                });
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
            if (debugPrint) Print("RETURN_QUANTITY: Returning position quantity " + shareQuantity + "($ " + Bars.GetClose(CurrentBar) * shareQuantity + " )" + " for account size " + ThisAcc.Get(AccountItem.CashValue, Currency.UsDollar));
            return shareQuantity;
        }


        protected override void OnExecutionUpdate(Cbi.Execution execution, string executionId, double price, int quantity,
            Cbi.MarketPosition marketPosition, string orderId, DateTime time)
        {
            // if the long entry filled, place a profit target and stop loss to protect the order
            if (longOrder != null && execution.Order == longOrder && NewOCO == true && longOrder.OrderAction == OrderAction.Buy)
            {
                if (debugPrint) Print(" >>>>>>>>>>>>>>>> N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R |");
                if (debugPrint) Print("ORDER_EXECUTE: Long order filled at " + longOrder.AverageFillPrice + " | Order type " + currentOrderClassification + " | quantity: " + longOrder.Quantity + " time " + Time[0]);

                double i = 1.01;
                while (stopPrice >= GetCurrentBid())
                {
                    stopPrice /= i;
                    i *= i;
                }

                if (ScaleHalf)
                {
                    i = 1.01;
                    while (stopPriceHalf >= GetCurrentBid())
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
                    if (debugPrint) Print("ORDER_SUBMIT: Limit order " + ocoString + " submitted at " + limitPrice + " quantity: " + limitOrder.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_SUBMIT: Stop order " + ocoString + " submitted at " + stopPrice + " quantity: " + stopOrder.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_SUBMIT: Limit order " + ocoStringHalf + " submitted at " + limitPriceHalf + " quantity: " + limitOrderHalf.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_SUBMIT: Stop order " + ocoStringHalf + " submitted at " + stopPriceHalf + " quantity: " + stopOrderHalf.Quantity + " time " + Time[0]);
                }
                else
                {
                    ocoString = "LongOCO{1}" + " P: " + execution.OrderId + " T: " + DateTime.Now.ToString("hhmmssffff");
                    limitOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, execution.Quantity, limitPrice, 0, ocoString, longLimitName);
                    stopOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, execution.Quantity, 0, stopPrice, ocoString, longStopName);
                    if (debugPrint) Print("ORDER_SUBMIT: Limit order " + ocoString + " submitted at " + limitPrice + " quantity: " + limitOrder.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_SUBMIT: Stop order " + ocoString + " submitted at " + stopPrice + " quantity: " + stopOrder.Quantity + " time " + Time[0]);
                }
                


            }

            // reverse the order types and prices for a short
            else if (shortOrder != null && execution.Order == shortOrder && NewOCO == true && shortOrder.OrderAction == OrderAction.SellShort)
            {
                if (debugPrint) Print(" >>>>>>>>>>>>>>>> N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R |");
                if (debugPrint) Print("ORDER_EXECUTE: Short order filled at " + shortOrder.AverageFillPrice + " | Order type " + currentOrderClassification + " | quantity: " + shortOrder.Quantity + " time " + Time[0]);

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
                    if (debugPrint) Print("ORDER_SUBMIT: Limit order " + ocoString + " submitted at " + limitPrice + " quantity: " + limitOrder.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_SUBMIT: Stop order " + ocoString + " submitted at " + stopPrice + " quantity: " + stopOrder.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_SUBMIT: Limit order " + ocoStringHalf + " submitted at " + limitPriceHalf + " quantity: " + limitOrderHalf.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_SUBMIT: Stop order " + ocoStringHalf + " submitted at " + stopPriceHalf + " quantity: " + stopOrderHalf.Quantity + " time " + Time[0]);
                }
                else
                {
                    ocoString = "ShortOCO{1}" + " P: " + execution.OrderId + " T: " + DateTime.Now.ToString("hhmmssffff");
                    limitOrder = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, execution.Quantity, limitPrice, 0, ocoString, shortLimitName);
                    stopOrder = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, execution.Quantity, 0, stopPrice, ocoString, shortStopName);
                    if (debugPrint) Print("ORDER_SUBMIT: Limit order " + ocoString + " submitted at " + limitPrice + " quantity: " + limitOrder.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_SUBMIT: Stop order " + ocoString + " submitted at " + stopPrice + " quantity: " + stopOrder.Quantity + " time " + Time[0]);
                }
                

            }



            // when the long profit or stop fills, set the long entry to null to allow a new entry and reset everything
            else if (limitOrder != null && execution.Name == longLimitName)
            {
                if (debugPrint) Print("ORDER_EXECUTE: Long limit order " + limitOrder.Oco + " executed at " + execution.Price + " quantity: " + limitOrder.Quantity + " time " + Time[0]);
                limitOrder = null;
                count++;
            }
            else if (stopOrder != null && execution.Name == longStopName)
            {
                if (debugPrint) Print("ORDER_EXECUTE: Long stop order " + stopOrder.Oco + " executed at " + execution.Price + " quantity: " + stopOrder.Quantity + " time " + Time[0]);
                stopOrder = null;
                count++;
            }
            else if (limitOrder != null && execution.Name == shortLimitName)
            {
                if (debugPrint) Print("ORDER_EXECUTE: Short limit order " + limitOrder.Oco + " executed at " + execution.Price + " quantity: " + limitOrder.Quantity + " time " + Time[0]);
                limitOrder = null;
                count++;
            }
            else if (stopOrder != null && execution.Name == shortStopName)
            {
                if (debugPrint) Print("ORDER_EXECUTE: Short stop order " + stopOrder.Oco + " executed at " + execution.Price + " quantity: " + stopOrder.Quantity + " time " + Time[0]);
                stopOrder = null;
                count++;
            }

            // Halves
            else if (limitOrderHalf != null && execution.Name == longLimitNameHalf)
            {
                if (debugPrint) Print("ORDER_EXECUTE: Long limit order " + limitOrderHalf.Oco + " executed at " + execution.Price + " quantity: " + limitOrderHalf.Quantity + " time " + Time[0]);
                limitOrderHalf = null;
                count++;
            }
            else if (stopOrderHalf != null && execution.Name == longStopNameHalf)
            {
                if (debugPrint) Print("ORDER_EXECUTE: Long stop order " + stopOrderHalf.Oco + " executed at " + execution.Price + " quantity: " + stopOrderHalf.Quantity + " time " + Time[0]);
                stopOrderHalf = null;
                count++;
            }
            else if (limitOrderHalf != null && execution.Name == shortLimitNameHalf)
            {
                if (debugPrint) Print("ORDER_EXECUTE: Short limit order " + limitOrderHalf.Oco + " executed at " + execution.Price + " quantity: " + limitOrderHalf.Quantity + " time " + Time[0]);
                limitOrderHalf = null;
                count++;
            }
            else if (stopOrderHalf != null && execution.Name == shortStopNameHalf)
            {
                if (debugPrint) Print("ORDER_EXECUTE: Short stop order " + stopOrderHalf.Oco + " executed at " + execution.Price + " quantity: " + stopOrderHalf.Quantity + " time " + Time[0]);
                stopOrderHalf = null;
                count++;
            }

            if (ScaleHalf && count == 2)
            {
                ProtectOverlapFromCancelDelayToEnter = true;
            }
            else if (ScaleHalf == false && count == 1)
            {
                ProtectOverlapFromCancelDelayToEnter = true;
            }

        }

        protected override void OnOrderUpdate(Cbi.Order order, double limitPrice, double stopPrice,
            int quantity, int filled, double averageFillPrice,
            Cbi.OrderState orderState, DateTime time, Cbi.ErrorCode error, string comment)
        {

            if (stopOrder != null && order == stopOrder && stopOrder.OrderState == OrderState.Cancelled)
            {
                if (debugPrint) Print("ORDER_CANCEL: Stop " + stopOrder.Oco + " cancelled" + " time " + Time[0]);
                stopOrder = null;
            }

            if (limitOrder != null && order == limitOrder && limitOrder.OrderState == OrderState.Cancelled)
            {
                if (debugPrint) Print("ORDER_CANCEL: Limit " + limitOrder.Oco + " cancelled" + " time " + Time[0]);
                limitOrder = null;
            }

            if (stopOrder != null && order == stopOrder && stopOrder.OrderState == OrderState.Accepted)
            {
                if (debugPrint) Print("ORDER_ACCEPT: Stop " + stopOrder.Oco + " set " + stopPrice + " time " + Time[0]);

            }
            if (limitOrder != null && order == limitOrder && limitOrder.OrderState == OrderState.Accepted)
            {
                if (debugPrint) Print("ORDER_ACCEPT: Limit " + limitOrder.Oco + " set " + limitPrice + " time " + Time[0]);
            }

            if (stopOrder != null && order == stopOrder && stopOrder.OrderState == OrderState.ChangePending)
            {
                if (debugPrint) Print("ORDER_PEND: Stop change" + stopOrder.Oco + " submitted " + stopPrice + " time " + Time[0]);
            }

            if (limitOrder != null && order == limitOrder && limitOrder.OrderState == OrderState.ChangePending)
            {
                if (debugPrint) Print("ORDER_PEND: Limit change " + limitOrder.Oco + " submitted " + limitPrice + " time " + Time[0]);
            }

            if (limitOrder != null && order == limitOrder && limitOrder.OrderState == OrderState.Rejected)
            {
                if (debugPrint) Print("ORDER_REJECT: Limit order " + limitOrder.Oco + " rejected " + limitOrder + " time " + Time[0]);
            }

            if (stopOrder != null && order == stopOrder && stopOrder.OrderState == OrderState.Rejected)
            {
                if (debugPrint) Print("ORDER_REJECT: Stop order " + stopOrder.Oco + " rejected " + stopOrder + " time " + Time[0]);
            }

            // Halves

            if (stopOrderHalf != null && order == stopOrderHalf && stopOrderHalf.OrderState == OrderState.Cancelled)
            {
                if (debugPrint) Print("ORDER_CANCEL: Stop " + stopOrderHalf.Oco + " cancelled" + " time " + Time[0]);
                stopOrderHalf = null;
            }

            if (limitOrderHalf != null && order == limitOrderHalf && limitOrderHalf.OrderState == OrderState.Cancelled)
            {
                if (debugPrint) Print("ORDER_CANCEL: Limit " + limitOrderHalf.Oco + " cancelled" + " time " + Time[0]);
                limitOrderHalf = null;
            }
        

            if (stopOrderHalf != null && order == stopOrderHalf && stopOrderHalf.OrderState == OrderState.Accepted)
            {
                if (debugPrint) Print("ORDER_ACCEPT: Stop " + stopOrderHalf.Oco + " set " + stopPriceHalf + " time " + Time[0]);

            }
            if (limitOrderHalf != null && order == limitOrderHalf && limitOrderHalf.OrderState == OrderState.Accepted)
            {
                if (debugPrint) Print("ORDER_ACCEPT: Limit " + limitOrderHalf.Oco + " set " + limitPriceHalf + " time " + Time[0]);
            }

            if (stopOrderHalf != null && order == stopOrderHalf && stopOrderHalf.OrderState == OrderState.ChangePending)
            {
                if (debugPrint) Print("ORDER_PEND: Stop change" + stopOrderHalf.Oco + " submitted " + stopPriceHalf + " time " + Time[0]);
            }

            if (limitOrderHalf != null && order == limitOrderHalf && limitOrderHalf.OrderState == OrderState.ChangePending)
            {
                if (debugPrint) Print("ORDER_PEND: Limit change " + limitOrderHalf.Oco + " submitted " + limitPriceHalf + " time " + Time[0]);
            }

            if (stopOrderHalf != null && order == stopOrderHalf && stopOrderHalf.OrderState == OrderState.Rejected)
            {
                if (debugPrint) Print("ORDER_REJECT: Stop half order " + stopOrderHalf.Oco + " rejected " + stopOrderHalf + " time " + Time[0]);
                TryResubmitOCO(stopOrderHalf);
            }

            if (limitOrderHalf != null && order == limitOrderHalf && limitOrderHalf.OrderState == OrderState.Rejected)
            {
                if (debugPrint) Print("ORDER_REJECT: Limit half order " + limitOrderHalf.Oco + " rejected " + limitOrderHalf + " time " + Time[0]);
            }

            if (longOrder != null && order == longOrder && longOrder.OrderState == OrderState.Rejected)
            {
                if (debugPrint) Print("ORDER_REJECT: Long order " + longOrder.Oco + " rejected " + longOrder + " time " + Time[0]);
            }

            if (shortOrder != null && order == shortOrder && shortOrder.OrderState == OrderState.Rejected)
            {
                if (debugPrint) Print("ORDER_REJECT: Short order " + shortOrder.Oco + " rejected " + shortOrder + " time " + Time[0]);
            }

            // MAKE SURE WE ARE FLAT AFTER ORDERS ARE CANCELLED
            if (ProtectOverlapFromCancelDelayToEnter && Position.MarketPosition == MarketPosition.Flat && stopOrder == null && stopOrderHalf == null && limitOrder == null && limitOrderHalf == null)
            {
                ProtectOverlapFromCancelDelayToEnter = false;
                if (debugPrint) Print(" >>>>>>>>>>>>>>>>>>> | F L A T | F L A T | F L A T | F L A T | F L A T | F L A T | F L A T | F L A T | F L A T | F L A T | F L A T | F L A T | F L A T | F L A T | F L A T | ");
                TryRequiredExitActions();
            }


            // when both orders are cancelled set to null for a new entry
            if ((longOrder != null && longOrder.OrderState == OrderState.Cancelled && shortOrder != null && shortOrder.OrderState == OrderState.Cancelled))
            {
                longOrder = null;
                shortOrder = null;
            }
        }


        public void TryRequiredExitActions()
        {
            ResetHelperVars();
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


        public void TryResubmitOCO(Order ord)
        {
            if (shortNames.Contains(currentOrderClassification))
            {
                if (ord == stopOrder)
                {
                    if (ord == null || stopOrder == null || limitOrder == null)
                    {
                        // want to exit here as well
                        return;
                    }
                    double i = 1.01;
                    while (stopPrice <= GetCurrentAsk())
                    {
                        stopPrice *= i;
                        i *= i;
                    }
                    string temp = DateTime.Now.ToString("hhmmssffff");
                    ocoString = "ShortOCO{1}" + " P: " + shortOrder.OrderId + " T: " + temp;
                    stopOrder = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, shortOrder.Quantity, 0, stopPrice, ocoString, shortStopName);
                    limitOrder = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, limitOrder.Quantity, limitPrice, 0, ocoString, shortLimitName);
                    if (debugPrint) Print("ORDER_RESUBMIT: Limit order " + ocoString + " resubmitted at " + limitPrice + " quantity: " + limitOrder.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_RESUBMIT: Stop order " + ocoString + " resubmitted at " + stopPrice + " quantity: " + stopOrder.Quantity + " time " + Time[0]);
                }

                else if (ord == stopOrderHalf)
                {
                    if (ord == null || stopOrderHalf == null || limitOrderHalf == null)
                    {
                        // want to exit here as well
                        return;
                    }
                    double i = 1.01;
                    while (stopPriceHalf <= GetCurrentAsk())
                    {
                        stopPriceHalf *= i;
                        i *= i;
                    }
                    string temp = DateTime.Now.ToString("hhmmssffff");
                    ocoStringHalf = "ShortOCO{2}" + " P: " + shortOrder.OrderId + " T: " + temp;
                    limitOrderHalf = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, limitOrderHalf.Quantity, limitPriceHalf, 0, ocoStringHalf, shortLimitNameHalf);
                    stopOrderHalf = SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, limitOrderHalf.Quantity, 0, stopPriceHalf, ocoStringHalf, shortStopNameHalf);
                    if (debugPrint) Print("ORDER_RESUBMIT: Limit order " + ocoStringHalf + " resubmitted at " + limitPriceHalf + " quantity: " + limitOrderHalf.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_RESUBMIT: Stop order " + ocoStringHalf + " resubmitted at " + stopPriceHalf + " quantity: " + stopOrderHalf.Quantity + " time " + Time[0]);
                }
            }
            else if (longNames.Contains(currentOrderClassification))
            {
                if (ord == stopOrder)
                {
                    if (ord == null || stopOrder == null || limitOrder == null)
                    {
                        // want to exit here as well
                        return;
                    }
                    double i = 1.01;
                    while (stopPrice >= GetCurrentBid())
                    {
                        stopPrice /= i;
                        i *= i;
                    }
                    string temp = DateTime.Now.ToString("hhmmssffff");
                    ocoString = "LongOCO{1}" + " P: " + longOrder.OrderId + " T: " + temp;
                    limitOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, limitOrder.Quantity, limitPrice, 0, ocoString, longLimitName);
                    stopOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, shortOrder.Quantity, 0, stopPrice, ocoString, longStopName);
                    if (debugPrint) Print("ORDER_RESUBMIT: Limit order " + ocoString + " resubmitted at " + limitPrice + " quantity: " + limitOrder.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_RESUBMIT: Stop order " + ocoString + " resubmitted at " + stopPrice + " quantity: " + stopOrder.Quantity + " time " + Time[0]);
                }

                else if (ord == stopOrderHalf)
                {
                    if (ord == null || stopOrderHalf == null || limitOrderHalf == null)
                    {
                        // want to exit here as well
                        return;
                    }
                    double i = 1.01;
                    while (stopPriceHalf >= GetCurrentBid())
                    {
                        stopPriceHalf /= i;
                        i *= i;
                    }
                    string temp = DateTime.Now.ToString("hhmmssffff");
                    ocoStringHalf = "LongOCO{2}" + " P: " + shortOrder.OrderId + " T: " + temp;
                    limitOrderHalf = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, limitOrderHalf.Quantity, limitPriceHalf, 0, ocoStringHalf, longLimitNameHalf);
                    stopOrderHalf = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, stopOrderHalf.Quantity, 0, stopPriceHalf, ocoStringHalf, longStopNameHalf);
                    if (debugPrint) Print("ORDER_RESUBMIT: Limit order " + ocoStringHalf + " resubmitted at " + limitPriceHalf + " quantity: " + limitOrderHalf.Quantity + " time " + Time[0]);
                    if (debugPrint) Print("ORDER_RESUBMIT: Stop order " + ocoStringHalf + " resubmitted at " + stopPriceHalf + " quantity: " + stopOrderHalf.Quantity + " time " + Time[0]);
                }
            }
        }

        public void CancelAndFlattenAll()
        {
            // SOMETHING TO REMEMBER: IF THESE DON'T FILL AT EOD FOR SOME REASON, WE HAVE A PROBLEM!!!
            if (Position.MarketPosition == MarketPosition.Flat) return;
            //if (debugPrint && limitOrder != null) Print("Invoke CancelAndFlattenAll for open position");
            double price = longNames.Contains(currentOrderClassification) ? GetCurrentBid() : GetCurrentAsk();
            if (ScaleHalf)
            {
                if (limitOrder != null)
                {
                    if (debugPrint && limitOrder != null) Print("ORDER_CHANGE Invoke CancelAndFlattenAll for limit order " + limitOrder + " to exit at price " + price + " time " + Time[0]);
                    ChangeOrder(limitOrder, limitOrder.Quantity, price, 0);
                }
                if (limitOrderHalf != null)
                {
                    if (debugPrint && limitOrderHalf != null) Print("ORDER_CHANGE Invoke CancelAndFlattenAll for limit order " + limitOrderHalf + " to exit at price " + price + " time " + Time[0]);
                    ChangeOrder(limitOrderHalf, limitOrderHalf.Quantity, price, 0);
                }
            }
            else
            {
                if (limitOrder != null)
                {
                    if (debugPrint && limitOrder != null) Print("ORDER_CHANGE Invoke CancelAndFlattenAll for limit order " + limitOrder + " to exit at price " + price + " time " + Time[0]);
                    ChangeOrder(limitOrder, limitOrder.Quantity, price, 0);
                }
            }
            cfa = true;

        }

        ///*
        /// <summary>
        /// Go short zone with default stops/limits
        /// </summary>
        public void GoShortZone()
        {
            shareQuantity = GetShareQuantity();
            double sp = Close[0] + (Close[0] * (ShortStopLossPercent / 100) * orderMultiplier);
            double lp = Close[0] - (Close[0] * (ShortProfitPercent / 100) * orderMultiplier);
            double spH = ScaleHalf ? Close[0] + (Close[0] * (ShortStopLossPercent / 200) * orderMultiplier) : 0;
            double lpH = ScaleHalf ? Close[0] - (Close[0] * (ShortProfitPercent / 200) * orderMultiplier) : 0;
            TrySubmitShortOrder(lp, lpH, lp, sp, spH, sp, "Short ZONE", false);
        }

        /// <summary>
        /// Go short zone with custom stops/limits
        /// </summary>
        public void GoShortZone(double stop, double stopH, double limit, double limitH, bool usesmasl=false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            TrySubmitShortOrder(limit, limitH, limit, stop, stopH, stop, "Short ZONE", usesmasl, smaslFast, smaslSlow);
        }


        /// <summary>
        /// Go long zone with default stops/limits
        /// </summary>
        public void GoLongZone()
        {
            shareQuantity = GetShareQuantity();
            double sp = Close[0] - (Close[0] * (LongStopLossPercent / 100) * orderMultiplier);
            double lp = Close[0] + (Close[0] * (LongProfitPercent / 100) * orderMultiplier);
            double spH = ScaleHalf ? Close[0] - (Close[0] * (LongStopLossPercent / 200) * orderMultiplier) : 0;
            double lpH = ScaleHalf ? Close[0] + (Close[0] * (LongProfitPercent / 200) * orderMultiplier) : 0;
            TrySubmitLongOrder(lp, lpH, lp, sp, spH, sp, "Long ZONE", false);
        }

        /// <summary>
        /// Go Long zone with custom stops/limits
        /// </summary>
        public void GoLongZone(double stop, double stopH, double limit, double limitH, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            TrySubmitLongOrder(limit, limitH, limit, stop, stopH, stop, "Long ZONE", usesmasl, smaslFast, smaslSlow);
        }

        public void GoShortSMACross()
        {

            shareQuantity = GetShareQuantity();
            double sp = Close[0] + (Close[0] * (0.095 / 100) * orderMultiplier);
            double lp = Close[0] - (Close[0] * (0.24 / 100) * orderMultiplier);
            double spH = ScaleHalf ? Close[0] + (Close[0] * (0.06 / 100) * orderMultiplier) : 0;
            double lpH = ScaleHalf ? Close[0] - (Close[0] * (0.085 / 100) * orderMultiplier) : 0;
            TrySubmitShortOrder(lp, lpH, lp, sp, spH, sp, "Short SMA CROSS", false);

        }

        public void GoShortSMACross(double stop, double stopH, double limit, double limitH, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            TrySubmitLongOrder(limit, limitH, limit, stop, stopH, stop, "Short SMA CROSS", usesmasl, smaslFast, smaslSlow);
        }

        public void GoLongSMACross()
        {
            shareQuantity = GetShareQuantity();
            double sp = Close[0] - (Close[0] * (0.09 / 100) * orderMultiplier);
            double lp = Close[0] + (Close[0] * (0.1 / 100) * orderMultiplier);
            double spH = ScaleHalf ? Close[0] - (Close[0] * (0.045 / 100) * orderMultiplier) : 0;
            double lpH = ScaleHalf ? Close[0] + (Close[0] * (0.09 / 100) * orderMultiplier) : 0;
            TrySubmitLongOrder(lp, lpH, lp, sp, spH, sp, "Long SMA CROSS", true);
        }

        public void GoLongSMACross(double stop, double stopH, double limit, double limitH, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            TrySubmitLongOrder(limit, limitH, limit, stop, stopH, stop, "Long SMA CROSS", usesmasl, smaslFast, smaslSlow);
        }

        public void GoLongInflectionPoint()
        {
            shareQuantity = GetShareQuantity();
            double sp = Close[0] - (Close[0] * (0.05 / 100));
            double lp = Close[0] + (Close[0] * (0.05 / 100));
            double spH = ScaleHalf ? Close[0] - (Close[0] * (0.05 / 100)) : 0;
            double lpH = ScaleHalf ? Close[0] + (Close[0] * (0.025 / 100)) : 0;
            TrySubmitLongOrder(lp, lpH, lp, sp, spH, sp, "Long INFLECTION", false);

        }

        public void GoLongInflectionPoint(double stop, double stopH, double limit, double limitH, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            TrySubmitLongOrder(limit, limitH, limit, stop, stopH, stop, "Long INFLECTION", usesmasl, smaslFast, smaslSlow);

        }


        public void GoLongNoResistance()
        {
            shareQuantity = GetShareQuantity();
            double sp = Close[0] - (Close[0] * (0.05 / 100));
            double lp = Close[0] + (Close[0] * (0.05 / 100));
            double spH = ScaleHalf ? Close[0] - (Close[0] * (0.05 / 100)) : 0;
            double lpH = ScaleHalf ? Close[0] + (Close[0] * (0.025 / 100)) : 0;
            TrySubmitLongOrder(lp, lpH, lp, sp, spH, sp, "Long ZERO-RES", false);

        }

        public void GoLongNoResistance(double stop, double stopH, double limit, double limitH, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            TrySubmitLongOrder(limit, limitH, limit, stop, stopH, stop, "Long ZERO-RES", usesmasl, smaslFast, smaslSlow);

        }




        public void GoLongRSI30mOversold()
        {
            if (CurrentBars[1] > rSI30m.Period - 1)
            {
                // TODO : oversold RSI profit and stop should be more bullish

                if (rSI30m[0] < 26 && Position.MarketPosition == MarketPosition.Flat)
                {
                    shareQuantity = GetShareQuantity();
                    double sp = Close[0] - (Close[0] * longRSIStopPercent);
                    double lp = Close[0] + (Close[0] * longRSILimitPercent);
                    double spH = ScaleHalf ? Close[0] - (Close[0] * longRSIStopPercent / 2) : 0;
                    double lpH = ScaleHalf ? Close[0] + (Close[0] * longRSILimitPercent / 2) : 0;
                    TrySubmitLongOrder(lp, lpH, lp, sp, spH, sp, "Long RSI", false);
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
                                if (debugPrint) Print("ORDER_CHANGE Long 10mRSIProfitProtection for stop half order " + stopOrderHalf + " to exit at price " + stopPriceHalf + " time " + Time[0]);
                                ChangeOrder(stopOrderHalf, stopOrderHalf.Quantity, 0, stopPriceHalf);
                            }
                            stopPrice = Open[0] - Open[0] * overBoughtRsiLowPriorityStopPercent; // overbought RSI protection low
                            if (debugPrint) Print("ORDER_CHANGE Long 10mRSIProfitProtection for stop order " + stopOrder + " to exit at price " + stopPrice + " time " + Time[0]);
                            ChangeOrder(stopOrder, stopOrder.Quantity, 0, stopPrice);
                        }
                        else
                        {
                            stopPrice = Open[0] - Open[0] * overBoughtRsiGenericStopPercent; // overbought RSI protection generic
                            if (debugPrint) Print("ORDER_CHANGE Long 10mRSIProfitProtection for stop order " + stopOrder + " to exit at price " + stopPrice + " time " + Time[0]);
                            ChangeOrder(stopOrder, stopOrder.Quantity, 0, stopPrice);
                        }
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
                                if (debugPrint) Print("ORDER_CHANGE Short 10mRSIProfitProtection for stop half order " + stopOrderHalf + " to exit at price " + stopPriceHalf + " time " + Time[0]);
                                ChangeOrder(stopOrderHalf, stopOrderHalf.Quantity, 0, stopPriceHalf);
                            }
                            stopPrice = Open[0] + Open[0] * overBoughtRsiLowPriorityStopPercent;
                            if (debugPrint) Print("ORDER_CHANGE Short 10mRSIProfitProtection for stop order " + stopOrder + " to exit at price " + stopPrice + " time " + Time[0]);
                            ChangeOrder(stopOrder, stopOrder.Quantity, 0, stopPrice);
                        }
                        else
                        {
                            stopPrice = Open[0] + Open[0] * overBoughtRsiGenericStopPercent;
                            if (debugPrint) Print("ORDER_CHANGE Short 10mRSIProfitProtection for stop order " + stopOrder + " to exit at price " + stopPrice + " time " + Time[0]);
                            ChangeOrder(stopOrder, stopOrder.Quantity, 0, stopPrice);
                        }
                        hasPriorityOrderBeenAdjusted = true;
                    }
                }
            }
        }

        public void TryZoneOrders()
        { 
            double LST = LongStrengthThreshold + (LongStrengthThreshold * (-PriceAdjustedDailySMASlope));
            double STT = ShortStrengthThreshold + (ShortStrengthThreshold * (-PriceAdjustedDailySMASlope));
            LST = 0;
            STT = 0;
            //double strength = ASRZ.GetZoneStrength(ASRZ.GetCurrentZone());
            int direction = ASRZ.GetZoneType(ASRZ.GetCurrentZone());
            if (ASRZ.DoesZoneStrengthComply(ASRZ.GetCurrentZone(), ">", LST) && direction == 0) // if we're in a long zone
            {
                if (Position.MarketPosition == MarketPosition.Flat) // if we have no open positions
                {
                    if (IsInflection("Up", 3) && !IsRSIWithinXBars("5m", 5, ">", 70)) // if the sma turns positive
                    {
                        GoLongZone();
                    }
                    else
                    {
                        GoShortZone();
                    }
                }
                else if (Position.MarketPosition == MarketPosition.Short && currentOrderClassification == "Short ZONE" && (Bars.GetTime(CurrentBar) - Bars.GetTime(CurrentBar - BarsSinceEntryExecution(0, "", 0))).Minutes >= DelayExitMinutes) // if we have a short position open
                {
                    // do some checks to see if we SHOULD close it
                    if (IsInflection("Up", 3))
                    {
                        NewOCO = false;
                        GoLongAfterExit = true;
                        ExitViaLimitOrder(Close[0]);
                    }

                }
            }
            else if (ASRZ.DoesZoneStrengthComply(ASRZ.GetCurrentZone(), ">", STT) && direction == 1)
            {
                if (Position.MarketPosition == MarketPosition.Flat) // if we have no open positions
                {
                    if (IsInflection("Down", 3)) // if the sma turns positive
                    {
                        GoShortZone();
                    }
                }
                else if (Position.MarketPosition == MarketPosition.Long && currentOrderClassification == "Long ZONE" && (Bars.GetTime(CurrentBar) - Bars.GetTime(CurrentBar - BarsSinceEntryExecution(0, "", 0))).Minutes >= DelayExitMinutes) // if we have a short position open
                {
                    // do some checks to see if we SHOULD close it
                    if (IsInflection("Down", 3)) 
                    {
                        NewOCO = false;
                        GoShortAfterExit = true;
                        ExitViaLimitOrder(Close[0]);
                    }
                    
                }
            }
        }


        public void TryUseSMAAsStopLoss(int period, int halfPeriod)
        {
            if (useSmaLineAsLongStopLoss && CurrentBar > period && CurrentBar > halfPeriod)
            {
                Order refOrder = null;
                if (longNames.Contains(currentOrderClassification)) refOrder = longOrder;
                else if (shortNames.Contains(currentOrderClassification)) refOrder = shortOrder;
                if (refOrder == null) return;
                stopPrice = SMA(period)[0];
                if (ScaleHalf)
                {
                    stopPriceHalf = SMA(halfPeriod)[0];
                    if (stopPriceHalf >= GetCurrentBid())
                    {
                        stopPriceHalf = (GetCurrentBid() + refOrder.AverageFillPrice) / 2;
                    }
                    if (stopOrderHalf != null)
                    {
                        if (debugPrint) Print("ORDER_CHANGE TryUseSMAAsStopLoss STOPHALF " + stopOrderHalf + " EXIT PRICE " + stopPriceHalf + " TIME " + Time[0]);
                        ChangeOrder(stopOrderHalf, stopOrderHalf.Quantity, 0, stopPriceHalf);
                    }

                    if (stopOrder != null)
                    {
                        if (debugPrint) Print("ORDER_CHANGE TryUseSMAAsStopLoss STOPHALF " + stopOrder + " EXIT PRICE " + stopPrice + " TIME " + Time[0]);
                        ChangeOrder(stopOrder, stopOrder.Quantity, 0, stopPrice);
                    }


                }
                else
                {
                    if (stopOrder != null)
                    {
                        if (debugPrint) Print("ORDER_CHANGE TryUseSMAAsStopLoss STOPHALF " + stopOrder + " EXIT PRICE " + stopPrice + " TIME " + Time[0]);
                        ChangeOrder(stopOrder, stopOrder.Quantity, 0, stopPrice);
                    }
                }
            }
        }

        public bool SetProtectionSentiment()
        {

            for (int i = 0; i < OrderManager.Count; i++)
            {
                if (OrderManager[i].Name == currentOrderClassification)
                {
                    if (longNames.Contains(currentOrderClassification) && longOrder != null)
                    {
                        if (GetCurrentBid() < longOrder.AverageFillPrice + longOrder.AverageFillPrice * OrderManager[i].Thres)
                        //if (GetCurrentBid() < OrderManager[i].Thres)
                        {
                            usingProfitProtection = true;
                            usingProfitMaximization = false;
                        }
                        else
                        {
                            usingProfitProtection = false;
                            usingProfitMaximization = true;
                        }
                        return true;
                    }
                    if (shortNames.Contains(currentOrderClassification) && shortOrder != null)
                    {
                        if (GetCurrentAsk() > shortOrder.AverageFillPrice - shortOrder.AverageFillPrice * OrderManager[i].Thres)
                        //if (GetCurrentAsk() > OrderManager[i].Thres)
                        {
                            usingProfitProtection = true;
                            usingProfitMaximization = false;
                        }
                        else
                        {
                            usingProfitProtection = false;
                            usingProfitMaximization = true;
                        }

                        return true;
                    }
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Handles profit taking and loss prevention in real time.
        /// Positions that are still considered risky are managed here
        /// Higher level functions deal with profit maximization are routed through this function
        /// For longs, we look for bearish signals and make sure to protect capital, or take profits quickly depending on the situation
        /// For shorts we look for bullish signals and make sure to protect capital, or take profits quickly depending on the situation
        /// When we have exceeded an acceptable profit target, high level functions deal with profit MAXIMIZATION
        /// </summary>
        public bool TryOCOProfitProtection()
        {

            if (SetProtectionSentiment())
            {
                if (Position.MarketPosition == MarketPosition.Long && longOrder != null)
                {


                    if (usingProfitProtection)
                    {
                        if (debugDraw) Draw.Rectangle(this, "askdmkasm" + CurrentBar, 0, High[0], 1, Low[1], Brushes.DarkBlue);
                        if (currentOrderClassification == "Long ZONE")
                        {
                            if (IsInflection("Down", 3, 6) && GetCurrentBid() > longOrder.AverageFillPrice && IsGapDown())
                            {
                                Draw.Rectangle(this, "askdmkasm" + CurrentBar, 0, High[0], 1, Low[1], Brushes.White);
                                ChangeStopLoss(longOrder.AverageFillPrice, (longOrder.AverageFillPrice + Close[0]) / 2, (longOrder.AverageFillPrice + Close[0]) / 2);
                            }
                        }
                        else if (currentOrderClassification == "Long RSI")
                        {

                        }
                        else if (currentOrderClassification == "Long SMA CROSS")
                        {

                        }
                        else if (currentOrderClassification == "Long INFLECTION")
                        {

                        }


                    }
                    else if (usingProfitMaximization)
                    {
                        if (debugDraw) Draw.Rectangle(this, "askdmkasm" + CurrentBar, 0, High[0], 1, Low[1], Brushes.Orange);
                        if (currentOrderClassification == "Long ZONE")
                        {
                            if (useSmaLineAsLongStopLoss == false && CurrentBar > sma20m.Period - 1 && CurrentBar > sma50m.Period - 1)
                            {
                                useSmaLineAsLongStopLoss = true;
                                TryUseSMAAsStopLoss(20, 50);
                            }
                        }
                        else if (currentOrderClassification == "Long RSI")
                        {

                        }
                        else if (currentOrderClassification == "Long SMA CROSS")
                        {

                        }
                        else if (currentOrderClassification == "Long INFLECTION")
                        {

                        }
                    }

                }
                else if (Position.MarketPosition == MarketPosition.Short && shortOrder != null)
                {
                    if (usingProfitProtection)
                    {
                        if (debugDraw) Draw.Rectangle(this, "askdmkasm" + CurrentBar, 0, High[0], 1, Low[1], Brushes.Yellow);
                        if (currentOrderClassification == "Short ZONE")
                        {
                            if (IsInflection("Up", 3, 6) && GetCurrentAsk() < shortOrder.AverageFillPrice)
                            {
                                Draw.Rectangle(this, "askdmkasm" + CurrentBar, 0, High[0], 1, Low[1], Brushes.Black);
                                //ChangeStopLoss(shortOrder.AverageFillPrice, (shortOrder.AverageFillPrice + Close[0]) / 2, (shortOrder.AverageFillPrice + Close[0]) / 2);
                            }
                        }
                        else if (currentOrderClassification == "Short RSI")
                        {

                        }
                        else if (currentOrderClassification == "Short SMA CROSS")
                        {

                        }
                        else if (currentOrderClassification == "Short INFLECTION")
                        {

                        }
                    }
                    else if (usingProfitMaximization)
                    {
                        if (debugDraw) Draw.Rectangle(this, "askdmkasm" + CurrentBar, 0, High[0], 1, Low[1], Brushes.Purple);
                        if (currentOrderClassification == "Short ZONE")
                        {

                        }
                        else if (currentOrderClassification == "Short RSI")
                        {

                        }
                        else if (currentOrderClassification == "Short SMA CROSS")
                        {

                        }
                        else if (currentOrderClassification == "Short INFLECTION")
                        {

                        }
                    }

                }

            }


            #region specific protection
            // LOOKING FOR BEARISH SIGNALS TO PROTECT A CURRENT LONG THAT HASN'T PRODUCED GOOD PROFIT ALREADY
            if (longOrder != null && currentOrderClassification == "Long RSI" && limitOrder != null && stopOrder != null && hasLongRSIOrderBeenAdjusted == false)
            {
                if (GetCurrentBid() > ((longOrder.AverageFillPrice + (longOrder.AverageFillPrice * longRSILimitPercent)) + longOrder.AverageFillPrice) / 2)
                {
                    if (ScaleHalf)
                    {
                        if (stopOrderHalf != null)
                        {
                            stopPriceHalf = (longOrder.AverageFillPrice);
                            if (debugPrint) Print("ORDER_CHANGE Short 10mRSIProfitProtection for stop half order " + stopOrderHalf + " to exit at price " + stopPriceHalf + " time " + Time[0]);
                            ChangeOrder(stopOrderHalf, stopOrderHalf.Quantity, 0, stopPriceHalf);
                        }
                        stopPrice = (GetCurrentBid() + longOrder.AverageFillPrice) / 2;
                        if (debugPrint) Print("ORDER_CHANGE Short 10mRSIProfitProtection for stop order " + stopOrder + " to exit at price " + stopPrice + " time " + Time[0]);
                        ChangeOrder(stopOrder, stopOrder.Quantity, 0, stopPrice);
                    }
                    else
                    {
                        stopPrice = (GetCurrentBid() + longOrder.AverageFillPrice) / 2;
                        if (debugPrint) Print("ORDER_CHANGE Long RSI for stop order " + stopOrder + " to exit at price " + stopPrice + " time " + Time[0]);
                        ChangeOrder(stopOrder, stopOrder.Quantity, 0, stopPrice);
                    }
                    hasLongRSIOrderBeenAdjusted = true;
                }
            }
            #endregion
            /*
            #region high level maximization
            if (longOrder != null && currentOrderClassification == "Long ZONE" && limitOrder != null && stopOrder != null && useSmaLineAsLongStopLoss == false && CurrentBar > sma20m.Period - 1 && CurrentBar > sma50m.Period - 1)
            {
                if (GetCurrentBid() > longOrder.AverageFillPrice + longOrder.AverageFillPrice * longZONEProfitMaximizationThreshold) // 1.2%
                {
                    useSmaLineAsLongStopLoss = true;
                    TryUseSMAAsStopLoss(20, 50);
                    if (debugPrint) Print("Adjusting long overbought protection SMA20 OCO");
                }
            }
            #endregion
            */
            return true;
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
                    if (debugPrint) Print("███████████████████████████████████████████████████████████████");
                    if (debugPrint) Print("SESSION BEGIN: " + sessBegin + " END: " + sessEnd);
                    if (debugPrint) Print("%──────────────────────────────────────────────────────────────%");
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

        // Utility functions
        public bool IsRSIWithinXBars(string agg, int lookback, string comp, double limit)
        {
            // MAKE SURE THESE INDICATORS ARE ALREADY LOADED IN CONFIGURE
            RSI refRSI;
            switch (agg)
            {
                case "1m":
                    refRSI = rSI1m;
                    break;

                case "10m":
                    refRSI = rSI10m;
                    break;

                case "30m":
                    refRSI = rSI30m;
                    break;

                default:
                    refRSI = rSI1m;
                    break;
            }
            if (CurrentBar > refRSI.Period - 1 + lookback)
            {
                for (int i = 0; i <= lookback; i++)
                {
                    if (comp == ">")
                    {
                        if (refRSI[i] > limit)
                        {
                            return true;
                        }
                    }
                    else if (comp == "<")
                    {
                        if (refRSI[i] < limit)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool IsInflection(string direction, int period, int smooth = 1, bool detrend = false, InputSeriesType ist = InputSeriesType.LinRegSlope, NormType nt = NormType.None) // "Up" or "Down"
        {
            SlopeEnhancedOp refLRS = SlopeEnhancedOp(period, 56, smooth, detrend, ist, nt, Brushes.Green, Brushes.Red);

            if (direction == "Up" && refLRS[1] < 0 && refLRS[0] > 0)
            {
                return true;
            }
            else if (direction == "Down" && refLRS[1] > 0 && refLRS[0] < 0)
            {
                return true;
            }
            return false;
        }

        public void ResetHelperVars()
        {
            currentOrderClassification = "";
            hasLongRSIOrderBeenAdjusted = false;
            hasGenericProtectionOrderBeenAdjusted = false;
            hasPriorityOrderBeenAdjusted = false;
            useSmaLineAsLongStopLoss = false;
            usingProfitProtection = false;
            usingProfitMaximization = false;
        }

        public double RelativeVolLastXBars(int period, int bars)
        {
            double tempRVol = 0;
            if (CurrentBar > bars)
            {
                for (int i = 0; i < bars; i++)
                {
                    tempRVol += RelativeVolumeNT8(period, 2, 20)[i];
                }
            }
            return tempRVol;
        }

        public void ChangeStopLoss(double sp, double sph, double spNoh, string msg = "Stop loss adjusted")
        {
            if (ScaleHalf)
            {
                if (stopOrderHalf != null)
                {
                    stopPriceHalf = sph;
                    if (debugPrint) Print("ORDER_CHANGE STOP LOSS HALF generic \"" + msg + "\" for stop half order " + stopOrderHalf + " to exit at price " + stopPriceHalf + " time " + Time[0]);
                    ChangeOrder(stopOrderHalf, stopOrderHalf.Quantity, 0, stopPriceHalf);
                }
                stopPrice = sp;
                if (stopOrder != null)
                {
                    if (debugPrint) Print("ORDER_CHANGE STOP LOSS generic \"" + msg + "\" for stop order " + stopOrder + " to exit at price " + stopPrice + " time " + Time[0]);
                    ChangeOrder(stopOrder, stopOrder.Quantity, 0, stopPrice);
                }
            }
            else
            {
                stopPrice = spNoh;
                if (debugPrint) Print("ORDER_CHANGE STOP LOSS generic \"" + msg + "\" for stop order " + stopOrder + " to exit at price " + stopPrice + " time " + Time[0]);
                if (stopOrder != null) ChangeOrder(stopOrder, stopOrder.Quantity, 0, stopPrice);
            }
        }


        public void ExitViaLimitOrder(double price, string msg = "Exit via limit order submitted")
        {
            if (Position.MarketPosition == MarketPosition.Flat) return;
            if (ScaleHalf)
            {
                if (limitOrder != null)
                {
                    if (debugPrint) Print("ORDER_CHANGE LIMIT EXIT generic \"" + msg + "\" for limit order " + limitOrder + " to exit at price " + limitOrder + " time " + Time[0]);
                    ChangeOrder(limitOrder, limitOrder.Quantity, price, 0);
                }

                if (limitOrderHalf != null)
                {
                    if (debugPrint) Print("ORDER_CHANGE LIMIT EXIT HALF generic \"" + msg + "\" for limit half order " + limitOrderHalf + " to exit at price " + limitOrderHalf + " time " + Time[0]);
                    ChangeOrder(limitOrderHalf, limitOrderHalf.Quantity, price, 0);
                }
            }
            else
            {
                if (limitOrder != null)
                {
                    if (debugPrint) Print("ORDER_CHANGE LIMIT EXIT generic \"" + msg + "\" for limit order " + limitOrder + " to exit at price " + limitOrder + " time " + Time[0]);
                    ChangeOrder(limitOrder, limitOrder.Quantity, price, 0);
                }
            }
        }


        public Tuple<string> GetGapStatus()
        {
            return Tuple.Create("");
        }

        public bool IsGapDown()
        {
            return IsGap[0]==2 ? true : false;
        }

        public bool IsGapUp()
        {
            return IsGap[0] == 1 ? true : false;
        }


        /// <summary>
        /// Adjust thresholds for various stoploss/profit maximization conditions
        /// </summary>
        public void UpdateOrderProfitThreshold()
        {
            if (OrderManager == null) return;
            for (int i=0;i<OrderManager.Count;i++)
            {

                    if (OrderManager[i].Name == "Long ZONE" && longOrder != null)
                    {
                        OrderManager[i].Thres = longOrder.AverageFillPrice + (longOrder.AverageFillPrice * longZONEProfitMaximizationThreshold);
                    }
                    
                    else if (OrderManager[i].Name == "Long SMA CROSS" && longOrder != null)
                    {
                        OrderManager[i].Thres = longOrder.AverageFillPrice + (longOrder.AverageFillPrice * longSMACROSSProfitMaximizationThreshold);
                    }
                    else if (OrderManager[i].Name == "Long RSI" && longOrder != null)
                    {
                        OrderManager[i].Thres = ((longOrder.AverageFillPrice + (longOrder.AverageFillPrice * longRSILimitPercent)) + longOrder.AverageFillPrice) / 2;
                    }
                    else if (OrderManager[i].Name == "Long INFLECTION" && longOrder != null)
                    {
                        OrderManager[i].Thres = longOrder.AverageFillPrice + (longOrder.AverageFillPrice * longINFLECTIONProfitMaximizationThreshold);
                    }

                    // Shorts

                    if (OrderManager[i].Name == "Short ZONE" && shortOrder != null)
                    {
                        OrderManager[i].Thres = shortOrder.AverageFillPrice - (shortOrder.AverageFillPrice * shortZONEProfitMaximizationThreshold);
                    }
                    else if (OrderManager[i].Name == "Short SMA CROSS" && shortOrder != null)
                    {
                        OrderManager[i].Thres = shortOrder.AverageFillPrice - (shortOrder.AverageFillPrice * shortSMACROSSProfitMaximizationThreshold);
                    }
                    else if (OrderManager[i].Name == "Short RSI" && shortOrder != null)
                    {
                        OrderManager[i].Thres = ((shortOrder.AverageFillPrice - (shortOrder.AverageFillPrice * longRSILimitPercent)) + shortOrder.AverageFillPrice) / 2;
                    }
                    else if (OrderManager[i].Name == "Short INFLECTION" && shortOrder != null)
                    {
                        OrderManager[i].Thres = shortOrder.AverageFillPrice - (shortOrder.AverageFillPrice * shortINFLECTIONProfitMaximizationThreshold);
                    }
            }
            
        }

        /// <summary>
        /// Submit a short order
        /// </summary>
        /// <param name="lp">Limit price</param>
        /// <param name="lph">Limit price half</param>
        /// <param name="lpNh">Limit price (no scale half)</param>
        /// <param name="sp">stop price</param>
        /// <param name="sph">Stop price half</param>
        /// <param name="spNh">Stop price (no scale half)</param>
        /// <param name="msg">Order type (zone, sma, etc.)</param>
        /// <param name="useSmaSL">Bool Use sma stop loss?</param>
        /// <param name="smaslFast">Fast stop loss line</param>
        /// <param name="smaslSlow">Slow stop loss line</param>
        /// <returns>Error int</returns>
        public int TrySubmitShortOrder(double lp, double lph, double lpNh, double sp, double sph, double spNh, string msg, bool useSmaSL, int smaslFast=20, int smaslSlow=50)
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                shareQuantity = GetShareQuantity();
                sp = ScaleHalf ? sp : spNh;
                lp = ScaleHalf ? lp : lpNh;
                double ask = GetCurrentAsk();
                double bid = GetCurrentBid();
                if (ScaleHalf)
                {
                    if (sph < ask)
                    {
                        if (debugPrint) Print("ERROR: Short stop price half is below any available ask price");
                        return 3;
                    }
                    if (sp <= lph)
                    {
                        if (debugPrint) Print("ERROR: Short stop price is below limit price half");
                        return 3;
                    }
                    if (sph <= lph)
                    {
                        if (debugPrint) Print("ERROR: Short stop price half is below limit price half");
                        return 3;
                    }
                    if (sph <= lp)
                    {
                        if (debugPrint) Print("ERROR: Short stop price half is below limit price");
                        return 3;
                    }
                    if (sph == bid)
                    {
                        if (debugPrint) Print("ERROR: Short stop price half is equal to bid price, order invalid");
                        return 3;
                    }
                    if (lph == bid)
                    {
                        if (debugPrint) Print("ERROR: Short limit price half is equal to ask price, order will exit immediately");
                        return 3;
                    }
                }
                if (sp <= lp)
                {
                    if (debugPrint) Print("ERROR: Short stop price is below limit price");
                    return 1;
                }
                if (sp < ask)
                {
                    if (debugPrint) Print("ERROR: Short stop price is below any available ask price");
                    return 2;
                }
                if (sp == bid)
                {
                    if (debugPrint) Print("ERROR: Short stop price is equal to ask price, order will exit immediately");
                    return 3;
                }
                if (lp == bid)
                {
                    if (debugPrint) Print("ERROR: Short limit price is equal to ask price, order will exit immediately");
                    return 3;
                }

                stopPrice = sp;
                stopPriceHalf = sph;
                limitPrice = lp;
                limitPriceHalf = lph;

                if (debugPrint) Print("ORDERS_TRY_ENTRY: SHORT limit price = " + limitPrice + " limit half price = " + limitPriceHalf + " stop price = " + stopPrice + " stop half price = " + stopPriceHalf + " current bid = " + bid + " current ask = " + ask);
                NewOCO = true;
                useSmaLineAsLongStopLoss = useSmaSL;
                smaSLSLOW = smaslSlow;
                smaSLFAST = smaslFast;
                ocoString = "Short order" + " T: " + DateTime.Now.ToString("hhmmssffff");
                if ((sessEnd - Time[0]).TotalSeconds > sessionEndSeconds) shortOrder = SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Market, shareQuantity, 0, 0, ocoString, msg);
                else if (debugPrint) Print("ERROR: Cannot submit short order after session end threshold");
                currentOrderClassification = msg;
                return 0;
            }
            else
            {
                if (debugPrint) Print("ERROR: Position is not flat. Short order cannot be submitted");
                return 4;
            }
        }

        /// <summary>
        /// Submit a long order
        /// </summary>
        /// <param name="lp">Limit price</param>
        /// <param name="lph">Limit price half</param>
        /// <param name="lpNh">Limit price (no scale half)</param>
        /// <param name="sp">stop price</param>
        /// <param name="sph">Stop price half</param>
        /// <param name="spNh">Stop price (no scale half)</param>
        /// <param name="msg">Order type (zone, sma, etc.)</param>
        /// <param name="useSmaSL">Bool Use sma stop loss?</param>
        /// <param name="smaslFast">Fast stop loss line</param>
        /// <param name="smaslSlow">Slow stop loss line</param>
        /// <returns>Error int</returns>
        public int TrySubmitLongOrder(double lp, double lph, double lpNh, double sp, double sph, double spNh, string msg, bool useSmaSL, int smaslFast=20, int smaslSlow=50)
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                shareQuantity = GetShareQuantity();

                sp = ScaleHalf ? sp : spNh;
                lp = ScaleHalf ? lp : lpNh;
                double ask = GetCurrentAsk();
                double bid = GetCurrentBid();
                if (ScaleHalf)
                {
                    if (sph > bid)
                    {
                        if (debugPrint) Print("ERROR: Long stop price half is below any available ask price");
                        return 3;
                    }
                    if (sp >= lph)
                    {
                        if (debugPrint) Print("ERROR: Long stop price is below limit price half");
                        return 3;
                    }
                    if (sph >= lph)
                    {

                        if (debugPrint) Print("ERROR: Long stop price half is below limit price half");
                        return 3;
                    }
                    if (sph >= lp)
                    {
                        if (debugPrint) Print("ERROR: Long stop price half is below limit price");
                        return 3;
                    }
                    if (sph == ask)
                    {
                        if (debugPrint) Print("ERROR: Long stop price half is equal to ask price, order invalid");
                        return 3;
                    }
                    if (lph == ask)
                    {
                        if (debugPrint) Print("ERROR: Long limit price half is equal to ask price, order will exit immediately");
                        return 3;
                    }
                }
                if (sp >= lp)
                {
                    if (debugPrint) Print("ERROR: Long stop price is below limit price");
                    return 1;
                }
                if (sp > bid)
                {
                    if (debugPrint) Print("ERROR: Long stop price is below any available ask price");
                    return 2;
                }
                if (sp == ask)
                {
                    if (debugPrint) Print("ERROR: Long stop price half is equal to ask price, order invalid");
                    return 3;
                }
                if (lp == ask)
                {
                    if (debugPrint) Print("ERROR: Long limit price is equal to ask price, order will exit immediately");
                    return 3;
                }

                stopPrice = sp;
                stopPriceHalf = sph;
                limitPrice = lp;
                limitPriceHalf = lph;

                if (debugPrint) Print("ORDERS_TRY_ENTRY: LONG limit price = " + limitPrice + " limit half price = " + limitPriceHalf + " stop price = " + stopPrice + " stop half price = " + stopPriceHalf + " current bid = " + bid + " current ask = " + ask);
                NewOCO = true;
                useSmaLineAsLongStopLoss = useSmaSL;
                smaSLSLOW = smaslSlow;
                smaSLFAST = smaslFast;
                ocoString = "Long order" + " T: " + DateTime.Now.ToString("hhmmssffff");
                if ((sessEnd - Time[0]).TotalSeconds > sessionEndSeconds) longOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, shareQuantity, 0, 0, ocoString, msg);
                else if (debugPrint) Print("ERROR: Cannot submit long order after session end threshold");
                currentOrderClassification = msg;
                return 0;

            }
            else
            {
                if (debugPrint) Print("ERROR: Position is not flat. Long order cannot be submitted");
                return 4;
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
                }
            }

            if (BarsInProgress == 1)
            {



                rSI30m.Update();
                GoLongRSI30mOversold();

            }


            if (BarsInProgress == 0)
            {

                // Update gaps, current session start, etc
                UpdateDayEvents();

                // Cancel and flatten orders at the end of day
                if ((sessEnd - Time[0]).TotalSeconds <= sessionEndSeconds)
                {
                    if (Position.MarketPosition != MarketPosition.Flat && cfa == false)
                    {
                        if (debugPrint) Print((sessEnd - Time[0]).TotalSeconds + " seconds until session end.");
                        CancelAndFlattenAll();
                    }
                    if (Bars.IsLastBarOfSession)
                    {
                        if (debugPrint) Print("%──────────────────────────────────────────────────────────────%");
                        if (debugPrint) Print("SESSION END: " + sessEnd + " BEGIN: " + sessBegin);
                        if (debugPrint) Print("███████████████████████████████████████████████████████████████");
                    }

                }

                if (CurrentBar >= 0)
                {


                    if (ASRZ.ZoneBoxList.Count > 0 + TradeDelay)
                    {

                        if (UseZoneStrengthOrderMultiplier)
                        {
                            orderMultiplier = Math.Floor(ZoneStrengthOrderScale * Math.Log(ASRZ.GetZoneStrength(ASRZ.GetCurrentZone()) + 1)) / 100;
                        }

                        // If the order was designated to use an SMA as a stoploss, this function updates the order


                        if (currentOrderClassification == "Long SMA CROSS") TryUseSMAAsStopLoss(6, 6);
                        else if (currentOrderClassification == "Short SMA CROSS") TryUseSMAAsStopLoss(6, 6);
                        else if (currentOrderClassification == "Long ZERO-RES") TryUseSMAAsStopLoss(smaSLFAST, smaSLSLOW);
                        //else TryUseSMAAsStopLoss(20, 50);


                        // Generic order OCO exit handling to make sure we keep some profits on the table
                        // Checking longs and shorts if they need to be exited
                        // Manage open RSI orders
                        // If RSI limit order goes above certain percent, raise the stop to breakeven
                        TryOCOProfitProtection();
                        //UpdateOrderProfitThreshold();


                        if (CurrentBar > sma20m.Period - 1 && CurrentBar > sma50m.Period - 1 && CurrentBar > sma5m.Period - 1 && CurrentBar > sma6m.Period)
                        {
                            if (sma20m[1] > sma50m[1] && sma20m[0] < sma50m[0])
                            {

                                if (rSI1m[0] > 30)
                                {

                                    //TryShortSMACross();
                                    //GoShortSMACross();

                                }
                                else if (IsRSIWithinXBars("10m", 3, ">", 69))
                                {
                                    //TryShortSMACross();
                                    //GoShortSMACross();
                                }

                            }
                            else if (sma5m[1] < sma20m[1] && sma5m[0] > sma20m[0] && rSI1m[0] < 85)
                            {
                                //TryLongSMACross();
                                GoLongSMACross();
                            }
                        }

                        if (ASRZ.GetNumberOfFullTradingDays() > 20 && ASRZ.WasZeroResX(0) && Position.MarketPosition == MarketPosition.Flat) //  if there is no resistance
                        {
                            double currentAsk = GetCurrentAsk();
                            var lp = currentAsk + (currentAsk * 0.05);
                            var lph = currentAsk + (currentAsk * 0.03);
                            var sp = currentAsk - (currentAsk * 0.05);
                            var sph = currentAsk - (currentAsk * 0.03);
                            if (ASRZ.WasZeroResX(1))
                            {
                                GoLongNoResistance(sp, sph, lp, lph, true, 20, 50);
                            }
                            else // first day of zero res in a while
                            {
                                GoLongNoResistance(sp, sph, lp, lph, true, 200, 200);
                            }
                        }

                        TryZoneOrders();
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