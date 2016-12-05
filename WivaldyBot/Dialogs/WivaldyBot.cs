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
        //TODO: change to get right URL
        private const string URL = "https://wivaldy.azurewebsites.net";

        private ResumptionCookie resumptionCookie;
        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(this.MessageReceivedAsync);
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
                        reply.Text = WivaldyBotResources.DialogErrorMessage + $"{err.Message}";
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
                        reply.Text = $"Your key is {key}";
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
                await this.WelcomeMessageAsync(context);
            }
            catch (Exception ex)
            {
                var reply = context.MakeMessage();

                reply.Text = $"Ups, big error, {ex.Message}";

                await context.PostAsync(reply);
            }

        }

        private async Task WelcomeMessageAsync(IDialogContext context)
        {
            var reply = context.MakeMessage();

            var options = new[]
            {
                WivaldyBotResources.DialogWelcomeElectricity,
                WivaldyBotResources.DialogWelcomeCompare,
                WivaldyBotResources.DialogWelcomeKey,
                WivaldyBotResources.DialogWelcomeLogout
            };
            reply.AddHeroCard(
                "Select your activity",
                "Tell us what you want to do",
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
                "Electric consumption",
                "Please select the cunsomption you want to see",
                options,
                new[] { $"{URL}/Images/wivaldy-W-é00x200.png" });

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
                "Electric consumption",
                "Please select the cunsomption you want to see",
                options,
                new[] { $"{URL}/Images/wivaldy-W-200x200.png" });

            await context.PostAsync(reply);

            context.Wait(this.OnOptionSelectedCompare);
        }

        private async Task OnOptionSelected(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            var reply = context.MakeMessage();
            string strresp = "Your total consumption for ";
            Electricity res = null;

            if (message.Text == WivaldyBotResources.DialogConsumptionInstant)
            {
                res = await myWivaldy.GetLastMeasures();
                strresp += "now is ";
            }
            else if (message.Text == WivaldyBotResources.DialogConsumptionHour)
            {
                res = await myWivaldy.GetMeasures(DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now);
                strresp += "the last hour is ";
            }
            else if (message.Text == WivaldyBotResources.DialogConsumptionDay)
            {
                DateTimeOffset today = new DateTimeOffset(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 0, 0, 0, DateTimeOffset.Now.Offset);
                res = await myWivaldy.GetMeasures(today, today.AddDays(1));
                strresp += "today is ";
            }
            else if (message.Text == WivaldyBotResources.DialogConsumptionLastDay)
            {
                DateTimeOffset yesterday = new DateTimeOffset(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 0, 0, 0, DateTimeOffset.Now.Offset);
                yesterday = yesterday.AddDays(-1);
                res = await myWivaldy.GetMeasures(yesterday, yesterday.AddDays(1));
                strresp += "yesterday is ";
            }

            if (res != null)
            {
                if (res.Consumptions.Length > 1)
                {
                    double wattshour = GetWattHour(res) / 1000;
                    //only for the prototype, cost is .13€ per KWh in France
                    double cost = wattshour * 0.13;
                    strresp += $"{wattshour.ToString("N0", CultureInfo.CurrentUICulture)} kWh. Cost is approximately {cost.ToString("N2", CultureInfo.CurrentUICulture)} €.";
                }
                else if (res.Consumptions.Length == 0)
                {
                    strresp += "Sorry but there are not data.";
                }
                else
                {
                    strresp += $"{res.Consumptions[0].watts.ToString("N0", CultureInfo.CurrentUICulture)} watts instant consumption.";
                }
            }
            else
            {
                strresp = WivaldyBotResources.DialogErrorMessage + "Can't get the consumption.";
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
            string strresp = "Your total consumption compare from ";
            Electricity resA = null, resB = null;
            DateTimeOffset today = new DateTimeOffset(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 0, 0, 0, DateTimeOffset.Now.Offset);
            if (message.Text == WivaldyBotResources.DialogCompareTodayYesterday)
            {
                resA = await myWivaldy.GetMeasures(today, DateTimeOffset.Now);
                resB = await myWivaldy.GetMeasures(today.AddDays(-1), DateTimeOffset.Now.AddDays(-1));
                strresp += "yesterday and today at same time is ";
            }
            else if (message.Text == WivaldyBotResources.DialogCompareYesterdayDayBefore)
            {
                resA = await myWivaldy.GetDayMeasures(today.AddDays(-1));
                resB = await myWivaldy.GetDayMeasures(today.AddDays(-2));
                strresp += "yesterday and day before is ";
            }
            else if (message.Text == WivaldyBotResources.DialogCompareLastHourYesterdaySameTime)
            {
                resA = await myWivaldy.GetMeasures(DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now);
                resB = await myWivaldy.GetMeasures(DateTimeOffset.Now.AddDays(-1).AddHours(-1), DateTimeOffset.Now.AddDays(-1));
                strresp += "today last hour and yesterday same time is ";
            }

            if ((resA != null) && (resB != null))
            {

                double wattshourA = GetWattHour(resA) / 1000;
                double wattshourB = GetWattHour(resB) / 1000;
                strresp += $"{wattshourA.ToString("N0", CultureInfo.CurrentUICulture)} kWh vs {wattshourB.ToString("N0", CultureInfo.CurrentUICulture)} kWh.\r\n\r\n";
                // need to add correct markdown image
                if (wattshourA > wattshourB)
                    strresp += $"![Good]({URL}/Images/wivaldy_icon_home-overload-200x200.png)";
                else
                    strresp += $"![Bad]({URL}/Images/wivaldy_icon_home-sleepy-200x200.png)";
            }
            else
            {
                strresp = WivaldyBotResources.DialogErrorMessage + "Can't get the consumption.";
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

    }
}