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
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class RelativeVolumeNT8 : Indicator
	{
		private double av;
		private double sd;
		private double relVol;
		private int WideSMAPeriod = 200;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Melvin E. Dickover : 'Evidence-Based Support & Resistance' - TASC April 2014";
				Name										= "RelativeVolumeNT8";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
				Period					= 60;
				SmaPeriod = 20;
				NumStDev					= 2;
				AddPlot(new Stroke(Brushes.Black, 2), PlotStyle.Bar, "RelativeVolume");
				AddPlot(new Stroke(Brushes.Gold, 2), PlotStyle.Line, "MA");
				AddPlot(new Stroke(Brushes.Firebrick, 2), PlotStyle.Line, "MAWide");
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar == 0)
			{
				Values[0][0] = 0;
				PlotBrushes[0][0] = Brushes.Black;
			}
			
			if (CurrentBar > Period && CurrentBar > SmaPeriod && CurrentBar > WideSMAPeriod)
			{
				av = SMA(Volume, Period)[0];
				sd = StdDev(Volume, Period)[0];
				relVol = ((Volume[0] - av) / sd);
					
				if (relVol > NumStDev)
				{
					Values[0][0] = relVol;
					PlotBrushes[0][0] = Brushes.Black;
				}
						
				else
				{
					Values[0][0] = relVol;
					PlotBrushes[0][0] = Brushes.DarkGray;
				}
				MA[0] = SMA(Values[0], SmaPeriod)[0];
				MAWide[0] = SMA(Values[0], WideSMAPeriod)[0];
			}
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Period", Description="Number of bars for StDev calculation", Order=1, GroupName="Parameters")]
		public int Period
		{ get; set; }


		[NinjaScriptProperty]
		[Range(1, double.MaxValue)]
		[Display(Name="NumStDev", Description="Num of StDevs to be significant", Order=2, GroupName="Parameters")]
		public double NumStDev
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Avg Period", Description = "Avg Number of bars for MA calculation", Order = 3, GroupName = "Parameters")]
		public int SmaPeriod
		{ get; set; }

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> RelativeVolume
		{
			get { return Values[0]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> MA
		{
			get { return Values[1]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> MAWide
		{
			get { return Values[2]; }
		}
		#endregion

	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private RelativeVolumeNT8[] cacheRelativeVolumeNT8;
		public RelativeVolumeNT8 RelativeVolumeNT8(int period, double numStDev, int smaPeriod)
		{
			return RelativeVolumeNT8(Input, period, numStDev, smaPeriod);
		}

		public RelativeVolumeNT8 RelativeVolumeNT8(ISeries<double> input, int period, double numStDev, int smaPeriod)
		{
			if (cacheRelativeVolumeNT8 != null)
				for (int idx = 0; idx < cacheRelativeVolumeNT8.Length; idx++)
					if (cacheRelativeVolumeNT8[idx] != null && cacheRelativeVolumeNT8[idx].Period == period && cacheRelativeVolumeNT8[idx].NumStDev == numStDev && cacheRelativeVolumeNT8[idx].SmaPeriod == smaPeriod && cacheRelativeVolumeNT8[idx].EqualsInput(input))
						return cacheRelativeVolumeNT8[idx];
			return CacheIndicator<RelativeVolumeNT8>(new RelativeVolumeNT8(){ Period = period, NumStDev = numStDev, SmaPeriod = smaPeriod }, input, ref cacheRelativeVolumeNT8);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RelativeVolumeNT8 RelativeVolumeNT8(int period, double numStDev, int smaPeriod)
		{
			return indicator.RelativeVolumeNT8(Input, period, numStDev, smaPeriod);
		}

		public Indicators.RelativeVolumeNT8 RelativeVolumeNT8(ISeries<double> input , int period, double numStDev, int smaPeriod)
		{
			return indicator.RelativeVolumeNT8(input, period, numStDev, smaPeriod);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RelativeVolumeNT8 RelativeVolumeNT8(int period, double numStDev, int smaPeriod)
		{
			return indicator.RelativeVolumeNT8(Input, period, numStDev, smaPeriod);
		}

		public Indicators.RelativeVolumeNT8 RelativeVolumeNT8(ISeries<double> input , int period, double numStDev, int smaPeriod)
		{
			return indicator.RelativeVolumeNT8(input, period, numStDev, smaPeriod);
		}
	}
}

#endregion
