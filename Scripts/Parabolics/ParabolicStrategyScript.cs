using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSLab.DataSource;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Helpers;
using TSLab.Script.Optimization;

namespace TSLabs.Scripts.Parabolics
{
    public class ParabolicStrategyScript : IExternalScript
    {
        public OptimProperty ParabolicAccelerationMaxPeriod = new OptimProperty(0.02, 0.01, 0.4, 0.01);
        public OptimProperty ParabolicAccelerationMinPeriod = new OptimProperty(0.01, 0.001, 0.1, 0.001);
        public OptimProperty ParabolicAccelerationStepPeriod = new OptimProperty(0.01, 0.001, 0.1, 0.001);

        public const string OpenLong = "OpenLong";
        public const string OpenShort = "OpenShort";
        public const string CloseLong = "CloseLong";
        public const string CloseShort = "CloseShort";

        public void Execute(IContext ctx, ISecurity sec)
        {
            ctx.Log("MaxValue: " + ParabolicAccelerationMaxPeriod.Value, MessageType.Info, true);
            ctx.Log("MinValue: " + ParabolicAccelerationMinPeriod.Value, MessageType.Info, true);
            ctx.Log("StepValue: " + ParabolicAccelerationStepPeriod.Value, MessageType.Info, true);

            ctx.First.Visible = true;
            ctx.First.HideLegend = false;

            var parabolicSar = new ParabolicSAR()
            {
                Context = ctx,
                AccelerationMax = ParabolicAccelerationMaxPeriod.Value,
                AccelerationStart = ParabolicAccelerationMinPeriod.Value,
                AccelerationStep = ParabolicAccelerationStepPeriod.Value
            };

            var list = parabolicSar.Execute(sec);

            var parabolic = ctx.GetData(nameof(ParabolicSAR), new[] {
                ParabolicAccelerationMaxPeriod.ToString(),
                ParabolicAccelerationMinPeriod.ToString(),
                ParabolicAccelerationStepPeriod.ToString()
            }, () => list);

            var barsCount = sec.Bars.Count();
            if (!ctx.IsLastBarUsed)
            {
                barsCount--;
            }

            var closePrices = sec.Bars.Select(x => x.Close).ToList();

            for (int i = 1; i < barsCount; i++)
            {
                var signalToOpenLong = closePrices[i] > parabolic[i] && closePrices[i - 1] <= parabolic[i - 1];
                var signalToOpenShort = closePrices[i] < parabolic[i] && closePrices[i - 1] >= parabolic[i - 1];
                var activeLongPosition = sec.Positions.GetLastActiveForSignal(OpenLong, i);
                var activeShortPosition = sec.Positions.GetLastActiveForSignal(OpenShort, i);

                if (activeLongPosition == null)
                {
                    if (signalToOpenLong)
                    {
                        sec.Positions.BuyAtMarket(i + 1, 1, OpenLong);
                    }
                }
                else
                {
                    if (signalToOpenShort)
                    {
                        activeLongPosition.CloseAtMarket(i + 1, CloseLong);
                    }
                }

                if (activeShortPosition == null)
                {
                    if (signalToOpenShort)
                    {
                        sec.Positions.SellAtMarket(i + 1, 1, OpenShort);
                    }
                }
                else
                {
                    if (signalToOpenLong)
                    {
                        activeShortPosition.CloseAtMarket(i + 1, CloseShort);
                    }
                }

            }


            if (ctx.IsOptimization)
            {
                return;
            }

            var line = ctx.First.AddList(nameof(ParabolicSAR), parabolic, ListStyles.LINE, ScriptColors.Cyan, LineStyles.DOT, PaneSides.RIGHT);
            line.Thickness = 3;
            line.Autoscaling = true;
            ctx.First.UpdatePrecision(PaneSides.RIGHT, sec.Decimals);
        }
    }
}
