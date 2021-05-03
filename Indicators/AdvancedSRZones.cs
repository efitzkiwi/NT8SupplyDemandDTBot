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

*/

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{


	public class ZONE
	{

		/*
		TYPE OF ZONE
		1 = HIGH OF DAY
		2 = LOW OF DAY
		3 = GENERAL
		*/
		public int Type { get; set; }
		public int ID { get; set; }
		public int CloseOverlap { get; set; }
		public int TotalTimesBroken { get; set; } // number of times the zone was broken
		public double TotalRelativeVolume { get; set; }
		public double TotalAbsoluteVolume { get; set; }
		/*
		STRENGTH OF ZONE (scale)
		0 = NO EFFECT ON PRICE
		inf = PRICE CANNOT BREAK THROUGH
		Strength will weaken if the zone is broken 
		Strength will strengthen if the price cannot break through
		The zone will be given an initial strength 
		*/
		public bool Expired { get; set; }
		private double Strength;
		public double Decay { get; set; }
		public bool OverlapCheck { get; set; }
		public double GetStrength()
		{

			if (Strength < 0) return 0;
			return Math.Truncate(Strength * 100) / 100;

		}
		public void SetStrength(double st)
		{
			//if (st < 0) return;
			// 1/x^2
			//Strength = st + Strength;
			//if (Strength == -5000) Strength = 0;
			//else Strength += (-500000 / ((st) * (st) + 5000)) + 100;
			//Strength += st;

			/*
			if (st >= 0) Strength += Math.Truncate(5 * Math.Sqrt(st) * 100) / 100;
			else Strength += Math.Truncate(-5 * Math.Sqrt(Math.Abs(st)) * 100) / 100;
			if (Strength > 100) Strength = 100;
			if (Strength < 0) Strength = 0;
			*/
			// https://www.desmos.com/calculator/t1oj2504gi
			double a = 50 / (Math.PI / 2);
			double calc = a * Math.Atan((1 / (2 * a)) * (st + Strength - 100)) + 50;
			Strength = calc;




			//this.Strength += st;
		}
		public void SetStrengthDirect(double st)
		{
			Strength = st;
		}
		/*
		NUMBER OF BARS WHERE THE ZONE GETS DELETED
		For large scale zones (yearly highs) the zone may never be deleted
		value of -1 means zone only expires when the data runs out
		*/
		public int MergeCount { get; set; }
		/*
		TOP RIGHT CORNER OF ZONE
		*/
		public DateTime ZoneRightX { get; set; } // Right time
		public double ZoneTopY { get; set; } // Top price
		public DateTime ZoneLeftX { get; set; } //Left Bar time
		public double ZoneBottomY { get; set; } //Bottom price
		/*
		SUPPORT OR RESISTANCE?
		If price is below this zone, it is resistance (1)
		If price is above this zone, it is support (-1)
		If price is inside this zone, it is consolidation (0)
		*/
		public int Direction { get; set; }
		public bool Tracked { get; set; }
		public int BarTracker { get; set; }
		public double TrackedAreaBetweenMAAndZeroLine { get; set; }
		public string TestType { get; set; }
		public double VolAccumulation { get; set; }
		public int ConsecutiveBounces { get; set; }
		private double RelativeStrength;
		public double GetRelativeStrength(NinjaScriptBase t)
		{
			//RelativeStrength = (TotalAbsoluteVolume / ((t.Bars.GetTime(t.CurrentBar) - ZoneLeftX).TotalMinutes)) / ((ConsecutiveBounces + 1) * TotalTimesBroken);
			RelativeStrength = (TotalAbsoluteVolume / ((t.Bars.GetTime(t.CurrentBar) - ZoneLeftX).TotalMinutes)) / ((ConsecutiveBounces + 1));
			// Equation option 1
			//double a = 50 / (Math.PI / 2);
			//double calc = a * Math.Atan((1 / (2 * a)) * (RelativeStrength - 100)) + 50;

			// Equation option 2
			double calc = (-500000 / ((RelativeStrength) * (RelativeStrength) + 5000)) + 100;
			return calc;
		}
		// unused
		public void SetRelativeStrength(double rst)
		{
			RelativeStrength = rst;
		}
		// Add function for strength addition
		//1/x + 100 where x is the strength?
		public ZONE(int s, int i, double st, int ex, DateTime zlx, double zty, DateTime zrx, double zby, int dr, double vol)
		{
			Type = s;
			ID = i;
			SetStrength(st);
			ZoneRightX = zrx;
			ZoneTopY = zty;
			ZoneLeftX = zlx;
			ZoneBottomY = zby;
			Direction = dr;
			Tracked = false;
			TrackedAreaBetweenMAAndZeroLine = 0;
			TestType = "";
			OverlapCheck = false;
			Expired = false;
			TotalRelativeVolume = 0;
			VolAccumulation = 0;
			MergeCount = 0;
			TotalTimesBroken = 0;
			RelativeStrength = 0;
			TotalAbsoluteVolume = vol;
			ConsecutiveBounces = 0;

		}
		public void UpdateEnd(NinjaScriptBase t)
		{
			if (!Expired)
			{
				ZoneRightX = t.Bars.GetTime(t.CurrentBar);
			}
		}
		public void RemoveZone() { }
	}

	public partial class LINK
	{
		public double Strength { get; set; }
		public int Direction { get; set; }
		public string Inflection { get; set; }
		public double ZonesAbove { get; set; }
		public double ZonesBelow { get; set; }
		public int NumFullTradingDays { get; set; }
		public double ZoneTopY { get; set; }
		public double ZoneBottomY { get; set; }
		public double AggregateTradeSentiment { get; set; } // 1. Percent of zones above and below 2. Total volume in this area 3. Inflection point occurance 4. Relative volume in this area 5. Relative strength in this area

	}

	public interface ICloneable<T>
	{
		T Clone();
	}

	public partial class LINK : ICloneable<LINK>
	{
		public LINK Clone()
		{
			return new LINK
			{
				Strength = this.Strength,
				Direction = this.Direction,
				Inflection = this.Inflection,
				ZonesAbove = this.ZonesAbove,
				ZonesBelow = this.ZonesBelow,
				NumFullTradingDays = this.NumFullTradingDays,
				ZoneTopY = this.ZoneTopY,
				ZoneBottomY = this.ZoneBottomY,
				AggregateTradeSentiment = this.AggregateTradeSentiment
			};
		}
	}

	// Contains a reference of zone statistics for comparison between strengths
	public class ZoneStatistics
	{
		public string ZoneType { get; set; }
		public double ThisAreaBetweenMAAndZeroLine { get; set; }
		public string PriceActionType { get; set; }
		public double Volume { get; set; }
		public double TimeAlive { get; set; }

	}




	public class ZoneBox
	{
		private readonly AdvancedSRZones indicatorObjectRef;
		private Brush outlineColor = Brushes.SlateGray;
		private Brush areaColor = Brushes.Green;
		private double opacity = 50;
		public int activeLeftSideAbsBar { get; set; }
		public int activeRightSideAbsBar { get; set; }
		public int originalLeftSideAbsBar { get; set; }
		public int originalRightSideAbsBar { get; set; }
		public double topPrice { get; set; }
		public double bottomPrice { get; set; }
		public bool isActive { get; set; }
		public double totalVolume { get; set; }
		public string ID { get; set; }
		public int type { get; set; } // 0 = supp 1 = res
		public ZoneBox(AdvancedSRZones obj, int LSAB, int RSAB, double TP, double BP)
		{
			indicatorObjectRef = obj;
			originalLeftSideAbsBar = activeLeftSideAbsBar = LSAB;
			originalRightSideAbsBar = activeRightSideAbsBar = RSAB;
			topPrice = TP;
			bottomPrice = BP;
			ID = "Box " + originalLeftSideAbsBar.ToString();
			UpdateBox();
		}
		public void DisplayBox()
		{
			activeRightSideAbsBar = indicatorObjectRef.CurrentBar;
			if (type == 0) areaColor = Brushes.Green;
			else if (type == 1) areaColor = Brushes.Red;
			Draw.Rectangle(indicatorObjectRef, ID, true, indicatorObjectRef.CurrentBar - activeLeftSideAbsBar, bottomPrice, indicatorObjectRef.CurrentBar - activeRightSideAbsBar, topPrice, outlineColor, areaColor, (int)opacity, true);

		}

		public void UpdateBox()
		{
			activeRightSideAbsBar = indicatorObjectRef.CurrentBar;
			if (indicatorObjectRef.GetCurrentAsk() >= bottomPrice)
			{
				type = 0;
			}
			else if (indicatorObjectRef.GetCurrentAsk() <= topPrice)
			{
				type = 1;
			}
		}
	}





	public class AdvancedSRZones : Indicator
	{
		bool debugPrint = false;
		public int iLi = 0, jLi = 0, xLi = 0;
		int SMALength = 3;
		// Current low, current high, final high, final low
		double dayLow = Double.MaxValue, dayHigh = double.MinValue, dayTOP = Double.MinValue, dayBOTTOM = Double.MaxValue;
		DateTime dayHighTime, dayLowTime;
		int sum = 0;
		public double totalStrength = 0;


		// LinRegVars
		private double avg;
		private double divisor;
		private double myPeriod;
		private double priorSumXY;
		private double priorSumY;
		private double sumX2;
		private double sumXY;
		private double sumY;
		private double sumSMA;
		private double priorSum;
		private int inflectionOffset = -0;

		private int ZonesAboveToday = -1;
		private int ZonesBelowToday = -1;

		private double[,] priceHitsArray;
		private Series<double> VolumeGraph;

		private List<Double> SMA_l = new List<double>();
		private List<Double> LRS_SMA = new List<double>();

		public List<LINK> LINKLIST = new List<LINK>();
		public List<bool> ZonesAboveExtrema = new List<bool>();
		public List<bool> ZonesBelowExtrema = new List<bool>();

		double zonesAboveTemp = 0;
		double zonesBelowTemp = 0;
		int numFullTradingDays = 0;
		int dayStartBar = 0;
		double slotHalfRange;
		int rightScrnBnum;
		int leftScrnBnum;
		SharpDX.Direct2D1.Brush sessBrush;
		private Brush slotSessionColor = Brushes.Lime;
		int TotalSlots = 500;
		double maxBar = 0;
		double maxPrice = 0;


		readonly List<ZONE> zones = new List<ZONE>();
		List<List<double>> BreakZoneList = new List<List<double>>();
		List<List<double>> BounceZoneList = new List<List<double>>();

		double SupportAreaStrengthThreshold;
		double BreakAreaStrengthThreshold;


		List<List<double>> InflectionPoints = new List<List<double>>();
		double AvgArea = 40;
		List<ZoneBox> ZoneBoxList = new List<ZoneBox>();


		protected override void OnStateChange()
		{


			if (State == State.SetDefaults)
			{
				Description = @"Draws nearest level of S/R lines above and below current market based on historical price swing High/Low pivots";
				Name = "AdvancedSRZones";
				Calculate = Calculate.OnBarClose;
				IsOverlay = false;
				DisplayInDataBox = true;
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
				avg = divisor = myPeriod = priorSumXY = priorSumY = sumX2 = sumY = sumXY = sumSMA = priorSum = 0;
				priceHitsArray = new double[2, TotalSlots];

			}
		}


		protected override void OnBarUpdate()
		{

			// Display high and low of each day


			if (CurrentBar >= 0)
			{

				// Do volume profile stuff
				//DrawVolumeProfileAccessories();

				for (int i=0;i<ZoneBoxList.Count;i++)
				{
					ZoneBoxList[i].UpdateBox();
					ZoneBoxList[i].DisplayBox();
				}

				if (CurrentBar >= 1)
				{
					SupportAreaStrengthThreshold = AreaStrengthMultiplier * 5 / Bars.GetClose(CurrentBar);
					BreakAreaStrengthThreshold = BreakStrengthMultiplier * 5 / Bars.GetClose(CurrentBar);
				}

				//Print(CurrentBar);
				if (Bars.IsFirstBarOfSession)
				{
					ResetSessionVars();
				}

				//Basic SMA Line for integral calculation

				if (CurrentBar > SMALength)
				{
					double value = 0;
					for (int i = SMALength - 1; i >= 0; i--)
					{
						value += Bars.GetClose(CurrentBar - i);
					}
					value /= SMALength;
					//Print(value);

				}


				// Get slope of SMA line
				if (BarsArray[0].BarsType.IsRemoveLastBarSupported)
				{
					// SMA
					if (CurrentBar == 0)
						SMA_l.Add(Input[0]);
					else
					{
						double last = SMA_l[SMA_l.Count - 2] * Math.Min(CurrentBar, SMALength);

						if (CurrentBar >= SMALength)
							SMA_l.Add((last + Input[0] - Input[SMALength]) / Math.Min(CurrentBar, SMALength));
						else
							SMA_l.Add(((last + Input[0]) / (Math.Min(CurrentBar, SMALength) + 1)));
					}
					// END SMA
					// LIN REG
					double sumX = (double)SMALength * (SMALength - 1) * 0.5;
					double divisor = sumX * sumX - (double)SMALength * SMALength * (SMALength - 1) * (2 * SMALength - 1) / 6;
					double sumXY = 0;

					for (int count = 0; count < SMALength && CurrentBar - count >= 0; count++)
						sumXY += count * Input[count];

					LRS_SMA.Add(((double)SMALength * sumXY - sumX * SUM(Inputs[0], SMALength)[0]) / divisor);
					// END LIN REG
				}
				else
				{

					priorSum = sumSMA; // SMA

					priorSumY = sumY;
					priorSumXY = sumXY;
					myPeriod = Math.Min(CurrentBar + 1, SMALength);
					sumX2 = myPeriod * (myPeriod + 1) * 0.5;
					divisor = myPeriod * (myPeriod + 1) * (2 * myPeriod + 1) / 6 - sumX2 * sumX2 / myPeriod;

					//SMA
					sumSMA = priorSum + Input[0] - (CurrentBar >= SMALength ? Input[SMALength] : 0);
					SMA_l.Add(sumSMA / (CurrentBar < SMALength ? CurrentBar + 1 : SMALength));
					// END SMA
					double input0 = Input[0];
					sumXY = priorSumXY - (CurrentBar >= SMALength ? priorSumY : 0) + myPeriod * input0;
					sumY = priorSumY + input0 - (CurrentBar >= SMALength ? Input[SMALength] : 0);
					avg = sumY / myPeriod;
					LRS_SMA.Add(CurrentBar <= SMALength ? 0 : (sumXY - sumX2 * avg) / divisor);

				}

				// Dynamic price zone strength adjustment
				// For supports:
				double totalStrength = 0;
				if (zones.Count > 0 && CurrentBar >= 2)
				{
					totalStrength = 0;
					for (int i = 0; i < zones.Count; i++)
					{

						if (Bars.GetClose(CurrentBar) >= zones[i].ZoneBottomY && Bars.GetClose(CurrentBar) <= zones[i].ZoneTopY)
						{
							totalStrength += zones[i].GetStrength();
						}
						if (Bars.GetClose(CurrentBar) < zones[i].ZoneBottomY)
						{
							zones[i].Direction = 1;
						}
						// If price is above zone, zone is support
						else if (Bars.GetClose(CurrentBar) > zones[i].ZoneTopY)
						{
							zones[i].Direction = -1;
						}
						else if (Bars.GetClose(CurrentBar) <= zones[i].ZoneTopY && Bars.GetClose(CurrentBar) >= zones[i].ZoneBottomY)
						{
							zones[i].TotalAbsoluteVolume += VOL()[0];
						}




						// Update currently tracked zones strengths
						if (zones[i].Tracked && zones[i].Expired == false)
						{

							// Update total absolute volume for zone
							double VolAccumulation = 0;
							if (UseVolAccumulation)
							{
								zones[i].VolAccumulation += RelativeVolumeNT8(TimeThreshold, 2, 30)[0];
								VolAccumulation = zones[i].VolAccumulation;
							}

							if (zones[i].TestType == "sup")
							{
								zones[i].TrackedAreaBetweenMAAndZeroLine += ProxyStrengthMultiplier * (SMA_l[SMA_l.Count - 1] - Bars.GetOpen(zones[i].BarTracker)) / Bars.GetClose(CurrentBar);
							}
							else
							{
								zones[i].TrackedAreaBetweenMAAndZeroLine += ProxyStrengthMultiplier * (Bars.GetOpen(zones[i].BarTracker) - SMA_l[SMA_l.Count - 1]) / Bars.GetClose(CurrentBar);
							}
							//Print(zones[i].StrengthProxy);
							if ((Bars.GetTime(CurrentBar) - Bars.GetTime(zones[i].BarTracker)).Minutes >= TimeThreshold)
							{
								// if the price bounces/reverses off the zone
								if (zones[i].TrackedAreaBetweenMAAndZeroLine >= SupportAreaStrengthThreshold)
								{

									zones[i].SetStrength(zones[i].TrackedAreaBetweenMAAndZeroLine + VolAccumulation);
									zones[i].ConsecutiveBounces++;
									/*
									if (zones[i].TestType == "sup") Draw.Text(this, zones[i].ID + " Sup Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
									else Draw.Text(this, zones[i].ID + " Res Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
									*/
								}
								// zones that are broken through should weaken
								else if (zones[i].TrackedAreaBetweenMAAndZeroLine <= BreakAreaStrengthThreshold)
								{
									//Print("break");
									// realistically, this should be a negative value.
									// supports that are broken will see the price further break down
									// resistance that is broken will see the price further rally
									// Relate the break of a zone with relative VOLUME!!! THe more volume needed to break, the STRONGER THE ZONE IS!!! If low volume was needed to break, THE ZONE IS AND WILL BE WEAK!
									zones[i].SetStrength(zones[i].TrackedAreaBetweenMAAndZeroLine + VolAccumulation);
									//Print(zones[i].GetStrength());
									zones[i].TotalTimesBroken++;
									zones[i].ConsecutiveBounces = 0;
									/*
									if (zones[i].TestType == "sup") Draw.Text(this, zones[i].ID + " Sup broken : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
									else Draw.Text(this, zones[i].ID + " Res broken : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
									*/
								}
								else
								{
									// Artificial way of adding "decay" to zones where price keeps on consolidating
									zones[i].SetStrength(VolAccumulation);
									/*
									if (zones[i].TestType == "sup") Draw.Text(this, zones[i].ID + " Strikeout Sup Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
									else Draw.Text(this, zones[i].ID + " Strikeout Res Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
									*/
								}
								zones[i].Tracked = false;
								zones[i].BarTracker = 0;
								zones[i].TestType = "";
								zones[i].TrackedAreaBetweenMAAndZeroLine = 0;
								zones[i].TotalRelativeVolume += VolAccumulation;
								zones[i].VolAccumulation = 0;

							}
						}
						if (zones[i].Direction == -1 && ((Bars.GetClose(CurrentBar) <= zones[i].ZoneTopY && Bars.GetClose(CurrentBar) >= zones[i].ZoneBottomY)))
						{
							if (zones[i].Tracked == false)
							{

								zones[i].Tracked = true;
								zones[i].BarTracker = CurrentBar;
								//Draw.Diamond(this, "Zero line #" + CurrentBar, true, Bars.GetTime(CurrentBar), Bars.GetClose(CurrentBar), Brushes.Green);
								zones[i].TestType = "sup";

							}
						}

						else if (zones[i].Direction == 1 && ((Bars.GetClose(CurrentBar) <= zones[i].ZoneTopY && Bars.GetClose(CurrentBar) >= zones[i].ZoneBottomY)))
						{
							if (zones[i].Tracked == false)
							{


								zones[i].Tracked = true;
								zones[i].BarTracker = CurrentBar;
								//Draw.Diamond(this, "Zero line #" + CurrentBar, true, Bars.GetTime(CurrentBar), Bars.GetClose(CurrentBar), Brushes.Red);
								zones[i].TestType = "res";

							}
						}
					}
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
							if (type == 1) Draw.Diamond(this, "Inflection pt dn " + (int)bar, true, Bars.GetTime((int)bar), Bars.GetHigh((int)bar), Brushes.Red);
							else if (type == 2) Draw.Diamond(this, "Inflection pt up " + (int)bar, true, Bars.GetTime((int)bar), Bars.GetLow((int)bar), Brushes.Green);

							int validPointExtrema = 0;
							int fastPeriod = 3;
							Brush outlineColor = Brushes.DarkSlateGray;
							Brush areaColor = Brushes.Khaki;
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
									Draw.Diamond(this, "Inflection pt dn extrema " + validPointExtrema, true, Bars.GetTime(validPointExtrema), Bars.GetHigh(validPointExtrema), Brushes.DarkRed);
									//Draw.Rectangle(this, "Inflection pt box " + validPointExtrema, true, CurrentBar-validPointExtrema, Bars.GetHigh(validPointExtrema), CurrentBar - (int)InflectionPoints[i][0], Bars.GetHigh((int)InflectionPoints[i][0]), outlineColor, areaColor, 80);
									ZoneBoxList.Add(new ZoneBox(this, validPointExtrema, (int)bar, Math.Max(Bars.GetHigh((int)bar), Bars.GetHigh(validPointExtrema)), Math.Min(Bars.GetHigh((int)bar), Bars.GetHigh(validPointExtrema))));
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
									Draw.Diamond(this, "Inflection pt up extrema " + validPointExtrema, true, Bars.GetTime(validPointExtrema), Bars.GetLow(validPointExtrema), Brushes.LightGreen);
									//Draw.Rectangle(this, "Inflection pt box " + validPointExtrema, true, CurrentBar - validPointExtrema, Bars.GetLow(validPointExtrema), CurrentBar - (int)InflectionPoints[i][0], Bars.GetLow((int)InflectionPoints[i][0]), outlineColor, areaColor, 80);
									ZoneBoxList.Add(new ZoneBox(this, validPointExtrema, (int)bar, Math.Min(Bars.GetLow((int)bar), Bars.GetLow(validPointExtrema)), Math.Max(Bars.GetLow((int)bar), Bars.GetLow(validPointExtrema))));

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
					totalTimeToCompare = 30;
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
					/*
					for (int i=0; i<priceHitsArray.Length/2;i++)
					{
						Print(priceHitsArray[0, i] + " | " + priceHitsArray[1, i]);
					}
					*/
					numFullTradingDays++;
					if (zones.Count > 0 && Expiration != -1)
					{
						for (int i = 0; i < zones.Count; i++)
						{
							if ((Bars.GetTime(CurrentBar) - zones[i].ZoneLeftX).Days >= Expiration)
							{
								zones[i].Expired = true;
							}
						}
					}
					// default values
					TimeSpan x1, x2;
					double tty, tby, st = NewZoneStrength;
					double tempVol = 1;
					x1 = new TimeSpan(1, 30, 0);
					//x2 = new TimeSpan(1, 30, 0);
					#region LODZONE
					tty = dayLow + (NewZoneTopMultiplier * dayLow);
					tby = dayLow - (NewZoneBottomMultiplier * dayLow);

					// retrieve volume information for the zone, dynamically

					for (int i = Bars.GetBar(dayLowTime - x1); i < Bars.GetBar(Bars.GetTime(CurrentBar)); i++)
					{
						if ((Bars.GetClose(i) <= tty && Bars.GetClose(i) >= tby) || (Bars.GetLow(i) <= tty && Bars.GetLow(i) >= tby))
						{
							tempVol += Bars.GetVolume(i);
						}
					}
					//Print(tempVol);
					zones.Add(new ZONE(2, zones.Count + 1, st, -1, dayLowTime - x1, tty, Bars.GetTime(CurrentBar), tby, 1, tempVol));
					sum++;
					// MergeZones();
					// Make sure we have zones to work with
					if (zones.Count > 0)
					{

						// Compute zone area based on chart history
						// Neural network
						zones[zones.Count - 1].Type = 2;

						// if close is under zone bottom
						if (Bars.GetClose(CurrentBar) <= zones[zones.Count - 1].ZoneBottomY)
						{
							// Bar closed below or at zone
							zones[zones.Count - 1].Direction = 1;
						}
						else if (Bars.GetClose(CurrentBar) >= zones[zones.Count - 1].ZoneTopY)
						{
							// Bar closed above or at zone
							zones[zones.Count - 1].Direction = -1;
						}
					}
					#endregion
					#region HODZONE
					// Add high and low zones to Zones
					// Top Y is day high plus 5% of day high
					// Default settings for HOD zone
					// Will be adjusted by neural net
					tty = dayHigh + (NewZoneTopMultiplier * dayHigh);
					tby = dayHigh - (NewZoneBottomMultiplier * dayHigh);

					for (int i = Bars.GetBar(dayLowTime - x1); i < Bars.GetBar(Bars.GetTime(CurrentBar)); i++)
					{
						if ((Bars.GetClose(i) <= tty && Bars.GetClose(i) >= tby) || (Bars.GetHigh(i) <= tty && Bars.GetHigh(i) >= tby))
						{
							tempVol += Bars.GetVolume(i);
						}
					}
					//Print(tempVol);
					zones.Add(new ZONE(1, zones.Count + 1, st, -1, dayHighTime - x1, tty, Bars.GetTime(CurrentBar), tby, 1, tempVol));
					// MergeZones();
					// Make sure we have zones to work with
					if (zones.Count > 0)
					{

						// Compute zone area based on chart history
						// Neural network
						zones[zones.Count - 1].Type = 1;

						// if close is under zone bottom
						if (Bars.GetClose(CurrentBar) <= zones[zones.Count - 1].ZoneBottomY)
						{
							// Bar closed below or at zone
							zones[zones.Count - 1].Direction = 1;
						}
						else if (Bars.GetClose(CurrentBar) >= zones[zones.Count - 1].ZoneTopY)
						{
							// Bar closed above or at zone
							zones[zones.Count - 1].Direction = -1;
						}
					}
					#endregion
					ZonesAboveExtrema.Add(true ? ZonesAboveToday == 0 : false);
					ZonesBelowExtrema.Add(true ? ZonesBelowToday == 0 : false);
					ZonesAboveToday = -1;
					ZonesBelowToday = -1;



					/*
					// FIRST DO BOXES CREATED TODAY
					// Merge boxes if they fit
					for (int i = 0; i < ZoneBoxList.Count; i++)
					{
						ZoneBox thisBox = ZoneBoxList[i];
						if (thisBox.originalLeftSideAbsBar >= dayStartBar)
						{
							for (int j = 1; j < ZoneBoxList.Count; j++)
							{
								ZoneBox thisSecondBox = ZoneBoxList[j];
								if (thisBox.originalLeftSideAbsBar >= dayStartBar)
								{
									// we're now comparing (each box against each box) created during the day

								}
							}
						}
						// find the closest box
						ZoneBox zbMin;
						for (int j = 1; j < ZoneBoxList.Count; j++)
						{
							if (ZoneBoxList[i].bottomPrice)
						}
					}
					*/

				}
				MergeZones();
				zones.ForEach(delegate (ZONE z)
				{
					z.UpdateEnd(this);
				});
				ComputeZones(zones);
				ScuffedVolumeProfile();
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
			dayStartBar = CurrentBar;
			dayTOP = Bars.GetHigh(CurrentBar);
			dayBOTTOM = Bars.GetLow(CurrentBar);
			dayHigh = Bars.GetHigh(CurrentBar);
			dayLow = Bars.GetLow(CurrentBar);
			dayHighTime = Bars.GetTime(CurrentBar);
			dayLowTime = Bars.GetTime(CurrentBar);
		}

		public void ComputeZones(List<ZONE> z)
		{
			if (zones.Count > 0)
			{
				double tempst = 0;
				int tempdir = 0;
				double zty;
				double zby;
				double tvol = 0;
				Brush outline = Brushes.DarkSlateGray;
				Brush area = Brushes.Khaki;
				zonesAboveTemp = 0;
				zonesBelowTemp = 0;
				for (int i = 0; i < zones.Count; i++)
				{

					if (Bars.GetClose(CurrentBar) <= zones[i].ZoneTopY && Bars.GetClose(CurrentBar) >= zones[i].ZoneBottomY && zones[i].Expired == false)
					{
						//tempst += zones[i].GetStrength();
						tempst += zones[i].GetRelativeStrength(this);
						tempdir += zones[i].Direction;
						zty = zones[i].ZoneTopY;
						zby = zones[i].ZoneBottomY;
						tvol += zones[i].TotalAbsoluteVolume;
					}


					// Get the percent of zones above the price and percent of zones below price, this can be a good indicator of how bullish or bearish a stock is
					// High percent of zones above the price means the stock is bearish, and vice versa
					// if the last bar is being scanned

					if ((zones[i].ZoneBottomY > Close[0] || zones[i].ZoneTopY > Close[0]) && zones[i].Expired == false)
					{
						zonesAboveTemp++;
					}
					else if ((zones[i].ZoneTopY < Close[0] || zones[i].ZoneBottomY < Close[0])  && zones[i].Expired == false)
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
					if (i == zones.Count - 1)
					{
						var total = zonesAboveTemp + zonesBelowTemp;
						//Print(zonesAboveTemp + " " + zonesBelowTemp);
						if (total != 0)
						{
							zonesAboveTemp /= total;
							zonesBelowTemp /= total;
						}

					}


					if (z[i].Direction == 1) area = ResZoneColor;
					else if (z[i].Direction == -1) area = SupZoneColor;
					else area = Brushes.Yellow;
					//Draw.Rectangle(this, "Zone ID: " + z[i].ID.ToString(), true, z[i].ZoneLeftX, z[i].ZoneBottomY, z[i].ZoneRightX, z[i].ZoneTopY, outline, area, Convert.ToInt32(z[i].GetRelativeStrength(this)/10), true);
					// Draw text for absolute volume inside zone, basically a scuffed volume profile indicator
					//Draw.Text(this, zones[i].ID.ToString(), zones[i].GetRelativeStrength(this).ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
				}
				double mul = 0;
				if (zonesAboveTemp > 0.5)
				{
					mul = -2 * zonesAboveTemp;
				}
				else if (zonesAboveTemp < 0.5)
				{
					mul = 2 * zonesAboveTemp;
				}
				double agg = (tvol / numFullTradingDays) * mul + tempst;
				var temp = "";
				if (LRS_SMA.Count > SMALength + Math.Abs(inflectionOffset))
				{
					if ((LRS_SMA[LRS_SMA.Count - 2 - Math.Abs(inflectionOffset)] > 0 && LRS_SMA[LRS_SMA.Count - 1 - Math.Abs(inflectionOffset)] < 0))
					{
						// crossed down (top)
						temp = "D";
					}
					else if (LRS_SMA[LRS_SMA.Count - 2 - Math.Abs(inflectionOffset)] < 0 && LRS_SMA[LRS_SMA.Count - 1 - Math.Abs(inflectionOffset)] > 0)
					{
						// crossed up (bottom)
						temp = "U";
					}
				}
				LINKLIST.Add(new LINK
				{
					Strength = tempst,
					Direction = tempdir,
					Inflection = temp,
					ZonesAbove = zonesAboveTemp,
					ZonesBelow = zonesBelowTemp,
					NumFullTradingDays = numFullTradingDays,
					AggregateTradeSentiment = agg
				});
			}
		}


		public void MergeZones()
		{
			/*
			 * Merge overlapping zones
			 * Very basic two-layer neural network
			 * Percent threshold decides merging of overlapping zones
			 * Theoretically, machine learning is NOT necessary for merging zones
			 * Only called after a new zone is created (excluding this function)
			*/
			// Check if new zone overlaps with any current zone
			if (zones.Count > 1)
			{
				for (int i = 0; i < zones.Count; i++)
				{
					ZONE z1 = zones[i];
					for (int j = 0; j < zones.Count; j++)
					{
						ZONE z2 = zones[j];
						//if (z1.Tracked || z2.Tracked) return;
						// don't merge expired zones
						if (z1.ID != z2.ID && z2.Expired == false && z1.Expired == false)
						{
							//Print("test");
							// ZoneTopY merge
							double Zone1TopThresholdA = z1.ZoneTopY + (z1.ZoneTopY * (MergeThreshold));
							double Zone1TopThresholdB = z1.ZoneTopY - (z1.ZoneTopY * (MergeThreshold));
							double Zone1BottomThresholdA = z1.ZoneBottomY - (z1.ZoneBottomY * (MergeThreshold));
							double Zone1BottomThresholdB = z1.ZoneBottomY + (z1.ZoneBottomY * (MergeThreshold));

							int merge = 0;

							if (z2.ZoneTopY <= Zone1TopThresholdA && z2.ZoneTopY >= Zone1TopThresholdB)
							{
								// Zone2 top can merge with zone 1 top
								merge++;
							}
							if (z2.ZoneBottomY >= Zone1TopThresholdB && z2.ZoneBottomY <= Zone1TopThresholdA)
							{
								// Zone 2 bottom can merge with zone 1 top
								merge++;
							}
							if (z2.ZoneBottomY >= Zone1BottomThresholdA && z2.ZoneBottomY <= Zone1BottomThresholdB)
							{
								// Zone 2 bottom can merge with zone 1 bottom
								merge++;
							}
							if (z2.ZoneTopY <= Zone1BottomThresholdA && z2.ZoneBottomY >= Zone1BottomThresholdB)
							{
								// Zone 2 top can merge with zone 1 bottom
								merge++;
							}
							//Print(merge);
							if (merge >= 2)
							{

								bool temp = false;
								int ind = 0;
								int ind2 = 0;
								// Zones z1 and z2 can be merged
								// ALWAYS MERGE NEW INTO OLD - so it looks pretty
								if (zones[j].ID > zones[i].ID)
								{
									ind = i;
									ind2 = j;
									temp = true;
								}
								else
								{
									ind = j;
									ind2 = i;
									temp = true;
								}
								if (temp && zones[ind].MergeCount <= MaxMergeCount)
								{
									//Print("Zone merged");
									zones[ind].ZoneRightX = Bars.GetTime(CurrentBar);
									zones[ind].ZoneTopY = (Math.Max(zones[ind].ZoneTopY, zones[ind2].ZoneTopY) + Math.Min(zones[ind].ZoneTopY, zones[ind2].ZoneTopY)) / 2;
									zones[ind].ZoneBottomY = (Math.Min(zones[ind].ZoneBottomY, zones[ind2].ZoneBottomY) + Math.Max(zones[ind].ZoneBottomY, zones[ind2].ZoneBottomY)) / 2;
									zones[ind].MergeCount++;
									// weighted strength application, the whale zones should weight far more than newely created tiny zones
									zones[ind].SetStrengthDirect((zones[ind2].GetStrength() * zones[ind2].TotalAbsoluteVolume + zones[ind].GetStrength() * zones[ind].TotalAbsoluteVolume) / (zones[ind2].TotalAbsoluteVolume + zones[ind].TotalAbsoluteVolume));
									zones[ind].TotalRelativeVolume += zones[ind2].TotalRelativeVolume;
									zones[ind].TotalTimesBroken += zones[ind2].TotalTimesBroken;
									//zones[ind].TotalAbsoluteVolume += zones[ind2].TotalAbsoluteVolume;
									zones.RemoveAt(ind2);
								}
								// BUG CHECK: MERGE CREATES ZONES THAT ARE TOO STRONG: EVEN AFTER ONE MERGE THEY GO TO 85 STRENGTH?

								// If zone J has already been merged more than two times

							}
						}
					}
				}
			}
			// restructure zone IDs

		}


		public void DrawVolumeProfileAccessories()
		{
			if (priceHitsArray.Length / 2 > 0)
			{
				Draw.Line(this, "max" + Time[0].Month + "/" + Time[0].Day, false, Bars.GetTime(dayStartBar), maxPrice, Bars.GetTime(CurrentBar), maxPrice, "");
			}
		}

		public bool WasZeroResX(int days)
		{
			if (ZonesAboveExtrema.Count > 0)
			{
				int index = ZonesAboveExtrema.Count - days;
				if (index < 0 && index >= ZonesAboveExtrema.Count) return false;
				if (ZonesAboveExtrema[index]) return true;
			}
			return false;
		}

		public ZONE GetZone(double price)
		{
			if (zones.Count > 0)
			{
				for (int i = 0; i < zones.Count; i++)
				{
					if (price >= zones[i].ZoneBottomY && price <= zones[i].ZoneTopY)
					{
						return zones[i];
					}
				}
			}
			return null;
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


				// draw
				if (Bars.IsLastBarOfSession)
				{
					Draw.Line(this, "vabot" + Time[0].Month + "/" + Time[0].Day, false, Bars.GetTime(dayStartBar), sessVAbot, Bars.GetTime(CurrentBar), sessVAbot, "");
					Draw.Line(this, "vatop" + Time[0].Month + "/" + Time[0].Day, false, Bars.GetTime(dayStartBar), sessVAtop, Bars.GetTime(CurrentBar), sessVAtop, "");
					Draw.Line(this, "max" + Time[0].Month + "/" + Time[0].Day, false, Bars.GetTime(dayStartBar), maxPrice, Bars.GetTime(CurrentBar), maxPrice, "");
				}
				

			}

		   
			

		}

		/*
	   private void DrawSlotsUV(ChartScale chartScale, int slotHeight, SharpDX.Direct2D1.Brush eBrush)
	   {
		   // draw slots universal
		   // left or right based, and percentage of area

		   int hWidth, yPos, vPos, vHeight, prevYpos = 0, barsInRange = 0;
		   double sessMaxHits = 0.0;
		   int ScreenPercent = 100;

		   int sbarNum = leftScrnBnum; //default is left most screen bar.
		   int ebarNum = sbarNum + barsInRange;
		   int barPaintWidth = ChartControl.GetBarPaintWidth(ChartBars);
		   int halfBarWidth = (int)(barPaintWidth * 0.5);
		   int totalHeight = Convert.ToInt32(ChartPanel.Y + ChartPanel.H);


		   if (--barsInRange < 1)
			   return; //have at least 1 bar to paint. Don't paint last bar.

		   int leftMostPos = ChartControl.GetXByBarIndex(ChartBars, sbarNum) - halfBarWidth;
		   int rightMostPos = ChartControl.GetXByBarIndex(ChartBars, ebarNum);

		   double price = ((rightMostPos - leftMostPos) * ((100 - ScreenPercent) * 0.01));    //temp usage of price
		   rightMostPos -= (int)price;

		   int totalWidth = rightMostPos - leftMostPos;


		   int ticksInRange = (int)Math.Round((dayHigh - dayLow) / TickSize, 0);

		   //fit ticks into array by so many TicksPerSlot
		   int ticksPerSlot = (ticksInRange / TotalSlots) + 1; //should just drop the fract part.
		   int le = ticksInRange / ticksPerSlot;


		   for (int i=0;i< le; i++)
		   {

			   yPos = chartScale.GetYByValue(price);

			   vPos = yPos + slotHeight;
			   if (i != 0)
				   vPos = prevYpos;

			   prevYpos = yPos;    //incase we continue here.
			   if (yPos >= totalHeight || vPos <= 1)
				   continue;   //too low or high to display

			   //take out any negitive portion
			   yPos += (yPos < 0 ? yPos * -1 : 0);

			   if (vPos > totalHeight)
				   vPos = totalHeight;
			   vHeight = vPos - yPos;
			   prevYpos = yPos;

			   price = priceHitsArray[0, i] + slotHalfRange;   //take it from mid to top.
			   hWidth = (int)((totalWidth) * (priceHitsArray[1, i] / (sessMaxHits)));

			   SharpDX.RectangleF rect = new SharpDX.RectangleF(leftMostPos + (totalWidth - hWidth), yPos, hWidth, vHeight);


			   RenderTarget.FillRectangle(rect, eBrush);
		   }


	   }

	   protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
	   {
		   base.OnRender(chartControl, chartScale);

		   if (BarsArray[0].Count < 2 || chartControl.SlotsPainted < 2)
			   return;

		   int rightScrnBnum = Math.Min(ChartBars.ToIndex, CurrentBar); //limit to actual bars vs. white space on right.
		   int leftScrnBnum = Math.Min(ChartBars.FromIndex, CurrentBar); //not bigger than CurrentBar (ie. only 1 bar on screen).

		   int rightMostSessNum = CurrentBar;

		   double chartLowPrice = double.MaxValue;
		   for (int i = leftScrnBnum; i <= rightScrnBnum; i++)
			   chartLowPrice = Math.Min(chartLowPrice, Bars.GetLow(i));

		   int screenVLowPos = chartScale.GetYByValue(chartLowPrice);



		   int SlotVHeight = screenVLowPos - chartScale.GetYByValue(chartLowPrice + (slotHalfRange * 2.0));
		   if (SlotVHeight < 1)
			   SlotVHeight = 1;    //at least 1
		   ScuffedVolumeProfile();
		   DrawSlotsUV(chartScale, SlotVHeight, sessBrush);

	   }

	   public override void OnRenderTargetChanged()
	   {
		   if (sessBrush != null)
			   sessBrush.Dispose();

		   if (RenderTarget != null)
		   {
			   try
			   {
				   sessBrush = slotSessionColor.ToDxBrush(RenderTarget);
			   }
			   catch (Exception e) { }
		   }
	   }
	   */






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
