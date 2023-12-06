using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Optimization;

namespace Scripts.Parabolics.Rts
{
    public class Main : IExternalScript
    {
        private IContext? Context { get; set; }
        private ISecurity? Source { get; set; }

        public OptimProperty PeriodTakeProfit = new OptimProperty(2000, 500, 5000, 500);
        public OptimProperty PeriodStopLoss = new OptimProperty(200, 200, 2000, 200);
        public OptimProperty PeriodBreakevenPass = new OptimProperty(100, 100, 1000, 100);
        public OptimProperty PeriodParabolicAccelerationMax = new OptimProperty(0.4, false, 0.01, 0.4, 0.01, 1);
        public OptimProperty PeriodParabolicAccelerationStep = new OptimProperty(0.001, false, 0.001, 0.1, 0.001, 1);

        private const string SignalNameOpenLongIfMore = "OpenLongIfMore";
        private const string SignalNameCloseLongTakeProfit = "CloseLongTakeProfit";
        private const string SignalNameCloseLongStopLoss = "CloseLongStopLoss";

        private const string SignalNameOpenShortIfLess = "OpenShortIfLess";
        private const string SignalNameCloseShortTakeProfit = "CloseShortTakeProfit";
        private const string SignalNameCloseShortStopLoss = "CloseShortStopLoss";

        private EntryPrice EntryPriceLong = new EntryPrice();
        private EntryPrice EntryPriceShort = new EntryPrice();

        private const double Lots = 1;

        public void Execute(IContext ctx, ISecurity sec)
        {
            Context = ctx;
            Source = sec;

            setAbsCommission();

            IList<double> cacheVolume = initVolume();
            IList<double> cacheClose = initClose();
            IList<double> cacheOpen = initOpen();
            IList<double> cacheTakeProfit = initTakeProfit();
            IList<double> cacheStopLoss = initStopLoss();
            IList<double> cacheBreakevenPass = initBreakevenPass();
            IList<double> cacheParabolic = initParabolic();
            IList<bool> cacheCrossUnder = initCrossUnder(cacheParabolic, cacheClose);
            IList<bool> cacheCrossOver = initCrossOver(cacheParabolic, cacheClose);

            int barsCount = Source.Bars.Count();
            if (!Context.IsLastBarUsed)
            {
                barsCount--;
            }

            for (int i = 0; i < barsCount; i++)
            {
                // Work With Long Positions
                IPosition openLongIfMore = Source.Positions.GetLastActiveForSignal(SignalNameOpenLongIfMore, i);
                bool signalToOpenLong = cacheCrossUnder[i];
                double entryPriceLong = EntryPriceLong.Execute(openLongIfMore, i);
                double formulaLongStopLoss = entryPriceLong - cacheStopLoss[i];
                double formulaLongTakeProfit = entryPriceLong + cacheTakeProfit[i];

                if(openLongIfMore == null)
                {
                    if(signalToOpenLong)
                    {
                        Source.Positions.OpenIfGreater(true, i + 1, Lots, cacheParabolic[i], SignalNameOpenLongIfMore, null);
                    }
                } 
                else
                {
                    openLongIfMore.CloseAtProfit(i + 1, formulaLongTakeProfit, SignalNameCloseLongTakeProfit);
                    openLongIfMore.CloseAtStop(i + 1, formulaLongStopLoss, SignalNameCloseLongStopLoss);
                }


                // Work With Short Positions
                IPosition openShortIfLess = Source.Positions.GetLastActiveForSignal(SignalNameOpenShortIfLess, i);
                bool signalToOpenShort = cacheCrossOver[i];
                double entryPriceShort = EntryPriceShort.Execute(openShortIfLess, i);
                double formulaShortStopLoss = entryPriceShort + cacheStopLoss[i];
                double formulaShortTakeProfit = entryPriceShort - cacheTakeProfit[i];

                if(openShortIfLess == null)
                {
                    if(signalToOpenShort)
                    {
                        Source.Positions.OpenIfLess(false, i + 1, Lots, cacheParabolic[i], SignalNameOpenShortIfLess, null);
                    }
                }
                else
                {
                    openShortIfLess.CloseAtProfit(i + 1, formulaShortTakeProfit, SignalNameCloseShortTakeProfit);
                    openShortIfLess.CloseAtStop(i + 1, formulaShortStopLoss, SignalNameCloseShortStopLoss);
                }
            }

            if (Context.IsOptimization) return;

            graphRendering(cacheParabolic, cacheVolume);
        }

        private void setAbsCommission()
        {
            var absCom = new AbsolutCommission()
            {
                Commission = 20,
            };
            absCom.Execute(Source);
        }

