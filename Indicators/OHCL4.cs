﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TSLab.Script;
using TSLab.Script.Handlers;
using TSLab.Script.Handlers.Options;

namespace TSLabs.Indicators
{

    [HandlerCategory("TSLabsIndicators")]
    [HelperName("OHLC4")]
    public class OHLC4 : IBar2DoubleHandler
    {

        public IList<double> Execute(ISecurity source)
        {
            return source.Bars.Select(x => (x.Open + x.High + x.Low + x.Close) / 4).ToList();
        }

    }

}