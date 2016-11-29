using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using WivaldyBot.Helpers;
using WivaldyBot.Models;
using static WivaldyBot.Models.Wivaldy;
using System.Globalization;

namespace WivaldyBot.Dialogs
{
    [Serializable]
    public class WivaldyDialog : IDialog<object>
    {

        private const string DialogConsumptionInstant = "Instant";
        private const string DialogConsumptionHour = "Last hour";
        private const string DialogConsumptionDay = "Today";
        private const string DialogConsumptionLastDay = "Last day";
        private const string WivaldiConnectionString = "wivaldystring";

        private Wivaldy myWivaldy = new Wivaldy();

        private ResumptionCookie resumptionCookie;
        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(this.MessageReceivedAsync);
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            if (this.resumptionCookie == null)
            {
                this.resumptionCookie = new ResumptionCookie(message);
            }

            string wiwaldyconnection;

            if (!context.UserData.TryGetValue(WivaldiConnectionString, out wiwaldyconnection))
            {
                PromptDialog.Text(context, this.ResumeAfterPrompt, "Before get started, can you please give me your private key?");
                return;
            } else
            {
                myWivaldy.Connection = wiwaldyconnection;
            }

            await this.WelcomeMessageAsync(context);
        }

        private async Task WelcomeMessageAsync(IDialogContext context)
        {
            var reply = context.MakeMessage();

            var options = new[]
            {
                DialogConsumptionInstant,
                DialogConsumptionHour,
                DialogConsumptionDay,
                DialogConsumptionLastDay
            };
            reply.AddHeroCard(
                "Electric consumption",
                "Please select the cunsomption you want to see",
                options,
                new[] { "http://storage.googleapis.com/instapage-user-media/2befcf7f/6297728-0-wivaldy-all.png" });

            await context.PostAsync(reply);

            context.Wait(this.OnOptionSelected);
        }

        private async Task OnOptionSelected(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            var reply = context.MakeMessage();
            string strresp = "Your total consumption for ";
            Electricity res = null;

            if (message.Text == DialogConsumptionInstant)
            {
                res = await myWivaldy.GetLastMeasures();
                strresp += "now is ";
            }
            else if (message.Text == DialogConsumptionHour)
            {
                res = await myWivaldy.GetMeasures(DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now);
                strresp += "the last hour is ";
            }
            else if (message.Text == DialogConsumptionDay)
            {
                DateTimeOffset today = new DateTimeOffset(DateTimeOffset.Now.Year, DateTimeOffset.Now.Month, DateTimeOffset.Now.Day, 0, 0, 0, DateTimeOffset.Now.Offset);
                res = await myWivaldy.GetMeasures(today, today.AddDays(1));
                strresp += "today is ";
            }
            else if (message.Text == DialogConsumptionLastDay)
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
                    long epochmin = res.Consumptions[0].epoch;
                    long epochmax = epochmin;
                    double wattshour = 0;
                    foreach (var elec in res.Consumptions)
                    {
                        wattshour += elec.watts*(elec.epoch-epochmin);
                        epochmin = elec.epoch;
                    }
                    wattshour = wattshour / 3600;
                    strresp += $"{wattshour.ToString("N0", CultureInfo.CurrentUICulture)} kWh.";
                }
                else
                {
                    strresp += $"{res.Consumptions[0].watts.ToString("N0", CultureInfo.CurrentUICulture)} watts instanatannés.";
                }
            }
            else
            {
                strresp = "Ups, something went wrong, can't get the consumption.";
            }

            reply.Text = strresp;
            reply.TextFormat = "markdown";
            await context.PostAsync(reply);
            context.Wait(MessageReceivedAsync);
        }

        private async Task ResumeAfterPrompt(IDialogContext context, IAwaitable<string> result)
        {
            try
            {
                var wivaldyconnection = await result;

                await context.PostAsync($"Thanks, now I have your jey and can get your electricity details!");

                context.UserData.SetValue(WivaldiConnectionString, wivaldyconnection);
            }
            catch (TooManyAttemptsException)
            {
            }

            context.Wait(this.MessageReceivedAsync);
        }

    }
}