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
    public class AutoManController : ApiController
    {
        private TcAdsClient adsClient;

        //Control Variables
        private int hbPut;
        private int hbGet;
        private int hbExecute;

        //Handlers and buffers
        struct WriteHandler
        {
            public int hswEffect;
        }
        private WriteHandler writeHandler;

        struct ReadHandler
        {
            public int hsrEffect;
        }
        private ReadHandler readHandler;

        struct ReadVar
        {
            public volatile string srEffect;
        }
        private ReadVar readVar;

        //Event signal
        private volatile bool signal = false;




        public AutoManController()
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
                writeHandler.hswEffect = adsClient.CreateVariableHandle("MAIN.swEffect");

                //Create event handlers for readvar
                readHandler.hsrEffect = adsClient.AddDeviceNotificationEx("Main.srEffect", AdsTransMode.OnChange, 200, 0, null, typeof(string), new int[] { 10 });
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
            readVar.srEffect = e.Value.ToString();
            signal = true;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                adsClient.DeleteVariableHandle(writeHandler.hswEffect);
                adsClient.DeleteVariableHandle(hbPut);
                adsClient.DeleteVariableHandle(hbGet);
                adsClient.DeleteVariableHandle(hbExecute);

                adsClient.DeleteDeviceNotification(readHandler.hsrEffect);
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

                if (now > (reftime + 30000000))
                {
                    return Request.CreateResponse(HttpStatusCode.GatewayTimeout, "ADS server response timeout!");
                }
                else
                {
                    JObject jo = new JObject();
                    jo.Add("effect", readVar.srEffect);
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
        public HttpResponseMessage Put([FromUri]string effect = "none")
        {
            try
            {
                switch (effect.ToLower())
                {
                    case "none":
                    case "colorloop":
                        adsClient.WriteAny(hbExecute, true);
                        adsClient.WriteAny(writeHandler.hswEffect, effect.ToLower(), new int[] { 10 });
                        adsClient.WriteAny(hbPut, true);

                        JObject jo = new JObject();
                        jo.Add("effect", effect.ToLower());
                        return Request.CreateResponse(HttpStatusCode.OK, jo);
                    default:
                        return Request.CreateResponse(HttpStatusCode.BadRequest, "Effect is either \"none\" or \"colorloop\"");
                }
            }
            catch (Exception err)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "PUT Error! " + err.ToString());
            }
        }

    }
}
