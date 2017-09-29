using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WivaldyBot.Models.WivaldyObjects
{
    [Serializable]
    public class RemoteCommand
    {
        public int id { get; set; }
        public string deviceId { get; set; }
        public string commandType { get; set; }
        public Date date { get; set; }
    }
    [Serializable]
    public class Date
    {
        public long millis { get; set; }
    }

}