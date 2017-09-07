using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using WivaldyBot.Helpers;
using WivaldyBot.Models;
using System.Globalization;
using static WivaldyBot.Models.Wivaldy;
using WivaldyBot.Properties;
using Microsoft.Bot.Builder.ConnectorEx;
using Newtonsoft.Json;
using WivaldyBot.Controllers;
using System.Threading;
using System.Web;
using System.Configuration;
using System.Runtime.Serialization;

namespace WivaldyBot.Dialogs
{
    [Serializable]
    public class WivaldyDialog : IDialog<object>
    {
        //for alerts
        [NonSerialized]
        Timer tAlert;
        DateTimeOffset StartAlert;
        private int AlertMaxNumber;
        private int NumberAlerts = 0;
        private Alert alert;

        // versionning
        private int version = 0;

        // WyvaldiAP + message detaisl for callback 
        private Wivaldy myWivaldy;
        MessageDetails me;

        //TODO: change to get right URL
        private const string URL = "https://wivaldy.azurewebsites.net";

        private ResumptionCookie resumptionCookie;

        public WivaldyDialog()
        {
            ResetSettings();
        }

        #region init Settings
        [OnDeserialized()]
        internal void OnDeserializingMethod(StreamingContext context)
        {
            int ver = 0;
            int.TryParse(ConfigurationManager.AppSettings["BotVersion"], out ver);
            if (ver > version)
            {
                version = ver;
                ResetSettings();
            }
        }
        private void ResetSettings()
        {
            alert = new Alert();
            NumberAlerts = 0;
            int.TryParse(ConfigurationManager.AppSettings["AlertMaxNumber"], out AlertMaxNumber);
            myWivaldy = new Wivaldy();
            me = new MessageDetails();
        }
        #endregion

        public async Task StartAsync(IDialogContext context)
        {
            await WelcomeMessageAsync(context);
            //context.Wait(this.MessageReceivedAsync);
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            try
            {
                var message = await result;
                if (this.resumptionCookie == null)
                {
                    this.resumptionCookie = new ResumptionCookie(message);
                }
                if (message.Text == WivaldyBotResources.DialogWelcomeLogout)
                {
                    var reply = context.MakeMessage();
                    try
                    {

                        context.PrivateConversationData.SetValue(WivaldyBotResources.WivaldiConnectionString, "");
                        myWivaldy.Connection = "";
                        reply.Text = WivaldyBotResources.DialogKeyRemoved;
                    }
                    catch (Exception err)
                    {
                        reply.Text = WivaldyBotResources.DialogErrorMessage + $" {err.Message}";
                    }
                    await context.PostAsync(reply);
                }
                else if (message.Text == WivaldyBotResources.DialogWelcomeKey)
                {
                    var reply = context.MakeMessage();
                    try
                    {
                        string key;
                        context.PrivateConversationData.TryGetValue(WivaldyBotResources.WivaldiConnectionString, out key);
                        reply.Text = String.Format(WivaldyBotResources.DialogKeyIs, key);
                    }
                    catch (Exception err)
                    {
                        reply.Text = WivaldyBotResources.DialogErrorMessage + $"{err.Message}";
                    }
                    await context.PostAsync(reply);
                }

                string wiwaldyconnection;

                if ((!context.PrivateConversationData.TryGetValue(WivaldyBotResources.WivaldiConnectionString, out wiwaldyconnection)) || (myWivaldy.Connection == ""))
                {
                    PromptDialog.Text(context, this.ResumeAfterPrompt, WivaldyBotResources.DialogGetPrivateKey);
                    return;
                }
                else
                {
                    myWivaldy.Connection = wiwaldyconnection;
                }

                if (message.Text == WivaldyBotResources.DialogWelcomeElectricity)
                {
                    await this.ElectricityMessageAsync(context);
                    return;
                }
                else if (message.Text == WivaldyBotResources.DialogWelcomeCompare)
                {
                    await this.CompareMessageAsync(context);
                    return;
                }
                else if (message.Text == WivaldyBotResources.DialogWelcomeAlert)
                {

                    // Store information about this specific point the conversation, so that the bot can resume this conversation later.
                    if (me.serviceUrl == null)
                    {
                        me = new MessageDetails();
                        me.toId = message.From.Id;
                        me.toName = message.From.Name;
                        me.fromId = message.Recipient.Id;
                        me.fromName = message.Recipient.Name;
                        me.serviceUrl = message.ServiceUrl;
                        me.channelId = message.ChannelId;
                        me.conversationId = message.Conversation.Id;
                        me.cultureInfo = System.Globalization.CultureInfo.CurrentUICulture;
                    }
                    bool bFound = false;
                    foreach (var mess in ConversationStarter.messageDetails)
                    {
                        if (mess.channelId == me.channelId)
                            if (mess.conversationId == me.conversationId)
                                if (mess.fromId == me.fromId)
                                    if (mess.fromName == me.fromName)
                                        if (mess.serviceUrl == me.serviceUrl)
                                            if (mess.toId == me.toId)
                                                if (mess.toName == me.toName)
                                                {
                                                    bFound = true;
                                                    break;
                                                }
                    }
                    if (!bFound)
                        ConversationStarter.messageDetails.Add(me);

                    context.Call(new DialogAlert(this.alert), this.DialogAlertResumeAfter);
                    return;
                }
                await this.WelcomeMessageAsync(context);
            }
            catch (Exception ex)
            {
                var reply = context.MakeMessage();

                reply.Text = $"{WivaldyBotResources.DialogErrorMessage}: {ex.Message}";
                ResetSettings();
                await context.PostAsync(reply);
                await this.WelcomeMessageAsync(context);
            }

        }

