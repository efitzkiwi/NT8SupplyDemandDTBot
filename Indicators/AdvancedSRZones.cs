//### 
//### Dynamic SR Lines
//###
//### User		Date 		
//### ------	-------- 	
//### Eoin Fitzpatrick	
//###
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
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Gui.DrawingTools;
using NinjaTrader.Gui.NinjaScript;
using System.Threading;
using System.Security.Cryptography;
using System.Windows.Navigation;
using System.Collections.Specialized;
using SharpDX;
#endregion


/*
 * TODO TO DO
 * A strong zone is defined by both price action and volume. Price that consistently bounces off a zone makes the zone stronger. A high amount of total volume inside the zone makes it stronger
 * A strong zone can still break, and it will break with high volume. Breaking a zone with high volume does not necessarily make it weaker
 * 
 */

/*
 * 
 * http://amunategui.github.io/unconventional-convolutional-networks/
 * 
MAKING ADVANCED SUPPORT/RESISTANCE LINES

Converting what humans view as a stock price reversal into machine code is not easy
There are several factors that make up a support and resistance zone:
    Price action (Candle)
    Volume (Shares traded)
    How long price remains in the zone (Period of Time)

First and foremost... zone type changes ALL THE TIME. It MUST BE DYNAMIC. 
    For example, a sup zone created 2 days ago BECOMES RESISTANCE once the price breaks below the zone and STAYS UNDER

Candlestick patterns are the first attribute checked to create a zone:
    Hammers
    Engulfing candles

Secondary strong price attributes include:
    Low of day
    High of day


Generally, the longer the price stays inside the designated zone, the stronger the zone.
Keep in mind, the strength of zones can vary based on each trading day. A zone created weeks ago is probably not as strong as a zone created yesterday.
Volume plays an important role in confirmation:
    The higher the volume INSIDE the zone, the STRONGER the zone
    If price breaks the zone and stays OUTSIDE for x amount of time, this zone SWITCHES TYPES (sup/res)

When a zone is created, it will either:
    Gain strength if price bounces off the zone
    Lose strength if price goes through the zone

A zone does not expire, it simply LOSES STRENGTH...
    But for the sake of memory limitations, the computer will need to cutoff the lookback length at some point since all zones are dynamically created.

Not all zones are perfect
    Humans are very good at finding patterns in images
    Computers need a THRESHOLD of error, unless you make a machine learning robot.

In cases where there are NO or LITTLE zones (no-man's land) virtual zones are created based on psychological factors
    A price with a zero at the end (50, 400, 20, etc)
    A meme number (69, 420, 420.69)

****** Machine code Translation ********
So how can we convert this into machine code?
Like all hard problems, we start from the bottom up
Look at ONE day of charting, 5m candles
Draw your own support and resistance lines that were respected ON THAT DAY only.
Machines go candle by candle - when the price starts churning inside this zone, the machine must understand CONSOLIDATION
Consolidation is a flat zone usually no wider than 0.15% of the stock price











NEW IDEA:

	Use volume profile to generate zones each day. This can be used in addition to price action for even stronger and more accurate zones. 
	To prevent clutter, a robust merge system must be implemented. This system will merge zones of close proximity into each other. 
	While these zones can be powerful, additional technical analysis can be used to make this strategy nearly perfect. 
		Market depth
		Market sentiment
		Candle patterns




NEW STATISTICS IDEA:
	FIND SKINNY ZONES AND MERGE THEM
	IF TWO ZONES ARE BELOW THE AVG ZONE HEIGHT "SIZE" MINUS SOME STANDARD DEVIATION
		AND IF YOU MERGE THE ZONES AND THE NEW CREATED ZONE IS **NO GREATER THAN THE AVG ZONE HEIGHT PLUS SOME STANDARD DEVIATION**
		MERGE THE ZONES TOGETHER USING THE HIGHER TOP AS NEW TOP AND LOWER LOW AS NEW LOW
		https://www.tutorialspoint.com/statistics/individual_series_standard_deviation.htm

*/

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{


	public class IDStep
	{
		private int Count { get; set; }
		public IDStep()
		{
			Count = 0;
		}
		public int GetCount()
		{
			int ret = Count;
			Count++;
			return ret;
		}
	}





	/// <summary>
	/// Useful so we don't need to deep copy the entire box to get statistics
	/// </summary>
	public class Candle
	{
		public int AbsBar { get; set; }
		public double Open { get; set; }
		public double Close { get; set; }
		public double Low { get; set; }
		public double High { get; set; }
		public double Volume { get; set; }
		public Candle(int absBar, double open, double close, double low, double high, double volume)
		{
			AbsBar = absBar;
			Open = open;
			Close = close;
			Low = low;
			High = high;
			Volume = volume;
		}
	}
	public class CandleStatistics
	{
		public Dictionary<int, Candle> CandleArchive;
		private List<double> BufferList;

		private double OpenCloseHeight;
		private double OpenCloseHeightGap;

		private double OpenCloseHeightNormalized;
		private double OpenCloseHeightNormalizedGap;

		private double OpenLowHeight;
		private double OpenLowHeightGap;

		private double OpenHighHeight;
		private double OpenHighHeightGap;

		private double CloseLowHeight;
		private double CloseLowHeightGap;

		private double CloseHighHeight;
		private double CloseHighHeightGap;

		private double LowHighHeight;
		private double LowHighHeightGap;

		private double HighMinusLowMinusOpenDivCloseAvg; // HAMMER candle 
		private double HighMinusLowMinusOpenDivCloseStdDev; // HAMMER candle 

		public CandleStatistics()
		{
			CandleArchive = new Dictionary<int, Candle>();
			OpenCloseHeight = 0;
			OpenCloseHeightGap = 0;

			OpenCloseHeightNormalized = 0;
			OpenCloseHeightNormalizedGap = 0;

			OpenLowHeight = 0;
			OpenLowHeightGap = 0;

			OpenHighHeight = 0;
			OpenHighHeightGap = 0;

			CloseLowHeight = 0;
			CloseLowHeightGap = 0;

			CloseHighHeight = 0;
			CloseHighHeightGap = 0;

			LowHighHeight = 0;
			LowHighHeightGap = 0;

			HighMinusLowMinusOpenDivCloseAvg = 0; // HAMMER candle 
			HighMinusLowMinusOpenDivCloseStdDev = 0;
		}
		public void UpdateArchive(int absBar, double open, double close, double low, double high, double volume)
		{
			if (!CandleArchive.ContainsKey(absBar))
			{
				CandleArchive.Add(absBar, new Candle(absBar, open, close, low, high, volume));
			}
			else
			{
				CandleArchive[absBar] = new Candle(absBar, open, close, low, high, volume); // shouldn't ever need this line
			}

			OpenCloseHeight += Math.Abs(open - close);
			OpenCloseHeightGap += Math.Pow((Math.Abs(open - close) - OpenCloseHeight / CandleArchive.Count), 2);

			if (high - low!=0)
			{
				OpenCloseHeightNormalized += Math.Abs(open - close) / (high-low);
				OpenCloseHeightNormalizedGap += Math.Pow(((Math.Abs(open - close) / (high - low)) - OpenCloseHeight / CandleArchive.Count), 2);
			}

			OpenLowHeight += Math.Abs(open - low);
			OpenLowHeightGap += Math.Pow((Math.Abs(open - low) - OpenLowHeight / CandleArchive.Count), 2);

			OpenHighHeight += Math.Abs(high - open);
			OpenHighHeightGap += Math.Pow((Math.Abs(high - open) - OpenHighHeight / CandleArchive.Count), 2);

			CloseLowHeight += Math.Abs(close - low);
			CloseLowHeightGap += Math.Pow((Math.Abs(close - low) - CloseLowHeight / CandleArchive.Count), 2);

			CloseHighHeight += Math.Abs(high - close);
			CloseHighHeightGap += Math.Pow((Math.Abs(high - close) - CloseHighHeight / CandleArchive.Count), 2);

			LowHighHeight += Math.Abs(high - low);
			LowHighHeightGap += Math.Pow((Math.Abs(high - low) - LowHighHeight / CandleArchive.Count), 2);

			if (high - low != 0)
			{
				HighMinusLowMinusOpenDivCloseAvg += Math.Abs(high - low - (high - Math.Abs(open + close) / 2)) / (high - low);
				HighMinusLowMinusOpenDivCloseStdDev += Math.Pow(((Math.Abs(high - low - (high - Math.Abs(open + close) / 2)) / (high - low)) - HighMinusLowMinusOpenDivCloseAvg / CandleArchive.Count), 2);
				
			}
		}

		public double GetAvgOpenCloseHeight()
		{
			return OpenCloseHeight / CandleArchive.Count;
		}
		public double GetOpenCloseHeightStDev()
		{
			return Math.Sqrt(OpenCloseHeightGap / CandleArchive.Count);
		}

		public double GetAvgOpenCloseHeightNormalized()
		{
			return OpenCloseHeightNormalized / CandleArchive.Count;
		}
		public double GetOpenCloseHeightNormalizedStDev()
		{
			return Math.Sqrt(OpenCloseHeightNormalizedGap / CandleArchive.Count);
		}

		public double GetAvgOpenLowHeight()
		{
			return OpenLowHeight / CandleArchive.Count;
		}
		public double GetOpenLowHeightStDev()
		{
			return Math.Sqrt(OpenLowHeightGap / CandleArchive.Count);
		}

		public double GetAvgOpenHighHeight()
		{
			return OpenHighHeight / CandleArchive.Count;
		}
		public double GetOpenHighHeightStDev()
		{
			return Math.Sqrt(OpenHighHeightGap / CandleArchive.Count);
		}

		public double GetAvgCloseLowHeight()
		{
			return CloseLowHeight / CandleArchive.Count;
		}
		public double GetCloseLowHeightStDev()
		{
			return Math.Sqrt(CloseLowHeightGap / CandleArchive.Count);
		}

		public double GetAvgCloseHighHeight()
		{
			return CloseHighHeight / CandleArchive.Count;
		}
		public double GetCloseHighHeightStDev()
		{
			return Math.Sqrt(CloseHighHeightGap / CandleArchive.Count);
		}

		public double GetAvgLowHighHeight()
		{
			return LowHighHeight / CandleArchive.Count;
		}
		public double GetLowHighHeightStDev()
		{
			return Math.Sqrt(LowHighHeightGap / CandleArchive.Count);
		}

		public double GetHighMinusLowMinusOpenDivCloseAvg()
		{
			return HighMinusLowMinusOpenDivCloseAvg / CandleArchive.Count;
		}
		public double GetHighMinusLowMinusOpenDivCloseStDev()
		{
			return Math.Sqrt(HighMinusLowMinusOpenDivCloseStdDev / CandleArchive.Count);
		}

	}




	/// <summary>
	/// Useful so we don't need to deep copy the entire box to get statistics
	/// </summary>
	public class ZBSHandler
	{
		public int ActiveLeftSideAbsBar { get; set; }
		public int ActiveRightSideAbsBar { get; set; }
		public int OriginalLeftSideAbsBar { get; set; }
		public int OriginalRightSideAbsBar { get; set; }
		public double TopPrice { get; set; }
		public double BottomPrice { get; set; }
		public double DailyVolumePeak { get; set; }
		public double DailyVolumePeakPrice { get; set; }
		public double TotalVolumePeak { get; set; }
		public double TotalVolumePeakPrice { get; set; }
		public double TotalVolumeLow { get; set; }
		public double TotalVolumeLowPrice { get; set; }
		public double Strength { get; set; }
		public double DailyVolumeWeightedAverageVolPrice { get; set; }
		public int DaysAlive { get; set; }
		public int ID { get; set; }
		public int Type { get; set; }
		public ZBSHandler(ZoneBox box)
		{
			ActiveLeftSideAbsBar = box.ActiveLeftSideAbsBar;
			ActiveRightSideAbsBar = box.ActiveRightSideAbsBar;
			OriginalLeftSideAbsBar = box.OriginalLeftSideAbsBar;
			OriginalRightSideAbsBar = box.OriginalRightSideAbsBar;
			TopPrice = box.TopPrice;
			BottomPrice = box.BottomPrice;
			DailyVolumePeak = box.DailyVolumePeak;
			DailyVolumePeakPrice = box.DailyVolumePeakPrice;
			TotalVolumePeak = box.TotalVolumePeak;
			TotalVolumePeakPrice = box.TotalVolumePeakPrice;
			TotalVolumeLow = box.TotalVolumeLow;
			TotalVolumeLowPrice = box.TotalVolumeLowPrice;
			Strength = box.Strength;
			BottomPrice = box.BottomPrice;
			DailyVolumeWeightedAverageVolPrice = box.DailyVolumeWeightedAverageVolPrice;
			DaysAlive = box.DaysAlive;
		}
	}
	public class ZoneBoxStatistics
	{
		public Dictionary<int, ZBSHandler> ZoneBoxArchive;
		public List<double> TopPriceMinusBottomPrice;
		public double HeightStandardDeviation { get; set; }
		public double HeightAverage { get; set; }
		public double AverageTimeAlive { get; set; }
		public ZoneBoxStatistics()
		{
			ZoneBoxArchive = new Dictionary<int, ZBSHandler>();
			TopPriceMinusBottomPrice = new List<double>();
			HeightAverage = 0;
			AverageTimeAlive = 0;
		}
		public void UpdateArchive(ZoneBox box)
		{
			if (!ZoneBoxArchive.ContainsKey(box.ID))
			{
				ZoneBoxArchive.Add(box.ID, new ZBSHandler(box));
			}
			else
			{
				ZoneBoxArchive[box.ID] = new ZBSHandler(box);
			}
		}
		public void CalculateStatistics()
		{
			CalculateAverageBoxHeight(); // set avg box height and standard deviation

		}
		public void CalculateAverageBoxHeight()
		{
			if (ZoneBoxArchive.Count == 0) return;
			double height = 0;
			TopPriceMinusBottomPrice.Clear();
			double diff = 0;
			foreach (KeyValuePair<int, ZBSHandler> group in ZoneBoxArchive)
			{
				diff = group.Value.TopPrice - group.Value.BottomPrice;
				height += diff;
				TopPriceMinusBottomPrice.Add(diff);
			}
			HeightAverage = height / ZoneBoxArchive.Count;

			diff = 0;
			foreach(double d in TopPriceMinusBottomPrice)
			{
				diff += Math.Pow((d - HeightAverage), 2);
			}
			HeightStandardDeviation = Math.Sqrt(diff / TopPriceMinusBottomPrice.Count);
		}
	}

	public class ZoneBox
	{
		private readonly AdvancedSRZones indicatorObjectRef;
		private Brush OutLineColor = Brushes.SlateGray;
		private Brush AreaColorRes = Brushes.Red;
		private Brush AreaColorSup = Brushes.Green;
		private double Opacity = 50;
		// STATISTICS
		public double DailyVolumePeak { get; set; }
		public double DailyVolumePeakPrice { get; set; }
		public double TotalVolumePeak { get; set; }
		public double TotalVolumePeakPrice { get; set; }
		public double TotalVolumeLow { get; set; }
		public double TotalVolumeLowPrice { get; set; }
		public bool IsPriceInside { get; set; }
		public double Strength { get; set; }
		public int ActiveLeftSideAbsBar { get; set; }
		public int ActiveRightSideAbsBar { get; set; }
		public int OriginalLeftSideAbsBar { get; set; }
		public int OriginalRightSideAbsBar { get; set; }
		public double TopPrice { get; set; }
		public double BottomPrice { get; set; }
		public bool IsActive { get; set; }
		public double TotalVolume { get; set; }
		private int SMAPeriodForCross { get; set; }
		private bool TrackingIntradayResBreak { get; set; }
		private bool TrackingIntradaySupBreak { get; set; }
		private int IntradayResBreakCount { get; set; }
		private int IntradaySupBreakCount { get; set; }
		public int LastIntradayBreakAbsBar { get; set; }
		public int DaysAlive { get; set; }
		public int DaysToLive { get; set; } // sets a timeout for this zone
		public int DaysToLiveConditionCounter { get; set; } // increment this to trigger the death of this zone
		public double DailyVolumeWeightedAverageVolPrice { get; set; } // calculate EOD
		public int ID { get; set; }
		public int Type { get; set; } // 0 = supp 1 = res
		public List<List<double>> DailyVolumeArray;
		public SortedDictionary<double, double> TotalVolumeDictionary;
		public ZoneBox(AdvancedSRZones obj, int LSAB, int RSAB, double TP, double BP)
		{
			indicatorObjectRef = obj;
			OriginalLeftSideAbsBar = ActiveLeftSideAbsBar = LSAB;
			OriginalRightSideAbsBar = ActiveRightSideAbsBar = RSAB;
			TopPrice = TP;
			BottomPrice = BP;
			if (TopPrice < BottomPrice)
			{
				var b = BottomPrice;
				BottomPrice = TopPrice;
				TopPrice = b;
			}
			ID = obj.globalIDStep.GetCount();
			DailyVolumeArray = new List<List<double>>();
			TotalVolumePeak = -1;
			TotalVolumePeakPrice = 0;
			TotalVolumeLow = double.MaxValue;
			TotalVolumeLowPrice = 0;
			TotalVolumeDictionary = new SortedDictionary<double, double>();
			TotalVolume = 0;
			SMAPeriodForCross = 20;
			TrackingIntradayResBreak = false;
			TrackingIntradaySupBreak = false;
			IntradayResBreakCount = 0;
			IntradaySupBreakCount = 0;
			LastIntradayBreakAbsBar = 0;
			DailyVolumePeak = 0;
			Strength = 100;
			DaysAlive = 0;
			DaysToLive = -1;
			DaysToLiveConditionCounter = -1;
			UpdateBox();
		}

		public void DisplayBox()
		{
			if (DaysAlive < 1) return;
			if (Type == 0) Draw.Rectangle(indicatorObjectRef, ID.ToString(), true, indicatorObjectRef.CurrentBar - ActiveLeftSideAbsBar, BottomPrice, indicatorObjectRef.CurrentBar - ActiveRightSideAbsBar, TopPrice, OutLineColor, AreaColorSup, (int)Opacity, true);
			else if (Type == 1) Draw.Rectangle(indicatorObjectRef, ID.ToString(), true, indicatorObjectRef.CurrentBar - ActiveLeftSideAbsBar, BottomPrice, indicatorObjectRef.CurrentBar - ActiveRightSideAbsBar, TopPrice, OutLineColor, AreaColorRes, (int)Opacity, true);

		}

		public void SetStyle(SolidColorBrush areaSup, SolidColorBrush areaRes, SolidColorBrush outline, int opac = 50)
		{
			AreaColorSup = areaSup;
			AreaColorRes = areaRes;
			OutLineColor = outline;
			Opacity = opac;
		}

		public void UpdateBox()
		{
			if (indicatorObjectRef.Bars.IsFirstBarOfSession)
			{

				// reset intraday utilities
				TotalVolume = 0;
				TrackingIntradayResBreak = false;
				TrackingIntradaySupBreak = false;
				IntradayResBreakCount = 0;
				IntradaySupBreakCount = 0;
				LastIntradayBreakAbsBar = 0;
				DaysAlive++;
				if (DaysToLive > 0 && DaysAlive > DaysToLive)
				{
					int id = ID;
					if (indicatorObjectRef.HideDrawObjects) indicatorObjectRef.RemoveDrawObject(id.ToString());
					indicatorObjectRef.ZoneBoxList.RemoveAll(b => b.ID == id);
				}
			}
			if (indicatorObjectRef.Bars.IsLastBarOfSession && DaysToLive > 0 && GetTotalIntradayBreakCount() == 0)
			{
				DaysToLive++;
			}
			if (TopPrice < BottomPrice)
			{
				var b = BottomPrice;
				BottomPrice = TopPrice;
				TopPrice = b;
			}
			// set type of zone
			ActiveRightSideAbsBar = indicatorObjectRef.CurrentBar;
			if (indicatorObjectRef.GetCurrentAsk() >= BottomPrice && indicatorObjectRef.GetCurrentAsk() <= TopPrice)
			{
				IsPriceInside = true;
			}
			else
			{
				IsPriceInside = false;
			}
			if (indicatorObjectRef.GetCurrentAsk() >= TopPrice && indicatorObjectRef.GetCurrentAsk() >= BottomPrice)
			{
				Type = 0;
			}
			else if (indicatorObjectRef.GetCurrentAsk() <= BottomPrice && indicatorObjectRef.GetCurrentAsk() <= TopPrice)
			{
				Type = 1;
			}
			// handler for intraday zone breaks
			if (indicatorObjectRef.CurrentBar > SMAPeriodForCross + 2)
			{

				if (indicatorObjectRef.SMA(SMAPeriodForCross)[2] < BottomPrice && indicatorObjectRef.SMA(SMAPeriodForCross)[1] < BottomPrice && indicatorObjectRef.SMA(SMAPeriodForCross)[0] >= BottomPrice)
				{
					TrackingIntradayResBreak = true;
				}
				else if (indicatorObjectRef.SMA(SMAPeriodForCross)[2] > TopPrice && indicatorObjectRef.SMA(SMAPeriodForCross)[1] > TopPrice && indicatorObjectRef.SMA(SMAPeriodForCross)[0] <= TopPrice)
				{
					TrackingIntradaySupBreak = true;
				}

				if (TrackingIntradayResBreak)
				{
					if (indicatorObjectRef.SMA(SMAPeriodForCross)[0] < BottomPrice)
					{
						TrackingIntradayResBreak = false;
					}
					else if (indicatorObjectRef.SMA(SMAPeriodForCross)[0] > TopPrice)
					{
						TrackingIntradayResBreak = false;
						IntradayResBreakCount++;
						LastIntradayBreakAbsBar = indicatorObjectRef.CurrentBar;
						Draw.TriangleUp(indicatorObjectRef, "up " + LastIntradayBreakAbsBar, true, indicatorObjectRef.Bars.GetTime(LastIntradayBreakAbsBar-1), TopPrice, Brushes.LawnGreen);
					}

				}
				else if (TrackingIntradaySupBreak && indicatorObjectRef.SMA(SMAPeriodForCross)[0] < BottomPrice)
				{
					if (indicatorObjectRef.SMA(SMAPeriodForCross)[0] > TopPrice)
					{
						TrackingIntradaySupBreak = false;
					}
					else if (indicatorObjectRef.SMA(SMAPeriodForCross)[0] < BottomPrice)
					{
						TrackingIntradaySupBreak = false;
						IntradaySupBreakCount++;
						LastIntradayBreakAbsBar = indicatorObjectRef.CurrentBar;
						Draw.TriangleDown(indicatorObjectRef, "up " + LastIntradayBreakAbsBar, true, indicatorObjectRef.Bars.GetTime(LastIntradayBreakAbsBar-1), BottomPrice, Brushes.Maroon);
					}
				}
			}
			// end
			// don't really know if volume occured in the zone recently, so just brute force it. A bit slow
			double price;
			for (int i = 0; i < indicatorObjectRef.priceHitsArray.GetLength(1); i++)
			{
				price = indicatorObjectRef.priceHitsArray[0, i];
				if (price <= TopPrice && price >= BottomPrice)
				{
					if (indicatorObjectRef.priceHitsArray[1, i] > TotalVolumePeak)
					{
						TotalVolumePeak = indicatorObjectRef.priceHitsArray[1, i];
						TotalVolumePeakPrice = indicatorObjectRef.priceHitsArray[0, i];
						if (TotalVolumePeak > indicatorObjectRef.BoxMaxVolume)
						{
							indicatorObjectRef.BoxMaxVolume = TotalVolumePeak;
							indicatorObjectRef.BoxMaxVolumePrice = TotalVolumePeakPrice;
						}
					}
					if (indicatorObjectRef.priceHitsArray[1, i] < TotalVolumeLow)
					{
						TotalVolumeLow = indicatorObjectRef.priceHitsArray[1, i];
						TotalVolumeLowPrice = indicatorObjectRef.priceHitsArray[0, i];
						if (TotalVolumeLow < indicatorObjectRef.BoxMinVolume)
						{
							indicatorObjectRef.BoxMinVolume = TotalVolumeLow;
							indicatorObjectRef.BoxMinVolumePrice = TotalVolumeLowPrice;
						}
					}
					if (!TotalVolumeDictionary.ContainsKey(price))
					{
						TotalVolumeDictionary.Add(price, indicatorObjectRef.priceHitsArray[1, i]);
					}
					else
					{
						TotalVolumeDictionary[price] += indicatorObjectRef.priceHitsArray[1, i];
					}
				}
			}
		}
		public double GetHeight()
		{
			return Math.Abs(TopPrice - BottomPrice);
		}
		public int GetTotalIntradayBreakCount()
		{
			return IntradayResBreakCount + IntradaySupBreakCount;
		}
		public double GetDailyVolume()
		{
			double price;
			double sum = 0;
			for (int i = 0; i < indicatorObjectRef.priceHitsArray.GetLength(1); i++)
			{
				price = indicatorObjectRef.priceHitsArray[0, i];
				if (price <= TopPrice && price >= BottomPrice)
				{
					sum += indicatorObjectRef.priceHitsArray[1, i];
				}
			}
			return sum;
		}
		/// <summary>
		/// Is ASK inside box
		/// </summary>
		/// <returns></returns>
		public bool IsCurrentAskInsideBox()
		{
			if (indicatorObjectRef.GetCurrentAsk() >= BottomPrice && indicatorObjectRef.GetCurrentAsk() <= TopPrice && indicatorObjectRef.CurrentBar >= ActiveLeftSideAbsBar && indicatorObjectRef.CurrentBar <= ActiveRightSideAbsBar)
			{
				SolidColorBrush color = Type == 1 ? Brushes.Blue : Brushes.White;
				//Draw.Diamond(indicatorObjectRef, "inside " + indicatorObjectRef.CurrentBar, true, indicatorObjectRef.Bars.GetTime(indicatorObjectRef.CurrentBar), indicatorObjectRef.GetCurrentAsk(), color);
				return true;
			}
			return false;
		}
	}




	public class AdvancedSRZones : Indicator
	{

		private SharpDX.Direct2D1.Brush dxBrush = null; // the SharpDX brush used for rendering      
		private System.Windows.Media.SolidColorBrush brushColor; // used to determine the color of the brush conditionally
		bool debugPrint = false;
		public int iLi = 0, jLi = 0, xLi = 0;
		int SMALength = 3;
		// Current low, current high, final high, final low
		double dayLow = Double.MaxValue, dayHigh = double.MinValue, dayTOP = Double.MinValue, dayBOTTOM = Double.MaxValue;
		DateTime dayHighTime, dayLowTime;
		int sum = 0;
		public double totalStrength = 0;
		public bool HideDrawObjects = true;

		// LinRegVars
		private int inflectionOffset = -0;

		private int ZonesAboveToday = -1;
		private int ZonesBelowToday = -1;

		public double[,] priceHitsArray;
		private Series<double> VolumeGraph;

		private List<Double> SMA_l = new List<double>();
		private List<Double> LRS_SMA = new List<double>();

		public List<bool> ZonesAboveExtrema = new List<bool>();
		public List<bool> ZonesBelowExtrema = new List<bool>();
		public Dictionary<int, double> HighOfDayDict = new Dictionary<int, double>();
		public Dictionary<int, double> LowOfDayDict = new Dictionary<int, double>();

		double zonesAboveTemp = 0;
		double zonesBelowTemp = 0;
		int NumFullTradingDays = 0;
		int dayStartBar = 0;
		int dayLastBar = 0;
		double slotHalfRange;
		SharpDX.Direct2D1.Brush sessBrush;
		private Brush slotSessionColor = Brushes.Lime;
		public readonly int TotalSlots = 300;
		double maxBar = 0;
		double maxPrice = 0;
		public double BoxMaxVolume = 0;
		public double BoxMaxVolumePrice = 0;
		public double BoxMinVolume = double.MaxValue;
		public double BoxMinVolumePrice = 0;




		List<List<double>> InflectionPoints = new List<List<double>>();
		double AvgArea = 40;
		public List<ZoneBox> ZoneBoxList = new List<ZoneBox>();
		public IDStep globalIDStep = new IDStep();
		public Series<double> evolvingBottom;
		public Series<double> evolvingPeak;
		public Series<double> evolvingTop;
		private ZoneBoxStatistics ZBoxStatistics;
		public CandleStatistics CandleStats;

		protected override void OnStateChange()
		{


			if (State == State.SetDefaults)
			{
				Description = @"Draws nearest level of S/R lines above and below current market based on historical price swing High/Low pivots";
				Name = "AdvancedSRZones";
				Calculate = Calculate.OnBarClose;
				IsOverlay = true;
				DisplayInDataBox = false;
				IsChartOnly = true;
				DrawOnPricePanel = true;
				DrawHorizontalGridLines = true;
				DrawVerticalGridLines = true;
				PaintPriceMarkers = true;
				ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive = true;


				AreaStrengthMultiplier = 5000;
				TimeThreshold = 30; // Minutes
				ProxyStrengthMultiplier = 1000;
				NewZoneStrength = 70;
				ZoneTimeoutStrength = -1;
				NewZoneTopMultiplier = 0.0005;
				NewZoneBottomMultiplier = 0.0005;
				ResZoneColor = Brushes.Red;
				SupZoneColor = Brushes.Green;
				BreakStrengthMultiplier = -2500;
				UseVolAccumulation = true;
				Expiration = 50; // Days (NOT trading days... just DAYS);
				MaxMergeCount = 2;
				MergeThreshold = 0.001;

			}
			else if (State == State.Configure)
			{
				//AddDataSeries(BarsPeriodType.Minute, 1);
				priceHitsArray = new double[2, TotalSlots];
				brushColor = Brushes.Red;
				ZBoxStatistics = new ZoneBoxStatistics();
				CandleStats = new CandleStatistics();

			}
			else if (State == State.Historical)
			{
				SetZOrder(-1); // default here is go below the bars and called in State.Historical
			}
			else if (State == State.DataLoaded)
			{
				evolvingBottom = new Series<double>(this,MaximumBarsLookBack.Infinite);
				evolvingPeak = new Series<double>(this, MaximumBarsLookBack.Infinite);
				evolvingTop = new Series<double>(this, MaximumBarsLookBack.Infinite);
			}
		}


		protected override void OnBarUpdate()
		{

			// Display high and low of each day


			if (CurrentBar >= 0)
			{

				CandleStats.UpdateArchive(CurrentBar, Open[0], Close[0], Low[0], High[0], VOL()[0]);

				// Do volume profile stuff
				//DrawVolumeProfileAccessories();

				foreach (ZoneBox box in ZoneBoxList.ToList())
				{
					box.UpdateBox();
					box.DisplayBox();
					box.IsCurrentAskInsideBox();
					// Perform statistics calculations
					ZBoxStatistics.UpdateArchive(box);
				}
				/*
				for (int i=0;i<ZoneBoxList.Count;i++)
				{
					ZoneBoxList[i].UpdateBox();
					ZoneBoxList[i].DisplayBox();
					// Perform statistics calculations
					ZBoxStatistics.UpdateArchive(ZoneBoxList[i]);
				}
				*/


				//Print(CurrentBar);
				if (Bars.IsFirstBarOfSession)
				{
					ResetSessionVars();
				}


				// inflection points stuff
				int periodI = 10;
				if (CurrentBar > periodI+1)
				{
					int totalTimeToCompare = 68;
					for (int i = 0; i < InflectionPoints.Count; i++)
					{
						double bar = InflectionPoints[i][0];
						double type = InflectionPoints[i][3];
						if (InflectionPoints[i][1] == 0 && InflectionPoints[i][2] < AvgArea)
						{
							if ((Bars.GetTime(CurrentBar) - Bars.GetTime((int)bar)).TotalMinutes > totalTimeToCompare/2)
							{
								// Invalid inflection point, never use it
								InflectionPoints[i][1] = -1;
							}
							// add more area to this inflection point
							if (type == 1) InflectionPoints[i][2] += Bars.GetHigh((int)bar) - SMA(periodI)[0];
							else if (type == 2) InflectionPoints[i][2] += SMA(periodI)[0] - Bars.GetLow((int)bar);
						}
						else if (InflectionPoints[i][2] >= AvgArea && InflectionPoints[i][1] != -1 && InflectionPoints[i][1] != 1)
						{
							// this point is a solid resistance point
							InflectionPoints[i][1] = 1;
							//if (type == 1) Draw.Diamond(this, "Inflection pt dn " + (int)bar, true, Bars.GetTime((int)bar), Bars.GetHigh((int)bar), Brushes.Red);
							//else if (type == 2) Draw.Diamond(this, "Inflection pt up " + (int)bar, true, Bars.GetTime((int)bar), Bars.GetLow((int)bar), Brushes.Green);

							int validPointExtrema = 0;
							int fastPeriod = 3;
							Brush outlineColor = Brushes.DarkSlateGray;
							Brush areaColor = Brushes.Khaki;
							double top = 0;
							double bottom = 0;
							if (type == 1)
							{
								for (int f = CurrentBar-(int)bar; f < CurrentBar-dayStartBar;f++)
								{
									//if ((Bars.GetTime(CurrentBar) - Bars.GetTime(f)).TotalMinutes > 30) break;
									if (IsInflection("Down", fastPeriod, 1, f))
									{
										validPointExtrema = CurrentBar - f;
										break;
									}
								}
								if (validPointExtrema != 0)
								{
									if (Bars.GetHigh(validPointExtrema - 1) > Bars.GetHigh(validPointExtrema)) validPointExtrema = validPointExtrema - 1;
									//Draw.Diamond(this, "Inflection pt dn extrema " + validPointExtrema, true, Bars.GetTime(validPointExtrema), Bars.GetHigh(validPointExtrema), Brushes.DarkRed);
									//Draw.Rectangle(this, "Inflection pt box " + validPointExtrema, true, CurrentBar-validPointExtrema, Bars.GetHigh(validPointExtrema), CurrentBar - (int)InflectionPoints[i][0], Bars.GetHigh((int)InflectionPoints[i][0]), outlineColor, areaColor, 80);
									// making sure the zone isn't insanely tiny
									top = Math.Max(Bars.GetHigh((int)bar), Bars.GetHigh(validPointExtrema));
									bottom = Math.Min(Bars.GetHigh((int)bar), Bars.GetHigh(validPointExtrema));
									/*
									if (top - bottom < ZBoxStatistics.HeightAverage - ZBoxStatistics.HeightStandardDeviation)
									{
										top = top + (ZBoxStatistics.HeightAverage / 2 + ZBoxStatistics.HeightStandardDeviation / 4);
										bottom = bottom - (ZBoxStatistics.HeightAverage / 2 + ZBoxStatistics.HeightStandardDeviation / 4);
									}
									*/
									ZoneBoxList.Add(new ZoneBox(this, validPointExtrema, (int)bar, top, bottom));
								}
							}
							else if (type == 2)
							{
								for (int f = CurrentBar-(int)bar; f < CurrentBar - dayStartBar; f++)
								{
									//if ((Bars.GetTime(CurrentBar) - Bars.GetTime(f)).TotalMinutes > 30) break;
									if (IsInflection("Up", fastPeriod, 1, f))
									{
										validPointExtrema = CurrentBar - f;
										break;
									}
								}
								if (validPointExtrema != 0)
								{
									if (Bars.GetLow(validPointExtrema - 1) < Bars.GetLow(validPointExtrema)) validPointExtrema = validPointExtrema - 1;
									//Draw.Diamond(this, "Inflection pt up extrema " + validPointExtrema, true, Bars.GetTime(validPointExtrema), Bars.GetLow(validPointExtrema), Brushes.LightGreen);
									//Draw.Rectangle(this, "Inflection pt box " + validPointExtrema, true, CurrentBar - validPointExtrema, Bars.GetLow(validPointExtrema), CurrentBar - (int)InflectionPoints[i][0], Bars.GetLow((int)InflectionPoints[i][0]), outlineColor, areaColor, 80);
									//SMA(periodI)[CurrentBar-validPointExtrema]
									// making sure the zone isn't insanely tiny
									top = Math.Max(Bars.GetLow((int)bar), Bars.GetLow(validPointExtrema));
									bottom = Math.Max(Bars.GetLow((int)bar), Bars.GetLow(validPointExtrema));
									/*
									if (top - bottom < ZBoxStatistics.HeightAverage - ZBoxStatistics.HeightStandardDeviation)
									{
										top = top + (ZBoxStatistics.HeightAverage / 2 + ZBoxStatistics.HeightStandardDeviation / 4);
										bottom = bottom - (ZBoxStatistics.HeightAverage / 2 + ZBoxStatistics.HeightStandardDeviation / 4);
									}
									*/
									ZoneBoxList.Add(new ZoneBox(this, validPointExtrema, (int)bar, top, bottom));

								}
							}



							var validPoints = 0;
							for (int j = 0; j < InflectionPoints.Count; j++)
							{
								if (InflectionPoints[j][1] == 1)
								{
									validPoints++;
									AvgArea += InflectionPoints[j][2];
								}
							}
							AvgArea = AvgArea / validPoints;
						}
					}
					totalTimeToCompare = 40;
					if (IsInflection("Down", periodI, 6))
					{
						// getting area from earlier time
						double initialAvg = 0;
						TimeSpan subt = new TimeSpan(0, totalTimeToCompare / 2, 0);
						int prevBar = Bars.GetBar(Bars.GetTime(CurrentBar).Subtract(subt));
						if (prevBar < dayStartBar) prevBar = dayStartBar;
						double zero = Bars.GetHigh(CurrentBar);
						for (int i = prevBar; i <= CurrentBar; i++)
						{
							initialAvg += zero - SMA(periodI)[CurrentBar-i];
						}
						List<double> tempL = new List<double>() { CurrentBar-2, 0, initialAvg, 1 };
						InflectionPoints.Add(tempL);
					}

					else if (IsInflection("Up", periodI, 6))
					{
						double initialAvg = 0;
						TimeSpan subt = new TimeSpan(0, totalTimeToCompare/2, 0);
						int prevBar = Bars.GetBar(Bars.GetTime(CurrentBar).Subtract(subt));
						if (prevBar < dayStartBar) prevBar = dayStartBar;
						double zero = Bars.GetLow(CurrentBar);
						for (int i = prevBar; i <= CurrentBar; i++)
						{
							initialAvg += SMA(periodI)[CurrentBar - i] - zero;
						}
						List<double> tempL = new List<double>() { CurrentBar-2, 0, initialAvg, 2 };
						InflectionPoints.Add(tempL);
					}
				}


				// NEURAL NETWORK
				// GET THE ROLLING AVERAGE PRICE FOR THE LAST 30 MINUTES
				// SET THIS PRICE AS 'ZERO LINE'
				// START RECORDING THE PRICE ACTION OF THE FOLLOWING n CANDLES
				// PLOT THE 10MA OF n CANDLES PLUS CANDLES FROM THE LAST 30 MINS VS ZERO LINE
				// IF THE AREA BETWEEN ZERO LINE AND THIS MOVING AVERAGE IS 'LOW', A NEW ZONE HAS BEEN FORMED
				// PROCEDURE:
				//	WHEN 14MA SLOPE CROSSES ZERO WE HAVE FOUND AN 'INFLECTION' POINT
				//	PLOT 'ZERO LINE' FROM 'INFLECTION' POINT
				//	COMPUTE 'AREA' BETWEEN '14MA' AND 'ZERO LINE'
				//	IF 'AREA' STAYS UNDER 'THRESHOLD' FOR x TIME, A ZONE IS CREATED
				//	
				//	ONCE 'LOW THRESHOLD IS PASSED, STRENGTH OF ZONE INCREASES IF PRICE CONTINUES TO STAY INSIDE ZONE FOR THE DAY
				ComputeHL();
				// update zone database for lows and highs for this day
				if (Bars.IsLastBarOfSession)
				{

					
					Print("Low High: " + CandleStats.GetAvgLowHighHeight() + " | " + CandleStats.GetLowHighHeightStDev());
					Print("Close High: " + CandleStats.GetAvgCloseHighHeight() + " | " + CandleStats.GetCloseHighHeightStDev());
					Print("Close Low: " + CandleStats.GetAvgCloseLowHeight() + " | " + CandleStats.GetCloseLowHeightStDev());
					Print("Open Close: " + CandleStats.GetAvgOpenCloseHeight() + " | " + CandleStats.GetOpenCloseHeightStDev());
					Print("Open High: " + CandleStats.GetAvgOpenHighHeight() + " | " + CandleStats.GetOpenHighHeightStDev());
					Print("Open Low: " + CandleStats.GetAvgOpenLowHeight() + " | " + CandleStats.GetOpenLowHeightStDev());
					Print("H/L Position avg: " + CandleStats.GetHighMinusLowMinusOpenDivCloseAvg() + " | " + CandleStats.GetHighMinusLowMinusOpenDivCloseStDev());
					Print("O/C normal: " + CandleStats.GetAvgOpenCloseHeightNormalized() + " | " + CandleStats.GetOpenCloseHeightNormalizedStDev());
					Print("___________________________________________________________________________________");
					
					dayLastBar = CurrentBar;
					NumFullTradingDays++;
					HighOfDayDict.Add(Bars.GetBar(dayHighTime), dayHigh);
					LowOfDayDict.Add(Bars.GetBar(dayLowTime), dayLow);

                    #region EVOLVINGZONES
                    SolidColorBrush botOut = Brushes.SkyBlue;
					SolidColorBrush topOut = Brushes.LawnGreen;
					SolidColorBrush peakOut = Brushes.Purple;
					Draw.Line(this, "vabot" + Time[0].Month + "/" + Time[0].Day, false, Bars.GetTime(dayStartBar), evolvingBottom[1], Bars.GetTime(CurrentBar), evolvingBottom[1], botOut, DashStyleHelper.Solid, 2);
					Draw.Line(this, "vatop" + Time[0].Month + "/" + Time[0].Day, false, Bars.GetTime(dayStartBar), evolvingTop[1], Bars.GetTime(CurrentBar), evolvingTop[1], topOut, DashStyleHelper.Solid, 2);
					Draw.Line(this, "max" + Time[0].Month + "/" + Time[0].Day, false, Bars.GetTime(dayStartBar), evolvingPeak[1], Bars.GetTime(CurrentBar), evolvingPeak[1], peakOut, DashStyleHelper.Solid, 2);


					ZBoxStatistics.CalculateStatistics();
					double top = maxPrice + (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation) / 4;
					double bottom = maxPrice - (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation) / 4;
					//Print(top + " | " + bottom);
					ZoneBox peakVol = new ZoneBox(this, CurrentBar, CurrentBar, top, bottom);
					peakVol.SetStyle(Brushes.Green, Brushes.Red, peakOut);
					peakVol.DaysToLive = 1;
					ZoneBoxList.Add(peakVol);

					top = evolvingTop[1] + (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation) / 4;
					bottom = evolvingTop[1] - (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation) / 4;
					//Print(top + " | " + bottom);
					ZoneBox topEvolving = new ZoneBox(this, CurrentBar, CurrentBar, top, bottom);
					topEvolving.SetStyle(Brushes.Green, Brushes.Red, topOut);
					topEvolving.DaysToLive = 1;
					ZoneBoxList.Add(topEvolving);

					top = evolvingBottom[1] + (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation) / 4;
					bottom = evolvingBottom[1] - (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation) / 4;
					//Print(top + " | " + bottom);
					ZoneBox botEvolving = new ZoneBox(this, CurrentBar, CurrentBar, top, bottom);
					botEvolving.SetStyle(Brushes.Green, Brushes.Red, botOut);
					botEvolving.DaysToLive = 1;
					ZoneBoxList.Add(botEvolving);
                    #endregion
                    #region HODLOD
                    // HOD and LOD zoneboxes
                    bool createdZone = false;
					int validPointExtrema = 0;
					//Draw.Diamond(this, "LOD " + CurrentBar, true, dayLowTime, dayLow, Brushes.DarkSeaGreen);
					for (int i = Bars.GetBar(dayLowTime) - 5; i < Bars.GetBar(dayLowTime) + 5; i++)
					{
						if (i < 0) continue;
						//Print(i + " | " + (CurrentBar - i) + " | " + dayLastBar + " | " + CurrentBar + " | " + Bars.GetBar(dayLowTime));
						if (i >= dayLastBar || i < dayStartBar) continue;
						if (IsInflection("Up", 3, 1, CurrentBar - i))
						{
							//Draw.Diamond(this, "testLOD" + i, true, Bars.GetTime(i), dayLow, Brushes.White);
							validPointExtrema = CurrentBar - i;
							ZoneBoxList.Add((i >= Bars.GetBar(dayLowTime)) ? new ZoneBox(this, Bars.GetBar(dayLowTime), i, SMA(3)[CurrentBar - Bars.GetBar(dayLowTime)], dayLow) : new ZoneBox(this, i, Bars.GetBar(dayLowTime), SMA(3)[CurrentBar - Bars.GetBar(dayLowTime)], dayLow));
							createdZone = true;
							break;
						}
					}
					// if the LOD happened to occur where no inflection happened (probably at the start or end of day)
					if (createdZone == false)
					{
						ZoneBoxList.Add(new ZoneBox(this, Bars.GetBar(dayLowTime), CurrentBar, SMA(8)[CurrentBar - Bars.GetBar(dayLowTime)], dayLow));

					}
					createdZone = false;
					validPointExtrema = 0;
					//Draw.Diamond(this, "HOD " + CurrentBar, true, dayHighTime, dayHigh, Brushes.BurlyWood);

					for (int i = Bars.GetBar(dayHighTime) - 5; i < Bars.GetBar(dayHighTime) + 5; i++)
					{
						if (i < 0) continue;
						//Print(i + " | " + (CurrentBar - i) + " | " + dayLastBar + " | " + CurrentBar + " | " + Bars.GetBar(dayHighTime));
						if (i >= dayLastBar || i < dayStartBar) continue;
						if (IsInflection("Down", 3, 1, CurrentBar - i))
						{
							//Draw.Diamond(this, "testHOD" + i, true, Bars.GetTime(i), dayHigh, Brushes.Black);
							validPointExtrema = CurrentBar - i;
							ZoneBoxList.Add((i >= Bars.GetBar(dayHighTime)) ? new ZoneBox(this, Bars.GetBar(dayHighTime), i, dayHigh, SMA(3)[CurrentBar - Bars.GetBar(dayHighTime)]) : new ZoneBox(this, i, Bars.GetBar(dayHighTime), dayHigh, SMA(3)[CurrentBar - Bars.GetBar(dayHighTime)]));
							createdZone = true;
							break;
						}
						//Draw.Diamond(this, "testHOD" + i, true, Bars.GetTime(i), dayHigh, Brushes.Black);
					}
					// if the HOD happened to occur where no inflection happened (probably at the start or end of day)
					if (createdZone == false)
					{
						ZoneBoxList.Add(new ZoneBox(this, Bars.GetBar(dayHighTime), CurrentBar, dayHigh, SMA(8)[CurrentBar - Bars.GetBar(dayHighTime)]));
					}
					createdZone = false;
                    #endregion


                    #region ZONEORGANIZE
                    // Organize, clean, and remove the trash repetitive zones created during the day. Clutter is our enemy.
                    // We get the price where the most volume occured inside each box
                    for (int box = ZoneBoxList.Count - 1; box >= 0; box--)
					{
						ZoneBoxList[box].DailyVolumeArray.Clear();
						if (ZoneBoxList[box].OriginalLeftSideAbsBar >= dayStartBar && ZoneBoxList[box].OriginalLeftSideAbsBar <= dayLastBar)
						{
							// get the number of zones above or below
							if (GetCurrentAsk() <= ZoneBoxList[box].TopPrice || GetCurrentAsk() <= ZoneBoxList[box].BottomPrice)
							{
								zonesAboveTemp++;
							}
							else if (GetCurrentAsk() >= ZoneBoxList[box].TopPrice || GetCurrentAsk() >= ZoneBoxList[box].BottomPrice)
							{
								zonesBelowTemp++;
							}
							if (zonesAboveTemp == 0)
							{
								ZonesAboveToday = 0;
							}
							else if (zonesBelowTemp == 0)
							{
								ZonesBelowToday = 0;
							}
							else
							{
								ZonesBelowToday = -1;
								ZonesAboveToday = -1;
							}
							/*
							 * CALCULATING DAILY VOLUME AT EOD FOR EACH BOX
							 */
							double maxVol = 0;
							double maxVolPrice = 0;
							bool didAdd = false;
							double weightCounter = 0;
							double weight = 0;
							for (int volBar = 0; volBar < priceHitsArray.Length / 2; volBar++)
							{
								//Print(priceHitsArray[0, volBar] + " | " + priceHitsArray[0, volBar]);
								//Draw.Line(this, "vol " + ZoneBoxList[box].ID + " " + priceHitsArray[0, volBar], false, Bars.GetTime(ZoneBoxList[box].activeLeftSideAbsBar), priceHitsArray[0, volBar], Bars.GetTime(ZoneBoxList[box].activeRightSideAbsBar), priceHitsArray[0, volBar], Brushes.Purple, DashStyleHelper.Dot, 2);

								if (priceHitsArray[0, volBar] <= ZoneBoxList[box].TopPrice && priceHitsArray[0, volBar] >= ZoneBoxList[box].BottomPrice)
								{
									//Draw.Line(this, "vol " + ZoneBoxList[box].ID + " " + priceHitsArray[0, volBar], false, Bars.GetTime(ZoneBoxList[box].activeLeftSideAbsBar), priceHitsArray[1, volBar], Bars.GetTime(ZoneBoxList[box].activeRightSideAbsBar), priceHitsArray[1, volBar], Brushes.Purple, DashStyleHelper.Dot, 2);

									if (priceHitsArray[1, volBar] > maxVol)
									{
										maxVol = priceHitsArray[1, volBar];
										maxVolPrice = priceHitsArray[0, volBar];
									}
									ZoneBoxList[box].DailyVolumeArray.Add(new List<double>() { priceHitsArray[0, volBar], priceHitsArray[1, volBar] });
									ZoneBoxList[box].DailyVolumePeak = maxVol;
									ZoneBoxList[box].DailyVolumePeakPrice = maxVolPrice > 0 ? maxVolPrice : (ZoneBoxList[box].TopPrice + ZoneBoxList[box].BottomPrice) / 2;
									weight += priceHitsArray[0, volBar] * priceHitsArray[1, volBar];
									weightCounter += priceHitsArray[1, volBar];
									ZoneBoxList[box].DailyVolumeWeightedAverageVolPrice = weight / weightCounter;
									didAdd = true;
									//Print(maxVol + " | " + maxVolPrice);
								}
							}
							if (didAdd == false)
							{
								ZoneBoxList[box].DailyVolumePeak = 0;
								ZoneBoxList[box].DailyVolumePeakPrice = (ZoneBoxList[box].TopPrice + ZoneBoxList[box].BottomPrice) / 2;
								//ZoneBoxList[box].DailyVolumeWeightedAverageVolPrice = 0;
							}
							if (ZoneBoxList[box].DailyVolumeWeightedAverageVolPrice == 0)
							{
								ZoneBoxList[box].DailyVolumeWeightedAverageVolPrice = (ZoneBoxList[box].TopPrice + ZoneBoxList[box].BottomPrice) / 2;
							}
							//Print(ZoneBoxList[box].dailyVolumePeakPrice + " | " + maxVolPrice);
							Draw.Line(this, "max vol " + ZoneBoxList[box].ID, false, Bars.GetTime(ZoneBoxList[box].ActiveLeftSideAbsBar), ZoneBoxList[box].DailyVolumePeakPrice, Bars.GetTime(ZoneBoxList[box].ActiveRightSideAbsBar), ZoneBoxList[box].DailyVolumePeakPrice, Brushes.Purple, DashStyleHelper.Dot, 4);
							Draw.Line(this, "dvwav " + ZoneBoxList[box].ID, false, Bars.GetTime(ZoneBoxList[box].ActiveLeftSideAbsBar), ZoneBoxList[box].DailyVolumeWeightedAverageVolPrice, Bars.GetTime(ZoneBoxList[box].ActiveRightSideAbsBar), ZoneBoxList[box].DailyVolumeWeightedAverageVolPrice, Brushes.Yellow, DashStyleHelper.Dot, 4);
						}
					}
                    #endregion

                    // manage zones above/below
                    ZonesAboveExtrema.Add(ZonesAboveToday == 0 ? true : false);
					ZonesBelowExtrema.Add(ZonesBelowToday == 0 ? true : false);
					ZonesAboveToday = -1;
					ZonesBelowToday = -1;

					// Merge zones close together
					TryMergeZones();

				}
				ScuffedVolumeProfile();
			}

		}


		/// <summary>
		/// Merge zones in close proximity
		/// </summary>
		public void TryMergeZones()
		{
			double peakPriceComparator = 1.0010;
			bool UseDailyVolPeakPriceMerge = true;
			bool UseDailyVolWeightedAvgVolPriceMergeThres = true;
			bool UseDailyVolWeightedAvgVolPriceMergeContainer = true;
			ZBoxStatistics.CalculateStatistics();
			//Print(ZBoxStatistics.HeightAverage + " | " + ZBoxStatistics.HeightStandardDeviation);
			foreach (ZoneBox box in ZoneBoxList.ToList())
			{
				// if box was "broken" x number of times on the same day it was created, remove it
				if (box.OriginalLeftSideAbsBar > dayStartBar && box.GetTotalIntradayBreakCount() > 1)
				{
					int id = box.ID;
					if (HideDrawObjects) RemoveDrawObject(id.ToString());
					ZoneBoxList.RemoveAll(b => b.ID == id);
					continue;
				}
				if (box.DaysToLive > 0) continue;

				// check if a tiny zone can be refactored to touch HOD or LOD
				if (box.GetHeight() < ZBoxStatistics.HeightAverage - ZBoxStatistics.HeightStandardDeviation / 2)
				{
					// do the bottom zones
					if (box.TopPrice < dayLow)
					{
						double top = dayLow;
						double bottom = box.BottomPrice;
						if (top - bottom < ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation * 3)
						{
							//Draw.Line(this, "jnajs " + dayLowTime, false, dayLowTime, bottom, dayLowTime, top, "");
							box.TopPrice = dayLow;
						}
					}
					else if (box.BottomPrice > dayHigh)
					{
						double top = box.TopPrice;
						double bottom = dayHigh;
						if (top - bottom < ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation * 3)
						{
							//Draw.Line(this, "jnajs " + dayHighTime, false, dayHighTime, bottom, dayHighTime, top, "");
							box.BottomPrice = dayHigh;
						}
					}
				}

				// use statistics to find the super small boxes
				
				foreach (ZoneBox box2 in ZoneBoxList.ToList())
				{
					if (box.ID != box2.ID)
					{
						if (box2.DaysToLive > 0) continue;
						// if box1 and box2 are tiny
						if (box.GetHeight() < ZBoxStatistics.HeightAverage - ZBoxStatistics.HeightStandardDeviation/2 && box2.GetHeight() < ZBoxStatistics.HeightAverage - ZBoxStatistics.HeightStandardDeviation/2)
						{
							// make sure we have sufficient data
							if (ZoneBoxList.Count > 10)
							{
								// find the one above
								double bottom = box.TopPrice > box2.TopPrice ? box2.BottomPrice : box.BottomPrice;
								double top = box.TopPrice > box2.TopPrice ? box.TopPrice : box2.TopPrice;
								// if merging them doesn't make them massive
								if (Math.Abs(top - bottom) < ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation)
								{
									// refactor the zones to merge them
									box2.TopPrice = top;
									box2.BottomPrice = bottom;
									int id = box.ID;
									if (HideDrawObjects) RemoveDrawObject(id.ToString());
									ZoneBoxList.RemoveAll(b => b.ID == id);
								}
							}
							//Print(box.ID + " ... " + box2.ID);

						}
						// if the box is tiny
						else if (box.GetHeight() < ZBoxStatistics.HeightAverage - ZBoxStatistics.HeightStandardDeviation)
						{
							// if the box is inside another box
							if ((box.BottomPrice >= box2.BottomPrice && box.BottomPrice <= box2.TopPrice) || (box.TopPrice <= box2.TopPrice && box.TopPrice >= box2.BottomPrice))
							{
								// refactor and merge the box
								int id = box.ID;
								if (HideDrawObjects) RemoveDrawObject(id.ToString());
								ZoneBoxList.RemoveAll(b => b.ID == id);
							}
						}
						/*
						else if (box.BottomPrice > box2.TopPrice && (box.BottomPrice - box2.TopPrice < ZBoxStatistics.HeightAverage - ZBoxStatistics.HeightStandardDeviation / 2))
						{
							if (box.TopPrice - box2.BottomPrice < ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation)
							{
								double top = box.TopPrice;
								double bottom = box2.BottomPrice;
								int id = box.ID;
								RemoveDrawObject(id.ToString());
								ZoneBoxList.RemoveAll(b => b.ID == id);
								id = box2.ID;
								RemoveDrawObject(id.ToString());
								ZoneBoxList.RemoveAll(b => b.ID == id);
								ZoneBoxList.Add(new ZoneBox(this, dayStartBar, CurrentBar, top, bottom));
							}
						}
						*/
						// Condition one: if maximum daily volume price is within threshold
						
						if ( UseDailyVolPeakPriceMerge && (box.DailyVolumePeakPrice <= box2.DailyVolumePeakPrice * peakPriceComparator && box.DailyVolumePeakPrice >= box2.DailyVolumePeakPrice * (1 / peakPriceComparator)))
						{

							// remove the zone with most volume
							if (box.GetDailyVolume() > box2.GetDailyVolume())
							{
								int id = box.ID;
								if (HideDrawObjects) RemoveDrawObject(id.ToString());
								ZoneBoxList.RemoveAll(b => b.ID == id);
							}
							else if (box.GetDailyVolume() < box2.GetDailyVolume())
							{
								int id = box2.ID;
								if (HideDrawObjects) RemoveDrawObject(id.ToString());
								ZoneBoxList.RemoveAll(b => b.ID == id);
							}

						}
						
						// Condition two: if volume-weighted avg vol price is within threshold
						if (UseDailyVolWeightedAvgVolPriceMergeThres && (box.DailyVolumeWeightedAverageVolPrice <= box2.DailyVolumeWeightedAverageVolPrice * peakPriceComparator && box.DailyVolumeWeightedAverageVolPrice >= box2.DailyVolumeWeightedAverageVolPrice * (1 / peakPriceComparator)))
						{

						}

						// Condition three: if box1 volume-weighted avg vol price is inside box2
						if (UseDailyVolWeightedAvgVolPriceMergeContainer && (box.DailyVolumeWeightedAverageVolPrice <= box2.TopPrice && box.DailyVolumeWeightedAverageVolPrice >= box2.BottomPrice)) 
						{
							// doing the ones below the evolving zones
							// If box2 vol-weighted avg price is below the bottom evolving zone
							if (box.DailyVolumeWeightedAverageVolPrice <= evolvingBottom[1])
							{
								// if box2 is above evbottom
								if (box2.DailyVolumeWeightedAverageVolPrice > evolvingBottom[1])
								{
									// remove box 2
									int id = box2.ID;
									if (HideDrawObjects) RemoveDrawObject(id.ToString());
									ZoneBoxList.RemoveAll(b => b.ID == id);
								}
								// if box1 is closer to evbottom than box2 is
								else if (Math.Abs(box.DailyVolumeWeightedAverageVolPrice - evolvingBottom[1]) < Math.Abs(box2.DailyVolumeWeightedAverageVolPrice - evolvingBottom[1]))
								{

										// remove box 2
										int id = box2.ID;
										if (HideDrawObjects) RemoveDrawObject(id.ToString());
										ZoneBoxList.RemoveAll(b => b.ID == id);
									
								}
								else
								{
									// if the height of the bottom zone bottom price compared to height of the top zone top price isn't eno

										// remove box 2
										int id = box.ID;
										if (HideDrawObjects) RemoveDrawObject(id.ToString());
										ZoneBoxList.RemoveAll(b => b.ID == id);
									
								}
							}
							// doing the ones above evolving zones
							else if (box.DailyVolumeWeightedAverageVolPrice >= evolvingTop[1])
							{
								if (box2.DailyVolumeWeightedAverageVolPrice < evolvingTop[1])
								{
									int id = box2.ID;
									if (HideDrawObjects) RemoveDrawObject(id.ToString());
									ZoneBoxList.RemoveAll(b => b.ID == id);
								}
								// if box1 is closer to evtop than box2 is
								else if (Math.Abs(box.DailyVolumeWeightedAverageVolPrice - evolvingTop[1]) < Math.Abs(box2.DailyVolumeWeightedAverageVolPrice - evolvingTop[1]))
								{
									int id = box2.ID;
									if (HideDrawObjects) RemoveDrawObject(id.ToString());
									ZoneBoxList.RemoveAll(b => b.ID == id);
								}
							}
							// Take the box with vwavp closer to evolvingbottom
						}


						// Condition four: if box 2 volume-weighted avg vol price is inside box1
						//if ()
						//

							// Condition two: 
					}
				}
			}
		}

		private void ComputeHL()
		{
			if (Bars.GetLow(CurrentBar) <= dayLow)
			{
				// Update low of day
				dayLow = Bars.GetLow(CurrentBar);
				dayLowTime = Bars.GetTime(CurrentBar);
				//Draw.Diamond(this, "Day Low: " + dayLow.ToString() + " at " + dayLowTime, true, dayLowTime, dayLow, Brushes.Green);
			}
			if (Bars.GetHigh(CurrentBar) >= dayHigh)
			{
				// Update high of day
				dayHigh = Bars.GetHigh(CurrentBar);
				dayHighTime = Bars.GetTime(CurrentBar);

				//Draw.Diamond(this, "Day High: " + dayHigh.ToString() + " at " + dayHighTime, true, dayHighTime, dayHigh, Brushes.Red);
			}
		}

		private void ResetSessionVars()
		{
			priceHitsArray = new double[2, TotalSlots];
			dayLastBar = CurrentBar;
			dayStartBar = CurrentBar;
			dayTOP = Bars.GetHigh(CurrentBar);
			dayBOTTOM = Bars.GetLow(CurrentBar);
			dayHigh = Bars.GetHigh(CurrentBar);
			dayLow = Bars.GetLow(CurrentBar);
			dayHighTime = Bars.GetTime(CurrentBar);
			dayLowTime = Bars.GetTime(CurrentBar);
		}


		public int GetNumberOfFullTradingDays()
		{
			return NumFullTradingDays;
		}





		public void DrawVolumeProfileAccessories()
		{
			if (priceHitsArray.Length / 2 > 0)
			{
				Draw.Line(this, "max" + Time[0].Month + "/" + Time[0].Day, false, Bars.GetTime(dayStartBar), maxPrice, Bars.GetTime(CurrentBar), maxPrice, "");
			}
		}

		public bool WasZeroResX(int daysAgo)
		{
			if (ZonesAboveExtrema.Count > 0)
			{
				int index = ZonesAboveExtrema.Count - daysAgo;
				if (index < 0 || index >= ZonesAboveExtrema.Count) return false;
				if (ZonesAboveExtrema[index]) return true;
			}
			return false;
		}


		public bool IsInflection(string direction, int period, int smooth = 1, int barsAgo = 0, bool detrend = false, InputSeriesType ist = InputSeriesType.LinRegSlope, NormType nt = NormType.None) // "Up" or "Down"
		{
			SlopeEnhancedOp refLRS = SlopeEnhancedOp(period, 56, smooth, detrend, ist, nt, Brushes.Green, Brushes.Red);

			if (CurrentBar < period + 1 + barsAgo) return false;
			if (direction == "Up" && refLRS[1+barsAgo] < 0 && refLRS[0+barsAgo] > 0)
			{
				return true;
			}
			else if (direction == "Down" && refLRS[1+barsAgo] > 0 && refLRS[0+barsAgo] < 0)
			{
				return true;
			}
			return false;
		}


		public ZoneBox GetCurrentZone()
		{
			foreach (ZoneBox box in ZoneBoxList)
			{
				if (box.IsCurrentAskInsideBox())
				{
					return box;
				}
			}
			return null;
		}

		public double GetZoneStrength(ZoneBox box)
		{
			if (box == null) return -1;
			return box.Strength;
		}

		public int GetZoneType(ZoneBox box)
		{
			if (box == null) return -999;
			return box.Type;
		}

		public int GetZoneDaysAlive(ZoneBox box)
		{
			if (box == null) return -999;
			return box.DaysAlive;
		}

		/// <summary>
		/// Check if zone strength is greater than or less than some threshold
		/// </summary>
		/// <param name="box"></param>
		/// <param name="id"></param>
		/// <param name="st"></param>
		/// <returns></returns>
		public bool DoesZoneStrengthComply(ZoneBox box, string id, double st)
		{
			if (box == null) return false;
			switch(id)
			{
				case ">":
					if (box.Strength > st) return true;
					break;

				case "<":
					if (box.Strength < st) return true;
					break;

				case ">=":
					if (box.Strength >= st) return true;
					break;

				case "<=":
					if (box.Strength <= st) return true;
					break;
				default:
					return false;

			}
			return false;
		}



		/*
		 * VOLUME PROFILE
		 */


		public void ScuffedVolumeProfile()
	   {

		   int x;

		   int ticksInRange = (int)Math.Round((dayHigh - dayLow) / TickSize, 0);

		   //fit ticks into array by so many TicksPerSlot
		   int ticksPerSlot = (ticksInRange / TotalSlots) + 1; //should just drop the fract part.
		   int lastSlotUsed = ticksInRange / ticksPerSlot; //Zero based, drop fract part.


		   slotHalfRange = ((TickSize * ticksPerSlot)) / 2;
		   double comboSlotOffset = (ticksPerSlot > 1 ? slotHalfRange - (((dayLow + ((lastSlotUsed + 1) * TickSize * ticksPerSlot)) - dayHigh) / 2) : 0);   //move down to center it.
		   //clear counts in any case.
		   for (x = 0; x <= lastSlotUsed; x++)
		   {
			   // 0 -> 999, reset from bottom up.
			   priceHitsArray[0, x] = (x * TickSize * ticksPerSlot) + comboSlotOffset; //Lowest Tick Value/Slot upped to mid value point
			   priceHitsArray[0, x] += dayLow; //add it to the bottom
			   priceHitsArray[1, x] = 0.0; //clear counts per value.
		   }
		   if (ticksInRange > 0)
		   {
				double BarH;
				double BarL;
				int index=0;
				maxBar = 0;
				maxPrice = 0;
				double tHxP = 0.0; 
				double hitsTotal = 0.0;
				double sessVAtop = 0.0;
				double sessVAbot = 0.0;
				double PctOfVolumeInVA = 0.7;

				int i = dayStartBar;
			   while (i <= CurrentBar)
			   {
				   BarH = Bars.GetHigh(i);
				   BarL = Bars.GetLow(i);

				   //Volume Weighted Time Price Opportunity - Disperses the Volume of the bar over the range of the bar so each price touched is weighted with volume
				   //BarH=High[i]; BarL=Low[i];
				   int TicksInBar = (int)Math.Round((BarH - Bars.GetLow(i)) / TickSize + 1, 0);
				   while (BarL <= BarH)
				   {
					   index = (int)Math.Round((BarL - dayLow) / TickSize, 0);
					   index /= ticksPerSlot;  //drop fract part.
					   priceHitsArray[1, index] += Bars.GetVolume(i) / TicksInBar;
					   BarL = BarL + TickSize;
				   }
					tHxP += (priceHitsArray[1, index] * priceHitsArray[0, index]);
					hitsTotal += priceHitsArray[1, index];
					if (priceHitsArray[1, index] > maxBar)
					{
						maxBar = priceHitsArray[1, index];
						maxPrice = priceHitsArray[0, index];
					}
					i++;
			   }

			   
				sessVAtop = tHxP / hitsTotal;
				sessVAbot = sessVAtop;

				//This loop calculates the percentage of hits contained within the Value Area
				double viA = 0.0;
				double tV = 0.00001;
				double adj = 0.0;
				i = 0;
				if (priceHitsArray.Length / 2 == 0) return;
				while (viA / tV < PctOfVolumeInVA)
				{
					sessVAbot = sessVAbot - adj;
					sessVAtop = sessVAtop + adj;
					viA = 0.0;
					tV = 0.00001;
					for (i = 0; i < priceHitsArray.Length/2; i++)
					{
						if (priceHitsArray[0, i] > sessVAbot - adj && priceHitsArray[0, i] < sessVAtop + adj)
							viA += priceHitsArray[1, i];
						tV += priceHitsArray[1, i];
					}
					adj = TickSize;
				}
				evolvingBottom[0] = sessVAbot;
				evolvingPeak[0] = maxPrice;
				evolvingTop[0] = sessVAtop;
				// only draw if it's real time or last bar of session to save performance
				/*
				if (State == State.Realtime || Bars.IsLastBarOfSession)
				{
					SolidColorBrush botOut = Brushes.SkyBlue;
					SolidColorBrush topOut = Brushes.LawnGreen;
					SolidColorBrush peakOut = Brushes.Purple;
					Draw.Line(this, "vabot" + Time[0].Month + "/" + Time[0].Day, false, Bars.GetTime(dayStartBar), sessVAbot, Bars.GetTime(CurrentBar), sessVAbot, botOut, DashStyleHelper.Solid, 2);
					Draw.Line(this, "vatop" + Time[0].Month + "/" + Time[0].Day, false, Bars.GetTime(dayStartBar), sessVAtop, Bars.GetTime(CurrentBar), sessVAtop, topOut, DashStyleHelper.Solid, 2);
					Draw.Line(this, "max" + Time[0].Month + "/" + Time[0].Day, false, Bars.GetTime(dayStartBar), maxPrice, Bars.GetTime(CurrentBar), maxPrice, peakOut, DashStyleHelper.Solid, 2);
					
					if (Bars.IsLastBarOfSession)
					{
						ZBoxStatistics.CalculateStatistics();
						double top = maxPrice + (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation) / 4;
						double bottom = maxPrice - (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation) / 4;
						//Print(top + " | " + bottom);
						ZoneBox peakVol = new ZoneBox(this, CurrentBar, CurrentBar, top, bottom);
						peakVol.SetStyle(Brushes.Green, Brushes.Red, peakOut);
						peakVol.DaysToLive = 1;
						ZoneBoxList.Add(peakVol);

						top = sessVAtop + (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation) / 4;
						bottom = sessVAtop - (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation) / 4;
						//Print(top + " | " + bottom);
						ZoneBox topEvolving = new ZoneBox(this, CurrentBar, CurrentBar, top, bottom);
						topEvolving.SetStyle(Brushes.Green, Brushes.Red, topOut);
						topEvolving.DaysToLive = 1;
						ZoneBoxList.Add(topEvolving);

						top = sessVAbot + (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation)/4;
						bottom = sessVAbot - (ZBoxStatistics.HeightAverage + ZBoxStatistics.HeightStandardDeviation)/4;
						//Print(top + " | " + bottom);
						ZoneBox botEvolving = new ZoneBox(this, CurrentBar, CurrentBar, top, bottom);
						botEvolving.SetStyle(Brushes.Green, Brushes.Red, botOut);
						botEvolving.DaysToLive = 1;
						ZoneBoxList.Add(botEvolving);
					}
				}
				*/
			}
		}

		public override void OnRenderTargetChanged()
		{
			// if dxBrush exists on first render target change, dispose of it
			if (dxBrush != null)
			{
				dxBrush.Dispose();
			}

			// recalculate dxBrush from value calculated in OnBarUpdated when RenderTarget is recreated
			if (RenderTarget != null)
			{
				try
				{
					dxBrush = brushColor.ToDxBrush(RenderTarget);
				}
				catch (Exception e) 
				{
					Print(e);
				}
			}
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			//base.OnRender(chartControl, chartScale);
			SharpDX.Direct2D1.AntialiasMode oldAntialiasMode = RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.Aliased;


			SharpDX.Vector2 startPoint;
			SharpDX.Vector2 endPoint;

			// For our custom script, we need a way to determine the Chart's RenderTarget coordinates to draw our custom shapes
			// This info can be found within the NinjaTrader.Gui.ChartPanel class.
			// You can also use various chartScale and chartControl members to calculate values relative to time and price
			// However, those concepts will not be discussed or used in this sample
			// Notes:  RenderTarget is always the full ChartPanel, so we need to be mindful which sub-ChartPanel we're dealing with
			// Always use ChartPanel X, Y, W, H - as chartScale and chartControl properties WPF units, so they can be drastically different depending on DPI set
			startPoint = new SharpDX.Vector2(ChartPanel.X, ChartPanel.Y);
			endPoint = new SharpDX.Vector2(ChartPanel.X + ChartPanel.W, ChartPanel.Y + ChartPanel.H);
			int colorAvg = 6;
			byte r = 0; byte g = 0; byte b = 0; byte a = 0 ;
			bool isFirstVolumeBar = false;
			for (int i = 0; i < ZoneBoxList.Count;i++)
			{
				isFirstVolumeBar = false;
				double topD = 0;
				List<SharpDX.Vector4> avgVector = new List<SharpDX.Vector4>();
				foreach (KeyValuePair<double,double> group in ZoneBoxList[i].TotalVolumeDictionary)
				{
					byte rt = 0; byte gt = 0; byte bt = 0; byte at = 0;
					if (isFirstVolumeBar)
					{
						startPoint = new System.Windows.Point(ChartControl.GetXByBarIndex(ChartBars, ZoneBoxList[i].ActiveLeftSideAbsBar), chartScale.GetYByValue(group.Key)).ToVector2();
						endPoint = new System.Windows.Point(ChartControl.GetXByBarIndex(ChartBars, ZoneBoxList[i].ActiveRightSideAbsBar), chartScale.GetYByValue(topD)).ToVector2();
						brushColor = new SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
						dxBrush = brushColor.ToDxBrush(RenderTarget);
						//Print(startPoint.X + " | " + endPoint.Y + " | " + (endPoint.X - startPoint.X) + " | " + (endPoint.Y - startPoint.Y) + " | " + r + " | " + g + " | " + b);
						//RenderTarget.FillRectangle(new SharpDX.RectangleF(startPoint.X, endPoint.Y, endPoint.X - startPoint.X, Math.Abs(endPoint.Y - startPoint.Y)), dxBrush);
					}
					if (ZoneBoxList[i].Type == 0) // support
					{
						//r = (byte)(int)Math.Floor((group.Value / BoxMaxVolume) * 255);
						r = 0;
						g = 255;
						b = 0;
						//a = (byte)(int)Math.Floor((group.Value / BoxMaxVolume) * 255);
						a = 200;

					}
					else if (ZoneBoxList[i].Type == 1) // res
					{
						r = 255;
						//g = (byte)(int)Math.Floor((group.Value / BoxMaxVolume) * 255);
						g = 0;
						b = 0;
						//a = (byte)(int)Math.Floor((group.Value / BoxMaxVolume) * 255);
						a = 200;

					}
					avgVector.Add(new Vector4(r, g, b, a)); // x y z w

					if (isFirstVolumeBar == false)
					{
						isFirstVolumeBar = true;
					}
					topD = group.Key;
				}
				
			}
			
			RenderTarget.AntialiasMode = oldAntialiasMode;
		}






		#region Properties


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
		[Range(int.MinValue, int.MaxValue)]
		[Display(Name = "Break strength multiplier", Description = "Multiplier for threshold strength applied if a zone is broken", Order = 10, GroupName = "Parameters")]
		public double BreakStrengthMultiplier
		{ get; set; }


		[NinjaScriptProperty]
		[Display(Name = "Use volume accumulation", Description = "(Suggest TRUE) Factors in relative volume into zone strength calculation", Order = 11, GroupName = "Parameters")]
		public bool UseVolAccumulation
		{ get; set; }

		[NinjaScriptProperty]
		[Range(int.MinValue, int.MaxValue)]
		[Display(Name = "Bar expiration", Description = "Number of days a bar can stay alive", Order = 12, GroupName = "Parameters")]
		public int Expiration
		{ get; set; }

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "Max merge count", Description = "Maximum number of times a zone can be merged", Order = 13, GroupName = "Parameters")]
		public int MaxMergeCount
		{ get; set; }

		[NinjaScriptProperty]
		[Range(0, int.MaxValue)]
		[Display(Name = "Merge threshold", Description = "Merge threshold required for two zones to combine", Order = 14, GroupName = "Parameters")]
		public double MergeThreshold
		{ get; set; }

		#endregion


	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private AdvancedSRZones[] cacheAdvancedSRZones;
		public AdvancedSRZones AdvancedSRZones(int areaStrengthMultiplier, int timeThreshold, int proxyStrengthMultiplier, int newZoneStrength, int zoneTimeoutStrength, double newZoneTopMultiplier, double newZoneBottomMultiplier, Brush resZoneColor, Brush supZoneColor, double breakStrengthMultiplier, bool useVolAccumulation, int expiration, int maxMergeCount, double mergeThreshold)
		{
			return AdvancedSRZones(Input, areaStrengthMultiplier, timeThreshold, proxyStrengthMultiplier, newZoneStrength, zoneTimeoutStrength, newZoneTopMultiplier, newZoneBottomMultiplier, resZoneColor, supZoneColor, breakStrengthMultiplier, useVolAccumulation, expiration, maxMergeCount, mergeThreshold);
		}

		public AdvancedSRZones AdvancedSRZones(ISeries<double> input, int areaStrengthMultiplier, int timeThreshold, int proxyStrengthMultiplier, int newZoneStrength, int zoneTimeoutStrength, double newZoneTopMultiplier, double newZoneBottomMultiplier, Brush resZoneColor, Brush supZoneColor, double breakStrengthMultiplier, bool useVolAccumulation, int expiration, int maxMergeCount, double mergeThreshold)
		{
			if (cacheAdvancedSRZones != null)
				for (int idx = 0; idx < cacheAdvancedSRZones.Length; idx++)
					if (cacheAdvancedSRZones[idx] != null && cacheAdvancedSRZones[idx].AreaStrengthMultiplier == areaStrengthMultiplier && cacheAdvancedSRZones[idx].TimeThreshold == timeThreshold && cacheAdvancedSRZones[idx].ProxyStrengthMultiplier == proxyStrengthMultiplier && cacheAdvancedSRZones[idx].NewZoneStrength == newZoneStrength && cacheAdvancedSRZones[idx].ZoneTimeoutStrength == zoneTimeoutStrength && cacheAdvancedSRZones[idx].NewZoneTopMultiplier == newZoneTopMultiplier && cacheAdvancedSRZones[idx].NewZoneBottomMultiplier == newZoneBottomMultiplier && cacheAdvancedSRZones[idx].ResZoneColor == resZoneColor && cacheAdvancedSRZones[idx].SupZoneColor == supZoneColor && cacheAdvancedSRZones[idx].BreakStrengthMultiplier == breakStrengthMultiplier && cacheAdvancedSRZones[idx].UseVolAccumulation == useVolAccumulation && cacheAdvancedSRZones[idx].Expiration == expiration && cacheAdvancedSRZones[idx].MaxMergeCount == maxMergeCount && cacheAdvancedSRZones[idx].MergeThreshold == mergeThreshold && cacheAdvancedSRZones[idx].EqualsInput(input))
						return cacheAdvancedSRZones[idx];
			return CacheIndicator<AdvancedSRZones>(new AdvancedSRZones(){ AreaStrengthMultiplier = areaStrengthMultiplier, TimeThreshold = timeThreshold, ProxyStrengthMultiplier = proxyStrengthMultiplier, NewZoneStrength = newZoneStrength, ZoneTimeoutStrength = zoneTimeoutStrength, NewZoneTopMultiplier = newZoneTopMultiplier, NewZoneBottomMultiplier = newZoneBottomMultiplier, ResZoneColor = resZoneColor, SupZoneColor = supZoneColor, BreakStrengthMultiplier = breakStrengthMultiplier, UseVolAccumulation = useVolAccumulation, Expiration = expiration, MaxMergeCount = maxMergeCount, MergeThreshold = mergeThreshold }, input, ref cacheAdvancedSRZones);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.AdvancedSRZones AdvancedSRZones(int areaStrengthMultiplier, int timeThreshold, int proxyStrengthMultiplier, int newZoneStrength, int zoneTimeoutStrength, double newZoneTopMultiplier, double newZoneBottomMultiplier, Brush resZoneColor, Brush supZoneColor, double breakStrengthMultiplier, bool useVolAccumulation, int expiration, int maxMergeCount, double mergeThreshold)
		{
			return indicator.AdvancedSRZones(Input, areaStrengthMultiplier, timeThreshold, proxyStrengthMultiplier, newZoneStrength, zoneTimeoutStrength, newZoneTopMultiplier, newZoneBottomMultiplier, resZoneColor, supZoneColor, breakStrengthMultiplier, useVolAccumulation, expiration, maxMergeCount, mergeThreshold);
		}

		public Indicators.AdvancedSRZones AdvancedSRZones(ISeries<double> input , int areaStrengthMultiplier, int timeThreshold, int proxyStrengthMultiplier, int newZoneStrength, int zoneTimeoutStrength, double newZoneTopMultiplier, double newZoneBottomMultiplier, Brush resZoneColor, Brush supZoneColor, double breakStrengthMultiplier, bool useVolAccumulation, int expiration, int maxMergeCount, double mergeThreshold)
		{
			return indicator.AdvancedSRZones(input, areaStrengthMultiplier, timeThreshold, proxyStrengthMultiplier, newZoneStrength, zoneTimeoutStrength, newZoneTopMultiplier, newZoneBottomMultiplier, resZoneColor, supZoneColor, breakStrengthMultiplier, useVolAccumulation, expiration, maxMergeCount, mergeThreshold);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.AdvancedSRZones AdvancedSRZones(int areaStrengthMultiplier, int timeThreshold, int proxyStrengthMultiplier, int newZoneStrength, int zoneTimeoutStrength, double newZoneTopMultiplier, double newZoneBottomMultiplier, Brush resZoneColor, Brush supZoneColor, double breakStrengthMultiplier, bool useVolAccumulation, int expiration, int maxMergeCount, double mergeThreshold)
		{
			return indicator.AdvancedSRZones(Input, areaStrengthMultiplier, timeThreshold, proxyStrengthMultiplier, newZoneStrength, zoneTimeoutStrength, newZoneTopMultiplier, newZoneBottomMultiplier, resZoneColor, supZoneColor, breakStrengthMultiplier, useVolAccumulation, expiration, maxMergeCount, mergeThreshold);
		}

		public Indicators.AdvancedSRZones AdvancedSRZones(ISeries<double> input , int areaStrengthMultiplier, int timeThreshold, int proxyStrengthMultiplier, int newZoneStrength, int zoneTimeoutStrength, double newZoneTopMultiplier, double newZoneBottomMultiplier, Brush resZoneColor, Brush supZoneColor, double breakStrengthMultiplier, bool useVolAccumulation, int expiration, int maxMergeCount, double mergeThreshold)
		{
			return indicator.AdvancedSRZones(input, areaStrengthMultiplier, timeThreshold, proxyStrengthMultiplier, newZoneStrength, zoneTimeoutStrength, newZoneTopMultiplier, newZoneBottomMultiplier, resZoneColor, supZoneColor, breakStrengthMultiplier, useVolAccumulation, expiration, maxMergeCount, mergeThreshold);
		}
	}
}

#endregion