        private IList<double> initVolume()
        {
            Volume volume = new Volume() 
            {
                Context = Context,
            };
            return Context?.GetData(nameof(Volume), new string[]
            {
                "Source"
            }, () => volume.Execute(Source)) ?? new List<double>();
        }

        private IList<double> initClose()
        {
            Close close = new Close()
            {
                Context = Context
            };
            return Context?.GetData(nameof(Close), new string[]
            {
                "Source"
            }, () => close.Execute(Source)) ?? new List<double>();
        }

        private IList<double> initOpen()
        {
            Open open = new Open()
            {
                Context = Context
            };
            return Context?.GetData(nameof(Open), new string[]
            {
                "Source"
            }, () => open.Execute(Source)) ?? new List<double>();
        }


        private IList<double> initTakeProfit()
        {
            ConstGen ConstTakeProfit = new ConstGen();
            ConstTakeProfit.Value = PeriodTakeProfit.Value;
            return Context?.GetData("TakeProfit", new string[]
            {
                ConstTakeProfit.Value.ToString(),
                "Source"
            }, () => ConstTakeProfit.Execute(Source)) ?? new List<double>();
        }

        private IList<double> initStopLoss()
        {
            ConstGen ConstStopLoss = new ConstGen();
            ConstStopLoss.Value = PeriodStopLoss.Value;
            return Context?.GetData("StopLoss", new string[]
            {
                ConstStopLoss.Value.ToString(),
                "Source"
            }, () => ConstStopLoss.Execute(Source)) ?? new List<double>();
        }

        private IList<double> initBreakevenPass()
        {
            ConstGen ConstBreakevenPass = new ConstGen();
            ConstBreakevenPass.Value = PeriodBreakevenPass.Value;
            return Context?.GetData("BreakevenPass", new string[]
            {
                ConstBreakevenPass.Value.ToString(),
                "Source"
            }, () => ConstBreakevenPass.Execute(Source)) ?? new List<double>();
        }

        private IList<double> initParabolic()
        {
            vvTSLtools.Parabolic parabolic = new vvTSLtools.Parabolic()
            {
                Context = Context,
                AccelerationMax = PeriodParabolicAccelerationMax.Value,
                AccelerationStep = PeriodParabolicAccelerationStep.Value,
            };
            return Context?.GetData(nameof(vvTSLtools.Parabolic), new string[]
            {
                PeriodParabolicAccelerationMax.Value.ToString(),
                PeriodParabolicAccelerationStep.Value.ToString()
            }, () => parabolic.Execute(Source)) ?? new List<double>();
        }

        private IList<bool> initCrossUnder(IList<double> cacheParabolic, IList<double> cacheClose)
        {
            CrossUnder crossUnder = new CrossUnder()
            {
                Context = Context
            };
            return Context?.GetData(nameof(CrossUnder), new string[]
            {
                PeriodParabolicAccelerationMax.Value.ToString(),
                PeriodParabolicAccelerationStep.Value.ToString(),
                "Source"
            }, () => crossUnder.Execute(cacheParabolic, cacheClose)) ?? new List<bool>();
        }

        private IList<bool> initCrossOver(IList<double> cacheParabolic, IList<double> cacheClose)
        {
            CrossOver crossOver = new CrossOver()
            {
                Context = Context
            };
            return Context?.GetData(nameof(CrossOver), new string[]
            {
                PeriodParabolicAccelerationMax.Value.ToString(),
                PeriodParabolicAccelerationStep.Value.ToString(),
                "Source"
            }, () => crossOver.Execute(cacheParabolic, cacheClose)) ?? new List<bool>();
        }

        private void graphRendering(IList<double> cacheParabolic, IList<double> cacheVolume)
        {
            IGraphList graphParabolic = Context.First.AddList(nameof(vvTSLtools.Parabolic), cacheParabolic, ListStyles.POINT, ScriptColors.Magenta, LineStyles.DOT, PaneSides.RIGHT);
            graphParabolic.Thickness = 3;
            graphParabolic.Autoscaling = true;
            Context?.First.UpdatePrecision(PaneSides.RIGHT, Source.Decimals);

            // Volume Panel
            TSLab.Script.IGraphPane volumePanel = Context.CreateGraphPane("Volume", null);
            volumePanel.Visible = true;
            volumePanel.HideLegend = false;
            IGraphList? graphVolume = volumePanel.AddList(nameof(Volume), cacheVolume, ListStyles.HISTOHRAM, -16711732, LineStyles.SOLID, PaneSides.RIGHT);
            graphVolume.Autoscaling = true;

            volumePanel.UpdatePrecision(PaneSides.RIGHT, 0);
        }

    }
}
