using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;
using TwinCAT.Ads;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Http_Server.Controllers
{
    public class ColorController : ApiController
    {
        private TcAdsClient adsClient;

        //Control Variables
        private int hbPut;
        private int hbGet;
        private int hbExecute;

        //Handlers and buffers
        struct WriteHandler
        {
            public int hnwSat;
            public int hnwBri;
            public int hnwHue;
            public int hnwTrans;
        }
        private WriteHandler writeHandler;

        struct ReadHandler
        {
            public int hnrSat;
            public int hnrBri;
            public int hnrHue;
            public int hnrTrans;
        }
        private ReadHandler readHandler;

        struct ReadNotificationHandler
        {
            public int hnrSat;
            public int hnrBri;
            public int hnrHue;
            public int hnrTrans;
        }
        private ReadNotificationHandler readNotificationHandler;

        struct ReadVar
        {
            public volatile byte nrSat;
            public volatile byte nrBri;
            public volatile ushort nrHue;
            public volatile ushort nrTrans;
        }
        private ReadVar readVar;

        //Event signal
        private volatile bool signal = false;




        public ColorController()
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
                writeHandler.hnwBri = adsClient.CreateVariableHandle("MAIN.nwBri");
                writeHandler.hnwSat = adsClient.CreateVariableHandle("MAIN.nwSat");
                writeHandler.hnwHue = adsClient.CreateVariableHandle("MAIN.nwHue");
                writeHandler.hnwTrans = adsClient.CreateVariableHandle("MAIN.nwTrans");

				//Create variable handlers for readvar
                readHandler.hnrBri = adsClient.CreateVariableHandle("MAIN.nrBri");
                readHandler.hnrSat = adsClient.CreateVariableHandle("MAIN.nrSat");
                readHandler.hnrHue = adsClient.CreateVariableHandle("MAIN.nrHue");
                readHandler.hnrTrans = adsClient.CreateVariableHandle("MAIN.nrTrans");

                //Create notification handlers for readvar
                readNotificationHandler.hnrBri = adsClient.AddDeviceNotificationEx("Main.nrBri", AdsTransMode.OnChange, 200, 0, null, typeof(byte));
                readNotificationHandler.hnrSat = adsClient.AddDeviceNotificationEx("Main.nrSat", AdsTransMode.OnChange, 200, 0, null, typeof(byte));
                readNotificationHandler.hnrHue = adsClient.AddDeviceNotificationEx("Main.nrHue", AdsTransMode.OnChange, 200, 0, null, typeof(ushort));
                readNotificationHandler.hnrTrans = adsClient.AddDeviceNotificationEx("Main.nrTrans", AdsTransMode.OnChange, 200, 0, null, typeof(ushort));
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
            signal = true;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                adsClient.DeleteVariableHandle(writeHandler.hnwBri);
                adsClient.DeleteVariableHandle(writeHandler.hnwSat);
                adsClient.DeleteVariableHandle(writeHandler.hnwHue);
                adsClient.DeleteVariableHandle(writeHandler.hnwTrans);
                adsClient.DeleteVariableHandle(hbPut);
                adsClient.DeleteVariableHandle(hbGet);
                adsClient.DeleteVariableHandle(hbExecute);

                adsClient.DeleteVariableHandle(readHandler.hnrBri);
                adsClient.DeleteVariableHandle(readHandler.hnrSat);
                adsClient.DeleteVariableHandle(readHandler.hnrHue);
                adsClient.DeleteVariableHandle(readHandler.hnrTrans);

                adsClient.DeleteDeviceNotification(readNotificationHandler.hnrBri);
                adsClient.DeleteDeviceNotification(readNotificationHandler.hnrSat);
                adsClient.DeleteDeviceNotification(readNotificationHandler.hnrHue);
                adsClient.DeleteDeviceNotification(readNotificationHandler.hnrTrans);
				//adsClient.AdsNotificationEx -= AdsClient_AdsNotificationEx;
                adsClient.Dispose();
            }
            catch (Exception err)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("ADS client cannot be disposed! Detail: " + err.ToString()) });
            }
            base.Dispose(disposing);
        }



        //GET api/values
        public async Task<HttpResponseMessage> Get()
        {
            try
            {
                signal = false;
                adsClient.WriteAny(hbExecute, true);
                adsClient.WriteAny(hbGet, true);

                bool read = await UpdateReadVar();
                if (!read)
                {
                    return Request.CreateResponse(HttpStatusCode.GatewayTimeout, "ADS server response timeout!");
                }
                else
                {
                    JObject jo = new JObject();
                    jo.Add("hue", readVar.nrHue);
                    jo.Add("sat", readVar.nrSat);
                    jo.Add("bri", readVar.nrBri);
                    jo.Add("transition", readVar.nrTrans);
                    return Request.CreateResponse(HttpStatusCode.OK, jo);
                }
            }
            catch (Exception err)
            {
                //throw new HttpResponseException (Request.CreateResponse(HttpStatusCode.InternalServerError, "Error! " + err.ToString()));
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "Error! " + err.ToString());
            }
        }

		[NonAction]
		public async Task<bool> UpdateReadVar()
        {
            long reftime = DateTime.Now.Ticks;
            long now = reftime;
            while ((!signal) && (now <= (reftime + 30000000)))
            {
                now = DateTime.Now.Ticks;
            }

            adsClient.AdsNotificationEx -= AdsClient_AdsNotificationEx;
            if (now > (reftime + 30000000))
            {
                return false;
            }
            else
            {
                readVar.nrBri = (byte) adsClient.ReadAny(readHandler.hnrBri, typeof(byte));
                readVar.nrHue = (ushort) adsClient.ReadAny(readHandler.hnrHue, typeof(ushort));
                readVar.nrSat = (byte) adsClient.ReadAny(readHandler.hnrSat, typeof(byte));
                readVar.nrTrans = (ushort) adsClient.ReadAny(readHandler.hnrTrans, typeof(ushort));
                return true;
            }
        }

        // PUT api/values/5
        public HttpResponseMessage Put([FromUri]ushort hue = 1, [FromUri]byte sat = 254, [FromUri]byte bri = 140, [FromUri]ushort transition = 1 )
        {
            try
            {
                adsClient.AdsNotificationEx -= AdsClient_AdsNotificationEx;
                adsClient.WriteAny(hbExecute, true);
                adsClient.WriteAny(writeHandler.hnwBri, bri);
                adsClient.WriteAny(writeHandler.hnwSat, sat);
                adsClient.WriteAny(writeHandler.hnwHue, hue);
                adsClient.WriteAny(writeHandler.hnwTrans, transition);
                adsClient.WriteAny(hbPut, true);

                JObject jo = new JObject();
                jo.Add("hue", hue);
                jo.Add("sat", sat);
                jo.Add("bri", bri);
                jo.Add("transition", transition);
                return Request.CreateResponse(HttpStatusCode.OK, jo);
            }
            catch (Exception err)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "Error! " + err.ToString());
            }
        }
    }
}