        private async Task WelcomeMessageAsync(IDialogContext context)
        {
            var reply = context.MakeMessage();

            var options = new[]
            {
                WivaldyBotResources.DialogWelcomeElectricity,
                WivaldyBotResources.DialogWelcomeCompare,
                WivaldyBotResources.DialogWelcomeAlert,
                WivaldyBotResources.DialogWelcomeKey,
                WivaldyBotResources.DialogWelcomeLogout
            };
            reply.AddHeroCard(
                WivaldyBotResources.DialogActivitySelect,
                WivaldyBotResources.DialogActivityTellUs,
                options,
                new[] { $"{URL}/Images/wivaldy-all-200x200px.png" });

            await context.PostAsync(reply);

            context.Wait(this.MessageReceivedAsync);
        }

        private async Task ElectricityMessageAsync(IDialogContext context)
        {
            var reply = context.MakeMessage();

            var options = new[]
            {
                WivaldyBotResources.DialogConsumptionInstant,
                WivaldyBotResources.DialogConsumptionHour,
                WivaldyBotResources.DialogConsumptionDay,
                WivaldyBotResources.DialogConsumptionLastDay
            };
            reply.AddHeroCard(
                WivaldyBotResources.DialogElectricityConsumption,
                WivaldyBotResources.DialogElectricityTellUs,
                options,
                new[] { $"{URL}/Images/wivaldy-W-200x200.png" });

            await context.PostAsync(reply);

            context.Wait(this.OnOptionSelected);
        }

        private async Task CompareMessageAsync(IDialogContext context)
        {
            var reply = context.MakeMessage();

            var options = new[]
            {
                WivaldyBotResources.DialogCompareTodayYesterday,
                WivaldyBotResources.DialogCompareYesterdayDayBefore,
                WivaldyBotResources.DialogCompareLastHourYesterdaySameTime,
            };
            reply.AddHeroCard(
                WivaldyBotResources.DialogElectricityConsumption,
                WivaldyBotResources.DialogElectricityTellUs,
                options,
                new[] { $"{URL}/Images/wivaldy-W-200x200.png" });

            await context.PostAsync(reply);

            context.Wait(this.OnOptionSelectedCompare);
        }

