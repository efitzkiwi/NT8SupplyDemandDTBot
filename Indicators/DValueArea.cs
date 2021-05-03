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
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace _dValueEnums
{
	public enum dValueAreaTypes
	{
		VOC,
		TPO,
		VWTPO,
		VTPO
	}
}

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	#region noteFromTheAuthor
	// <summary> (origonal -> CalculateValueArea)
	// The Value Area is the price range where 70% of yesterdays volume traded
	// Written by Ben L. at sbgtrading@yahoo.com
	//   Theory taken from several sources, but summarized at: http://www.secretsoftraders.com/ValueAreaHelpGuide.htm
	// May God bless you and your trading!
	//

	//	Description of the "ProfileType" parameter:

	//		I've given the option of creating the profile in 3 different ways:
	//			1)  VOC - This method loads all the volume of a bar onto the closing price of that bar.
	//						e.g.  A 5-minute bar has a volume of 280 and a range of 1.5 points with a close at 1534.25, then
	//						all 280 hits of volume are loaded onto the 1534.25 closing price.
	//			2)  TPO - This method disregards volume altogether, and gives a single hit to each price in the range of the bar.
	//						e.g.  A 5-minute bar has a range of 1.5 points with a High at 1534 and a Low at 1532.5, then
	//						1 contract (or "hit") is loaded to the seven prices in that range: 1532.50, 1532.75, 1533.0, 1533.25, 1533.50, 1533.75, and 1534
	//			3)  VWTPO - This method distribues the volume of a bar over the price range of the bar.
	//						e.g.  A 5-minute bar has a volume of 280 and a range of 1.5 points with a High at 1534 and a Low at 1532.5, then
	//						40 contracts (=280/7) are loaded to each of the seven prices in that range: 1532.50, 1532.75, 1533.0, 1533.25, 1533.50, 1533.75, and 1534
	//			4)  VTPO - This method distribues the volume of a bar Evenly over the price range of the bar.
	//						e.g.  A 5-minute bar has a volume of 280 and a range of 1.5 points with a High at 1534 and a Low at 1532.5, then
	//						280 contracts are loaded to each of the seven prices in that range: 1532.50, 1532.75, 1533.0, 1533.25, 1533.50, 1533.75, and 1534
	//						Since the calcs. are Relative to other bars / price points, more volume bars / price points will show that.
	//
	// Mods DeanV - 11/2008
	//	3/20/2010 - NT7 conversion (version 7.0)
	//		Adjusted call params for v7 requirements and non-equidistant charts, tweeked session detection, and re-positioned a few lables.
	//		Included a global enum namespace/file with distribution (_dValueEnums) so different versions don't have to deal with it).
	//	v7.0.1 - 3/23 - Added Session Template time override. Overwrites start / end settings with template in use settings.
	//	v7.0.2 - 3/28 - Added ZOrder flag... attempt to show behind other stuff.
	//	v7.0.3 - 4/07/10 - merged MDay's features... Map visable screen or combo's of days. Added / Changed these inputs
	//		ScreenMapType - 0=daily(daily maps), 1=screen(whatever is on screen), 2=combine days (uses PreviousSessions to determin # of days to combine)
	//		PreviousSessions - # of days to add to today's activity when ScreenMapType = 2. 0 = today only, 1 = today and yesterday.
	//		ShowDailyPlots - if true will show daily plot lines (regardless of SCreenMapType).
	//		* ShowEvolvingPOC's - added type 3 = Extended lines (full screen display of evolving lines in combo modes)
	//		* increase TotalSlots Maximum to 1000 (combo's probably need more slots for larger ranging)
	//		* combo's will include pre-sessions if part of chart display.
	//	v7.0.4 - 4/19-20 - Added Zero (Auto) setting on "Slot Min. Height".. 0 = fill in gaps between slots, etc., to display as contiguous verticals (no gaps or overprints)
	//		(when slots are combining ticks (TotalSlot<ticks in range & slot min. = 0), will increase POC & VA's buy about 1/2 tick... probably a little more accurate)
	//	v7.0.5 - 4/21 - Added -1 input (Auto slots seperated by 1 pix.) to "Slot Min. Height". -1 = separates slots by 1 pix.
	//		When "Slot Min. Height" <=0 - maps will adjust to center when slots are combining. Reworked POC, VA calc's to reflect centered slots.
	//
	#endregion

	public class DValueArea : Indicator
	{
		#region Variables
		private DateTime				cSEndTime;
		private DateTime				cSStartTime;
		private int						eLineTransparency	= 40;
		private SharpDX.Direct2D1.Brush ePOCBrush;
		private SharpDX.Direct2D1.Brush eVAbBrush;
		private SharpDX.Direct2D1.Brush eVAtBrush;
		private Brush					evolvingPOCColor	= Brushes.Red;
		private Brush					evolvingVAbColor	= Brushes.Green;
		private Brush					evolvingVAtColor	= Brushes.Green;
		private int						lastSlotUsed;
		private int						leftScrnBnum;	//left most bar# in display
		private int						openHour;
		private int						openMinute;
		private Rectangle				plotbounds;
		private double					plotMax;
		private double					plotMMDiff;
		private double					plotMin;
		private SharpDX.Direct2D1.Brush	preSessBrush;
		private double[,]				priceHitsArray;// = new double[2,500];
		private double					priceOfPOC			= 0.0;
		private int						rightScrnBnum;	//right most bar# on screen.
		private int						sEndBnum			= 0;
		private Series<int>				sesNum;
		private Brush					sessColor;
		private SharpDX.Direct2D1.Brush sessBrush;
		private SessionIterator			sessionIterator;
		private double					sessMaxHits			= 0.0;
		private double					sessPriceOfPOC		= 0.0;
		private double					sessVAtop			= 0.0;
		private double					sessVAbot			= 0.0;
		private double					slotHalfRange;
		private Brush					slotPreSessionColor = Brushes.Red;
		private Brush					slotSessionColor	= Brushes.Lime;
		private int						slotTransparency	= 60;
		private int						sStartBnum			= 0;	//first date bar# we care about
		private string					textForm			= "0.";
		private double					theSessionHigh;
		private double					theSessionLow;
		private int						ticksPerSlot;
		private double					vAbot				= 0.0;
		private double					vAtop				= 0.0;
		#endregion

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description							= @"";
				Name								= "DValueArea";
				Calculate							= Calculate.OnBarClose;
				IsOverlay							= true;
				DisplayInDataBox					= true;
				DrawOnPricePanel					= true;
				DrawHorizontalGridLines				= true;
				DrawVerticalGridLines				= true;
				PaintPriceMarkers					= true;
				IsAutoScale							= false;
				ScaleJustification					= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive			= true;
				
				ELineTransparency					= 40;
				ETextDecimals						= 2;
				ETextPosition						= 0;
				ELineHeight							= 2;
				InclWeekendVol						= false;
				OpenTime							= new TimeSpan(8, 30, 0);
				PctOfVolumeInVA						= 0.7;
				PresentMethod						= 2;
				PreviousSessions					= 2;
				ProfileType							= _dValueEnums.dValueAreaTypes.VWTPO;
				ScreenMapType						= 0;
				ScreenPercent						= 100;
				ScreenPosition						= 1;
				SessionLengthHours					= 6.75;
				ShowDailyPlots						= true;
				ShowEvolvingPOCs					= 2;
				SlotMinHeight						= 0;
				SlotTransparency					= 60;
				TotalSlots							= 300;
				VACalcType							= 2;
				ZOrderPutBehind						= false;

				AddPlot(Brushes.Chocolate, "RtPOC");
				AddPlot(Brushes.Green, "POC");
				AddPlot(new Stroke(Brushes.Pink, 1), PlotStyle.Dot, "VAb");
				AddPlot(new Stroke(Brushes.LightGreen, 1), PlotStyle.Dot, "VAt");
			}
			else if (State == State.DataLoaded)
			{
				sesNum = new Series<int>(this, MaximumBarsLookBack.Infinite);
			}
			else if (State == State.Historical)
			{
				sessionIterator = new SessionIterator(BarsArray[0]);

				openHour = OpenTime.Hours;
				openMinute = OpenTime.Minutes;

				priceHitsArray = new double[2, TotalSlots];
				
				for (int i = ETextDecimals; i > 0; i--)
					textForm += "0";

				if (ZOrderPutBehind)
					ZOrder = -1;
				else
					ZOrder = 0;
			}
		}

		private void DetermineHL_BarRange()
		{
			// use bar number range from SetSessionTimes(>0).
			theSessionHigh = Bars.GetHigh(sEndBnum);
			theSessionLow = Bars.GetLow(sEndBnum);
	
			for (int x = sEndBnum; x >= sStartBnum; x--)
			{
				if (Bars.GetHigh(x) > theSessionHigh)
					theSessionHigh = Bars.GetHigh(x);

				if (Bars.GetLow(x) < theSessionLow)
					theSessionLow = Bars.GetLow(x);
			}
		}

		private void DrawEvolving(int ThisSesNum, SharpDX.Direct2D1.Brush eBrush, double ePrice)
		{
			int x, yPos, vPos, vHeight;
			double price;
			int hWidth;

			int barPaintWidth = ChartControl.GetBarPaintWidth(ChartBars);
			int halfBarWidth = (int)(barPaintWidth * 0.5);

			int sbarNum = leftScrnBnum;	//default is off screen to left
			int barsInRange = 0;
			for (x = leftScrnBnum; x <= rightScrnBnum; x++)
			{
				//scan left to right
				if (sesNum.GetValueAt(x) == ThisSesNum)
				{
					if (PresentMethod > 0 && x < sStartBnum)
						continue; //wait till we get an open session bar.
					if (PresentMethod == 2 && x > sEndBnum)
						break; //stop wen we get an open session bar if sEB says it's a presession.
					barsInRange++;
				}

				if (barsInRange == 1)
					sbarNum = x;	//found first valid bar	
			}

			if (--barsInRange < 1)
				return;	//have at least 1 bar to paint. Do paint last bar.

			int ebarNum = sbarNum + barsInRange;

			int leftMostPos = ChartControl.GetXByBarIndex(ChartBars, sbarNum) - halfBarWidth;
			int rightMostPos = ChartControl.GetXByBarIndex(ChartBars, ebarNum) + halfBarWidth;

			if (sEndBnum + 1 >= CurrentBar)
				rightMostPos = Convert.ToInt32(ChartPanel.W);

			int totalHeight = Convert.ToInt32(ChartPanel.Y + ChartPanel.H);

			price = ePrice;
			hWidth = (int)((rightMostPos - leftMostPos));

			yPos = (int)((totalHeight) - ((price - plotMin) / plotMMDiff) * ChartPanel.H);
			vPos = yPos + ELineHeight;
			if (yPos >= totalHeight)
				return;	//too low or high to display
			if (vPos <= 1)
				vPos = 1;

			//take out any negitive portion
			yPos += (yPos < 0 ? yPos * -1 : 0);

			if (vPos > totalHeight)
				vPos = totalHeight;
			vHeight = vPos - yPos;

			SharpDX.RectangleF rect = new SharpDX.RectangleF(leftMostPos, yPos, hWidth, vHeight);
			RenderTarget.FillRectangle(rect, eBrush);
		}
	
		private void DrawSlotsUV(ChartScale chartScale, int slotHeight, int ThisSesNum, SharpDX.Direct2D1.Brush eBrush)
		{
			// draw slots universal
			// left or right based, and percentage of area

			int x, yPos, vPos, vHeight;
			double price;
			int hWidth;
			int halfslotHeight = 0;

			//if(slotHeight < minSlotHeight) HalfslotHeight = minSlotHeight /2;
			if (SlotMinHeight > 0 && slotHeight < SlotMinHeight)
				halfslotHeight = SlotMinHeight / 2;

			int barPaintWidth = ChartControl.GetBarPaintWidth(ChartBars);
			int halfBarWidth = (int)(barPaintWidth * 0.5);

			int sbarNum = leftScrnBnum;	//default is left most screen bar.
			int barsInRange = 0;

			//determine barsInRage based on screenMapType..
			if (ScreenMapType == 1)
				barsInRange = rightScrnBnum - leftScrnBnum;

			else if (ScreenMapType == 2)
			{	//day's combined
				barsInRange = rightScrnBnum - leftScrnBnum;
				if (sesNum.GetValueAt(leftScrnBnum) < ThisSesNum)
				{ //find first onscreen bar
					for (x = leftScrnBnum; x <= rightScrnBnum; x++)
					{	//scan left to right
						if (sesNum.GetValueAt(x) == ThisSesNum)
						{
							sbarNum = x;
							x = rightScrnBnum + 1;	//break
						}
						else barsInRange--;
					}
				}
			}
			else
			{	//daily only			
				for (x = leftScrnBnum; x <= rightScrnBnum; x++)
				{	//scan left to right
					if (sesNum.GetValueAt(x) == ThisSesNum)
					{
						if (PresentMethod > 0 && x < sStartBnum)
							continue; //wait till we get an open session bar.
						if (PresentMethod == 2 && x > sEndBnum)
							break; //stop wen we get an open session bar if sEB says it's a presession.
						barsInRange++;
					}
					if (barsInRange == 1)
						sbarNum = x;	//found first valid bar	
				}
			}//end screenMapType

			if (--barsInRange < 1)
				return;	//have at least 1 bar to paint. Don't paint last bar.
			int ebarNum = sbarNum + barsInRange;

			int leftMostPos		= ChartControl.GetXByBarIndex(ChartBars, sbarNum) - (ScreenPosition == 1 ? halfBarWidth : 0);
			int rightMostPos	= ChartControl.GetXByBarIndex(ChartBars, ebarNum) + (ScreenPosition == 2 ? halfBarWidth : 0);

			//adjust right screen display area if combo draws.
			if (ScreenMapType > 0)
				rightMostPos = Convert.ToInt32(ChartPanel.W);

			//now for some draw stuff...
			int totalHeight = Convert.ToInt32(ChartPanel.Y + ChartPanel.H);

			//reduce pix to show % amount of it.
			price = ((rightMostPos - leftMostPos) * ((100 - ScreenPercent) * 0.01));	//temp usage of price

			//reset left or right depending on screenposition..
			if (ScreenPosition == 1)
				rightMostPos -= (int)price;
			else
				leftMostPos += (int)price;
			int totalWidth = rightMostPos - leftMostPos;

			int prevYpos = 0;
			for (x = 0; x <= lastSlotUsed; x++)
			{
				price = priceHitsArray[0, x] + slotHalfRange;	//take it from mid to top.
				hWidth = (int)((totalWidth) * (priceHitsArray[1, x] / (sessMaxHits)));

				yPos = chartScale.GetYByValue(price);

				vPos = yPos + slotHeight + halfslotHeight;
				if (SlotMinHeight == 0 && x != 0)
					vPos = prevYpos;
				if (SlotMinHeight == -1 && x != 0)
					vPos = prevYpos - 1;

				yPos = yPos - halfslotHeight;
				prevYpos = yPos;	//incase we continue here.
				if (yPos >= totalHeight || vPos <= 1)
					continue;	//too low or high to display

				//take out any negitive portion
				yPos += (yPos < 0 ? yPos * -1 : 0);

				if (vPos > totalHeight)
					vPos = totalHeight;
				vHeight = vPos - yPos;
				prevYpos = yPos;

				SharpDX.RectangleF rect = new SharpDX.RectangleF(leftMostPos + (totalWidth - hWidth), yPos, hWidth, vHeight);

				if (ScreenPosition == 1)
					rect = new SharpDX.RectangleF(leftMostPos, yPos, hWidth, vHeight);

				RenderTarget.FillRectangle(rect, eBrush);
			}

			//draw evolving here if not daily (screenMapType > 0)
			if (ScreenMapType > 0)
			{
				if (ShowEvolvingPOCs >= 2)
				{
					int leftPos = leftMostPos;	//defaults are screen %
					int rightWidth = totalWidth;

					if (ShowEvolvingPOCs == 3)
					{	//show evolving lines full screen
						leftPos = ChartPanel.X;
						rightWidth = Convert.ToInt32(ChartPanel.Width);
					}
					if (EvolvingPOCColor != Brushes.Transparent)
					{
						SharpDX.RectangleF rect = new SharpDX.RectangleF(leftPos, chartScale.GetYByValue(sessPriceOfPOC), rightWidth, ELineHeight);
						RenderTarget.FillRectangle(rect, ePOCBrush);
					}
					if (EvolvingVAbColor != Brushes.Transparent)
					{
						SharpDX.RectangleF rect = new SharpDX.RectangleF(leftPos, chartScale.GetYByValue(sessVAbot), rightWidth, ELineHeight);
						RenderTarget.FillRectangle(rect, eVAbBrush);
					}
					if (EvolvingVAtColor != Brushes.Transparent)
					{
						SharpDX.RectangleF rect = new SharpDX.RectangleF(leftPos, chartScale.GetYByValue(sessVAtop), rightWidth, ELineHeight);
						RenderTarget.FillRectangle(rect, eVAtBrush);
					}
				}
				else if (ShowEvolvingPOCs == 1)
				{
					DrawTextValue(chartScale, ePOCBrush, sessPriceOfPOC);
					DrawTextValue(chartScale, eVAbBrush, sessVAbot);
					DrawTextValue(chartScale, eVAtBrush, sessVAtop);
				}
			}
		}

		private void DrawTextValue(ChartScale chartScale, SharpDX.Direct2D1.Brush eBrush, double eValue)
		{
			int yPos = chartScale.GetYByValue(eValue) - (int)(ChartControl.Properties.LabelFont.Size * 0.5);
			int rightMostPos = ChartControl.GetXByBarIndex(ChartBars, Bars.Count + ETextPosition);

			int recWidth = (int)(ChartControl.Properties.LabelFont.Size * eValue.ToString(textForm).Length);

			SharpDX.DirectWrite.Factory fontFactory = new SharpDX.DirectWrite.Factory(SharpDX.DirectWrite.FactoryType.Isolated);
			RenderTarget.DrawText(eValue.ToString(textForm), new SharpDX.DirectWrite.TextFormat(fontFactory, "Arial", 13f), new SharpDX.RectangleF(rightMostPos, yPos, recWidth, 100), eBrush);
		}

		private void NewSessStart()
		{
			//it's a new session, increment and calc POC from yesterday.
			if (!InclWeekendVol && (Time[1].DayOfWeek == DayOfWeek.Saturday || Time[1].DayOfWeek == DayOfWeek.Sunday))
				return;

			SetSessionBars(0);	//calc for just completed (1 session ago) session
			//Do this in any case to correct start,end times.
			SetCurSessEndTime();	//get current session End date/time, so we can compare on next bar, regardless.
			//cancel session increment with no live hrs. Sess bars
			if (sEndBnum - sStartBnum == 0)
			{	//nothing in Previous session
				//Print(Time[0] +" " +CurrentBar+" " +SesNum[0] +" No Live Session"  +" " +sStartBnum +" "+sEndBnum);
				return;
			}

			//increment session number.
			//SesNum's switch on first bar after last bar of defined session.
			//  They include any pre / post bars displayed, as well as open hours.
			sesNum[0] = sesNum[0] + 1;

			SetSessionBars(1);  //calc for Real (current bar excluded), just completed (1 session ago) session

			DetermineHL_BarRange();	//using previous call.

			if (!StuffHits_BarRange())
				return;

			vAtop = sessVAtop;
			vAbot = sessVAbot;
			priceOfPOC = sessPriceOfPOC;

		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 3)
			{
				if (UseSessTemplate)
				{
					if (CurrentBar == 0)
						sessionIterator.GetNextSession(Time[0], true);

					openHour = sessionIterator.ActualSessionBegin.Hour;
					openMinute = sessionIterator.ActualSessionBegin.Minute;

					SessionLengthHours = (sessionIterator.ActualSessionEnd - sessionIterator.ActualSessionBegin).TotalHours;
				}

				cSStartTime = new DateTime(Time[0].Year, Time[0].Month, Time[0].Day, openHour, openMinute, 0, 0, DateTimeKind.Utc);
				cSEndTime = cSStartTime.AddHours(SessionLengthHours);

				if (CurrentBar > 1)
					SetCurSessEndTime();	//get it started (skip bad 1st bar data issue).

				sesNum[0] = 0;
				return;
			}

			sesNum[0] = sesNum[1];	//copy previous # for default.

			// (do on first (complete) bar, outside of session range. Show previous session.)
			if (Time[0].CompareTo(cSEndTime) > 0)
				NewSessStart();

			if (vAtop > 0.0 && ShowDailyPlots)
			{
				VAt[0] = vAtop;
				VAb[0] = vAbot;
				POC[0] = priceOfPOC;
			}

			//recalc for real time.
			if (ShowRtPOC && ShowDailyPlots)
			{
				SetSessionBars(0);	//current session

				if (Time[0].CompareTo(cSStartTime) <= 0)
					SetPreSessBars(0);	// could be pre-session
				if (sEndBnum - sStartBnum == 0)
					return;	//must have at least 2 bars

				DetermineHL_BarRange();

				if (!StuffHits_BarRange())
					return;

				RtPOC[0] = sessPriceOfPOC;
			}
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (PresentMethod < 1 || BarsArray[0].Count < 2 || chartControl.SlotsPainted < 2 || IsInHitTest)
				return;

			int x;

			rightScrnBnum	= Math.Min(ChartBars.ToIndex, CurrentBar); //limit to actual bars vs. white space on right.
			leftScrnBnum	= Math.Min(ChartBars.FromIndex, CurrentBar); //not bigger than CurrentBar (ie. only 1 bar on screen).
			
			int leftMostSessNum		= sesNum.GetValueAt(leftScrnBnum);
			int rightMostSessNum	= sesNum.GetValueAt(rightScrnBnum);

			//set these globals 1x, before any calls that use them
			plotMin		= chartScale.MinValue;
			plotMax		= chartScale.MaxValue;
			plotMMDiff	= chartScale.MaxMinusMin; //don't know why we do it this way?

			// Find lowest screen price for ref to calc slot pix's.
			double chartLowPrice = double.MaxValue;
			for (int i = leftScrnBnum; i <= rightScrnBnum; i++)
				chartLowPrice = Math.Min(chartLowPrice, Bars.GetLow(i));

			int screenVLowPos = chartScale.GetYByValue(chartLowPrice);	//low value screen position

			//draw combo's or daily...
			if (ScreenPosition > 0 && ScreenMapType == 1)
			{
				//use bars on screen. Dispaly on right side.
				sEndBnum = (rightScrnBnum);
				sStartBnum = (leftScrnBnum);

				if (sEndBnum > sStartBnum)
				{//Have some bars, should be OK.
					DetermineHL_BarRange();

					if (!StuffHits_BarRange())
						return;

					//calc slot height for this BarRange (needed SlotHalfRange).
					int slotVHeight = screenVLowPos - chartScale.GetYByValue(chartLowPrice + (slotHalfRange * 2.0));//-1;	//at least 1
					if (slotVHeight < 1)
						slotVHeight = 1;

					DrawSlotsUV(chartScale, slotVHeight, 0, sessBrush);
				}
			}
			else if (ScreenPosition > 0 && ScreenMapType == 2)
			{
				//combine day's
				int oldestSnum = sesNum.GetValueAt(CurrentBar) - PreviousSessions;
				if (oldestSnum < 0)
					oldestSnum = 0;

				SetSessBarDays(PreviousSessions);
				//SetSessionBars(SesNum[0] - x);
				if (sEndBnum > sStartBnum)
				{	//regular session should be OK.
					DetermineHL_BarRange();

					if (!StuffHits_BarRange())
						return;

					//calc slot height for this BarRange.
					int slotVHeight = screenVLowPos - chartScale.GetYByValue(chartLowPrice + (slotHalfRange * 2.0));//-1;	//at least 1
					if (slotVHeight < 1)
						slotVHeight = 1;

					DrawSlotsUV(chartScale, slotVHeight, oldestSnum, sessBrush);
				}
			}
			//daily stuff...
			else if (ScreenMapType == 0 && leftMostSessNum <= sesNum.GetValueAt(CurrentBar))
			{
				//current or previous session is in the display.
				//loop through screen dipslay day history				
				for (x = leftMostSessNum; x <= rightMostSessNum; x++)
				{
					//do pre-session first
					if (PresentMethod == 2)
					{
						SetSessionBars(sesNum.GetValueAt(CurrentBar) - x);	//have to do this first.
						SetPreSessBars(sesNum.GetValueAt(CurrentBar) - x);

						if (sEndBnum > sStartBnum)
						{	//1st bar of presession, or presess hasn't started yet.

							DetermineHL_BarRange();	//using previous call.
							if (!StuffHits_BarRange())
								continue;
							
							int SlotVHeight = screenVLowPos - chartScale.GetYByValue(chartLowPrice + (slotHalfRange * 2.0));
							if (SlotVHeight < 1)
								SlotVHeight = 1;	//at least 1

							DrawSlotsUV(chartScale, SlotVHeight, x, preSessBrush);

							if (ShowEvolvingPOCs > 1 && x == sesNum.GetValueAt(CurrentBar))
							{
								if (EvolvingPOCColor != Brushes.Transparent)
									DrawEvolving(sesNum.GetValueAt(CurrentBar), ePOCBrush, sessPriceOfPOC);
								if (EvolvingVAbColor != Brushes.Transparent)
									DrawEvolving(sesNum.GetValueAt(CurrentBar), eVAbBrush, sessVAbot);
								if (EvolvingVAtColor != Brushes.Transparent)
									DrawEvolving(sesNum.GetValueAt(CurrentBar), eVAtBrush, sessVAtop);
							}
						}
					}

					//now do defined session
					SetSessionBars(sesNum.GetValueAt(CurrentBar) - x);
					if (sEndBnum > sStartBnum)
					{	//regular session should be OK.
						DetermineHL_BarRange();	//using previous call.

						if (!StuffHits_BarRange())
							return;

						//calc slot height for this BarRange.
						int slotVHeight = screenVLowPos - chartScale.GetYByValue(chartLowPrice + (slotHalfRange * 2.0));
						if (slotVHeight < 1)
							slotVHeight = 1;

						DrawSlotsUV(chartScale, slotVHeight, x, sessBrush);

						if (ShowEvolvingPOCs > 1 && x == sesNum.GetValueAt(CurrentBar))
						{
							if (EvolvingPOCColor != Brushes.Transparent)
								DrawEvolving(sesNum.GetValueAt(CurrentBar), ePOCBrush, sessPriceOfPOC);
							if (EvolvingVAbColor != Brushes.Transparent)
								DrawEvolving(sesNum.GetValueAt(CurrentBar), eVAbBrush, sessVAbot);
							if (EvolvingVAtColor != Brushes.Transparent)
								DrawEvolving(sesNum.GetValueAt(CurrentBar), eVAtBrush, sessVAtop);
						}
					}

					//do text for either pre or reg session...
					if (ShowEvolvingPOCs == 1 && x == sesNum.GetValueAt(CurrentBar))
					{
						DrawTextValue(chartScale, ePOCBrush, sessPriceOfPOC);
						DrawTextValue(chartScale, eVAbBrush, sessVAbot);
						DrawTextValue(chartScale, eVAtBrush, sessVAtop);
					}
				}
			}
		}
		
		public override void OnRenderTargetChanged()
		{
			//Print(State);
			if (sessBrush != null)
				sessBrush.Dispose();

			if (preSessBrush != null)
				preSessBrush.Dispose();

			if (ePOCBrush != null)
				ePOCBrush.Dispose();

			if (eVAtBrush != null)
				eVAtBrush.Dispose();

			if (eVAbBrush != null)
				eVAbBrush.Dispose();

			if (RenderTarget != null)
			{
				try
				{
					sessBrush		= SlotSessionColor.ToDxBrush(RenderTarget);
					preSessBrush	= SlotPreSessionColor.ToDxBrush(RenderTarget);
					ePOCBrush		= EvolvingPOCColor.ToDxBrush(RenderTarget);
					eVAtBrush		= EvolvingVAtColor.ToDxBrush(RenderTarget);
					eVAbBrush		= EvolvingVAbColor.ToDxBrush(RenderTarget);
				}
				catch (Exception e) { }
			}
		}

		private void SetCurSessEndTime()
		{
			double slenAdd = SessionLengthHours >= 24 ? 24.0 - 1.0 / 60.0 : SessionLengthHours;
			cSEndTime = cSStartTime.AddHours(slenAdd);
			while (cSEndTime.CompareTo(Time[0]) < 0)
			{	//already happened, so add days for catch (weekends)
				cSEndTime = cSEndTime.AddHours(24);
				cSStartTime = cSStartTime.AddHours(24);
			}
		}

		private void SetPreSessBars(int SessionAgo)
		{
			//reg session has to be set before this call.
			int sNum = sesNum.GetValueAt(CurrentBar) - SessionAgo;
			int x = 1;

			sEndBnum = sStartBnum - 1;

			for (x = CurrentBar - sEndBnum; x < CurrentBar; x++)
				if (sesNum.GetValueAt(CurrentBar - x) != sNum)
					break;

			sStartBnum = CurrentBar - x + 1;

			//EBnum will be -1 from SBnum, if no pre-ses bars found.
		}

		private void SetSessBarDays(int DaysAgo)
		{
			//find start/end bars going back DaysAgo days (end is always CurrentBar)
			// days are really SesNum's.
			//have to have a default. Usefull if sessionago == 0.
			int sNum = sesNum.GetValueAt(CurrentBar) - DaysAgo;	//last sess num we car about
			if (sNum < 0)
				sNum = 0;			//not less than zero.

			sEndBnum = sStartBnum = CurrentBar;

			//now find first sNum bar, looking forwards
			for (int x = 1; x < CurrentBar; x++)
			{
				//start at oldest barago and go forwards
				if (sesNum.GetValueAt(x) == sNum)
				{ // first bar of session range (could be pre - session)
					sStartBnum = x; //combo's include pre-session, if part of chart.
					break;
				}
			}
		}

		private void SetSessionBars(int SessionAgo)
		{
			//have to have a default. Usefull if sessionago == 0.
			DateTime StartTime = cSStartTime;
			DateTime EndTime;
			int sNum = sesNum.GetValueAt(CurrentBar) - SessionAgo;

			if (sNum == sesNum.GetValueAt(CurrentBar) || sNum < 0)
			{	//not a previous session
				sEndBnum = sStartBnum = CurrentBar;

				for (int x = CurrentBar; x > 0; x--)
				{
					if (Bars.GetTime(x).CompareTo(StartTime) <= 0)
					{
						sStartBnum = x + 1;
						break;
					}
					if (sesNum.GetValueAt(x) != sesNum.GetValueAt(CurrentBar))
						break;
				}
			}
			else
			{ //find and set pervious session date/times and barnumber range we care about.
				sStartBnum	= 1;
				sEndBnum	= 0;
				
				int y = 0;
				int z = 0;
				for (int x = 1; x < CurrentBar; x++)
				{
					z = CurrentBar - x;

					//start at newest barago and go backwards to catch right date
					if (sEndBnum == 0 && sesNum.GetValueAt(z) == sNum)
					{   // last bar of session range
						//skip weekends? (Weekends are included with Mondays sess# when skipped)
						sEndBnum = CurrentBar - x;// +1;
												  //use last bar of session count to set correct date/times of session.
						StartTime = new DateTime(Bars.GetTime(z).Year, Bars.GetTime(z).Month, Bars.GetTime(z).Day, openHour, openMinute, 0, 0, DateTimeKind.Utc);
						if (Bars.GetTime(z).CompareTo(StartTime) < 0) //it's a previous day, so subtract 24 hrs
							StartTime = StartTime.AddHours(-24);
						//keep going till we find the ses start.
						EndTime = StartTime.AddHours(SessionLengthHours);

						if (Bars.GetTime(z - 1).CompareTo(EndTime) > 0)
						{   //no sess bars showing
							sStartBnum = sEndBnum;
							break;
						}

						for (y = x; y < CurrentBar; y++)
						{
							z = CurrentBar - y;
							if (Bars.GetTime(z).CompareTo(StartTime) <= 0)
							{
								if (sesNum.GetValueAt(z) == sNum)
									sStartBnum = CurrentBar - y + 1;
								else
									sStartBnum = sEndBnum;
								break;
							}
						}
					}

					z = CurrentBar - x;

					if (sEndBnum > 0 && Bars.GetTime(z).CompareTo(StartTime) <= 0)
					{   // 1 bar too far.
						sStartBnum = CurrentBar - x + 1;    //don't need pre-session bars.
						break;  //end
					}
				}

				if (sEndBnum == 0)
					sEndBnum = CurrentBar;	//safety if havn't got there yet.
			}
		}

		private bool StuffHits_BarRange()
		{
			int x;

			int ticksInRange = (int)Math.Round((theSessionHigh - theSessionLow) / TickSize, 0);

			//fit ticks into array by so many TicksPerSlot
			ticksPerSlot = (ticksInRange / TotalSlots) + 1;	//should just drop the fract part.
			lastSlotUsed = ticksInRange / ticksPerSlot;	//Zero based, drop fract part.

			double comboSlotOffset;
			if (SlotMinHeight > 0)
			{
				slotHalfRange = ((TickSize * ticksPerSlot) - TickSize) / 2;
				comboSlotOffset = (ticksPerSlot > 1 ? slotHalfRange : 0);
			}
			else
			{
				slotHalfRange = ((TickSize * ticksPerSlot)) / 2;
				comboSlotOffset = (ticksPerSlot > 1 ? slotHalfRange - (((theSessionLow + ((lastSlotUsed + 1) * TickSize * ticksPerSlot)) - theSessionHigh) / 2) : 0);	//move down to center it.
			}

			//clear counts in any case.
			for (x = 0; x <= lastSlotUsed; x++)
			{
				// 0 -> 999, reset from bottom up.
				priceHitsArray[0, x] = (x * TickSize * ticksPerSlot) + comboSlotOffset; //Lowest Tick Value/Slot upped to mid value point
				priceHitsArray[0, x] += theSessionLow; //add it to the bottom
				priceHitsArray[1, x] = 0.0;	//clear counts per value.
			}

			if (ticksInRange > 0)
			{
				double	BarH;
				double	BarL;
				int		index = 0;

				int	i = sStartBnum;

				while (i <= sEndBnum)
				{
					BarH = Bars.GetHigh(i);
					BarL = Bars.GetLow(i);

					if (ProfileType == _dValueEnums.dValueAreaTypes.VOC)
					{
						//Volume On Close - puts all the volume for that bar on the close price
						index = (int)Math.Round((Bars.GetClose(i) - theSessionLow) / TickSize, 0);
						index /= ticksPerSlot;  //drop fract part.
						priceHitsArray[1, index] += Bars.GetVolume(i);
					}

					if (ProfileType == _dValueEnums.dValueAreaTypes.TPO)
					{
						//Time Price Opportunity - disregards volume, only counts number of times prices are touched
						//BarH=High[i]; BarL=Low[i];
						while (BarL <= BarH)
						{
							index = (int)Math.Round((BarL - theSessionLow) / TickSize, 0);  //ticks from bottom
							index /= ticksPerSlot;  //drop fract part.
							priceHitsArray[1, index] += 1;  //increment this value count.
							BarL = BarL + TickSize; //up 1 tick
						}
					}

					if (ProfileType == _dValueEnums.dValueAreaTypes.VWTPO)
					{
						//Volume Weighted Time Price Opportunity - Disperses the Volume of the bar over the range of the bar so each price touched is weighted with volume
						//BarH=High[i]; BarL=Low[i];
						int TicksInBar = (int)Math.Round((BarH - Bars.GetLow(i)) / TickSize + 1, 0);
						while (BarL <= BarH)
						{
							index = (int)Math.Round((BarL - theSessionLow) / TickSize, 0);
							index /= ticksPerSlot;  //drop fract part.
							priceHitsArray[1, index] += Bars.GetVolume(i) / TicksInBar;
							BarL = BarL + TickSize;
						}
					}

					if (ProfileType == _dValueEnums.dValueAreaTypes.VTPO)
					{
						//Volume  Time Price Opportunity - Counts raw Volume
						//BarH=High[i]; BarL=Low[i];
						while (BarL <= BarH)
						{
							index = (int)Math.Round((BarL - theSessionLow) / TickSize, 0);
							index /= ticksPerSlot;  //drop fract part.
							priceHitsArray[1, index] += Bars.GetVolume(i);
							BarL = BarL + TickSize;
						}
					}
					i++;
				}
				

				//arrays are stuffed.
				//Calculate the Average price as weighted by the hit counts AND find the price with the highest hits (POC price)
				i = 0;
				double tHxP = 0.0; //Total of Hits multiplied by Price at that volume
				double hitsTotal = 0.0;
				sessPriceOfPOC = 0.0;
				sessMaxHits = 0.0;
				int pOCIndex = 0;	//track POC slot#

				double midPoint = theSessionLow + ((theSessionHigh - theSessionLow) * .5);
				double midUpCnt = 0, midDnCnt = 0;	//counts above/below midpoint.

				while (i <= lastSlotUsed)
				{	//Sum up Volume*Price in THxP...and sum up Volume in VolumeTotal
					if (priceHitsArray[1, i] > 0.0)
					{
						tHxP += (priceHitsArray[1, i] * priceHitsArray[0, i]);
						hitsTotal += priceHitsArray[1, i];
						if (priceHitsArray[1, i] > sessMaxHits)
						{ //used to determine POC level
							sessMaxHits = priceHitsArray[1, i];
							sessPriceOfPOC = priceHitsArray[0, i];
							pOCIndex = i;	//OK if only 1
						}
						//sum up hits for possable later use
						if (priceHitsArray[0, i] > midPoint)
							midUpCnt += priceHitsArray[1, i];	//don't count equals
						if (priceHitsArray[0, i] < midPoint)
							midDnCnt += priceHitsArray[1, i];
					}
					i++;
				}

				if (hitsTotal == 0)
					return false;	//nothing to do.

				//now lowest (or only) sessMaxHits/POC is known.
				//determine in others match, and pick best choice.
				//
				//Rules to use are:
				// 1. If there is more than 1 price with the same 'most' TPO's then the price closest to the mid-point of the range (high - low) is used. 
				// 2. If the 2 'most' TPO prices are equi-distance from the mid-point then the price on the side of the mid-point with the most TPO's is used. 
				// 3. If there are equal number of TPO's on each side then the lower price is used. 
				//
				int pOCcount = 0;
				double mid1 = midPoint, mid2 = midPoint;	//Distance from midPoint, set larger than possable
				int mid1Dx = 0, mid2Dx = 0;	//array index count of finds.

				i = 0;
				while (i <= lastSlotUsed)
				{	//scan known array from bottom to top
					if (priceHitsArray[1, i] == sessMaxHits)
					{	//should be at least 1.
						pOCcount++;
						//find 2 closest to midpoint
						if (Math.Abs(midPoint - priceHitsArray[0, i]) <= mid1)
						{
							mid2 = mid1;//rotate next closest
							mid2Dx = mid1Dx;
							mid1 = Math.Abs(midPoint - priceHitsArray[0, i]);	//how far away from midpoint
							mid1Dx = i;
						}
					}
					i++;
				}

				if (pOCcount > 1)
				{
					if (mid1 != mid2)
					{	//found it, rule #1
						sessPriceOfPOC = priceHitsArray[0, mid1Dx];
						pOCIndex = mid1Dx;
					}
					else
					{	//they are equal, Rule #2 may apply
						if (midUpCnt == midDnCnt)
						{	//must use Rule #3
							sessPriceOfPOC = priceHitsArray[0, mid2Dx];	//use the lower.
							pOCIndex = mid2Dx;
						}
						else
						{	//Rule #2
							if (midUpCnt > midDnCnt)
							{
								sessPriceOfPOC = priceHitsArray[0, mid1Dx];	//mid1 = upper of 2
								pOCIndex = mid1Dx;
							}
							else
							{
								sessPriceOfPOC = priceHitsArray[0, mid2Dx];	//must be the lower.
								pOCIndex = mid2Dx;
							}
						}//end Rule #2
					}
				}
				//end of finding best fit for POC

				if (VACalcType == 1)
				{	//start mid-range and expand
					//AvgPrice = THxP/HitsTotal;
					sessVAtop = tHxP / hitsTotal;
					sessVAbot = sessVAtop;

					//This loop calculates the percentage of hits contained within the Value Area
					double viA = 0.0;
					double tV = 0.00001;
					double adj = 0.0;
					while (viA / tV < PctOfVolumeInVA)
					{
						sessVAbot = sessVAbot - adj;
						sessVAtop = sessVAtop + adj;
						viA = 0.0;
						tV = 0.00001;
						for (i = 0; i <= lastSlotUsed; i++)
						{
							if (priceHitsArray[0, i] > sessVAbot - adj && priceHitsArray[0, i] < sessVAtop + adj)
								viA += priceHitsArray[1, i];
							tV += priceHitsArray[1, i];
						}
						adj = TickSize;
					}
				}
				else
				{	//start at POC Slot and expand by slots.
					//start at POC and add largest of each side till done.
					double viA = priceHitsArray[1, pOCIndex];
					int upSlot = pOCIndex + 1, dnSlot = pOCIndex - 1;
					double upCnt, dnCnt;

					while (viA / hitsTotal < PctOfVolumeInVA)
					{
						if (upSlot <= lastSlotUsed)
							upCnt = priceHitsArray[1, upSlot];
						else upCnt = 0;
						if (dnSlot >= 0)
							dnCnt = priceHitsArray[1, dnSlot];
						else dnCnt = 0;
						if (upCnt == dnCnt)
						{	//if both equal, add this one.
							if (pOCIndex - dnSlot < upSlot - pOCIndex)
								upCnt = 0;	//use closest
							else
								dnCnt = 0;	//use upper if the same.
						}
						if (upCnt >= dnCnt)
						{	//if still equal (i.e. zero), do it.
							viA += upCnt;
							if (upSlot <= lastSlotUsed)
								upSlot++;
						}
						if (upCnt <= dnCnt)
						{	//need equals to increment counts.
							viA += dnCnt;
							if (dnSlot >= 0)
								dnSlot--;
						}
						if (upSlot > lastSlotUsed && dnSlot < 0)
						{	//error.
							upSlot = lastSlotUsed;
							dnSlot = 0;
							break;
						}
					}
					//index's have gone one too far...
					sessVAtop = priceHitsArray[0, --upSlot];
					sessVAbot = priceHitsArray[0, ++dnSlot];
				}
			}	//ticksinRange
			return true;
		}

		#region Properties
		[Range(1, 100)]
		[NinjaScriptProperty]
		[Display(Name = "Evolving Line Transparency", Order = 5, GroupName = "Display Settings")]
		public int ELineTransparency
		{
			get { return eLineTransparency; }
			set
			{
				eLineTransparency = value;

				Brush newBrush;

				if (evolvingPOCColor != null)
				{
					newBrush = evolvingPOCColor.Clone();
					newBrush.Opacity = eLineTransparency / 100d;
					newBrush.Freeze();
					evolvingPOCColor = newBrush;
				}

				if (evolvingVAtColor != null)
				{
					newBrush = evolvingVAtColor.Clone();
					newBrush.Opacity = eLineTransparency / 100d;
					newBrush.Freeze();
					evolvingVAtColor = newBrush;
				}

				if (evolvingVAbColor != null)
				{
					newBrush = EvolvingVAbColor.Clone();
					newBrush.Opacity = eLineTransparency / 100d;
					newBrush.Freeze();
					EvolvingVAbColor = newBrush;
				}
			}
		}

		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name="E-Text Decimals", Order = 1, GroupName="Display Settings")]
		public int ETextDecimals
		{ get; set; }

		[Range(0, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "E-Text Position", Order = 2, GroupName = "Display Settings")]
		public int ETextPosition
		{ get; set; }

		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Evolving Line Height", Order = 4, GroupName = "Display Settings")]
		public int ELineHeight
		{ get; set; }

		[XmlIgnore]
		[Display(Name = "Evolving POC Color", Order = 7, GroupName = "Display Settings")]
		public Brush EvolvingPOCColor
		{
			get { return evolvingPOCColor; }
			set
			{
				evolvingPOCColor = value;
				if (evolvingPOCColor == null)
					return;

				if (evolvingPOCColor.IsFrozen)
					evolvingPOCColor = evolvingPOCColor.Clone();

				evolvingPOCColor.Opacity = eLineTransparency / 100d;
				evolvingPOCColor.Freeze();
			}
		}

		[Browsable(false)]
		public string EvolvingPOCColorSerializable
		{
			get { return Serialize.BrushToString(EvolvingPOCColor); }
			set { EvolvingPOCColor = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name = "Evolving VAb Color", Order = 8, GroupName = "Display Settings")]
		public Brush EvolvingVAbColor
		{
			get { return evolvingVAbColor; }
			set
			{
				evolvingVAbColor = value;
				if (evolvingVAbColor == null)
					return;

				if (evolvingVAbColor.IsFrozen)
					evolvingVAbColor = evolvingVAbColor.Clone();

				evolvingVAbColor.Opacity = eLineTransparency / 100d;
				evolvingVAbColor.Freeze();
			}
		}

		[Browsable(false)]
		public string EvolvingVAbColorSerializable
		{
			get { return Serialize.BrushToString(EvolvingVAbColor); }
			set { EvolvingVAbColor = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name = "Evolving VAt Color", Order = 7, GroupName = "Display Settings")]
		public Brush EvolvingVAtColor
		{
			get { return evolvingVAtColor; }
			set
			{
				evolvingVAtColor = value;

				if (evolvingVAtColor == null)
					return;

				if (evolvingVAtColor.IsFrozen)
					evolvingVAtColor = evolvingVAtColor.Clone();

				evolvingVAtColor.Opacity = eLineTransparency / 100d;
				evolvingVAtColor.Freeze();
			}
		}

		[Browsable(false)]
		public string EvolvingVAtColorSerializable
		{
			get { return Serialize.BrushToString(EvolvingVAtColor); }
			set { EvolvingVAtColor = Serialize.StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[Display(Name = "InclWeekendVol", Order = 1, GroupName = "Parameters", Description = "Include Weekend Volume")]
		public bool InclWeekendVol
		{ get; set; }

		[XmlIgnore]
		[NinjaScriptProperty]
		[Display(Name = "Open Time", Order = 2, GroupName = "Parameters")]
		public TimeSpan OpenTime
		{ get; set; }

		[Browsable(false)]
		public string OpenTimeSerializable
		{
			get { return OpenTime.ToString(); }
			set { OpenTime = TimeSpan.Parse(value); }
		}

		[Range(0, double.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Percent of Volume in VA", Order = 6, GroupName = "Parameters")]
		public double PctOfVolumeInVA
		{ get; set; }

		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Present Method", Order = 4, GroupName = "Parameters")]
		public int PresentMethod
		{ get; set; }

		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "PreviousSessions", Order = 5, GroupName = "Parameters")]
		public int PreviousSessions
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Profile Type", Order = 6, GroupName = "Parameters", Description = "Type of profile (VOC = Volume at Close, TPO = Price, VWTPO = Volume Weighted Price, VTPO = Volume)")]
		public _dValueEnums.dValueAreaTypes ProfileType
		{ get; set; }

		[Range(0, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "ScreenMapType", Order = 8, GroupName = "Parameters")]
		public int ScreenMapType
		{ get; set; }

		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "ScreenPercent", Order = 9, GroupName = "Parameters")]
		public int ScreenPercent
		{ get; set; }

		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "ScreenPosition", Order = 10, GroupName = "Parameters")]
		public int ScreenPosition
		{ get; set; }

		[Range(1, double.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Session Length", Order = 5, GroupName = "Parameters", Description = "In hours")]
		public double SessionLengthHours
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Daily Plots", Order = 11, GroupName = "Parameters")]
		public bool ShowDailyPlots
		{ get; set; }

		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Show Evolving POCs", Order = 3, GroupName = "Display Settings")]
		public int ShowEvolvingPOCs
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Real-time POC", Order = 13, GroupName = "Display Settings")]
		public bool ShowRtPOC
		{ get; set; }

		[Range(0, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Slot Min Height", Order = 9, GroupName = "Display Settings")]
		public int SlotMinHeight
		{ get; set; }

		[XmlIgnore]
		[Display(Name = "Slot Pre-session Color", Order = 11, GroupName = "Display Settings")]
		public Brush SlotPreSessionColor
		{ 
			get { return slotPreSessionColor; }
			set
			{
				slotPreSessionColor = value;

				if (slotPreSessionColor == null)
					return;

				if (slotPreSessionColor.IsFrozen)
					slotPreSessionColor = slotPreSessionColor.Clone();

				slotPreSessionColor.Opacity = slotTransparency / 100d;
				slotPreSessionColor.Freeze();
			}
		}

		[Browsable(false)]
		public string SlotPreSessionColorSerializable
		{
			get { return Serialize.BrushToString(SlotPreSessionColor); }
			set { SlotPreSessionColor = Serialize.StringToBrush(value); }
		}			

		[XmlIgnore]
		[Display(Name = "Slot Session Color", Order = 10, GroupName = "Display Settings")]
		public Brush SlotSessionColor
		{
			get { return slotSessionColor; }
			set
			{
				slotSessionColor = value;

				if (slotSessionColor == null)
					return;

				if (slotSessionColor.IsFrozen)
					slotSessionColor = slotSessionColor.Clone();

				slotSessionColor.Opacity = slotTransparency / 100d;
				slotSessionColor.Freeze();
			}
		}

		[Browsable(false)]
		public string SlotSessionColorSerializable
		{
			get { return Serialize.BrushToString(SlotSessionColor); }
			set { SlotSessionColor = Serialize.StringToBrush(value); }
		}			

		[Range(1, 100)]
		[NinjaScriptProperty]
		[Display(Name = "Slot Transparency", Order = 12, GroupName = "Display Settings")]
		public int SlotTransparency
		{
			get { return slotTransparency; }
			set
			{
				slotTransparency = value;

				Brush newBrush;

				if (slotSessionColor != null)
				{
					newBrush = slotSessionColor.Clone();
					newBrush.Opacity = slotTransparency / 100d;
					newBrush.Freeze();
					slotSessionColor = newBrush;
				}

				if (slotPreSessionColor != null)
				{
					newBrush = slotPreSessionColor.Clone();
					newBrush.Opacity = slotTransparency / 100d;
					newBrush.Freeze();
					slotPreSessionColor = newBrush;
				}
			}
		}

		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "TotalSlots", Order = 12, GroupName = "Parameters")]
		public int TotalSlots
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Use Session Template", Order = 2, GroupName = "Parameters", Description = "Use Session Template for Open Hours and Length (Changes Input settings).")]
		public bool UseSessTemplate
		{ get; set; }

		[Range(1, 2)]
		[NinjaScriptProperty]
		[Display(Name = "VACalcType", Order = 13, GroupName = "Parameters", Description = "Old (midrange based) or New (POC / Slot based) VA calc type (1=old, 2=new).")]
		public int VACalcType
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = "ZOrderPutBehind", Order = 15, GroupName = "Display Settings")]
		public bool ZOrderPutBehind
		{ get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> RtPOC
		{
			get { return Values[0]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> POC
		{
			get { return Values[1]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> VAb
		{
			get { return Values[2]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> VAt
		{
			get { return Values[3]; }
		}
		#endregion

	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private DValueArea[] cacheDValueArea;
		public DValueArea DValueArea(int eLineTransparency, int eTextDecimals, int eTextPosition, int eLineHeight, bool inclWeekendVol, TimeSpan openTime, double pctOfVolumeInVA, int presentMethod, int previousSessions, _dValueEnums.dValueAreaTypes profileType, int screenMapType, int screenPercent, int screenPosition, double sessionLengthHours, bool showDailyPlots, int showEvolvingPOCs, bool showRtPOC, int slotMinHeight, int slotTransparency, int totalSlots, bool useSessTemplate, int vACalcType, bool zOrderPutBehind)
		{
			return DValueArea(Input, eLineTransparency, eTextDecimals, eTextPosition, eLineHeight, inclWeekendVol, openTime, pctOfVolumeInVA, presentMethod, previousSessions, profileType, screenMapType, screenPercent, screenPosition, sessionLengthHours, showDailyPlots, showEvolvingPOCs, showRtPOC, slotMinHeight, slotTransparency, totalSlots, useSessTemplate, vACalcType, zOrderPutBehind);
		}

		public DValueArea DValueArea(ISeries<double> input, int eLineTransparency, int eTextDecimals, int eTextPosition, int eLineHeight, bool inclWeekendVol, TimeSpan openTime, double pctOfVolumeInVA, int presentMethod, int previousSessions, _dValueEnums.dValueAreaTypes profileType, int screenMapType, int screenPercent, int screenPosition, double sessionLengthHours, bool showDailyPlots, int showEvolvingPOCs, bool showRtPOC, int slotMinHeight, int slotTransparency, int totalSlots, bool useSessTemplate, int vACalcType, bool zOrderPutBehind)
		{
			if (cacheDValueArea != null)
				for (int idx = 0; idx < cacheDValueArea.Length; idx++)
					if (cacheDValueArea[idx] != null && cacheDValueArea[idx].ELineTransparency == eLineTransparency && cacheDValueArea[idx].ETextDecimals == eTextDecimals && cacheDValueArea[idx].ETextPosition == eTextPosition && cacheDValueArea[idx].ELineHeight == eLineHeight && cacheDValueArea[idx].InclWeekendVol == inclWeekendVol && cacheDValueArea[idx].OpenTime == openTime && cacheDValueArea[idx].PctOfVolumeInVA == pctOfVolumeInVA && cacheDValueArea[idx].PresentMethod == presentMethod && cacheDValueArea[idx].PreviousSessions == previousSessions && cacheDValueArea[idx].ProfileType == profileType && cacheDValueArea[idx].ScreenMapType == screenMapType && cacheDValueArea[idx].ScreenPercent == screenPercent && cacheDValueArea[idx].ScreenPosition == screenPosition && cacheDValueArea[idx].SessionLengthHours == sessionLengthHours && cacheDValueArea[idx].ShowDailyPlots == showDailyPlots && cacheDValueArea[idx].ShowEvolvingPOCs == showEvolvingPOCs && cacheDValueArea[idx].ShowRtPOC == showRtPOC && cacheDValueArea[idx].SlotMinHeight == slotMinHeight && cacheDValueArea[idx].SlotTransparency == slotTransparency && cacheDValueArea[idx].TotalSlots == totalSlots && cacheDValueArea[idx].UseSessTemplate == useSessTemplate && cacheDValueArea[idx].VACalcType == vACalcType && cacheDValueArea[idx].ZOrderPutBehind == zOrderPutBehind && cacheDValueArea[idx].EqualsInput(input))
						return cacheDValueArea[idx];
			return CacheIndicator<DValueArea>(new DValueArea(){ ELineTransparency = eLineTransparency, ETextDecimals = eTextDecimals, ETextPosition = eTextPosition, ELineHeight = eLineHeight, InclWeekendVol = inclWeekendVol, OpenTime = openTime, PctOfVolumeInVA = pctOfVolumeInVA, PresentMethod = presentMethod, PreviousSessions = previousSessions, ProfileType = profileType, ScreenMapType = screenMapType, ScreenPercent = screenPercent, ScreenPosition = screenPosition, SessionLengthHours = sessionLengthHours, ShowDailyPlots = showDailyPlots, ShowEvolvingPOCs = showEvolvingPOCs, ShowRtPOC = showRtPOC, SlotMinHeight = slotMinHeight, SlotTransparency = slotTransparency, TotalSlots = totalSlots, UseSessTemplate = useSessTemplate, VACalcType = vACalcType, ZOrderPutBehind = zOrderPutBehind }, input, ref cacheDValueArea);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.DValueArea DValueArea(int eLineTransparency, int eTextDecimals, int eTextPosition, int eLineHeight, bool inclWeekendVol, TimeSpan openTime, double pctOfVolumeInVA, int presentMethod, int previousSessions, _dValueEnums.dValueAreaTypes profileType, int screenMapType, int screenPercent, int screenPosition, double sessionLengthHours, bool showDailyPlots, int showEvolvingPOCs, bool showRtPOC, int slotMinHeight, int slotTransparency, int totalSlots, bool useSessTemplate, int vACalcType, bool zOrderPutBehind)
		{
			return indicator.DValueArea(Input, eLineTransparency, eTextDecimals, eTextPosition, eLineHeight, inclWeekendVol, openTime, pctOfVolumeInVA, presentMethod, previousSessions, profileType, screenMapType, screenPercent, screenPosition, sessionLengthHours, showDailyPlots, showEvolvingPOCs, showRtPOC, slotMinHeight, slotTransparency, totalSlots, useSessTemplate, vACalcType, zOrderPutBehind);
		}

		public Indicators.DValueArea DValueArea(ISeries<double> input , int eLineTransparency, int eTextDecimals, int eTextPosition, int eLineHeight, bool inclWeekendVol, TimeSpan openTime, double pctOfVolumeInVA, int presentMethod, int previousSessions, _dValueEnums.dValueAreaTypes profileType, int screenMapType, int screenPercent, int screenPosition, double sessionLengthHours, bool showDailyPlots, int showEvolvingPOCs, bool showRtPOC, int slotMinHeight, int slotTransparency, int totalSlots, bool useSessTemplate, int vACalcType, bool zOrderPutBehind)
		{
			return indicator.DValueArea(input, eLineTransparency, eTextDecimals, eTextPosition, eLineHeight, inclWeekendVol, openTime, pctOfVolumeInVA, presentMethod, previousSessions, profileType, screenMapType, screenPercent, screenPosition, sessionLengthHours, showDailyPlots, showEvolvingPOCs, showRtPOC, slotMinHeight, slotTransparency, totalSlots, useSessTemplate, vACalcType, zOrderPutBehind);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.DValueArea DValueArea(int eLineTransparency, int eTextDecimals, int eTextPosition, int eLineHeight, bool inclWeekendVol, TimeSpan openTime, double pctOfVolumeInVA, int presentMethod, int previousSessions, _dValueEnums.dValueAreaTypes profileType, int screenMapType, int screenPercent, int screenPosition, double sessionLengthHours, bool showDailyPlots, int showEvolvingPOCs, bool showRtPOC, int slotMinHeight, int slotTransparency, int totalSlots, bool useSessTemplate, int vACalcType, bool zOrderPutBehind)
		{
			return indicator.DValueArea(Input, eLineTransparency, eTextDecimals, eTextPosition, eLineHeight, inclWeekendVol, openTime, pctOfVolumeInVA, presentMethod, previousSessions, profileType, screenMapType, screenPercent, screenPosition, sessionLengthHours, showDailyPlots, showEvolvingPOCs, showRtPOC, slotMinHeight, slotTransparency, totalSlots, useSessTemplate, vACalcType, zOrderPutBehind);
		}

		public Indicators.DValueArea DValueArea(ISeries<double> input , int eLineTransparency, int eTextDecimals, int eTextPosition, int eLineHeight, bool inclWeekendVol, TimeSpan openTime, double pctOfVolumeInVA, int presentMethod, int previousSessions, _dValueEnums.dValueAreaTypes profileType, int screenMapType, int screenPercent, int screenPosition, double sessionLengthHours, bool showDailyPlots, int showEvolvingPOCs, bool showRtPOC, int slotMinHeight, int slotTransparency, int totalSlots, bool useSessTemplate, int vACalcType, bool zOrderPutBehind)
		{
			return indicator.DValueArea(input, eLineTransparency, eTextDecimals, eTextPosition, eLineHeight, inclWeekendVol, openTime, pctOfVolumeInVA, presentMethod, previousSessions, profileType, screenMapType, screenPercent, screenPosition, sessionLengthHours, showDailyPlots, showEvolvingPOCs, showRtPOC, slotMinHeight, slotTransparency, totalSlots, useSessTemplate, vACalcType, zOrderPutBehind);
		}
	}
}

#endregion
