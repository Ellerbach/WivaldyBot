using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WivaldyBot.Models;
using WivaldyBot.Properties;

namespace WivaldyBot.Controllers
{
    public class ConversationStarter
    {
        //storing all the users infos
        public static List<MessageDetails> messageDetails = new List<MessageDetails>();
        //store existing timers hash and the conversationId + channelId
        public static Dictionary<string, int> Timers = new Dictionary<string, int>();

        //This will send simple notification
        public static async Task Resume(string conversationId, string channelId, float consumption, Alert alert)
        {
            //find the good person in the list
            MessageDetails myPerson = GetPerson(conversationId, channelId);
            if (myPerson != null)
            {
                var connector = new ConnectorClient(new Uri(myPerson.serviceUrl));
                var message = await GetMessageAsync(myPerson);
                // TO DO send the right message to the User, take more params as entry
                string res = "";
                if (alert.IsInstant)
                    res = "AlertExceedInstant";
                else
                    res = "AlertExceedTotal";
                message.Text = string.Format(WivaldyBotResources.ResourceManager.GetString(res, myPerson.cultureInfo), consumption.ToString("N1", myPerson.cultureInfo), alert.Threshold.ToString("N1", myPerson.cultureInfo));
                message.TextFormat = "markdown";
                //message.Text = "Hello, this is a notification";
                message.Locale = myPerson.cultureInfo.Name;
                await connector.Conversations.SendToConversationAsync((Activity)message);
            }
        }

        public static async Task EndAlerts(string conversationId, string channelId)
        {
            MessageDetails myPerson = GetPerson(conversationId, channelId);
            if (myPerson != null)
            {
                var connector = new ConnectorClient(new Uri(myPerson.serviceUrl));
                var message = await GetMessageAsync(myPerson);
                // TO DO send the right message to the User, take more params as entry                
                message.Text = WivaldyBotResources.ResourceManager.GetString("AlertEnd", myPerson.cultureInfo);
                message.TextFormat = "markdown";
                //message.Text = "Hello, this is a notification";
                message.Locale = myPerson.cultureInfo.Name;
                await connector.Conversations.SendToConversationAsync((Activity)message);
            }
        }

        public static async Task EndAlertsMax(string conversationId, string channelId)
        {
            MessageDetails myPerson = GetPerson(conversationId, channelId);
            if (myPerson != null)
            {
                var connector = new ConnectorClient(new Uri(myPerson.serviceUrl));
                var message = await GetMessageAsync(myPerson);
                // TO DO send the right message to the User, take more params as entry                
                message.Text = WivaldyBotResources.ResourceManager.GetString("AlertEndMax", myPerson.cultureInfo);
                message.TextFormat = "markdown";
                //message.Text = "Hello, this is a notification";
                message.Locale = myPerson.cultureInfo.Name;
                await connector.Conversations.SendToConversationAsync((Activity)message);
            }
        }

        private static async Task<IMessageActivity> GetMessageAsync(MessageDetails myPerson)
        {
            var userAccount = new ChannelAccount(myPerson.toId, myPerson.toName);
            var botAccount = new ChannelAccount(myPerson.fromId, myPerson.fromName);
            var connector = new ConnectorClient(new Uri(myPerson.serviceUrl));

            IMessageActivity message = Activity.CreateMessageActivity();
            string conversationId = myPerson.conversationId;
            if (!string.IsNullOrEmpty(myPerson.conversationId) && !string.IsNullOrEmpty(myPerson.channelId))
            {
                message.ChannelId = myPerson.channelId;
            }
            else
            {
                conversationId = (await connector.Conversations.CreateDirectConversationAsync(botAccount, userAccount)).Id;
            }
            message.From = botAccount;
            message.Recipient = userAccount;
            message.Conversation = new ConversationAccount(id: conversationId);
            return message;
        }

        private static MessageDetails GetPerson(string conversationId, string channelId)
        {
            MessageDetails myPerson = null;
            foreach (var mess in messageDetails)
            {
                if ((mess.conversationId == conversationId) && (mess.channelId == channelId))
                {
                    myPerson = mess;
                    break;
                }
            }
            return myPerson;
        }
    }
}

