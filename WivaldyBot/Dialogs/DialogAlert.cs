using Microsoft.Bot.Builder.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WivaldyBot.Models;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using WivaldyBot.Properties;
using System.Configuration;

namespace WivaldyBot.Dialogs
{
    [Serializable]
    public class DialogAlert : IDialog<Alert>
    {
        private Alert myAlert;
        private const int MAXTRY = 3;
        private int attempts = MAXTRY;

        TimeSpan AlertMaxTime = new TimeSpan(0, 10, 0);
        private int AlertMinRefresh;

        public DialogAlert(Alert alert)
        {            
            int.TryParse(ConfigurationManager.AppSettings["AlertMinRefresh"], out AlertMinRefresh);
            TimeSpan.TryParse(ConfigurationManager.AppSettings["AlertMaxTime"], out AlertMaxTime);
            myAlert = alert;
        }

        public async Task StartAsync(IDialogContext context)
        {


            await this.WelcomeMessageAsync(context);

        }

        private async Task WelcomeMessageAsync(IDialogContext context)
        {
            var reply = context.MakeMessage();

            if ((myAlert.Interval != TimeSpan.Zero) && (myAlert.Threshold > 0))
            {
                reply.Attachments = new List<Attachment>();
                List<CardAction> cardButtons = new List<CardAction>();
                cardButtons.Add(new CardAction() { Title = WivaldyBotResources.DialogYes, Value = WivaldyBotResources.DialogYes, Type = "postBack" });
                cardButtons.Add(new CardAction() { Title = WivaldyBotResources.DialogNo, Value = WivaldyBotResources.DialogNo, Type = "postBack" });
                HeroCard plCard = new HeroCard()
                {
                    Buttons = cardButtons
                };

                plCard.Title = WivaldyBotResources.AlertChange;
                if (myAlert.IsInstant)
                    plCard.Subtitle = String.Format(WivaldyBotResources.AlertChangeInstant, myAlert.Interval.TotalSeconds, myAlert.Threshold);
                else
                    plCard.Subtitle = String.Format(WivaldyBotResources.AlertChangeTotal, myAlert.Interval.TotalSeconds, myAlert.Threshold);

                Attachment plAttachment = plCard.ToAttachment();
                reply.Attachments.Add(plAttachment);

                await context.PostAsync(reply);
                context.Wait(this.MessageReceivedAsync);
            }
            else
                await MessageReceived(context);
        }

        public async Task MessageReceived(IDialogContext context)
        {
            //ask for the interval in seconds
            await context.PostAsync(WivaldyBotResources.AlertInterval);
            context.Wait(this.AskInterval);
        }

        public async Task MessageIsInstant(IDialogContext context)
        {
            attempts = MAXTRY;
            //Ask for instant alert or total
            var reply = context.MakeMessage();
            reply.Attachments = new List<Attachment>();
            List<CardAction> cardButtons = new List<CardAction>();
            cardButtons.Add(new CardAction() { Title = WivaldyBotResources.AlertInstant, Value = WivaldyBotResources.AlertInstant, Type = "postBack" });
            cardButtons.Add(new CardAction() { Title = WivaldyBotResources.AlertTotal, Value = WivaldyBotResources.AlertTotal, Type = "postBack" });
            HeroCard plCard = new HeroCard()
            {
                Buttons = cardButtons
            };
            plCard.Title = WivaldyBotResources.AlertAskInstant;
            Attachment plAttachment = plCard.ToAttachment();
            reply.Attachments.Add(plAttachment);
            await context.PostAsync(reply);
            context.Wait(this.AskInstant);
        }

        public async Task MessageThreshold(IDialogContext context)
        {
            //ask for the threshold
            if (myAlert.IsInstant)
                await context.PostAsync(WivaldyBotResources.AlertThresholdWatts);
            else
                await context.PostAsync(WivaldyBotResources.AlertThresholdkWh);
            context.Wait(this.AskThreshold);
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {

            var message = await result;
            if (message == null)
            {
                await this.WelcomeMessageAsync(context);
            }
            if (message.Text == WivaldyBotResources.DialogYes)
            {
                await this.MessageReceived(context);
            }
            else if (message.Text == WivaldyBotResources.DialogNo)
            {
                context.Done(this.myAlert);
            }
            else
            {
                await this.WelcomeMessageAsync(context);
                context.Wait(MessageReceivedAsync);
            }
        }

        private async Task AskInterval(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            //Need to check if it is a valid number
            //string crr = message;
            int sec = 0;
            if (int.TryParse(message.Text, out sec))
            {
                if (sec > 0)
                {
                    if (sec < AlertMinRefresh)
                    {
                        await context.PostAsync(string.Format(WivaldyBotResources.AlertMinSec, AlertMinRefresh));
                        sec = AlertMinRefresh;
                    }
                    myAlert.Interval = TimeSpan.FromSeconds(sec);

                    await this.MessageMaxTime(context);
                    return;
                }
            }

            --attempts;
            if (attempts > 0)
            {
                await context.PostAsync(WivaldyBotResources.AlertRetryInterval);
                context.Wait(this.AskInterval);
            }
            else
            {
                await context.PostAsync(WivaldyBotResources.AlertReallySorry);
                context.Done(this.myAlert);
            }
        }

        public async Task MessageMaxTime(IDialogContext context)
        {
            //ask for the interval in seconds
            await context.PostAsync(WivaldyBotResources.AlertTime);
            context.Wait(this.AskMaxTime);
        }


        private async Task AskMaxTime(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            //Need to check if it is a valid number
            //string crr = message;
            int sec = 0;
            if (int.TryParse(message.Text, out sec))
            {
                if (sec > 0)
                {
                    if (sec > AlertMaxTime.TotalMinutes)
                    {
                        await context.PostAsync(string.Format(WivaldyBotResources.AlertMaxTime, AlertMaxTime.TotalMinutes));
                        sec = AlertMinRefresh;
                    }
                    myAlert.MaxTime = TimeSpan.FromMinutes(sec);

                    await this.MessageIsInstant(context);
                    return;
                }
            }

            --attempts;
            if (attempts > 0)
            {
                await context.PostAsync(WivaldyBotResources.AlertRetryMaxTime);
                context.Wait(this.AskMaxTime);
            }
            else
            {
                await context.PostAsync(WivaldyBotResources.AlertReallySorry);
                context.Done(this.myAlert);
            }
        }

        private async Task AskInstant(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            //Need to check if it is a valid number
            if (message.Text == WivaldyBotResources.AlertInstant)
            {
                myAlert.IsInstant = true;
            }
            else
            {
                myAlert.IsInstant = false;
            }
            await this.MessageThreshold(context);
        }
        private async Task AskThreshold(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            //Need to check if it is a valid number
            string crr = message.Text;
            float sec = 0;
            if (float.TryParse(crr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentUICulture, out sec))
            {
                if (sec > 0)
                {
                    myAlert.Threshold = sec;
                    context.Done(myAlert);
                    return;
                }
            }

            --attempts;
            if (attempts > 0)
            {
                await context.PostAsync(string.Format(WivaldyBotResources.AlertRetryThreshold, System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.CurrencyDecimalSeparator));
                context.Wait(this.AskThreshold);
            }
            else
            {
                await context.PostAsync(WivaldyBotResources.AlertReallySorry);
                context.Done(this.myAlert);
            }
        }


    }
}