        private async Task OnOptionSelected(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            var reply = context.MakeMessage();
            string strresp = "";
            Electricity res = null;

            if (message.Text == WivaldyBotResources.DialogConsumptionInstant)
            {
                res = await myWivaldy.GetLastMeasures();
                strresp = WivaldyBotResources.TotalConsumptionNow;
            }
            else if (message.Text == WivaldyBotResources.DialogConsumptionHour)
            {
                res = await myWivaldy.GetMeasures(DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now);
                strresp = WivaldyBotResources.TotalConsumptionLastHour;
            }
            else if (message.Text == WivaldyBotResources.DialogConsumptionDay)
            {
                DateTimeOffset today = new DateTimeOffset(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 0, 0, 0, DateTimeOffset.Now.Offset);
                res = await myWivaldy.GetMeasures(today, today.AddDays(1));
                strresp = WivaldyBotResources.TotalConsumptionTodayIs;
            }
            else if (message.Text == WivaldyBotResources.DialogConsumptionLastDay)
            {
                DateTimeOffset yesterday = new DateTimeOffset(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 0, 0, 0, DateTimeOffset.Now.Offset);
                yesterday = yesterday.AddDays(-1);
                res = await myWivaldy.GetMeasures(yesterday, yesterday.AddDays(1));
                strresp = WivaldyBotResources.TotalConsumptionYesterday;
            }

            if (res != null)
            {
                if (res.Consumptions.Length > 1)
                {
                    double wattshour = GetWattHour(res) / 1000;
                    //only for the prototype, cost is .13€ per KWh in France
                    double cost = wattshour * 0.13;
                    strresp += String.Format(WivaldyBotResources.TotalConsumptionKwh, wattshour.ToString("N1", CultureInfo.CurrentUICulture), cost.ToString("N2", CultureInfo.CurrentUICulture));
                }
                else if (res.Consumptions.Length == 0)
                {
                    strresp = WivaldyBotResources.TotalConsumptionNoData;
                }
                else
                {
                    strresp += String.Format(WivaldyBotResources.TotalConsumptionInstant, res.Consumptions[0].watts.ToString("N1", CultureInfo.CurrentUICulture));
                }
            }
            else
            {
                strresp = WivaldyBotResources.DialogErrorMessage + " " + WivaldyBotResources.TotalConsumptionNoData;
            }

            reply.Text = strresp;
            reply.TextFormat = "markdown";
            await context.PostAsync(reply);
            //context.Wait(MessageReceivedAsync);
            await this.WelcomeMessageAsync(context);
        }

        private async Task OnOptionSelectedCompare(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            var reply = context.MakeMessage();
            string strresp = "";
            Electricity resA = null, resB = null;
            DateTimeOffset today = new DateTimeOffset(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 0, 0, 0, DateTimeOffset.Now.Offset);
            if (message.Text == WivaldyBotResources.DialogCompareTodayYesterday)
            {
                resA = await myWivaldy.GetMeasures(today, DateTimeOffset.Now);
                resB = await myWivaldy.GetMeasures(today.AddDays(-1), DateTimeOffset.Now.AddDays(-1));
                strresp = WivaldyBotResources.CompareConsumptionTodayYesterday;
            }
            else if (message.Text == WivaldyBotResources.DialogCompareYesterdayDayBefore)
            {
                resA = await myWivaldy.GetDayMeasures(today.AddDays(-1));
                resB = await myWivaldy.GetDayMeasures(today.AddDays(-2));
                strresp = WivaldyBotResources.CompareConsumptionYesterdayDayBefore;
            }
            else if (message.Text == WivaldyBotResources.DialogCompareLastHourYesterdaySameTime)
            {
                resA = await myWivaldy.GetMeasures(DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now);
                resB = await myWivaldy.GetMeasures(DateTimeOffset.Now.AddDays(-1).AddHours(-1), DateTimeOffset.Now.AddDays(-1));
                strresp += WivaldyBotResources.CompareConsumptionLastHourYesterday;
            }

            if ((resA != null) && (resB != null))
            {

                double wattshourA = GetWattHour(resA) / 1000;
                double wattshourB = GetWattHour(resB) / 1000;
                strresp += String.Format(WivaldyBotResources.CompareConsumptionkWh, wattshourA.ToString("N1", CultureInfo.CurrentUICulture), wattshourB.ToString("N1", CultureInfo.CurrentUICulture));
                strresp += "\n\n";
                // need to add correct markdown image
                if (wattshourA > wattshourB)
                    strresp += String.Format(WivaldyBotResources.CompareConsumptionGood, URL);
                else
                    strresp += String.Format(WivaldyBotResources.CompareConsumptionBad, URL);
            }
            else
            {
                strresp = WivaldyBotResources.DialogErrorMessage + " " + WivaldyBotResources.TotalConsumptionNoData;
            }

            reply.Text = strresp;
            reply.TextFormat = "markdown";
            await context.PostAsync(reply);
            //context.Wait(MessageReceivedAsync);
            await this.WelcomeMessageAsync(context);
        }

