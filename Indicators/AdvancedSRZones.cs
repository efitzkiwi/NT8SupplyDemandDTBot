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

	public class LINK
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

	// Contains a reference of zone statistics for comparison between strengths
	public class ZoneStatistics
	{
		public string ZoneType { get; set; }
		public double ThisAreaBetweenMAAndZeroLine { get; set; }
		public string PriceActionType { get; set; }
		public double Volume { get; set; }
		public double TimeAlive { get; set; }

	}
	public class AdvancedSRZones : Indicator
	{
		bool debugPrint = false;
		public int i = 0, j = 0, x = 0;
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

		private List<Double> SMA_l = new List<double>();
		private List<Double> LRS_SMA = new List<double>();

		public List<LINK> LINKLIST = new List<LINK>();

		double zonesAboveTemp = 0;
		double zonesBelowTemp = 0;
		int numFullTradingDays = 0;


		readonly List<ZONE> zones = new List<ZONE>();
		List<List<double>> BreakZoneList = new List<List<double>>();
		List<List<double>> BounceZoneList = new List<List<double>>();

		double SupportAreaStrengthThreshold;
		double BreakAreaStrengthThreshold;


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
			}
		}


		protected override void OnBarUpdate()
		{
			//Value[0] = 10;
			//Print("INDI 1");	
			// Display high and low of each day

			if (CurrentBar >= 0)
			{
				//Print("INDI 2");
				if (CurrentBar >= 1)
				{
					SupportAreaStrengthThreshold = AreaStrengthMultiplier * 5 / Bars.GetClose(CurrentBar);
					BreakAreaStrengthThreshold = BreakStrengthMultiplier * 5 / Bars.GetClose(CurrentBar);
				}
				//Print(CurrentBar);
				if (Bars.IsFirstBarOfSession)
				{
					dayTOP = Bars.GetHigh(CurrentBar);
					dayBOTTOM = Bars.GetLow(CurrentBar);
					dayHigh = Bars.GetHigh(CurrentBar);
					dayLow = Bars.GetLow(CurrentBar);
					dayHighTime = Bars.GetTime(CurrentBar);
					dayLowTime = Bars.GetTime(CurrentBar);
					//Draw.Diamond(this, "Day Low: " + dayLow.ToString() + " at " + dayLowTime, true, dayLowTime, dayLow, Brushes.White);
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














						// if (zones[i].OverlapCheck)
						// {
						// 	if (Bars.IsFirstBarOfSession) zones[i].BarTracker = CurrentBar;
						// 	if (zones[i].TestType == "sup")
						// 	{
						// 		zones[i].StrengthProxy += ProxyStrengthMultiplier * (SMA_l[SMA_l.Count - 1] - Bars.GetClose(zones[i].BarTracker)) / Bars.GetClose(CurrentBar);
						// 		//Print(zones[i].StrengthProxy);
						// 		if (((Bars.GetTime(CurrentBar) - Bars.GetTime(zones[i].BarTracker)).Minutes >= zones[i].CloseOverlap) && zones[i].OverlapCheck)
						// 		{
						// 			//Print(zones[i].ID.ToString() + " Sup timed out, strength: " + zones[i].GetStrength() + ", proxy strength: " + zones[i].StrengthProxy + ", Threshold: " + SupportAreaStrengthThreshold.ToString());
						// 			// The zone failed to maintain the threshold to be considered a "good support" during this time period
						// 			// Zone adds whatever strength occurred during this time, be it positive or negative as long as it was under the thresholds
						// 			////*
						// 			if (Math.Abs(zones[i].StrengthProxy) >= SupportAreaStrengthThreshold)
						// 			{
						// 				zones[i].SetStrength(zones[i].StrengthProxy);
						// 				Draw.Text(this, zones[i].ID + " Sup overlap Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, Bars.GetClose(CurrentBar));
						// 			}
						// 			else
						// 			{
						// 				// Artificial way of adding "decay" to zones where price keeps on consolidating
						// 				zones[i].SetStrength(-1);
						// 				Print("Strikeout! Overlap sup");
						// 				Draw.Text(this, zones[i].ID + " Strikeout overlap Sup Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, Bars.GetClose(CurrentBar));
						// 			}


						// 			zones[i].Tracked = false;
						// 			zones[i].OverlapCheck = false;
						// 			zones[i].BarTracker = 0;
						// 			zones[i].TestType = "";
						// 			zones[i].StrengthProxy = 0;

						// 		}

						// 		// sma line minus zero line at the open
						// 	}
						// 	else if (zones[i].TestType == "res")
						// 	{
						// 		zones[i].StrengthProxy += ProxyStrengthMultiplier * (Bars.GetClose(zones[i].BarTracker) - SMA_l[SMA_l.Count - 1]) / Bars.GetClose(CurrentBar);
						// 		//Print(zones[i].StrengthProxy);
						// 		if (((Bars.GetTime(CurrentBar) - Bars.GetTime(zones[i].BarTracker)).Minutes >= zones[i].CloseOverlap) && zones[i].OverlapCheck)
						// 		{
						// 			//Print(zones[i].ID.ToString() + " Res timed out, strength: " + zones[i].GetStrength() + ", proxy strength: " + zones[i].StrengthProxy + ", Threshold: " + SupportAreaStrengthThreshold.ToString());
						// 			// The zone failed to maintain the threshold to be considered a "good support" during this time period
						// 			// Zone adds whatever strength occurred during this time, be it positive or negative as long as it was under the thresholds
						// 			if (Math.Abs(zones[i].StrengthProxy) >= SupportAreaStrengthThreshold)
						// 			{
						// 				zones[i].SetStrength(zones[i].StrengthProxy);
						// 				Draw.Text(this, zones[i].ID + "  res Overlap Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, Bars.GetClose(CurrentBar));

						// 			}
						// 			else
						// 			{
						// 				// Artificial way of adding "decay" to zones where price keeps on consolidating
						// 				zones[i].SetStrength(-1);
						// 				Print("Strikeout! Overlap res");
						// 				Draw.Text(this, zones[i].ID + " Strikeout overlap res Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, Bars.GetClose(CurrentBar));
						// 			}
						// 			zones[i].Tracked = false;
						// 			zones[i].OverlapCheck = false;
						// 			zones[i].BarTracker = 0;
						// 			zones[i].TestType = "";
						// 			zones[i].StrengthProxy = 0;

						// 		}
						// 	}
						// }













						// Debug
						//Draw.Text(this, zones[i].ID + " : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
						// (Bars.GetOpen(CurrentBar) + Bars.GetClose(CurrentBar))/2
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
							//Print(VOL()[0]);
						}





						//Print(((decimal)(100 * Math.Pow((double)(1 / 2), (double)(5 / 5750)))).ToString());
						//Print(   Math.Pow(1 / 2, 2));
						//Print(zones[i].Decay);
						// Update currently tracked zones strengths
						if (zones[i].Tracked && zones[i].Expired == false)
						{

							// if (((Bars.GetTime(CurrentBar) - Bars.GetTime(zones[i].BarTracker)).Minutes < TimeThreshold) && Bars.IsLastBarOfSession)
							// {

							// 	zones[i].Tracked = false;
							// 	zones[i].OverlapCheck = true;
							// 	zones[i].CloseOverlap = (Bars.GetTime(CurrentBar) - Bars.GetTime(zones[i].BarTracker)).Minutes - TimeThreshold;


							// }
							// Update total absolute volume for zone
							double VolAccumulation = 0;
							if (UseVolAccumulation)
							{
								zones[i].VolAccumulation += RelativeVolumeNT8(TimeThreshold, 2)[0];
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
								//Print(SupportAreaStrengthThreshold + " | " + BreakAreaStrengthThreshold + " | " +  zones[i].StrengthProxy);
								//Print(zones[i].ID.ToString() + " Sup timed out, strength: " + zones[i].GetStrength() + ", proxy strength: " + zones[i].StrengthProxy + ", Threshold: " + SupportAreaStrengthThreshold.ToString());
								// The zone failed to maintain the threshold to be considered a "good support" during this time period
								// Zone adds whatever strength occurred during this time, be it positive or negative as long as it was under the thresholds
								////*
								// if the price bounces/reverses off the zone
								if (zones[i].TrackedAreaBetweenMAAndZeroLine >= SupportAreaStrengthThreshold)
								{

									zones[i].SetStrength(zones[i].TrackedAreaBetweenMAAndZeroLine + VolAccumulation);
									zones[i].ConsecutiveBounces++;
									if (zones[i].TestType == "sup") Draw.Text(this, zones[i].ID + " Sup Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
									else Draw.Text(this, zones[i].ID + " Res Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
								}
								// zones that are broken through should weaken
								else if (zones[i].TrackedAreaBetweenMAAndZeroLine <= BreakAreaStrengthThreshold)
								{
									//Print("break");
									// realistically, this should be a negative value.
									// supports that are broken will see the price further break down
									// resistance that is broken will see the price further rally
									// Relate the break of a zone with relative VOLUME!!! THe more volume needed to break, the STRONGER THE ZONE IS!!! If low volume was needed to break, THE ZONE IS AND WILL BE WEAK!
									//Print(zones[i].StrengthProxy + " | " + VolAccumulation);
									//Print( zones[i].GetStrength() + " | " + zones[i].StrengthProxy + VolAccumulation / 2);
									zones[i].SetStrength(zones[i].TrackedAreaBetweenMAAndZeroLine + VolAccumulation);
									//Print(zones[i].GetStrength());
									zones[i].TotalTimesBroken++;
									zones[i].ConsecutiveBounces = 0;
									if (zones[i].TestType == "sup") Draw.Text(this, zones[i].ID + " Sup broken : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
									else Draw.Text(this, zones[i].ID + " Res broken : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
								}
								else
								{
									// Artificial way of adding "decay" to zones where price keeps on consolidating
									zones[i].SetStrength(VolAccumulation);
									//Print("Strikeout!");
									if (zones[i].TestType == "sup") Draw.Text(this, zones[i].ID + " Strikeout Sup Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
									else Draw.Text(this, zones[i].ID + " Strikeout Res Tested : " + zones[i].GetStrength().ToString(), zones[i].GetStrength().ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
								}
								//Print(zones[i].GetStrength() + " " + zones[i].VolAccumulation);
								//Print(zones[i].GetStrength());
								zones[i].Tracked = false;
								zones[i].BarTracker = 0;
								zones[i].TestType = "";
								zones[i].TrackedAreaBetweenMAAndZeroLine = 0;
								zones[i].TotalRelativeVolume += VolAccumulation;
								zones[i].VolAccumulation = 0;

							}
						}
						// If previous bar LOW OR CLOSE is inside a zone and this zone is not currently being tracked (prevent duplicate adjustment)
						// || (Bars.GetLow(CurrentBar - 1) <= zones[i].ZoneTopY && Bars.GetLow(CurrentBar - 1) >= zones[i].ZoneBottomY)
						if (zones[i].Direction == -1 && ((Bars.GetClose(CurrentBar) <= zones[i].ZoneTopY && Bars.GetClose(CurrentBar) >= zones[i].ZoneBottomY)))
						{
							if (zones[i].Tracked == false)
							{

								// if (Bars.GetClose(CurrentBar) > Bars.GetOpen(CurrentBar))
								// {
								// 	zones[i].Tracked = true;
								// 	zones[i].BarTracker = CurrentBar;
								// 	//Draw.Line(NinjaScriptBase owner, string tag, int startBarsAgo, double startY, int endBarsAgo, double endY, Brush brush)
								// 	Draw.Diamond(this, "Zero line #" + CurrentBar, true, Bars.GetTime(CurrentBar), Bars.GetClose(CurrentBar), Brushes.White);
								// 	zones[i].TestType = "sup";

								// }

								zones[i].Tracked = true;
								zones[i].BarTracker = CurrentBar;
								//Draw.Line(NinjaScriptBase owner, string tag, int startBarsAgo, double startY, int endBarsAgo, double endY, Brush brush)
								Draw.Diamond(this, "Zero line #" + CurrentBar, true, Bars.GetTime(CurrentBar), Bars.GetClose(CurrentBar), Brushes.Green);
								zones[i].TestType = "sup";

							}
						}


						// Res
						// || (Bars.GetHigh(CurrentBar - 1) <= zones[i].ZoneTopY && Bars.GetHigh(CurrentBar - 1) >= zones[i].ZoneBottomY)
						else if (zones[i].Direction == 1 && ((Bars.GetClose(CurrentBar) <= zones[i].ZoneTopY && Bars.GetClose(CurrentBar) >= zones[i].ZoneBottomY)))
						{
							if (zones[i].Tracked == false)
							{

								// if (Bars.GetClose(CurrentBar) < Bars.GetOpen(CurrentBar))
								// {
								// 	zones[i].Tracked = true;
								// 	zones[i].BarTracker = CurrentBar;
								// 	Draw.Diamond(this, "Zero line #" + CurrentBar, true, Bars.GetTime(CurrentBar), Bars.GetClose(CurrentBar), Brushes.White);
								// 	zones[i].TestType = "res";

								// }

								zones[i].Tracked = true;
								zones[i].BarTracker = CurrentBar;
								Draw.Diamond(this, "Zero line #" + CurrentBar, true, Bars.GetTime(CurrentBar), Bars.GetClose(CurrentBar), Brushes.Red);
								zones[i].TestType = "res";

							}
						}
						//	Record zone(s) id
						//	If THIS BAR closes green
						//		Draw zeroline from THIS BAR open
						//		During time threshold limit (30 minutes?), if SMA area between zero line INCREASES:
						//			Zone strength increases by a factor determined by increased area
						//			Stop tracking zone
						//		Else
						//			Zone strength decreases by a factor determiend by decreased area
					}
					//Print(totalStrength);
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



				// Update direction (up or down) for zones

				// START COMPUTING PREVIOUS DAYS' ZONES
				// IF Zones[] IS NOT EMPTY (checks to make sure a day already passed)
				//		Adjust shape, strength, size of High/Low zones

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

				// update zone database for lows and highs for this day
				if (Bars.IsLastBarOfSession)
				{
					numFullTradingDays++;
					if (zones.Count > 0 && Expiration != -1)
					{
						for (int i = 0; i < zones.Count; i++)
						{
							if ((Bars.GetTime(CurrentBar) - zones[i].ZoneLeftX).Days >= Expiration)
							{
								//Print("bar expired");
								zones[i].Expired = true;
							}
							//Print("Zone " + zones[i].ID + ": " + zones[i].TotalRelativeVolume + " / " + zones[i].NumTimeBroken + " * " + zones[i].GetStrength() +  " = " + zones[i].TotalRelativeVolume / zones[i].NumTimeBroken * zones[i].GetStrength());
							//Print(zones[i].TotalAbsoluteVolume);
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
					// Display bars on 
				}
				MergeZones();
				zones.ForEach(delegate (ZONE z)
				{
					z.UpdateEnd(this);
				});
				ComputeZones(zones);
				//Print("TEST");


			}

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
					//Print(zonesAboveTemp + " " + zonesBelowTemp);


					if (z[i].Direction == 1) area = ResZoneColor;
					else if (z[i].Direction == -1) area = SupZoneColor;
					else area = Brushes.Yellow;
					Draw.Rectangle(this, "Zone ID: " + z[i].ID.ToString(), true, z[i].ZoneLeftX, z[i].ZoneBottomY, z[i].ZoneRightX, z[i].ZoneTopY, outline, area, Convert.ToInt32(z[i].GetRelativeStrength(this)/10), true);
					// Draw text for absolute volume inside zone, basically a scuffed volume profile indicator
					Draw.Text(this, zones[i].ID.ToString(), zones[i].GetRelativeStrength(this).ToString(), 0, (zones[i].ZoneBottomY + zones[i].ZoneTopY) / 2);
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
