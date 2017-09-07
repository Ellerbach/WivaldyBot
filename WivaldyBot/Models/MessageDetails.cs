using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;

namespace WivaldyBot.Models
{
    [Serializable]
    public class MessageDetails
    {
        public string fromId { get; set; }
        public string fromName { get; set; }
        public string toId { get; set; }
        public string toName { get; set; }
        public string serviceUrl { get; set; }
        public string channelId { get; set; }
        public string conversationId { get; set; }
        public CultureInfo cultureInfo { get; set; }
    }
}