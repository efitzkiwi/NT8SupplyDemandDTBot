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
	public class SlopeEnhancedOp : Indicator
	{
		
		#region Variables
			private bool detrend = false;		 //flag to enable/disable detrended data
          	
			private int period = 14; 		//default period for the moving average (does nothing for default series)
			private int smooth = 5;   		//default smoothing period
			private int detrendPeriod = 56; //default detrend period
			
			private Series<double> normdiff;  	//stores un-smoothed slopes
			private Series<double> calcseries; 	//stores detrended (or not) price series
			
			private InputSeriesType inputType	= InputSeriesType.SMA;  //enum for InputSeriesType
			private NormType normType = NormType.None;   				//enum for NormType
		
			private IndicatorBase indicator;  //the indicator to use
			
			private Brush colorUp = Brushes.Green;  //color for slope > 0
			private Brush colorDown = Brushes.Red;  //color for slope <= 0
		
        #endregion
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				
				Description									= @"Calculates slope of a data series.";
				Name										= "SlopeEnhancedOp";
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
				
				
			}
			else if (State == State.Configure)
			{
				AddPlot(new Stroke(Brushes.Navy), PlotStyle.Bar, "Slope");
				AddPlot(Brushes.DarkSeaGreen, "Zeroline");
				
//				Add(new Plot(new Pen(Color.Navy, 3), PlotStyle.Bar, "Slope"));
//				Add(new Line(Color.DarkSeaGreen, 0, "Zeroline"));
				
				normdiff = new Series<double>(this, MaximumBarsLookBack.Infinite);
				calcseries = new Series<double>(this, MaximumBarsLookBack.Infinite);
				
				
				 indicator = AssignIndicatorInstance(calcseries,inputType,Period);  //assign instance of an indicator base
			}
		}

		protected override void OnBarUpdate()
		{
			//Add your custom indicator logic here.
			try{
			// Variables
			double diff = 0;
			double normalize = 1;
			double detrendval = 0;
				
			// Set the detrend value
			if (Detrend) detrendval = LinReg(DetrendPeriod)[0];
			
			// Set the input series for and detrend
			calcseries[0] = Input[0]-detrendval;
			
			// Make sure enough bars exist on the chart
        	if (CurrentBar < Smooth || CurrentBar < Period || (Detrend && CurrentBar < DetrendPeriod) )
				return;

			// Set normalization value
			switch (normType)
			{
				case NormType.CurrentPrice:
				{
					normalize = Input[0];
					break;
				}
				case NormType.AveragePrice:
				{
					normalize = SMA(Input,Period)[0];
					break;
				}
				case NormType.AverageTrueRange:
				{
					normalize = ATR(Input,Period)[0];
					break;
				}
				case NormType.StandardDeviation:
				{
					normalize = StdDev(Input,Period)[0];
					break;
				}
				default:
				{
					normalize = 1;
					break;
				}
			}
			
			// Assign the slope, uses forward difference
			switch (inputType)
			{
				case InputSeriesType.DefaultInput:
				{
					diff = (calcseries[2] - 4*calcseries[1] +3*calcseries[0])/2;
					break;
				}
				case InputSeriesType.LinRegSlope:
				{
					diff = LinRegSlope(calcseries,Period)[0];
					break;
				} 
				default:
				{
					if(indicator != null)
					{
						diff = (indicator.Value[2] - 4*indicator.Value[1] +3*indicator.Value[0])/2;
						RemoveDrawObject("error");
					}
					else
					{
						Draw.TextFixed(this, "error", "Error", TextPosition.TopRight);
					}
					break;
				}
			}
			
			// Set the diff series and then assign the smoothed series current value to the plot
			normdiff[0] = diff / normalize;
			
			Slope[0] = SMA(normdiff,Smooth)[0];
			
			// Assign plot colors
			if ( Slope[0] > 0)
			{
				PlotBrushes[0][0] = ColorUp;
			}
			else
			{
				PlotBrushes[0][0] = ColorDown;
			}
			
			}
			catch(Exception e)
			{
				Print(e.ToString());
			}
			
		}
		
			// method to assign an instance of the chosen indicator
		private IndicatorBase AssignIndicatorInstance(Series<double> inputSeries, InputSeriesType inputType, int period)
		{
			IndicatorBase indicatorBase = null;
			
			switch (inputType)
			{
				case InputSeriesType.EMA:
				{
					indicatorBase = EMA(inputSeries,period);
					break;
				}
				case InputSeriesType.SMA:
				{
					indicatorBase = SMA(inputSeries,period);
					break;
				}
				case InputSeriesType.HMA:
				{
					indicatorBase = HMA(inputSeries,period);
					break;
				}
				case InputSeriesType.WMA:
				{
					indicatorBase = WMA(inputSeries,period);
					break;
				}
				case InputSeriesType.TEMA:
				{
					indicatorBase = TEMA(inputSeries,period);
					break;
				}
				case InputSeriesType.TMA:
				{
					indicatorBase = TMA(inputSeries,period);
					break;
				}
				case InputSeriesType.DEMA:
				{
					indicatorBase = DEMA(inputSeries,period);
					break;
				}
				case InputSeriesType.ZLEMA:
				{
					indicatorBase = ZLEMA(inputSeries,period);
					break;
				}
				default:
				{
					return(null);
					break;
				}
			}
			
			if(indicatorBase == null)
			{
				return(null);
			}
			
            indicatorBase.BarsRequiredToPlot = BarsRequiredToPlot;
            indicatorBase.Calculate = Calculate.OnBarClose;
            indicatorBase.MaximumBarsLookBack = MaximumBarsLookBack;

			
			return indicatorBase;
		}
		
        #region Properties
        [Browsable(false)]	
        [XmlIgnore()]		
        public Series<double> Slope
        {
            get { return Values[0]; }
        }
		
		
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]
		[Display(Name="Period", Description="Period of the moving average (doesn't affect DefaultInput).", Order=1, GroupName="Parameters")]
        public int Period
        {
            get { return period; }
            set { period = Math.Max(1, value); }
        }
		
		
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]
		[Display(Name="DetrendPeriod", Description="Period for the detrending linear regression (affects nothing if Detrend is not set to true).", Order=2, GroupName="Parameters")]
        public int DetrendPeriod
        {
            get { return detrendPeriod; }
            set { detrendPeriod = Math.Max(2, value); }
        }
		
		
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]
		[Display(Name="Smooth", Description="Smoothing parameter", Order=3, GroupName="Parameters")]
        public int Smooth
        {
            get { return smooth; }
            set { smooth = Math.Max(1, value); }
        }
		
		
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]
		[Display(Name="Detrend", Description="Set to true if you want to detrend the price data before calculating the slope (uses linear regression for detrending).", Order=4, GroupName="Parameters")]
        public bool Detrend
        {
            get { return detrend; }
            set { detrend = value; }
        }
		
		
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]
		[Display(Name="InputType", Description="Choose an input series.", Order=5, GroupName="Parameters")]
		public InputSeriesType InputType
		{
			get { return inputType; }
			set { inputType = value; }
		}
		
		
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]
		[Display(Name="NormType", Description="Choose a Normalization type. Normalization helps reduce scaling issues when you change chart timeframe, or factors in volatility.", Order=6, GroupName="Parameters")]
		public NormType NormType
		{
			get { return normType; }
			set { normType = value; }
		}
		
		
		[XmlIgnore()]
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]
		[Display(Name="ColorUp", Description="Color for slope greater than zero.", Order=7, GroupName="Parameters")]
		public Brush ColorUp
		{
			get { return colorUp; }
			set { colorUp = value; }
		}
		
		[Browsable(false)]
		public string ColorUpSerialize
		{
			get { return Serialize.BrushToString(ColorUp); }
			set { ColorUp = Serialize.StringToBrush(value); }
		}
		
		
		[XmlIgnore()]
		[NinjaScriptProperty]
		[RefreshProperties(RefreshProperties.All)]
		[Display(Name="ColorDown", Description="Color for slope less than zero..", Order=8, GroupName="Parameters")]
		public Brush ColorDown
		{
			get { return colorDown; }
			set { colorDown = value; }
		}
		
		
		[Browsable(false)]
		public string ColorDownSerialize
		{
			get { return Serialize.BrushToString(ColorDown); }
			set { ColorDown = Serialize.StringToBrush(value); }
		}

        #endregion
	}
	
	

}
	public enum InputSeriesType
{
	DefaultInput,
	DEMA,
	EMA,
	HMA,
	LinRegSlope,
	SMA,
	TEMA,
	TMA,
	WMA,
	ZLEMA,
}

