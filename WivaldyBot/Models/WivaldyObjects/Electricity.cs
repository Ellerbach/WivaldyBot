using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WivaldyBot.Models.WivaldyObjects
{
    [Serializable]
    public class Electricity
    {
        public Consumption[] Consumptions { get; set; }
    }

    [Serializable]
    public class Consumption
    {
        public int epoch { get; set; }
        public int watts { get; set; }
    }
}