using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Optimization;

namespace Scripts
{
    public sealed class ParabolicTakeStopAbsoluteStrategyScript : IExternalScript
    {
        public OptimProperty ParabolicAccelerationMaxPeriod = new OptimProperty(0.02, 0.01, 0.4, 0.01);
        public OptimProperty ParabolicAccelerationMinPeriod = new OptimProperty(0.01, 0.001, 0.1, 0.001);
        public OptimProperty ParabolicAccelerationStepPeriod = new OptimProperty(0.01, 0.001, 0.1, 0.001);
        public OptimProperty StopLossPeriod = new OptimProperty(200, 200, 2000, 200);
        public OptimProperty TakeProfitPeriod = new OptimProperty(2000, 500, 5000, 500);
        
        private const string OpenLongSignalName = "OpenLong";
        private const string OpenShortSignalName = "OpenShort";
        private const string CloseLongTakeProfit = "CloseLongTakeProfit";
        private const string CloseLongStopLoss = "CloseLongStopLoss";
        private const string CloseShortTakeProfit = "CloseShortTakeProfit";
        private const string CloseShortStopLoss = "CloseShortStopLoss";

        private EntryPrice EntryPriceLong = new EntryPrice();
        private EntryPrice EntryPriceShort = new EntryPrice();

        private CrossUnder CrossUnder = new CrossUnder();
        private CrossOver CrossOver = new CrossOver();
        
        private HasPositionActive HasPositionActive = new HasPositionActive();

        private And And1 = new And();
        private And And2 = new And();
        private Not Not = new Not();
        
        private ConstGen TakeProfitConst = new ConstGen();
        private ConstGen StopLossConst = new ConstGen();

        private const int Lots = 1;

        public void Execute(IContext ctx, ISecurity sec)
        {
            // AND|NOT Condition
            And1.Context = ctx;
            And2.Context = ctx;
            Not.Context = ctx;

            // Const StopLoss
            StopLossConst.Value = StopLossPeriod.Value;
            var stopLossCached = ctx.GetData("StopLoss", new string[]
            {
                StopLossConst.Value.ToString(),
                "Source"
            }, () => StopLossConst.Execute(ctx));

            // Const TakeProfit
            TakeProfitConst.Value = TakeProfitPeriod.Value;
            var takeProfitCached = ctx.GetData("TakeProfit", new string[]
            {
                TakeProfitConst.Value.ToString(),
                "Source"
            }, () => TakeProfitConst.Execute(ctx));

            // Absolute Commission
            var absCommission = new AbsolutCommission()
            {
                Commission = 20
            };
            absCommission.Execute(sec);

            // Parabolic
            var parabolicSAR = new ParabolicSAR()
            {
                Context = ctx,
                AccelerationMax = ParabolicAccelerationMaxPeriod,
                AccelerationStart = ParabolicAccelerationMinPeriod,
                AccelerationStep = ParabolicAccelerationStepPeriod
            };
            var parabolicCached = ctx.GetData(nameof(ParabolicSAR), new[] {
                ParabolicAccelerationMaxPeriod.ToString(),
                ParabolicAccelerationMinPeriod.ToString(),
                ParabolicAccelerationStepPeriod.ToString()
            }, () => parabolicSAR.Execute(sec));

            // Close Position
            var closePosition = new Close()
            {
                Context = ctx
            };
            var closePositionCached = ctx.GetData(nameof(Close), new string[]
            {
                "Source"
            }, () => closePosition.Execute(sec));

            // CrossUnder
            CrossUnder.Context = ctx;
            var crossUnder = ctx.GetData(nameof(CrossUnder), new string[]
            {
                ParabolicAccelerationMaxPeriod.ToString(),
                ParabolicAccelerationMinPeriod.ToString(),
                ParabolicAccelerationStepPeriod.ToString(),
                "Source"
            }, () => CrossUnder.Execute(parabolicCached, closePositionCached));

            // CrossOver
            CrossOver.Context = ctx;
            var crossOver = ctx.GetData(nameof(CrossOver), new string[]
            {
                ParabolicAccelerationMaxPeriod.ToString(),
                ParabolicAccelerationMinPeriod.ToString(),
                ParabolicAccelerationStepPeriod.ToString(),
                "Source"
            }, () => CrossOver.Execute(parabolicCached, closePositionCached));


            // rm current bar value
            var barsCount = sec.Bars.Count();
            if (!ctx.IsLastBarUsed)
            {
                barsCount--;
            }

            // General Work Operation
            for (int i = 0; i < barsCount; i++)
            {
                bool haveActivePosition = HasPositionActive.Execute(sec, i);
                bool notPosition = Not.Execute(haveActivePosition, i);

                // Work with Long position
                IPosition OpenLong = sec.Positions.GetLastActiveForSignal(OpenLongSignalName, i);
                double entryPriceLong = EntryPriceLong.Execute(OpenLong, i);
                bool signalToOpenLong = And1.Execute(crossUnder[i], notPosition);
                double formulaTakeProfitLong = entryPriceLong + takeProfitCached[i];
                double formulaStopLossLong = entryPriceLong - stopLossCached[i];

                if (OpenLong == null)
                {
                    if(signalToOpenLong && ctx.TradeFromBar <= i)
                    {
                        sec.Positions.OpenAtMarket(true, i + 1, Lots, OpenLongSignalName, null, PositionExecution.Normal);
                    }
                }
                else
                {
                    if(OpenLong.EntryBarNum <= i)
                    {
                        OpenLong.CloseAtProfit(i + 1, formulaTakeProfitLong, CloseLongTakeProfit);
                        OpenLong.CloseAtStop(i + 1, formulaStopLossLong, CloseLongStopLoss);
                    }
                }


                // Work with Short position
                IPosition OpenShort = sec.Positions.GetLastActiveForSignal(OpenShortSignalName, i);
                double entryPriceShort = EntryPriceShort.Execute(OpenShort, i);
                bool signalToOpenShort = And2.Execute(crossOver[i], notPosition);
                double formulaTakeProfitShort = entryPriceShort - takeProfitCached[i];
                double formulaStopLossShort = entryPriceShort + stopLossCached[i];

                if(OpenShort == null)
                {
                    if(signalToOpenShort && ctx.TradeFromBar <= i)
                    {
                        sec.Positions.OpenAtMarket(false, i + 1, Lots, OpenShortSignalName, null, PositionExecution.Normal);
                    }
                }
                else
                {
                    if(OpenShort.EntryBarNum <= i) 
                    {
                        OpenShort.CloseAtProfit(i + 1, formulaTakeProfitShort, CloseShortTakeProfit);
                        OpenShort.CloseAtStop(i + 1, formulaStopLossShort, CloseShortStopLoss);
                    }
                }
            }

            if (ctx.IsOptimization)
            {
                return;
            }

            var line = ctx.First.AddList(nameof(ParabolicSAR), parabolicCached, ListStyles.POINT, ScriptColors.Cyan, LineStyles.DOT, PaneSides.RIGHT);
            line.Thickness = 3;
            line.Autoscaling = true;
            ctx.First.UpdatePrecision(PaneSides.RIGHT, sec.Decimals);
        }

    }
}