public enum NormType
{
	None,
	CurrentPrice,
	AveragePrice,
	AverageTrueRange,
	StandardDeviation,
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private SlopeEnhancedOp[] cacheSlopeEnhancedOp;
		public SlopeEnhancedOp SlopeEnhancedOp(int period, int detrendPeriod, int smooth, bool detrend, InputSeriesType inputType, NormType normType, Brush colorUp, Brush colorDown)
		{
			return SlopeEnhancedOp(Input, period, detrendPeriod, smooth, detrend, inputType, normType, colorUp, colorDown);
		}

		public SlopeEnhancedOp SlopeEnhancedOp(ISeries<double> input, int period, int detrendPeriod, int smooth, bool detrend, InputSeriesType inputType, NormType normType, Brush colorUp, Brush colorDown)
		{
			if (cacheSlopeEnhancedOp != null)
				for (int idx = 0; idx < cacheSlopeEnhancedOp.Length; idx++)
					if (cacheSlopeEnhancedOp[idx] != null && cacheSlopeEnhancedOp[idx].Period == period && cacheSlopeEnhancedOp[idx].DetrendPeriod == detrendPeriod && cacheSlopeEnhancedOp[idx].Smooth == smooth && cacheSlopeEnhancedOp[idx].Detrend == detrend && cacheSlopeEnhancedOp[idx].InputType == inputType && cacheSlopeEnhancedOp[idx].NormType == normType && cacheSlopeEnhancedOp[idx].ColorUp == colorUp && cacheSlopeEnhancedOp[idx].ColorDown == colorDown && cacheSlopeEnhancedOp[idx].EqualsInput(input))
						return cacheSlopeEnhancedOp[idx];
			return CacheIndicator<SlopeEnhancedOp>(new SlopeEnhancedOp(){ Period = period, DetrendPeriod = detrendPeriod, Smooth = smooth, Detrend = detrend, InputType = inputType, NormType = normType, ColorUp = colorUp, ColorDown = colorDown }, input, ref cacheSlopeEnhancedOp);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.SlopeEnhancedOp SlopeEnhancedOp(int period, int detrendPeriod, int smooth, bool detrend, InputSeriesType inputType, NormType normType, Brush colorUp, Brush colorDown)
		{
			return indicator.SlopeEnhancedOp(Input, period, detrendPeriod, smooth, detrend, inputType, normType, colorUp, colorDown);
		}

		public Indicators.SlopeEnhancedOp SlopeEnhancedOp(ISeries<double> input , int period, int detrendPeriod, int smooth, bool detrend, InputSeriesType inputType, NormType normType, Brush colorUp, Brush colorDown)
		{
			return indicator.SlopeEnhancedOp(input, period, detrendPeriod, smooth, detrend, inputType, normType, colorUp, colorDown);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.SlopeEnhancedOp SlopeEnhancedOp(int period, int detrendPeriod, int smooth, bool detrend, InputSeriesType inputType, NormType normType, Brush colorUp, Brush colorDown)
		{
			return indicator.SlopeEnhancedOp(Input, period, detrendPeriod, smooth, detrend, inputType, normType, colorUp, colorDown);
		}

		public Indicators.SlopeEnhancedOp SlopeEnhancedOp(ISeries<double> input , int period, int detrendPeriod, int smooth, bool detrend, InputSeriesType inputType, NormType normType, Brush colorUp, Brush colorDown)
		{
			return indicator.SlopeEnhancedOp(input, period, detrendPeriod, smooth, detrend, inputType, normType, colorUp, colorDown);
		}
	}
}

#endregion
