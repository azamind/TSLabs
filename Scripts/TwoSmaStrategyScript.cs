using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Helpers;
using TSLab.Script.Optimization;

namespace TSLabs.Scripts
{
    public class TwoSmaStrategyScript : IExternalScript
    {
        public OptimProperty PeriodFast = new OptimProperty(10, 10, 50, 5);
        public OptimProperty PeriodSlow = new OptimProperty(50, 50, 200, 10);

        public void Execute(IContext ctx, ISecurity sec)
        {
            var smaSlow = ctx.GetData("SMA", new[] { PeriodSlow.ToString() }, () => Series.SMA(sec.GetClosePrices(ctx), PeriodSlow));
            var smaFast = ctx.GetData("SMA", new[] { PeriodFast.ToString() }, () => Series.SMA(sec.GetClosePrices(ctx), PeriodFast));

            var barsCount = sec.Bars.Count();
            // Если последняя свеча до конца не сформировалась, ее не нужно использовать в цикле торговли
            if (!ctx.IsLastBarUsed)
            {
                barsCount--;
            }

            var startBar = Math.Max(PeriodSlow, ctx.TradeFromBar);

            for (int i = startBar; i < barsCount; i++)
            {
                // Вычисляем сигналы
                var sLong = smaFast[i] > smaSlow[i] && smaFast[i - 1] <= smaSlow[i - 1];
                var sShort = smaFast[i] < smaSlow[i] && smaFast[i - 1] >= smaSlow[i - 1];

                // Получаем активные позиции
                var posLong = sec.Positions.GetLastActiveForSignal("LE", i);
                var posShort = sec.Positions.GetLastActiveForSignal("SE", i);

                if (posLong == null)
                {
                    // Если нет активной длинной позиции и есть сигнал на покупку, то покупаем по рынку
                    if (sLong)
                    {
                        sec.Positions.BuyAtMarket(i + 1, 1, "LE");
                    }
                }
                else
                {
                    // Если есть длинная позиция и есть сигнал на продажу, то закрывам лонг по рынку
                    if (sShort)
                    {
                        posLong.CloseAtMarket(i + 1, "LX");
                    }
                }


                if (posShort == null)
                {
                    // Если нет активной короткой позиции и есть сигнал на продажу, то продаем по рынку
                    if (sShort)
                    {
                        sec.Positions.SellAtMarket(i + 1, 1, "SE");
                    }
                }
                else
                {
                    // Если есть короткая позиция и есть сигнал на покупку, то закрываем шорт по рынку
                    if (sLong)
                    {
                        posShort.CloseAtMarket(i + 1, "SX");
                    }
                }
            }

            if (ctx.IsOptimization)
            {
                return;
            }

            ctx.First.AddList(string.Format("SMAFast({0})", PeriodFast), smaFast, ListStyles.LINE, ScriptColors.Green,
               LineStyles.SOLID, PaneSides.RIGHT);
            ctx.First.AddList(string.Format("SMASlow({0})", PeriodSlow), smaSlow, ListStyles.LINE, ScriptColors.Red,
                LineStyles.SOLID, PaneSides.RIGHT);

        }

    }
}
