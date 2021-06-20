//
// Copyright (C) 2020, NinjaTrader LLC <www.ninjatrader.com>.
// NinjaTrader reserves the right to modify or overwrite this NinjaScript component with each release.
//
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

namespace NinjaTrader.NinjaScript.Indicators
{
	/// <summary>
	/// The Hull Moving Average (HMA) employs weighted MA calculations to offer superior
	/// smoothing, and much less lag, over traditional SMA indicators.
	/// This indicator is based on the reference article found here:
	/// http://www.justdata.com.au/Journals/AlanHull/hull_ma.htm
	/// </summary>
	public class HMACustom : Indicator
	{
		private Series<double> diffSeries;
		private WMA wma1;
		private WMA wma2;
		private WMA wmaDiffSeries;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"thinkorswim's HMA formula";
				Name = "HMACustom";
				IsSuspendedWhileInactive = true;
				Period = 45;
				IsOverlay = true;

				AddPlot(Brushes.Goldenrod, "HMACustom");
			}
			else if (State == State.DataLoaded)
			{
				diffSeries = new Series<double>(this);

				int halfHMALength = (int)Math.Ceiling((double)Period / 2);
				int halfPeriod = Convert.ToInt32(halfHMALength);

				wma1 = WMA(Inputs[0], halfPeriod);
				wma2 = WMA(Inputs[0], Period);

				int sq = Convert.ToInt32(Math.Round(Math.Sqrt(Period), 0));

				wmaDiffSeries = WMA(diffSeries, sq);
			}
		}

		protected override void OnBarUpdate()
		{
			diffSeries[0] = Math.Round(2 * wma1[0], 4) - Math.Round(wma2[0], 4);
			Value[0] = Math.Round(wmaDiffSeries[0], 4);
		}

		#region Properties
		[Range(2, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Period", GroupName = "NinjaScriptParameters", Order = 0)]
		public int Period
		{ get; set; }
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private HMACustom[] cacheHMACustom;
		public HMACustom HMACustom(int period)
		{
			return HMACustom(Input, period);
		}

		public HMACustom HMACustom(ISeries<double> input, int period)
		{
			if (cacheHMACustom != null)
				for (int idx = 0; idx < cacheHMACustom.Length; idx++)
					if (cacheHMACustom[idx] != null && cacheHMACustom[idx].Period == period && cacheHMACustom[idx].EqualsInput(input))
						return cacheHMACustom[idx];
			return CacheIndicator<HMACustom>(new HMACustom(){ Period = period }, input, ref cacheHMACustom);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.HMACustom HMACustom(int period)
		{
			return indicator.HMACustom(Input, period);
		}

		public Indicators.HMACustom HMACustom(ISeries<double> input , int period)
		{
			return indicator.HMACustom(input, period);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.HMACustom HMACustom(int period)
		{
			return indicator.HMACustom(Input, period);
		}

		public Indicators.HMACustom HMACustom(ISeries<double> input , int period)
		{
			return indicator.HMACustom(input, period);
		}
	}
}

#endregion
