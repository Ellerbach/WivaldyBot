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

namespace WivaldyBot.Dialogs
{
    [Serializable]
    public class WivaldyDialog : IDialog<object>
    {
        private Wivaldy myWivaldy = new Wivaldy();
        private Alert alert = new Alert();
        //TODO: change to get right URL
        private const string URL = "https://wivaldy.azurewebsites.net";

        private ResumptionCookie resumptionCookie;
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
                    context.Call(new DialogAlert(this.alert), this.DialogAletrResumeAfter);
                    return;
                }
                await this.WelcomeMessageAsync(context);
            }
            catch (Exception ex)
            {
                var reply = context.MakeMessage();

                reply.Text = $"{WivaldyBotResources.DialogErrorMessage}: {ex.Message}";

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
                strresp += String.Format(WivaldyBotResources.CompareConsumptionkWh,wattshourA.ToString("N1", CultureInfo.CurrentUICulture), wattshourB.ToString("N1", CultureInfo.CurrentUICulture));
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

            context.Wait(this.MessageReceivedAsync);
        }

        private async Task DialogAletrResumeAfter(IDialogContext context, IAwaitable<Alert> result)
        {
            this.alert = await result;
            var reply = context.MakeMessage();
            reply.Text = string.Format(WivaldyBotResources.AlertOK);
            if (alert.IsInstant)
                reply.Text += string.Format(WivaldyBotResources.AlertChangeInstant, alert.Interval, alert.Threshold);
            else
                reply.Text += string.Format(WivaldyBotResources.AlertChangeTotal, alert.Interval, alert.Threshold);
            await context.PostAsync(reply);
            reply.Text = "Alert not yet implemented";
            await context.PostAsync(reply);

            await WelcomeMessageAsync(context);
        }

    }
}