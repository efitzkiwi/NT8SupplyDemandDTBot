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
using System.Diagnostics;
using NinjaTrader.Vendor;
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
        private OrderFlowVWAP OFVWAP;


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
        int currentOrderClassification = -1;
        int currentOrderCategory = -1;
        bool hasLongRSIOrderBeenAdjusted = false;
        bool hasGenericProtectionOrderBeenAdjusted = false;
        bool hasPriorityOrderBeenAdjusted = false;
        bool useSmaLineAsLongStopLoss = false;
        bool usingProfitMaximization = false;
        bool usingProfitProtection = false;
        bool ProcessingOrder = false;
        bool CancelOCOAndExitLongOrder = false;
        bool CancelOCOAndExitShortOrder = false;
        bool ProtectOverlapFromCancelDelayToEnter = false;
        private ZoneBox ActiveZoneBox = null;
        private ZoneBox EntryZoneBox = null;
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
        int NumFullTradingDays = 0;
        

        double longRSIProfitMaximizationThreshold = 0.009;
        double longSMACROSSProfitMaximizationThreshold = 0.011;
        double longZONEProfitMaximizationThreshold = 0.012;
        double longINFLECTIONProfitMaximizationThreshold = 0.012;
        double longNOWALLSProfitMaximizationThreshold = 0.012;

        double shortRSIProfitMaximizationThreshold = 0.009;
        double shortSMACROSSProfitMaximizationThreshold = 0.011;
        double shortZONEProfitMaximizationThreshold = 0.012;
        double shortINFLECTIONProfitMaximizationThreshold = 0.01;
        double shortNOWALLSProfitMaximizationThreshold = 0.01;

        int vwapBounceOrderType = VWAPBouncePrices.None;

        int vwapStopPrice = VWAPBounceTypes.None;
        int vwapConditionCount = 0;
        bool useVwapConditionalStop = false;
        int vwapConditionTarget = 0;

        private Series<ZoneBox> ActiveZones;
        private Series<Double[]> SlopeOfSlope;

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
                TimeInForce = TimeInForce.Day;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelCloseIgnoreRejects;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 1;
                // Disable this property for performance gains in Strategy Analyzer optimizations
                // See the Help Guide for additional information
                IsInstantiatedOnEachOptimizationIteration = true;
                // Indicator vars
                AreaStrengthMultiplier = 1500;
                TimeThreshold = 45; // Minutes
                ProxyStrengthMultiplier = 500;
                NewZoneStrength = 60;
                DaysToLoadZones = 50;
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
                ShortStopLossPercent = 0.2;
                LongStopLossPercent = 0.3;
                ShortProfitPercent = 0.7;
                LongProfitPercent = 0.8;
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
                ASRZ = AdvancedSRZones(AreaStrengthMultiplier, TimeThreshold, ProxyStrengthMultiplier, NewZoneStrength, DaysToLoadZones, NewZoneTopMultiplier, NewZoneBottomMultiplier, ResZoneColor, SupZoneColor, BreakStrengthMultiplier, UseVolAccumulation, Expiration, MaxMergeCount, MergeThreshold);
                AddChartIndicator(ASRZ);
                RVOL = RelativeVolumeNT8(60, 2, 30);
                //AddChartIndicator(RVOL);
                rSI1m = RSI(14, 3);
                //AddChartIndicator(rSI1m);
                rSI30m = RSI(BarsArray[1], 14, 3);
                rSI10m = RSI(BarsArray[3], 14, 3);
                sma5m = SMA(5);
                sma6m = SMA(6);
                sma20m = SMA(20);
                sma50m = SMA(50);
                LRSDaily = LinRegSlope(BarsArray[2], 20);
                ActiveZones = new Series<ZoneBox>(this, MaximumBarsLookBack.Infinite);
                SlopeOfSlope = new Series<Double[]>(this, MaximumBarsLookBack.Infinite);
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

        struct OrderCategories
        {
            public static int
                Long = 0,
                Short = 1;
        }

        public enum OrderClassifications : int
        {
            Zone = 0,
            RSI = 1,
            SMA_Cross = 2,
            Inflection = 3,
            No_Walls = 4,
            VWAP = 5
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
            if (longOrder != null && execution.Order == longOrder && longOrder.OrderAction == OrderAction.Buy)
            {
                if (debugPrint) Print(" >>>>>>>>>>>>>>>> N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R |");
                string str = string.Empty;
                if (currentOrderClassification != -1)
                {
                    str = ((OrderClassifications)currentOrderClassification).ToString();
                }
                if (debugPrint) Print("ORDER_EXECUTE: Long order filled at " + longOrder.AverageFillPrice + " | Order type " + "Long " + str + " | quantity: " + longOrder.Quantity + " time " + Time[0]);
                ProcessingOrder = false;
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
            else if (shortOrder != null && execution.Order == shortOrder && shortOrder.OrderAction == OrderAction.SellShort)
            {
                if (debugPrint) Print(" >>>>>>>>>>>>>>>> N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R | N E W _ O R D E R |");
                string str = string.Empty;
                if (currentOrderClassification != -1)
                {
                    str = ((OrderClassifications)currentOrderClassification).ToString();
                }
                if (debugPrint) Print("ORDER_EXECUTE: Short order filled at " + shortOrder.AverageFillPrice + " | Order type " + "Short " + str + " | quantity: " + shortOrder.Quantity + " time " + Time[0]);
                ProcessingOrder = false;
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
                if (debugPrint) Print("ORDER_TRY GoLongAfterExit ");
                GoLongZone();
                GoLongAfterExit = false;
            }
            else if (GoShortAfterExit)
            {
                if (debugPrint) Print("ORDER_TRY GoShortAfterExit ");
                GoShortZone();
                GoShortAfterExit = false;
            }
        }


        public void TryResubmitOCO(Order ord)
        {
            if (currentOrderCategory == (int)OrderCategories.Short)
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
            else if (currentOrderCategory == (int)OrderCategories.Long)
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
            double price = currentOrderCategory == (int)OrderCategories.Long ? GetCurrentBid() - GetCurrentBid()*0.05 : GetCurrentAsk() + GetCurrentAsk() * 0.05;
            //Print(price);
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


        /// <summary>
        /// Go long vwap with default stops/limits
        /// </summary>
        public int GoLongVWAP(int vwbt, bool useVwAsStop = false, int conditionTarget = 2)
        {
            vwapBounceOrderType = vwbt;
            shareQuantity = GetShareQuantity();
            double sp = Close[0] - (Close[0] * (LongStopLossPercent / 100) * orderMultiplier);
            double lp = Close[0] + (Close[0] * (LongProfitPercent / 100) * orderMultiplier);
            double spH = ScaleHalf ? Close[0] - (Close[0] * (LongStopLossPercent / 200) * orderMultiplier) : 0;
            double lpH = ScaleHalf ? Close[0] + (Close[0] * (LongProfitPercent / 200) * orderMultiplier) : 0;
            //Print(orderMultiplier + " | " + Close[0]);
            if (useVwAsStop)
            {
                UseVwapConditionalStop(conditionTarget);
            }
            TrySubmitLongOrder(lp, lpH, lp, sp, spH, sp, (int)OrderClassifications.VWAP, false);
            return 1;
        }

        /// <summary>
        /// Go Long vwap with custom stops/limits
        /// </summary>
        public int GoLongVWAP(double stop, double stopH, double limit, double limitH, int vwbt, bool useVwAsStop = false, int conditionTarget = 2, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            vwapBounceOrderType = vwbt;
            shareQuantity = GetShareQuantity();
            if (useVwAsStop)
            {
                UseVwapConditionalStop(conditionTarget);
            }
            TrySubmitLongOrder(limit, limitH, limit, stop, stopH, stop, (int)OrderClassifications.VWAP, usesmasl, smaslFast, smaslSlow);
            return 1;
        }

        ///*
        /// <summary>
        /// Go short vwap with default stops/limits
        /// </summary>
        public int GoShortVWAP(int vwbt, bool useVwAsStop = false, int conditionTarget = 2)
        {
            vwapBounceOrderType = vwbt;
            shareQuantity = GetShareQuantity();
            double sp = Close[0] + (Close[0] * (ShortStopLossPercent / 100) * orderMultiplier);
            double lp = Close[0] - (Close[0] * (ShortProfitPercent / 100) * orderMultiplier);
            double spH = ScaleHalf ? Close[0] + (Close[0] * (ShortStopLossPercent / 200) * orderMultiplier) : 0;
            double lpH = ScaleHalf ? Close[0] - (Close[0] * (ShortProfitPercent / 200) * orderMultiplier) : 0;
            //Print(orderMultiplier + " | " + Close[0]);
            if (useVwAsStop)
            {
                UseVwapConditionalStop(conditionTarget);
            }
            TrySubmitShortOrder(lp, lpH, lp, sp, spH, sp, (int)OrderClassifications.VWAP, false);
            return 1;
        }

        /// <summary>
        /// Go short vwap with custom stops/limits
        /// </summary>
        public int GoShortVWAP(double stop, double stopH, double limit, double limitH, int vwbt, bool useVwAsStop = false, int conditionTarget = 2, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            vwapBounceOrderType = vwbt;
            shareQuantity = GetShareQuantity();
            if (useVwAsStop)
            {
                UseVwapConditionalStop(conditionTarget);
            }
            TrySubmitShortOrder(limit, limitH, limit, stop, stopH, stop, (int)OrderClassifications.VWAP, usesmasl, smaslFast, smaslSlow);
            return 1;
        }




        ///*
        /// <summary>
        /// Go short zone with default stops/limits
        /// </summary>
        public int GoShortZone()
        {
            shareQuantity = GetShareQuantity();
            if (ActiveZoneBox != null) EntryZoneBox = ActiveZoneBox;
            double sp = Close[0] + (Close[0] * (ShortStopLossPercent / 100) * orderMultiplier);
            double lp = Close[0] - (Close[0] * (ShortProfitPercent / 100) * orderMultiplier);
            double spH = ScaleHalf ? Close[0] + (Close[0] * (ShortStopLossPercent / 200) * orderMultiplier) : 0;
            double lpH = ScaleHalf ? Close[0] - (Close[0] * (ShortProfitPercent / 200) * orderMultiplier) : 0;
            //Print(orderMultiplier + " | " + Close[0]);
            TrySubmitShortOrder(lp, lpH, lp, sp, spH, sp, (int)OrderClassifications.Zone, false);
            return 1;
        }

        /// <summary>
        /// Go short zone with custom stops/limits
        /// </summary>
        public int GoShortZone(double stop, double stopH, double limit, double limitH, bool usesmasl=false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            if (ActiveZoneBox != null) EntryZoneBox = ActiveZoneBox;
            TrySubmitShortOrder(limit, limitH, limit, stop, stopH, stop, (int)OrderClassifications.Zone, usesmasl, smaslFast, smaslSlow);
            return 1;
        }


        /// <summary>
        /// Go long zone with default stops/limits
        /// </summary>
        public int GoLongZone()
        {
            shareQuantity = GetShareQuantity();
            if (ActiveZoneBox != null) EntryZoneBox = ActiveZoneBox;
            double sp = Close[0] - (Close[0] * (LongStopLossPercent / 100) * orderMultiplier);
            double lp = Close[0] + (Close[0] * (LongProfitPercent / 100) * orderMultiplier);
            double spH = ScaleHalf ? Close[0] - (Close[0] * (LongStopLossPercent / 200) * orderMultiplier) : 0;
            double lpH = ScaleHalf ? Close[0] + (Close[0] * (LongProfitPercent / 200) * orderMultiplier) : 0;
            //Print(orderMultiplier + " | " + Close[0]);
            TrySubmitLongOrder(lp, lpH, lp, sp, spH, sp, (int)OrderClassifications.Zone, false);
            return 1;
        }

        /// <summary>
        /// Go Long zone with custom stops/limits
        /// </summary>
        public int GoLongZone(double stop, double stopH, double limit, double limitH, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            if (ActiveZoneBox != null) EntryZoneBox = ActiveZoneBox;
            TrySubmitLongOrder(limit, limitH, limit, stop, stopH, stop, (int)OrderClassifications.Zone, usesmasl, smaslFast, smaslSlow);
            return 1;
        }

        public int GoShortSMACross()
        {
            shareQuantity = GetShareQuantity();
            double sp = Close[0] + (Close[0] * (0.095 / 100) * orderMultiplier);
            double lp = Close[0] - (Close[0] * (0.24 / 100) * orderMultiplier);
            double spH = ScaleHalf ? Close[0] + (Close[0] * (0.06 / 100) * orderMultiplier) : 0;
            double lpH = ScaleHalf ? Close[0] - (Close[0] * (0.085 / 100) * orderMultiplier) : 0;
            TrySubmitShortOrder(lp, lpH, lp, sp, spH, sp, (int)OrderClassifications.SMA_Cross, false);
            return 1;

        }

        public int GoShortSMACross(double stop, double stopH, double limit, double limitH, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            TrySubmitLongOrder(limit, limitH, limit, stop, stopH, stop, (int)OrderClassifications.SMA_Cross, usesmasl, smaslFast, smaslSlow);
            return 1;
        }

        public int GoLongSMACross()
        {
            shareQuantity = GetShareQuantity();
            double sp = Close[0] - (Close[0] * (0.09 / 100) * orderMultiplier);
            double lp = Close[0] + (Close[0] * (0.1 / 100) * orderMultiplier);
            double spH = ScaleHalf ? Close[0] - (Close[0] * (0.045 / 100) * orderMultiplier) : 0;
            double lpH = ScaleHalf ? Close[0] + (Close[0] * (0.09 / 100) * orderMultiplier) : 0;
            TrySubmitLongOrder(lp, lpH, lp, sp, spH, sp, (int)OrderClassifications.SMA_Cross, true);
            return 1;
        }

        public int GoLongSMACross(double stop, double stopH, double limit, double limitH, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            TrySubmitLongOrder(limit, limitH, limit, stop, stopH, stop, (int)OrderClassifications.SMA_Cross, usesmasl, smaslFast, smaslSlow);
            return 1;
        }

        public int GoLongInflectionPoint()
        {
            shareQuantity = GetShareQuantity();
            double sp = Close[0] - (Close[0] * (0.05 / 100));
            double lp = Close[0] + (Close[0] * (0.05 / 100));
            double spH = ScaleHalf ? Close[0] - (Close[0] * (0.05 / 100)) : 0;
            double lpH = ScaleHalf ? Close[0] + (Close[0] * (0.025 / 100)) : 0;
            TrySubmitLongOrder(lp, lpH, lp, sp, spH, sp, (int)OrderClassifications.Inflection, false);
            return 1;

        }

        public int GoLongInflectionPoint(double stop, double stopH, double limit, double limitH, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            TrySubmitLongOrder(limit, limitH, limit, stop, stopH, stop, (int)OrderClassifications.Inflection, usesmasl, smaslFast, smaslSlow);
            return 1;

        }

        public int GoShortInflectionPoint()
        {
            shareQuantity = GetShareQuantity();
            double sp = Close[0] + (Close[0] * (0.095 / 100) * orderMultiplier);
            double lp = Close[0] - (Close[0] * (0.24 / 100) * orderMultiplier);
            double spH = ScaleHalf ? Close[0] + (Close[0] * (0.06 / 100) * orderMultiplier) : 0;
            double lpH = ScaleHalf ? Close[0] - (Close[0] * (0.085 / 100) * orderMultiplier) : 0;
            TrySubmitShortOrder(lp, lpH, lp, sp, spH, sp, (int)OrderClassifications.Inflection, false);
            return 1;

        }

        public int GoShortInflectionPoint(double stop, double stopH, double limit, double limitH, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            TrySubmitShortOrder(limit, limitH, limit, stop, stopH, stop, (int)OrderClassifications.Inflection, usesmasl, smaslFast, smaslSlow);
            return 1;

        }


        public int GoLongNoResistance()
        {
            shareQuantity = GetShareQuantity();
            double sp = Close[0] - (Close[0] * (0.05 / 100));
            double lp = Close[0] + (Close[0] * (0.05 / 100));
            double spH = ScaleHalf ? Close[0] - (Close[0] * (0.05 / 100)) : 0;
            double lpH = ScaleHalf ? Close[0] + (Close[0] * (0.025 / 100)) : 0;
            TrySubmitLongOrder(lp, lpH, lp, sp, spH, sp, (int)OrderClassifications.No_Walls, false);
            return 1;

        }

        public int GoLongNoResistance(double stop, double stopH, double limit, double limitH, bool usesmasl = false, int smaslFast = 20, int smaslSlow = 50)
        {
            shareQuantity = GetShareQuantity();
            TrySubmitLongOrder(limit, limitH, limit, stop, stopH, stop, (int)OrderClassifications.No_Walls, usesmasl, smaslFast, smaslSlow);
            return 1;
        }




        public int GoLongRSI30mOversold()
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
                    TrySubmitLongOrder(lp, lpH, lp, sp, spH, sp, (int)OrderClassifications.RSI, false);
                    return 1;
                }
                // not shorting overbought RSI because the stocks are generally BULLISH and we'd die.
            }
            return 0;
        }
        

        public void Try10mRSIProfitProtection()
        {
            // If the 10m RSI is oversold 
            if (CurrentBars[3] > rSI10m.Period - 1)
            {

                if (longOrder != null && currentOrderCategory == (int)OrderCategories.Long && limitOrder != null && stopOrder != null && hasPriorityOrderBeenAdjusted == false)
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


                if (shortOrder != null && currentOrderCategory == (int)OrderCategories.Short && limitOrder != null && stopOrder != null && hasPriorityOrderBeenAdjusted == false)
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

        public void TryInflectionOrders()
        {
            if (Position.MarketPosition == MarketPosition.Flat)
            {
                if (IsInflection((int)InflectionTypes.Down, 5))
                {
                    GoShortInflectionPoint();
                }
                else if (IsInflection((int)InflectionTypes.Up, 5))
                {
                    GoLongInflectionPoint();
                }
            }
            else if (Position.MarketPosition == MarketPosition.Long)
            {
                if (IsInflection((int)InflectionTypes.Down, 5))
                {
                    //GoShortAfterExit = true;
                    ExitViaLimitOrder(GetCurrentBid());
                }
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                if (IsInflection((int)InflectionTypes.Up, 5))
                {
                    //GoLongAfterExit = true;
                    ExitViaLimitOrder(GetCurrentAsk());
                }
            }
        }

        public void TryZoneOrders(int zonesWithinBars = 0, int inflectionWithinBars = 1, int inflectionPer = 3)
        {
            double LST = LongStrengthThreshold + (LongStrengthThreshold * (-PriceAdjustedDailySMASlope));
            double STT = ShortStrengthThreshold + (ShortStrengthThreshold * (-PriceAdjustedDailySMASlope));
            LST = 0;
            STT = 0;
            //double strength = ASRZ.GetZoneStrength(ASRZ.GetCurrentZone());
            int direction = ASRZ.GetZoneType(ActiveZoneBox);
            int zoneDaysAlive = ASRZ.GetZoneDaysAlive(ActiveZoneBox);
            bool SZBStrength = ASRZ.DoesZoneStrengthComply(ActiveZoneBox, ">", STT);
            bool LZBSStrength = ASRZ.DoesZoneStrengthComply(ActiveZoneBox, ">", LST);
            if (NumFullTradingDays < 5) return;


            if (ActiveZoneBox.Type == (int)ZoneBox.Types.None && EntryZoneBox != null && EntryZoneBox.Type != (int)ZoneBox.Types.None)
            {
                if (Position.MarketPosition == MarketPosition.Short && GetCurrentAsk() > EntryZoneBox.TopPrice)
                {
                    GoLongAfterExit = true;
                    ExitViaLimitOrder(GetCurrentAsk());
                    return;
                }
                else if (Position.MarketPosition == MarketPosition.Long && GetCurrentAsk() < EntryZoneBox.BottomPrice)
                {
                    GoShortAfterExit = true;
                    ExitViaLimitOrder(GetCurrentBid());
                    return;
                }
                else if (Position.MarketPosition == MarketPosition.Long && GetCurrentAsk() > EntryZoneBox.TopPrice)
                {
                    ChangeStopLoss(longOrder.AverageFillPrice, longOrder.AverageFillPrice, longOrder.AverageFillPrice, "Breakeven stoploss long zone");
                    return;
                }
                else if (Position.MarketPosition == MarketPosition.Short && GetCurrentBid() < EntryZoneBox.BottomPrice)
                {
                    ChangeStopLoss(shortOrder.AverageFillPrice, shortOrder.AverageFillPrice, shortOrder.AverageFillPrice, "Breakeven stoploss short zone");
                    return;
                }
            }

            if (Position.MarketPosition == MarketPosition.Short && GetCurrentBid() < shortOrder.AverageFillPrice - shortOrder.AverageFillPrice * 0.002)
            {
                ChangeStopLoss((shortOrder.AverageFillPrice + GetCurrentBid()) / 2, shortOrder.AverageFillPrice, shortOrder.AverageFillPrice, "Profit protection stoploss short zone");
                return;
            }
            else if (Position.MarketPosition == MarketPosition.Long && GetCurrentAsk() > longOrder.AverageFillPrice - longOrder.AverageFillPrice * 0.003)
            {
                ChangeStopLoss((longOrder.AverageFillPrice + GetCurrentAsk()) / 2, longOrder.AverageFillPrice, longOrder.AverageFillPrice, "Profit protection stoploss long zone");
                return;
            }


            ZoneBox tempBox;
            for (int i = 0; i <= zonesWithinBars; i++)
            {
                tempBox = ActiveZones[i];
                if (tempBox == null) continue;
                if (tempBox.Type == (int)ZoneBox.Types.Demand)
                {
                    if (Position.MarketPosition == MarketPosition.Flat)
                    {
                        if (IsInflection((int)InflectionTypes.Up, inflectionPer, inflectionWithinBars))
                        {
                            GoLongZone();
                            break;
                        }
                    }
                    else if (Position.MarketPosition == MarketPosition.Short)
                    {
                        if (EntryZoneBox != null)
                        {
                            if (tempBox.ID != EntryZoneBox.ID)
                            {
                                //GoLongAfterExit = true;
                                ExitViaLimitOrder(GetCurrentAsk());
                                break;
                            }
                        }
                    }
                }
                else if (tempBox.Type == (int)ZoneBox.Types.Supply)
                {
                    if (Position.MarketPosition == MarketPosition.Flat)
                    {
                        if (IsInflection((int)InflectionTypes.Down, inflectionPer, inflectionWithinBars))
                        {
                            GoShortZone();
                            break;
                        }
                    }
                    else if (Position.MarketPosition == MarketPosition.Long)
                    {
                        if (EntryZoneBox != null)
                        {
                            if (tempBox.ID != EntryZoneBox.ID)
                            {
                                //GoShortAfterExit = true;
                                ExitViaLimitOrder(GetCurrentBid());
                                break;
                            }
                        }
                    }

                }
            }

        }

        struct VWAPBounceTypes
        {
            public const int
                Upper1 = 0,
                Lower1 = 1,
                Upper2 = 2,
                Lower2 = 3,
                Upper3 = 4,
                Lower3 = 5,
                Avg = 6,
                None = 7;
        };

        struct VWAPBouncePrices
        {
            public const int
                Low = 0,
                High = 1,
                None = 2;
        };


        public int TryVWAPOrders(int waitXMinsFromClose, double threshold, int inflectionPer, int barsBack = 1, int smaPeriod = 3)
        {
            double thres = threshold;
            SMA smaCompare = SMA(Low, smaPeriod);
            //double priceToCompare = SMA(smaPeriod)[0];
            int prevVWAPBounceType = vwapBounceOrderType;
            int vwapBounceType = VWAPBounceTypes.None;
            int VWAPBouncePrice = VWAPBouncePrices.None;
            int bounceBarsAgo = 0;
            double bouncePrice = 0;
            double vwapPrice = 0;

            for (int i = 0; i < barsBack; i++)
            {
                if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Lower[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Lower1;
                    VWAPBouncePrice = VWAPBouncePrices.Low;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Lower[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Lower[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Lower2;
                    VWAPBouncePrice = VWAPBouncePrices.Low;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Lower[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Lower[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Lower3;
                    VWAPBouncePrice = VWAPBouncePrices.Low;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Lower[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Upper[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Upper1;
                    VWAPBouncePrice = VWAPBouncePrices.Low;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Upper[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Upper[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Upper2;
                    VWAPBouncePrice = VWAPBouncePrices.Low;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Upper[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Upper[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Upper3;
                    VWAPBouncePrice = VWAPBouncePrices.Low;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Upper[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).VWAP[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Avg;
                    VWAPBouncePrice = VWAPBouncePrices.Low;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).VWAP[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                
                smaCompare = SMA(High, smaPeriod);

                if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Lower[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Lower1;
                    VWAPBouncePrice = VWAPBouncePrices.High;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Lower[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Lower[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Lower2;
                    VWAPBouncePrice = VWAPBouncePrices.High;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Lower[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Lower[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Lower3;
                    VWAPBouncePrice = VWAPBouncePrices.High;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Lower[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Upper[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Upper1;
                    VWAPBouncePrice = VWAPBouncePrices.High;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Upper[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Upper[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Upper2;
                    VWAPBouncePrice = VWAPBouncePrices.High;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Upper[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Upper[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Upper3;
                    VWAPBouncePrice = VWAPBouncePrices.High;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Upper[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                else if (IsNumWithinBands(smaCompare[i], OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).VWAP[i], thres) != 0)
                {
                    vwapBounceType = VWAPBounceTypes.Avg;
                    VWAPBouncePrice = VWAPBouncePrices.High;
                    vwapPrice = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).VWAP[1];
                    bounceBarsAgo = i;
                    bouncePrice = smaCompare[i];
                }
                
            }



            // implement stop based on slope of \
            bool zbNull = true;
            if (ActiveZoneBox != null) zbNull = false;
            int ret = 0;
            bool allowRiskyEntries = false;
            bool useLinRegSlope = false;
            if (vwapBounceType != VWAPBounceTypes.None)
            {

                double pricee = 0;
                if (vwapBounceType == VWAPBounceTypes.Avg) pricee = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).VWAP[0];
                else if (vwapBounceType == VWAPBounceTypes.Upper1) pricee = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Upper[0];
                else if (vwapBounceType == VWAPBounceTypes.Upper2) pricee = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Upper[0];
                else if (vwapBounceType == VWAPBounceTypes.Upper3) pricee = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Upper[0];
                else if (vwapBounceType == VWAPBounceTypes.Lower1) pricee = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Lower[0];
                else if (vwapBounceType == VWAPBounceTypes.Lower2) pricee = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Lower[0];
                else if (vwapBounceType == VWAPBounceTypes.Lower3) pricee = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Lower[0];
                if (waitXMinsFromClose > 0 && (Time[0] - sessBegin).TotalMinutes < waitXMinsFromClose) return 0;

                if (Position.MarketPosition == MarketPosition.Flat)
                {
                    double stop = 0;
                    double stoph = 0;
                    double limit = 0;
                    double limith = 0;

                    /*
                    if (!zbNull && ActiveZoneBox.Type == (int)ZoneBox.Types.Demand)
                    {
                        if (Close[0] >= vwapPrice)
                        {
                            ret = GoLongVWAP(GetStopPrice(OrderCategories.Long, 0.5), GetStopPrice(OrderCategories.Long, 0.3), GetProfitPrice(OrderCategories.Long, 0.52), GetProfitPrice(OrderCategories.Long, 0.4), vwapBounceType, true, 2);
                        }
                    }
                    */
                    if (IsInflection((int)InflectionTypes.Up, inflectionPer))
                    //if (CandleReverse((int)InflectionTypes.Up))
                    {

                            if (Close[0] >= vwapPrice && IsNumWithinBands(Close[0], pricee, thres) != 0)
                            {
                                stop = 0.7;
                                stoph = 0.8;
                                limit = 0.5;
                                limith = 0.25;
                                ret = GoLongVWAP(GetStopPrice(OrderCategories.Long, stop), GetStopPrice(OrderCategories.Long, stoph), GetProfitPrice(OrderCategories.Long, limit), GetProfitPrice(OrderCategories.Long, limith), vwapBounceType, true, 2);
                                //Draw.Diamond(this, vwapBounceType.ToString() + " " + (CurrentBar - bounceBarsAgo).ToString(), true, Bars.GetTime(CurrentBar - bounceBarsAgo), bouncePrice, Brushes.Green);
                            }
                            else if (allowRiskyEntries)
                            {
                                stop = 0.4;
                                stoph = 0.3;
                                limit = 0.4;
                                limith = 0.3;
                                ret = GoLongVWAP(GetStopPrice(OrderCategories.Long, stop), GetStopPrice(OrderCategories.Long, stoph), GetProfitPrice(OrderCategories.Long, limit), GetProfitPrice(OrderCategories.Long, limith), vwapBounceType, true, 2);

                            }

                        
                        /*
                        else
                        {
                            stop = 0.25;
                            stoph = 0.2;
                            limit = 0.22;
                            limith = 0.15;
                        }
                        if (Close[0] >= vwapPrice)
                        {
                            ret = GoLongVWAP(GetStopPrice(OrderCategories.Long, stop), GetStopPrice(OrderCategories.Long, stoph), GetProfitPrice(OrderCategories.Long, limit), GetProfitPrice(OrderCategories.Long, limith), vwapBounceType, true, 2);
                            Draw.Diamond(this, vwapBounceType.ToString() + " " + (CurrentBar - bounceBarsAgo).ToString(), true, Bars.GetTime(CurrentBar - bounceBarsAgo), bouncePrice, Brushes.Green);
                        }
                        */

                    }
                    /*
                    if (!zbNull && ActiveZoneBox.Type == (int)ZoneBox.Types.Supply)
                    {
                        if (Close[0] <= vwapPrice)
                        {
                            ret = GoShortVWAP(GetStopPrice(OrderCategories.Short, 0.37), GetStopPrice(OrderCategories.Short, 0.165), GetProfitPrice(OrderCategories.Short, 0.4), GetProfitPrice(OrderCategories.Short, 0.3), vwapBounceType, true, 2);
                        }

                    }
                    */
                    else if (IsInflection((int)InflectionTypes.Down, inflectionPer))
                    //else if (CandleReverse((int)InflectionTypes.Down))
                    {

                            if (Close[0] <= vwapPrice && IsNumWithinBands(Close[0], pricee, thres) != 0)
                            {
                                stop = 0.6;
                                stoph = 0.7;
                                limit = 0.45;
                                limith = 0.25;
                                ret = GoShortVWAP(GetStopPrice(OrderCategories.Short, stop), GetStopPrice(OrderCategories.Short, stoph), GetProfitPrice(OrderCategories.Short, limit), GetProfitPrice(OrderCategories.Short, limith), vwapBounceType, true, 2);
                                //Draw.Diamond(this, vwapBounceType.ToString() + " " + (CurrentBar - bounceBarsAgo).ToString(), true, Bars.GetTime(CurrentBar - bounceBarsAgo), bouncePrice, Brushes.Red);
                            }
                            else if (allowRiskyEntries)
                            {
                                stop = 0.3;
                                stoph = 0.2;
                                limit = 0.28;
                                limith = 0.20;
                                ret = GoShortVWAP(GetStopPrice(OrderCategories.Short, stop), GetStopPrice(OrderCategories.Short, stoph), GetProfitPrice(OrderCategories.Short, limit), GetProfitPrice(OrderCategories.Short, limith), vwapBounceType, true, 2);

                                //Draw.Diamond(this, vwapBounceType.ToString() + " " + (CurrentBar - bounceBarsAgo).ToString(), true, Bars.GetTime(CurrentBar - bounceBarsAgo), bouncePrice, Brushes.Red);
                            }

                        
                        /*
                        else
                        {
                            stop = 0.25;
                            stoph = 0.2;
                            limit = 0.2;
                            limith = 0.10;
                        }
                        if (Close[0] <= vwapPrice)
                        {
                            ret = GoShortVWAP(GetStopPrice(OrderCategories.Short, stop), GetStopPrice(OrderCategories.Short, stoph), GetProfitPrice(OrderCategories.Short, limit), GetProfitPrice(OrderCategories.Short, limith), vwapBounceType, true, 2);
                            Draw.Diamond(this, vwapBounceType.ToString() + " " + (CurrentBar - bounceBarsAgo).ToString(), true, Bars.GetTime(CurrentBar - bounceBarsAgo), bouncePrice, Brushes.Red);
                        }
                        */
                    }
                }


                if (IsInflection((int)InflectionTypes.Up, inflectionPer))
                //if (CandleReverse((int)InflectionTypes.Up))
                {

                    Draw.Diamond(this, vwapBounceType.ToString() + " " + (CurrentBar - bounceBarsAgo).ToString(), true, Bars.GetTime(CurrentBar - bounceBarsAgo), bouncePrice, Brushes.Green);
                }
                else if (IsInflection((int)InflectionTypes.Down, inflectionPer))
                //else if (CandleReverse((int)InflectionTypes.Down))
                {

                    Draw.Diamond(this, vwapBounceType.ToString() + " " + (CurrentBar - bounceBarsAgo).ToString(), true, Bars.GetTime(CurrentBar - bounceBarsAgo), bouncePrice, Brushes.Red);
                }




                else if (Position.MarketPosition == MarketPosition.Short && currentOrderClassification == (int)OrderClassifications.VWAP)
                {
                    if (prevVWAPBounceType != vwapBounceType)
                    {
                        ExitViaLimitOrder(Close[0]);
                    }
                }
                else if (Position.MarketPosition == MarketPosition.Long && currentOrderClassification == (int)OrderClassifications.VWAP)
                {
                    if (prevVWAPBounceType != vwapBounceType)
                    {
                        ExitViaLimitOrder(Close[0]);
                    }
                }
                
                /*
                double pricee = 0;
                if (VWAPBouncePrice == VWAPBouncePrices.Low) pricee = Low[1];
                else if (VWAPBouncePrice == VWAPBouncePrices.High) pricee = High[1];
                if (LinRegSlope(6)[0] < 0)
                {
                    Draw.Diamond(this, vwapBounceType.ToString() + " " + (CurrentBar - 1).ToString(), true, Bars.GetTime(CurrentBar - 1), pricee, Brushes.Green);
                }
                else if (LinRegSlope(6)[0] > 0)
                {
                    Draw.Diamond(this, vwapBounceType.ToString() + " " + (CurrentBar - 1).ToString(), true, Bars.GetTime(CurrentBar - 1), pricee, Brushes.Red);
                }
                else
                {
                    Draw.Diamond(this, vwapBounceType.ToString() + " " + (CurrentBar - 1).ToString(), true, Bars.GetTime(CurrentBar - 1), pricee, Brushes.White);
                }
                */
                
            }
            return ret;
        }


        public void TryUseSMAAsStopLoss(int period, int halfPeriod)
        {
            if (useSmaLineAsLongStopLoss && CurrentBar > period && CurrentBar > halfPeriod)
            {
                Order refOrder = null;
                if (currentOrderCategory == (int)OrderCategories.Long) refOrder = longOrder;
                else if (currentOrderCategory == (int)OrderCategories.Short) refOrder = shortOrder;
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

            double tempstr = 0;
            switch(currentOrderClassification) 
            {
                case (int)OrderClassifications.Zone:
                    if (currentOrderCategory == (int)OrderCategories.Long)
                    {
                        tempstr = longZONEProfitMaximizationThreshold;
                        break;
                    }
                    else if (currentOrderCategory == (int)OrderCategories.Short)
                    {
                        tempstr = shortZONEProfitMaximizationThreshold;
                        break;
                    }
                    break;

                case (int)OrderClassifications.SMA_Cross:
                    if (currentOrderCategory == (int)OrderCategories.Long)
                    {
                        tempstr = longSMACROSSProfitMaximizationThreshold;
                        break;
                    }
                    else if (currentOrderCategory == (int)OrderCategories.Short)
                    {
                        tempstr = shortSMACROSSProfitMaximizationThreshold;
                        break;
                    }
                    break;


                case (int)OrderClassifications.RSI:
                    if (currentOrderCategory == (int)OrderCategories.Long)
                    {
                        tempstr = longRSIProfitMaximizationThreshold;
                        break;
                    }
                    else if (currentOrderCategory == (int)OrderCategories.Short)
                    {
                        tempstr = shortRSIProfitMaximizationThreshold;
                        break;
                    }
                    break;


                case (int)OrderClassifications.Inflection:
                    if (currentOrderCategory == (int)OrderCategories.Long)
                    {
                        tempstr = longINFLECTIONProfitMaximizationThreshold;
                        break;
                    }
                    else if (currentOrderCategory == (int)OrderCategories.Short)
                    {
                        tempstr = shortINFLECTIONProfitMaximizationThreshold;
                        break;
                    }
                    break;

                case (int)OrderClassifications.No_Walls:
                    if (currentOrderCategory == (int)OrderCategories.Long)
                    {
                        tempstr = longNOWALLSProfitMaximizationThreshold;
                        break;
                    }
                    else if (currentOrderCategory == (int)OrderCategories.Short)
                    {
                        tempstr = shortNOWALLSProfitMaximizationThreshold;
                        break;
                    }
                    break;
            }

            if (currentOrderCategory == (int)OrderCategories.Long)
            {
                if (currentOrderClassification == (int)OrderClassifications.Zone)
                {
                    if (GetCurrentBid() < longOrder.AverageFillPrice + longOrder.AverageFillPrice * tempstr)
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
            }
            else if (currentOrderCategory == (int)OrderCategories.Short)
            {
                if (GetCurrentAsk() > shortOrder.AverageFillPrice - shortOrder.AverageFillPrice * tempstr)
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
                        if (currentOrderClassification == (int)OrderClassifications.Zone)
                        {
                            if (IsInflection((int)InflectionTypes.Down, 3) && GetCurrentBid() > longOrder.AverageFillPrice && IsGapDown())
                            {
                                Draw.Rectangle(this, "askdmkasm" + CurrentBar, 0, High[0], 1, Low[1], Brushes.White);
                                ChangeStopLoss(longOrder.AverageFillPrice, (longOrder.AverageFillPrice + Close[0]) / 2, (longOrder.AverageFillPrice + Close[0]) / 2);
                            }
                        }
                        else if (currentOrderClassification == (int)OrderClassifications.RSI)
                        {

                        }
                        else if (currentOrderClassification == (int)OrderClassifications.SMA_Cross)
                        {

                        }
                        else if (currentOrderClassification == (int)OrderClassifications.Inflection)
                        {

                        }


                    }
                    else if (usingProfitMaximization)
                    {
                        if (debugDraw) Draw.Rectangle(this, "askdmkasm" + CurrentBar, 0, High[0], 1, Low[1], Brushes.Orange);
                        if (currentOrderClassification == (int)OrderClassifications.Zone)
                        {
                            if (useSmaLineAsLongStopLoss == false && CurrentBar > sma20m.Period - 1 && CurrentBar > sma50m.Period - 1)
                            {
                                useSmaLineAsLongStopLoss = true;
                                TryUseSMAAsStopLoss(20, 50);
                            }
                        }
                        else if (currentOrderClassification == (int)OrderClassifications.RSI)
                        {

                        }
                        else if (currentOrderClassification == (int)OrderClassifications.SMA_Cross)
                        {

                        }
                        else if (currentOrderClassification == (int)OrderClassifications.Inflection)
                        {

                        }
                    }

                }
                else if (Position.MarketPosition == MarketPosition.Short && shortOrder != null)
                {
                    if (usingProfitProtection)
                    {
                        if (debugDraw) Draw.Rectangle(this, "askdmkasm" + CurrentBar, 0, High[0], 1, Low[1], Brushes.Yellow);
                        if (currentOrderClassification == (int)OrderClassifications.Zone)
                        {
                            if (IsInflection((int)InflectionTypes.Up, 3) && GetCurrentAsk() < shortOrder.AverageFillPrice)
                            {
                                Draw.Rectangle(this, "askdmkasm" + CurrentBar, 0, High[0], 1, Low[1], Brushes.Black);
                                //ChangeStopLoss(shortOrder.AverageFillPrice, (shortOrder.AverageFillPrice + Close[0]) / 2, (shortOrder.AverageFillPrice + Close[0]) / 2);
                            }
                        }
                        else if (currentOrderClassification == (int)OrderClassifications.RSI)
                        {

                        }
                        else if (currentOrderClassification == (int)OrderClassifications.SMA_Cross)
                        {

                        }
                        else if (currentOrderClassification == (int)OrderClassifications.Inflection)
                        {

                        }
                    }
                    else if (usingProfitMaximization)
                    {
                        if (debugDraw) Draw.Rectangle(this, "askdmkasm" + CurrentBar, 0, High[0], 1, Low[1], Brushes.Purple);
                        if (currentOrderClassification == (int)OrderClassifications.Zone)
                        {

                        }
                        else if (currentOrderClassification == (int)OrderClassifications.RSI)
                        {

                        }
                        else if (currentOrderClassification == (int)OrderClassifications.SMA_Cross)
                        {

                        }
                        else if (currentOrderClassification == (int)OrderClassifications.Inflection)
                        {

                        }
                    }

                }

            }


            #region specific protection
            // LOOKING FOR BEARISH SIGNALS TO PROTECT A CURRENT LONG THAT HASN'T PRODUCED GOOD PROFIT ALREADY
            if (longOrder != null && currentOrderClassification == (int)OrderClassifications.RSI && currentOrderCategory == (int)OrderCategories.Long && limitOrder != null && stopOrder != null && hasLongRSIOrderBeenAdjusted == false)
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

        
        private double[] calculateSlopeOfSlope(HMACustom thisCalculatedHMA)
        {

            double[] outputArray = new double[2];

            double firstDerivCondition = 0;
            double secondDerivCondition = 0;



            return outputArray;
        }

        public void HandleSlopeOfSlope(int hmalen)
        {
            HMACustom calculatedHMA = HMACustom(Medians[0], hmalen);
            SlopeOfSlope[0] = calculateSlopeOfSlope(calculatedHMA);
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

        /// <summary>
        /// Compare numbers inside threshold
        /// </summary>
        /// <param name="a">Number to compare</param>
        /// <param name="b">Number to compare against</param>
        /// <param name="threshold">% Threshold for comparison</param>
        /// <returns></returns>
        public int IsNumWithinBands(double a, double b, double threshold)
        {
            if (a >= b - (b * threshold) && a <= b + (b * threshold))
            {
                if (a >= b) return 1;
                else if (a <= b) return -1;
            }
            return 0;
        }

        public enum InflectionTypes
        {
            Up = 0,
            Down = 1
        }

        public bool IsInflection(int direction, int period, int withinBars = 1, int smooth = 1, bool detrend = false, InputSeriesType ist = InputSeriesType.SMA, NormType nt = NormType.None) // "Up" or "Down"
        {
            SlopeEnhancedOp refLRS = SlopeEnhancedOp(period, 56, smooth, detrend, ist, nt, Brushes.Green, Brushes.Red, PlotStyle.Bar);
            for (int i = 1; i <= withinBars; i++)
            {
                if (CurrentBar > period + 1)
                {
                    if (direction == (int)InflectionTypes.Up && refLRS[i] < 0 && refLRS[i-1] > 0)
                    {
                        return true;
                    }
                    else if (direction == (int)InflectionTypes.Down && refLRS[i] > 0 && refLRS[i-1] < 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void ResetHelperVars()
        {
            currentOrderClassification = -1;
            currentOrderCategory = -1;
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
            else if (Position.MarketPosition == MarketPosition.Short) price = price + price * 0.01;
            else if (Position.MarketPosition == MarketPosition.Long) price = price - price * 0.01;
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

        public void UseVwapConditionalStop(int conditionTarget)
        {
            vwapStopPrice = 0;
            vwapConditionCount = 0;
            vwapConditionTarget = conditionTarget;
            useVwapConditionalStop = true;
        }


        public void CheckVwapConditionalStop()
        {

            if (!useVwapConditionalStop || currentOrderClassification != (int)OrderClassifications.VWAP) return;
            double price = 0;
            if (vwapBounceOrderType == VWAPBounceTypes.Avg) price = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).VWAP[0];
            else if (vwapBounceOrderType == VWAPBounceTypes.Upper1) price = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Upper[0];
            else if (vwapBounceOrderType == VWAPBounceTypes.Upper2) price = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Upper[0];
            else if (vwapBounceOrderType == VWAPBounceTypes.Upper3) price = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Upper[0];
            else if (vwapBounceOrderType == VWAPBounceTypes.Lower1) price = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev1Lower[0];
            else if (vwapBounceOrderType == VWAPBounceTypes.Lower2) price = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev2Lower[0];
            else if (vwapBounceOrderType == VWAPBounceTypes.Lower3) price = OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).StdDev3Lower[0];
            if (currentOrderCategory == OrderCategories.Long && Close[0] < price)
            {
                vwapConditionCount++;
            }
            else if (currentOrderCategory == OrderCategories.Short && Close[0] > price)
            {
                vwapConditionCount++;
            }
            if (vwapConditionCount >= vwapConditionTarget)
            {
                ExitViaLimitOrder(Close[0]);
                useVwapConditionalStop = false;
            }
        }


        public void CheckVwapProfitProtection()
        {

            if (currentOrderClassification != (int)OrderClassifications.VWAP) return;
            if (currentOrderCategory == OrderCategories.Long && Position.MarketPosition == MarketPosition.Long)
            {
                if (longOrder == null) return;
                if (IsInflection((int)InflectionTypes.Down, 6) && Close[0] > longOrder.AverageFillPrice)
                {
                    ChangeStopLoss((longOrder.AverageFillPrice + Close[0]) / 2, longOrder.AverageFillPrice, longOrder.AverageFillPrice, "Vwap Long Stop Order adjusted");
                }
            }
            else if (currentOrderCategory == OrderCategories.Short && Position.MarketPosition == MarketPosition.Short)
            {
                if (shortOrder == null) return;
                if (IsInflection((int)InflectionTypes.Up, 6) && Close[0] > shortOrder.AverageFillPrice)
                {
                    ChangeStopLoss((shortOrder.AverageFillPrice + Close[0]) / 2, shortOrder.AverageFillPrice, shortOrder.AverageFillPrice, "Vwap Short Stop Order adjusted");
                }
            }
        }


        public bool CandleReverse(int direction)
        {
            if (direction == (int)InflectionTypes.Down && Close[1] > Open[1] && Close[0] < Open[0])
            {
                return true;
            }
            if (direction == (int)InflectionTypes.Up && Close[1] < Open[1] && Close[0] > Open[0])
            {
                return true;
            }
            return false;

        }


        /// <summary>
        /// Return profit target based on last close and percent
        /// </summary>
        /// <param name="marketDirection"></param>
        /// <param name="perc"></param>
        /// <param name="multi"></param>
        /// <returns></returns>
        public double GetProfitPrice(int marketDirection, double perc, double multi=1)
        {
            if (marketDirection == (int)OrderCategories.Long)
            {
                return Close[0] + (Close[0] * (perc / 100) * multi);
            }
            else
            {
                return Close[0] - (Close[0] * (perc / 100) * multi);
            }
        }


        /// <summary>
        /// Return profit target based on last close and percent
        /// </summary>
        /// <param name="marketDirection"></param>
        /// <param name="perc"></param>
        /// <param name="multi"></param>
        /// <returns></returns>
        public double GetProfitPriceHalf(int marketDirection, double perc, double multi = 1)
        {
            if (marketDirection == (int)OrderCategories.Long)
            {
                return Close[0] + (Close[0] * (perc / 100) * multi);
            }
            else
            {
                return Close[0] - (Close[0] * (perc / 100) * multi);
            }
        }

        /// <summary>
        /// Return stop target based on last close and percent
        /// </summary>
        /// <param name="marketDirection"></param>
        /// <param name="perc"></param>
        /// <param name="multi"></param>
        /// <returns></returns>
        public double GetStopPrice(int marketDirection, double perc, double multi = 1)
        {
            if (marketDirection == (int)OrderCategories.Long)
            {
                return Close[0] - (Close[0] * (perc / 100) * multi);
            }
            else
            {
                return Close[0] + (Close[0] * (perc / 100) * multi);
            }
        }

        /// <summary>
        /// Return stop target based on last close and percent
        /// </summary>
        /// <param name="marketDirection"></param>
        /// <param name="perc"></param>
        /// <param name="multi"></param>
        /// <returns></returns>
        public double GetStopPriceHalf(int marketDirection, double perc, double multi = 1)
        {
            if (marketDirection == (int)OrderCategories.Long)
            {
                return Close[0] - (Close[0] * (perc / 100) * multi);
            }
            else
            {
                return Close[0] + (Close[0] * (perc / 100) * multi);
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

        public bool IsHammer(bool drawDiamonds = false)
        {
            if (Math.Abs(Open[0] - Close[0]) < ASRZ.CandleStats.GetAvgOpenCloseHeightNormalized() - ASRZ.CandleStats.GetOpenCloseHeightNormalizedStDev())
            {
                if (Math.Abs(High[0] - Low[0] - (High[0] - Math.Abs(Open[0] + Close[0]) / 2)) / (High[0] - Low[0]) < ASRZ.CandleStats.GetHighMinusLowMinusOpenDivCloseAvg() - ASRZ.CandleStats.GetHighMinusLowMinusOpenDivCloseStDev()/2)
                {
                    if (drawDiamonds) Draw.Diamond(this, "hammer down " + CurrentBar, true, Bars.GetTime(CurrentBar), High[0], Brushes.Red);
                    return true;
                }
                else if (Math.Abs(High[0] - Low[0] - (High[0] - Math.Abs(Open[0] + Close[0]) / 2)) / (High[0] - Low[0]) > ASRZ.CandleStats.GetHighMinusLowMinusOpenDivCloseAvg() + ASRZ.CandleStats.GetHighMinusLowMinusOpenDivCloseStDev()/2)
                {
                    if (drawDiamonds) Draw.Diamond(this, "hammer up " + CurrentBar, true, Bars.GetTime(CurrentBar), Low[0], Brushes.Green);
                    return true;
                }
            }
            return false;
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
        public int TrySubmitShortOrder(double lp, double lph, double lpNh, double sp, double sph, double spNh, int msg, bool useSmaSL, int smaslFast=20, int smaslSlow=50)
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
                /*
                if (ProcessingOrder)
                {
                    if (debugPrint) Print("ERROR: Processing previous Long order entry ");
                    return -1;
                }
                ProcessingOrder = true;
                */
                useSmaLineAsLongStopLoss = useSmaSL;
                smaSLSLOW = smaslSlow;
                smaSLFAST = smaslFast;
                ocoString = "Short order" + " T: " + DateTime.Now.ToString("hhmmssffff");
                string mesgg = "Short " + ((OrderClassifications)msg).ToString();
                currentOrderClassification = msg;
                currentOrderCategory = (int)OrderCategories.Short;
                if ((sessEnd - Time[0]).TotalSeconds > sessionEndSeconds) shortOrder = SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Market, shareQuantity, 0, 0, ocoString, mesgg);
                else if (debugPrint) Print("ERROR: Cannot submit short order after session end threshold");
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
        public int TrySubmitLongOrder(double lp, double lph, double lpNh, double sp, double sph, double spNh, int msg, bool useSmaSL, int smaslFast=20, int smaslSlow=50)
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
                /*
                if (ProcessingOrder)
                {
                    if (debugPrint) Print("ERROR: Processing previous Long order entry ");
                    return -1;
                }
                ProcessingOrder = true;
                */
                useSmaLineAsLongStopLoss = useSmaSL;
                smaSLSLOW = smaslSlow;
                smaSLFAST = smaslFast;
                ocoString = "Long order" + " T: " + DateTime.Now.ToString("hhmmssffff");
                string mesgg = "Long " + ((OrderClassifications)msg).ToString();
                currentOrderClassification = msg;
                currentOrderCategory = (int)OrderCategories.Long;
                if ((sessEnd - Time[0]).TotalSeconds > sessionEndSeconds) longOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, shareQuantity, 0, 0, ocoString, mesgg);
                else if (debugPrint) Print("ERROR: Cannot submit long order after session end threshold");
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
            rSI1m.Update();


            if (CurrentBars[0] < BarsRequiredToTrade) return;

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
                    PriceAdjustedDailySMASlope = (LRSDaily[0] / Close[0]) * 10;
                }
            }

            if (BarsInProgress == 1)
            {



                rSI30m.Update();
                //GoLongRSI30mOversold();

                //return;
            }


            if (BarsInProgress == 0)
            {
                // disable this if you don't want to use zones in the strategy, it will speed things up 10x
                ASRZ.Update();
                // Update gaps, current session start, etc
                UpdateDayEvents();
                IsHammer();
                // Update the current zonebox, if it exists
                ActiveZoneBox = ASRZ.GetCurrentZone();
                ActiveZones[0] = ActiveZoneBox;
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
                        NumFullTradingDays++;
                    }

                }

                if (CurrentBar >= 0)
                {

                    // Try SMA inflection entries
                    //TryInflectionOrders();

                    if (ASRZ.ZoneBoxList.Count > 0)
                    {
                        //TryInflectionOrders();
                        if (UseZoneStrengthOrderMultiplier)
                        {
                            orderMultiplier = 1;
                            if (ActiveZoneBox != null && ActiveZoneBox.Type != (int)ZoneBox.Types.None)
                            {
                                orderMultiplier = Math.Floor(ZoneStrengthOrderScale * Math.Log(ASRZ.GetZoneStrength(ActiveZoneBox) + 1)) / 100;
                            }
                        }
                        // If the order was designated to use an SMA as a stoploss, this function updates the order


                        if (currentOrderCategory == (int)OrderCategories.Long && currentOrderClassification == (int)OrderClassifications.SMA_Cross) TryUseSMAAsStopLoss(6, 6);
                        else if (currentOrderCategory == (int)OrderCategories.Short && currentOrderClassification == (int)OrderClassifications.SMA_Cross) TryUseSMAAsStopLoss(6, 6);
                        else if (currentOrderCategory == (int)OrderCategories.Long && currentOrderClassification == (int)OrderClassifications.No_Walls) TryUseSMAAsStopLoss(smaSLFAST, smaSLSLOW);
                        //else TryUseSMAAsStopLoss(20, 50);


                        // Generic order OCO exit handling to make sure we keep some profits on the table
                        // Checking longs and shorts if they need to be exited
                        // Manage open RSI orders
                        // If RSI limit order goes above certain percent, raise the stop to breakeven
                        //TryOCOProfitProtection();
                        //UpdateOrderProfitThreshold();


                        if (CurrentBar > sma20m.Period - 1 && CurrentBar > sma50m.Period - 1 && CurrentBar > sma5m.Period - 1 && CurrentBar > sma6m.Period)
                        {
                            if (sma20m[1] > sma50m[1] && sma20m[0] < sma50m[0])
                            {

                                if (rSI1m[0] > 30)
                                {

                                    //TryShortSMACross();
                                    //GoShortSMACross();
                                    //return;

                                }
                                else if (IsRSIWithinXBars("10m", 3, ">", 69))
                                {
                                    //TryShortSMACross();
                                    //GoShortSMACross();
                                    //return;
                                }

                            }
                            else if (sma5m[1] < sma20m[1] && sma5m[0] > sma20m[0] && rSI1m[0] < 85)
                            {
                                //TryLongSMACross();
                                //GoLongSMACross();
                                //return;
                            }
                        }

                        /*
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
                                return;
                            }
                            else // first day of zero res in a while
                            {
                                GoLongNoResistance(sp, sph, lp, lph, true, 200, 200);
                                return;
                            }
                        }
                        */

                        // implement stops if bar closes below the entry vwap then exit
                        // eg if 2 bars close below vwap, exit (when long)

                        /*
                        if (currentOrderClassification == (int)OrderClassifications.VWAP && vwapBounceOrderType == VWAPBounceTypes.Avg)
                        {
                            if (Position.MarketPosition == MarketPosition.Long)
                            {
                                if (longOrder.AverageFillPrice > OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).VWAP[0] &&  Close[0] < OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).VWAP[0])
                                {
                                    ExitViaLimitOrder(Close[0] - Close[0] * 0.002);
                                }
                            }
                            else if (Position.MarketPosition == MarketPosition.Short)
                            {
                                if (shortOrder.AverageFillPrice < OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).VWAP[0] && Close[0] > OrderFlowVWAP(VWAPResolution.Standard, Bars.TradingHours, VWAPStandardDeviations.Three, 1, 2, 3).VWAP[0])
                                {
                                    ExitViaLimitOrder(Close[0] + Close[0] * 0.002);
                                }
                            }
                        }
                        */
                        //CheckVwapProfitProtection();
                        /*
                        CheckVwapConditionalStop();
                        int ordered = 0;
                        if (ordered == 0)
                        {
                            ordered = TryVWAPOrders(30, 0.001, 3, 1, 3);
                        }
                        if (ordered == 0)
                        {
                            TryZoneOrders();
                        }
                        */
                        int hmalen = 20;
                        HandleSlopeOfSlope(hmalen);
                        TryZoneOrders(2,1,6);
                    }
                }

            }

        }

        #region properties
        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "Area strength threshold multiplier", Description = "Multiplies the minimum threshold required for zones to gain strength", Order = 1, GroupName = "Parameters")]
        public int AreaStrengthMultiplier
        { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Time Threshold", Description = "Amount of time in minutes the bot uses as a limit for adjusting integral zone strength", Order = 2, GroupName = "Parameters")]
        public int TimeThreshold
        { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "Proxy strength multiplier", Description = "Proximity of pivots in Ticks to determine where S/R level will be", Order = 3, GroupName = "Parameters")]
        public int ProxyStrengthMultiplier
        { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "New zone strength", Description = "Default pre-computed strength of a new zone. Equation is 5 * Sqrt ( input ) ", Order = 4, GroupName = "Parameters")]
        public int NewZoneStrength
        { get; set; }

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Days to load zones", Description = "How many days to load to calculate zones", Order = 5, GroupName = "Parameters")]
        public int DaysToLoadZones
        { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "New zone top multiplier", Description = "Multiplier amount to extend the top of a new zone upon creation", Order = 6, GroupName = "Parameters")]
        public double NewZoneTopMultiplier
        { get; set; }

        [Browsable(false)]
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

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Long zone strength", Description = "The cumulative strength of the current bar a zone is in that triggers a long order", Order = 11, GroupName = "Parameters")]
        public double LongStrengthThreshold
        { get; set; }


        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Short zone strength", Description = "The cumulative strength of the current bar a zone is in that triggers a short order", Order = 12, GroupName = "Parameters")]
        public double ShortStrengthThreshold
        { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Display(Name = "Zone strength order multiplier", Description = "Adjusts stop loss and profit taking based on percieved zone strength upon entry condition. ", Order = 13, GroupName = "Parameters")]
        public bool UseZoneStrengthOrderMultiplier
        { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(50, 200)]
        [Display(Name = "Zone strength order scale", Description = "Adjusts the zone scale equation. Higher = wider stop losses and limit sells", Order = 14, GroupName = "Parameters")]
        public int ZoneStrengthOrderScale
        { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(int.MinValue, int.MaxValue)]
        [Display(Name = "Break strength multiplier", Description = "Multiplier for threshold strength applied if a zone is broken", Order = 15, GroupName = "Parameters")]
        public double BreakStrengthMultiplier
        { get; set; }

        [Browsable(false)]
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

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Trade delay", Description = "Delay each trade condition by X minutes", Order = 21, GroupName = "Parameters")]
        public int TradeDelay
        { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Delay exit (minutes)", Description = "Prevents rapid, unnecessary orders from firing in a tight area of zones", Order = 22, GroupName = "Parameters")]
        public int DelayExitMinutes
        { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Zone expiration", Description = "Number of days it takes for a bar to expire. -1 means they never expire", Order = 23, GroupName = "Parameters")]
        public int Expiration
        { get; set; }

        [Browsable(false)]
        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Max merge count", Description = "Maximum number of times a zone can be merged", Order = 24, GroupName = "Parameters")]
        public int MaxMergeCount
        { get; set; }

        [Browsable(false)]
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