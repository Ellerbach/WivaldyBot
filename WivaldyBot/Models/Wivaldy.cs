using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using System.Net.Http;
using System.Diagnostics;
using System.Threading.Tasks;
using WivaldyBot.Models.WivaldyObjects;

namespace WivaldyBot.Models
{
    [Serializable]
    public class Wivaldy
    {
        public string Connection { get; set; }

        public Wivaldy() { }

        public Wivaldy(string connection)
        {
            Connection = connection;
        }

        #region Consumption

        public async Task<Electricity> GetMeasures(DateTimeOffset start, DateTimeOffset stop)
        {
            //API is http://app.wivaldy.com/api/v1/json/privatekey/2016-11-01-08:00/2016-11-01-08:10
            //API is http://app.wivaldy.com/api/v1/json/{PRIVATE_KEY}/last
            try
            {
                HttpClient cli = new HttpClient();
                string urlrequest = $"https://app.wivaldy.com/api/v1/json/{Connection}/";
                //if both are null, then only last measure
                if ((start == DateTimeOffset.MinValue) && (stop == DateTimeOffset.MinValue))
                    urlrequest += "last";
                //if only stop is nullm than a full day
                else if (stop == DateTimeOffset.MinValue)
                    urlrequest += start.ToString("yyyy-MM-dd") + "/" + start.AddDays(1).ToString("yyyy-MM-dd");
                else
                {
                    //TEMP FIX TO REMOVE WHILE NO DATA
                    //start = start.AddDays(-8);
                    //stop = stop.AddDays(-8);
                    // END REMOVE
                    if (DateTimeOffset.Compare(start, stop) > 1)
                    { stop = start.AddDays(1); }
                    urlrequest += start.ToString("yyyy-MM-dd-HH:mm") + "/" + stop.ToString("yyyy-MM-dd-HH:mm");
                }
                var str = await cli.GetStringAsync(new Uri(urlrequest));
                Electricity elec = new Electricity();
                if (str.Contains("["))
                {
                    var measuregroup = JsonConvert.DeserializeObject<List<Consumption>>(str);                  
                    elec.Consumptions = measuregroup.ToArray();                    
                }
                else
                {
                    var measure = JsonConvert.DeserializeObject<Consumption>(str);
                    elec.Consumptions = new Consumption[1];
                    elec.Consumptions[0] = measure;
                }
                return elec;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"exception: {ex.Message}");
            }
            return null;
        }

        public async Task<Electricity> GetLastMeasures()
        {
            return await GetMeasures(DateTimeOffset.MinValue, DateTimeOffset.MinValue);
        }

        public async Task<Electricity> GetDayMeasures(DateTimeOffset start)
        {
            return await GetMeasures(start, DateTimeOffset.MinValue);
        }

        public static double GetWattHour(Electricity res)
        {
            if (res.Consumptions.Length > 1)
            {
                long epochmin = res.Consumptions[0].epoch;
                long epochmax = epochmin;
                double wattshour = 0;
                foreach (var elec in res.Consumptions)
                {
                    wattshour += elec.watts * (elec.epoch - epochmin);
                    epochmin = elec.epoch;
                }
                wattshour = wattshour / 3600;
                return wattshour;
            }
            else
            {
                if (res.Consumptions[0] != null)
                    return res.Consumptions[0].watts;
                else
                    return 0;
            }
        }

        public static double GetKiloWattHour(Electricity res)
        {
            return GetWattHour(res) / 1000.0;
        }

        #endregion

#region DeviceRemoteCommand

        public async Task<RemoteCommand>GetRemoteCommand()
        {
            //https://app-recette.wivaldy.com/api/v1/device-remote-command/{secret key}/last
            try
            {
                HttpClient cli = new HttpClient();
                string urlrequest = $"https://app-recette.wivaldy.com/api/v1/device-remote-command/{Connection}/last";
                //if both are null, then only last measure
                
                var str = await cli.GetStringAsync(new Uri(urlrequest));
                var measure = JsonConvert.DeserializeObject<RemoteCommand>(str);
                return measure;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"exception: {ex.Message}");
            }
            return null;
        }



        #endregion

    }
}