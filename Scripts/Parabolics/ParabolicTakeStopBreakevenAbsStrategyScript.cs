using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Optimization;

namespace TSLabs.Scripts.Parabolics
{
    public class ParabolicTakeStopBreakevenAbsStrategyScript : IExternalScript
    {
        private const string OpenLongSignalName = "OpenLong";
        private const string OpenShortSignalName = "OpenShort";
        private const string CloseLongTakeProfit = "CloseLongTakeProfit";
        private const string CloseLongStopLoss = "CloseLongStopLoss";
        private const string CloseLongBreakeven = "CloseLongBreakeven";
        private const string CloseShortTakeProfit = "CloseShortTakeProfit";
        private const string CloseShortStopLoss = "CloseShortStopLoss";
        private const string CloseShortBreakeven = "CloseShortBreakeven";

        public OptimProperty ParabolicAccelerationMaxPeriod = new OptimProperty(0.4, 0.01, 0.4, 0.01);
        public OptimProperty ParabolicAccelerationStartPeriod = new OptimProperty(0.001, 0.001, 0.1, 0.001);
        public OptimProperty ParabolicAccelerationStepPeriod = new OptimProperty(0.001, 0.001, 0.1, 0.001);
        public OptimProperty TakeProfitPeriod = new OptimProperty(2000, 500, 5000, 500);
        public OptimProperty StopLossPeriod = new OptimProperty(1000, 100, 2000, 200);
        public OptimProperty BreakevenPeriod = new OptimProperty(500, 100, 1000, 100);

        private ConstGen TakeProfitConst = new ConstGen();
        private ConstGen StopLossConst = new ConstGen();
        private ConstGen BreakevenConst = new ConstGen();

        private CrossUnder CrossUnder = new CrossUnder();
        private CrossOver CrossOver = new CrossOver();

        private And And1 = new And();
        private And And2 = new And();
        private Not Not = new Not();

        private HasPositionActive HasPositionActive = new HasPositionActive();

        private EntryPrice EntryPriceLong = new EntryPrice();
        private EntryPrice EntryPriceShort = new EntryPrice();

        private const int Lots = 1;

