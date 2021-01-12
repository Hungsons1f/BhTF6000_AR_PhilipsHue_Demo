using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;
using TwinCAT.Ads;
using Newtonsoft.Json.Linq;

namespace Http_Server.Controllers
{
    public class OnOffController : ApiController
    {
        private TcAdsClient adsClient;

        //Control Variables
        private int hbPut;
        private int hbGet;
        private int hbExecute;

        //Handlers and buffers
        struct WriteHandler
        {
            public int hbwOn;
        }
        private WriteHandler writeHandler;

        struct ReadHandler
        {
            public int hbrOn;
        }
        private ReadHandler readHandler;

        struct ReadVar
        {
            public volatile bool brOn;
        }
        private ReadVar readVar;

        //Event signal
        private volatile bool signal = false;




        public OnOffController()
        {
            try
            {
                //Initiate and connect to ADS
                adsClient = new TcAdsClient();
                adsClient.Connect(GlobalVariables.AMSNetID, GlobalVariables.Port);

                //Create variable handlers for writevar
                hbPut = adsClient.CreateVariableHandle("MAIN.bPut");
                hbGet = adsClient.CreateVariableHandle("MAIN.bGet");
                hbExecute = adsClient.CreateVariableHandle("MAIN.bExecute");
                writeHandler.hbwOn = adsClient.CreateVariableHandle("MAIN.bwON");

                //Create event handlers for readvar
                readHandler.hbrOn = adsClient.AddDeviceNotificationEx("Main.brOn", AdsTransMode.OnChange, 200, 0, null, typeof(bool));
                adsClient.AdsNotificationEx += AdsClient_AdsNotificationEx;

                //Readvar buffer
                readVar = new ReadVar();
            }
            catch (Exception err)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("ADS client cannot be initiated! Detail: " + err.ToString()) });
            }
        }

        private void AdsClient_AdsNotificationEx(object sender, AdsNotificationExEventArgs e)
        {
            readVar.brOn = (bool)e.Value;
            signal = true;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                adsClient.DeleteVariableHandle(writeHandler.hbwOn);
                adsClient.DeleteVariableHandle(hbPut);
                adsClient.DeleteVariableHandle(hbGet);
                adsClient.DeleteVariableHandle(hbExecute);

                adsClient.DeleteDeviceNotification(readHandler.hbrOn);
                adsClient.AdsNotificationEx -= AdsClient_AdsNotificationEx;
                adsClient.Dispose();
            }
            catch (Exception err)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("ADS client cannot be disposed! Detail: " + err.ToString()) });
            }
            base.Dispose(disposing);
        }




        //GET api/values
        public HttpResponseMessage Get()
        {
            try
            {
                signal = false;
                adsClient.WriteAny(hbExecute, true);
                adsClient.WriteAny(hbGet, true);

                long reftime = DateTime.Now.Ticks;
                long now = reftime;
                while (!signal && (now <= (reftime + 30000000)))
                {
                    now = DateTime.Now.Ticks;
                }

                if (now > (reftime+30000000))
                {
                    return Request.CreateResponse(HttpStatusCode.GatewayTimeout, "ADS server response timeout!");
                }
                else
                {
                    JObject jo = new JObject();
                    jo.Add("on", readVar.brOn);
                    return Request.CreateResponse(HttpStatusCode.OK, jo);
                }
            }
            catch (Exception err)
            {
                //throw new HttpResponseException (Request.CreateResponse(HttpStatusCode.InternalServerError, "Error! " + err.ToString()));
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "GET Error! " + err.ToString());
            }
        }

        // PUT api/values/5
        public HttpResponseMessage Put([FromUri]string on = "true")
        {
            try
            {
                switch (on.ToLower())
                {
                    case "true":
                    case "false":
                        adsClient.WriteAny(hbExecute, true);
                        adsClient.WriteAny(writeHandler.hbwOn, bool.Parse(on.ToLower()));
                        adsClient.WriteAny(hbPut, true);

                        JObject jo = new JObject();
                        jo.Add("on", bool.Parse(on.ToLower()));
                        return Request.CreateResponse(HttpStatusCode.OK, jo);
                    default:
                        return Request.CreateResponse(HttpStatusCode.BadRequest, "On is either \"true\" or \"false\"");
                }
            }
            catch (Exception err)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "PUT Error! " + err.ToString());
            }
        }

    }
}
