using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSLab.Script;
using TSLab.Script.Handlers;

namespace TSLabs.Scripts
{
    public class BuyScript : IExternalScript
    {
        public const string OPEN_LONG = "OL";

        public void Execute(IContext ctx, ISecurity sec)
        {
            //var args = new Dictionary<string, object> { { "agent", ctx.Runtime.TradeName } };
            ctx.Log("Start Buy Script", MessageType.Info, true);
            for (int i = 0; i < ctx.BarsCount; i++)
            {
                var openLong = sec.Positions.GetLastActiveForSignal(OPEN_LONG, i);

                if (openLong == null)
                {
                    sec.Positions.BuyAtMarket(i + 1, 1, OPEN_LONG);
                }
                else
                {
                    openLong.CloseAtStop(i + 1, openLong.EntryPrice * 0.995, "CloseLongStopLoss");
                    openLong.CloseAtProfit(i + 1, openLong.EntryPrice * 1.005, "CloseLongTakeProfit");
                }
            }
            ctx.Log("Finish Buy Script", MessageType.Info, true);
        }
    }
}
