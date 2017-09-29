using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WivaldyBot.Models
{
    [Serializable]
    public class Alert
    {
        public TimeSpan Interval { get; set; }
        public float Threshold { get; set; }
        public AlertEnum AlertType { get; set; }
        public TimeSpan MaxTime { get; set; }
    }

    public enum AlertEnum
    { Instant, Total, Switch }
}