        private async Task ResumeAfterPrompt(IDialogContext context, IAwaitable<string> result)
        {
            try
            {
                var wivaldyconnection = await result;

                await context.PostAsync(WivaldyBotResources.DialogConfirmationKey);
                myWivaldy.Connection = wivaldyconnection;
                context.PrivateConversationData.SetValue(WivaldyBotResources.WivaldiConnectionString, wivaldyconnection);
            }
            catch (Exception ex)
            {
                await context.PostAsync(WivaldyBotResources.DialogErrorMessage + $"{ex.Message}");
            }

            //context.Wait(this.MessageReceivedAsync);
            await this.WelcomeMessageAsync(context);
        }

        private async Task DialogAlertResumeAfter(IDialogContext context, IAwaitable<Alert> result)
        {
            this.alert = await result;
            var reply = context.MakeMessage();
            reply.Text = string.Format(WivaldyBotResources.AlertOK);
            if (alert.IsInstant)
                reply.Text += string.Format(WivaldyBotResources.AlertChangeInstant, alert.Interval.TotalSeconds, alert.Threshold);
            else
                reply.Text += string.Format(WivaldyBotResources.AlertChangeTotal, alert.Interval.TotalSeconds, alert.Threshold);
            await context.PostAsync(reply);

            StartAlert = DateTimeOffset.Now;
            tAlert = new Timer(new TimerCallback(TimerEventAsync));
            tAlert.Change((int)alert.Interval.TotalMilliseconds, (int)alert.Interval.TotalMilliseconds);
            NumberAlerts = 0;
            try
            {
                var ret = ConversationStarter.Timers[context.Activity.Conversation.Id + context.Activity.ChannelId];
                ConversationStarter.Timers[context.Activity.Conversation.Id + context.Activity.ChannelId] = tAlert.GetHashCode();
            }
            catch (Exception)
            {
                ConversationStarter.Timers.Add(context.Activity.Conversation.Id + context.Activity.ChannelId, tAlert.GetHashCode());
            }

            //var url = HttpContext.Current.Request.Url;
            //We now tell the user that we will talk to them in a few seconds

            //reply.Text = "Hello! In a few seconds I'll send you a message proactively to demonstrate how bots can initiate messages. You can also make me send a message by accessing: " +
            //        url.Scheme + "://" + url.Host + ":" + url.Port + "/api/CustomWebApi";
            //await context.PostAsync(reply);

            await WelcomeMessageAsync(context);
        }

        private void TimerEventAsync(object target)
        {
            //remove previously created timers
            var ret = ConversationStarter.Timers[me.conversationId + me.channelId];
            if (ret != tAlert.GetHashCode())
            {
                tAlert.Dispose();
                return;
            }

            if (StartAlert.Add(alert.MaxTime) <= DateTimeOffset.Now)
            {
                tAlert.Dispose();
                ConversationStarter.EndAlerts(me.conversationId, me.channelId);
            }
            if (NumberAlerts > AlertMaxNumber)
            {
                tAlert.Dispose();
                ConversationStarter.EndAlertsMax(me.conversationId, me.channelId);
            }
            Electricity res = null;
            float consumption = 0;
            if (alert.IsInstant)
            {
                var t = myWivaldy.GetMeasures(DateTimeOffset.Now.Add(-alert.Interval), DateTimeOffset.Now);
                t.Wait();
                res = t.Result;
                if (res != null)
                {
                    foreach (var wat in res.Consumptions)
                    {
                        if (wat.watts >= alert.Threshold)
                        {
                            if (consumption < wat.watts)
                                consumption = wat.watts;
                        }
                    }
                }
            }
            else
            {
                DateTimeOffset today = new DateTimeOffset(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 0, 0, 0, DateTimeOffset.Now.Offset);
                var t = myWivaldy.GetDayMeasures(today);
                t.Wait();
                res = t.Result;
                if (res != null)
                {
                    var cons = GetWattHour(res);
                    if (cons >= alert.Threshold)
                        consumption = (float)cons;
                }

            }
            if (consumption > 0)
            {
                NumberAlerts++;
                ConversationStarter.Resume(me.conversationId, me.channelId, consumption, alert);
            }
        }



    }
}