        public void Execute(IContext context, ISecurity source)
        {
            // AND|NOT Condition 
            And1.Context = context;
            And2.Context = context;
            Not.Context = context;

            // Absolute Commission
            AbsolutCommission absCommission = new AbsolutCommission()
            {
                Commission = 20
            };
            absCommission.Execute(source);

            // Close Price
            Close closePrice = new Close()
            {
                Context = context
            };
            IList<double> closePriceCached = context.GetData(nameof(Close), new string[]
            {
                "Source"
            }, () => closePrice.Execute(source));

            // Take profit
            TakeProfitConst.Value = TakeProfitPeriod.Value;
            IList<double> takeProfitCached = context.GetData("TakeProfit", new string[]
            {
                TakeProfitConst.Value.ToString(),
                "Source"
            }, () => TakeProfitConst.Execute(context));

            // Stop loss
            StopLossConst.Value = StopLossPeriod.Value;
            IList<double> stopLossCached = context.GetData("StopLoss", new string[]
            {
                StopLossConst.Value.ToString(),
                "Source"
            }, () => StopLossConst.Execute(context));

            // Breakeven
            BreakevenConst.Value = BreakevenPeriod.Value;
            IList<double> breakevenCached = context.GetData("Breakeven", new string[]
            {
                BreakevenConst.Value.ToString(),
                "Source"
            }, () => BreakevenConst.Execute(context));

            // Parabolic indicator
            ParabolicSAR parabolicSAR = new ParabolicSAR()
            {
                Context = context,
                AccelerationStart = ParabolicAccelerationStartPeriod,
                AccelerationMax = ParabolicAccelerationMaxPeriod,
                AccelerationStep = ParabolicAccelerationStepPeriod
            };
            IList<double> parabolicCached = context.GetData(nameof(ParabolicSAR), new string[]
            {
                ParabolicAccelerationStartPeriod.ToString(),
                ParabolicAccelerationMaxPeriod.ToString(),
                ParabolicAccelerationStepPeriod.ToString()
            }, () => parabolicSAR.Execute(source));

            // Cross Under
            CrossUnder.Context = context;
            IList<bool> crossUnderCached = context.GetData(nameof(CrossUnder), new string[]
            {
                ParabolicAccelerationStartPeriod.ToString(),
                ParabolicAccelerationMaxPeriod.ToString(),
                ParabolicAccelerationStepPeriod.ToString(),
                "Source"
            }, () => CrossUnder.Execute(parabolicCached, closePriceCached));

            // Cross Over
            CrossOver.Context = context;
            IList<bool> crossOverCached = context.GetData(nameof(CrossOver), new string[]
            {
                ParabolicAccelerationStartPeriod.ToString(),
                ParabolicAccelerationMaxPeriod.ToString(),
                ParabolicAccelerationStepPeriod.ToString(),
                "Source"
            }, () => CrossOver.Execute(parabolicCached, closePriceCached));

            // rm last bar for generate signal
            int barsCount = source.Bars.Count();
            if (!context.IsLastBarUsed)
            {
                barsCount--;
            }

            bool isActiveBreakevenLong = false;
            bool isActiveBreakevenShort = false;

            for (int i = 0; i < barsCount; i++)
            {
                bool hasPositionActive = HasPositionActive.Execute(source, i);
                bool hasNotPositionActive = Not.Execute(hasPositionActive, i);

                // Work With Long Positions
                IPosition openLong = source.Positions.GetLastActiveForSignal(OpenLongSignalName, i);
                double entryPriceLong = EntryPriceLong.Execute(openLong, i);
                bool signalToOpenLongPosition = And1.Execute(crossUnderCached[i], hasNotPositionActive);
                double formulaTakeProfitLong = entryPriceLong + takeProfitCached[i];
                double formulaStopLossLong = entryPriceLong - stopLossCached[i];
                double formulaBreakevenLong = entryPriceLong + breakevenCached[i];
                if (openLong == null)
                {
                    isActiveBreakevenLong = false;
                    if (signalToOpenLongPosition && context.TradeFromBar <= i)
                    {
                        source.Positions.OpenAtMarket(isLong: true, barNum: i + 1, shares: Lots, signalName: OpenLongSignalName, notes: null, execution: PositionExecution.Normal);
                    }
                }
                else
                {
                    if (openLong.EntryBarNum <= i)
                    {
                        openLong.CloseAtProfit(i + 1, formulaTakeProfitLong, CloseLongTakeProfit);

                        if (isActiveBreakevenLong)
                        {
                            openLong.CloseAtStop(i + 1, entryPriceLong, CloseLongBreakeven);
                        }
                        else
                        {
                            openLong.CloseAtStop(i + 1, formulaStopLossLong, CloseLongStopLoss);

                            if (closePriceCached[i] >= formulaBreakevenLong)
                            {
                                isActiveBreakevenLong = true;
                            }
                        }
                    }
                }

                // Work With Short Positions
                IPosition openShort = source.Positions.GetLastActiveForSignal(OpenShortSignalName, i);
                double entryPriceShort = EntryPriceShort.Execute(openShort, i);
                bool signalToOpenShortPosition = And2.Execute(crossOverCached[i], hasNotPositionActive);
                double formulaTakeProfitShort = entryPriceShort - takeProfitCached[i];
                double formulaStopLossShort = entryPriceShort + stopLossCached[i];
                double formulaBreakevenShort = entryPriceShort - breakevenCached[i];
                if (openShort == null)
                {
                    isActiveBreakevenShort = false;
                    if (signalToOpenShortPosition && context.TradeFromBar <= i)
                    {
                        source.Positions.OpenAtMarket(isLong: false, barNum: i + 1, shares: Lots, signalName: OpenShortSignalName, notes: null, execution: PositionExecution.Normal);
                    }
                }
                else
                {
                    if (openShort.EntryBarNum <= i)
                    {
                        openShort.CloseAtProfit(i + 1, formulaTakeProfitShort, CloseShortTakeProfit);

                        if (isActiveBreakevenShort)
                        {
                            openShort.CloseAtStop(i + 1, entryPriceShort, CloseShortBreakeven);
                        }
                        else
                        {
                            openShort.CloseAtStop(i + 1, formulaStopLossShort, CloseLongStopLoss);

                            if (closePriceCached[i] <= formulaBreakevenShort)
                            {
                                isActiveBreakevenShort = true;
                            }
                        }
                    }
                }
            }

            if (context.IsOptimization) return;

            IGraphList? line = context.First.AddList(nameof(ParabolicSAR), parabolicCached, ListStyles.POINT, ScriptColors.Magenta, LineStyles.DOT, PaneSides.RIGHT);
            line.Thickness = 3;
            line.Autoscaling = true;
            context.First.UpdatePrecision(PaneSides.RIGHT, source.Decimals);
        }

    }
}
