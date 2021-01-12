using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json.Linq;

namespace Http_Server.Controllers
{
    public class AddressController : ApiController
    {
        public HttpResponseMessage Get()
        {
            JObject jo = new JObject();
            jo.Add("AMSNetID", GlobalVariables.AMSNetID);
            jo.Add("Port", GlobalVariables.Port);
            return Request.CreateResponse(HttpStatusCode.OK, jo);
        }

        public HttpResponseMessage Put([FromUri]string amsnetid, [FromUri]int port = 851)
        {
            try
            {
                GlobalVariables.AMSNetID = amsnetid;
                GlobalVariables.Port = port;

                JObject jo = new JObject();
                jo.Add("AMSNetID", GlobalVariables.AMSNetID);
                jo.Add("Port", GlobalVariables.Port);
                return Request.CreateResponse(HttpStatusCode.OK, jo);
            }
            catch (Exception err)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, "PUT Error! " + err.ToString());
            }
        }
    }
}
