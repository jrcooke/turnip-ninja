using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using MountainView.Base;
using MountainViewCore.Base;
using System.IO;
using System;

namespace MountainViewFn
{
    public static class ProcessChunk
    {
        [FunctionName("ProcessChunk")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            // parse query parameter
            string ids = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "id", true) == 0)
                .Value;

            if (string.IsNullOrEmpty(ids))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Please pass an id on the query string");
            }

            if (!long.TryParse(ids, out long id))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "id must be a long int");
            }

            string cs = Environment.GetEnvironmentVariable("ConnectionString", EnvironmentVariableTarget.Process);
            BlobHelper.SetConnectionString(cs);
            BlobHelper.CacheLocally = true;

            var config = Config.Juaneta();
            //var config = Config.JuanetaAll();
            ////config.Lat = homeLat;
            ////config.Lon = homeLon;
            //config.MinAngle = Angle.FromDecimalDegrees(89.0);
            //config.MaxAngle = Angle.FromDecimalDegrees(91.0);

            var imageFile = config.Name + "." + id + ".png";

            int numParts = (int)((config.ElevationViewMax.Radians - config.ElevationViewMin.Radians) / config.AngularResolution.Radians);

            MyColor[][] www3 = null;
            View.ColorHeight[][] view3 = null;
            string maptxt = "";
            if (id == 0)
            {
                www3 = View.ProcessImageBackdrop(config.NumTheta, numParts);
            }
            else
            {
                view3 = new View.ColorHeight[config.NumTheta][];
                for (int i = 0; i < view3.Length; i++)
                {
                    view3[i] = new View.ColorHeight[numParts];
                }

                var view2 = View.GetPolarData(config, id);
                foreach (var elem in view2)
                {
                    view3[elem.iTheta][elem.iViewElev] = elem.ToColorHeight();
                }

                www3 = View.ProcessImage(view3);
            }

            Utils.WriteImageFile(www3, Path.Combine(Path.GetTempPath(), imageFile), a => a, OutputType.PNG);
            string location = BlobHelper.WriteStream("share", imageFile, Path.Combine(Path.GetTempPath(), imageFile));

            if (id == 0)
            {
                maptxt = View.ProcessImageMapBackdrop(location);
            }
            else
            {
                maptxt = View.ProcessImageMap(view3, location);
            }

            return req.CreateResponse(HttpStatusCode.OK, maptxt);
        }
    }
}
