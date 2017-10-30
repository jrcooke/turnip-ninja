using MountainView.Base;
using MountainViewCore.Base;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Http;

namespace MountainViewApi.Controllers
{
    public class LatLonPoint
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public class ValuesController : ApiController
    {
        // GET api/values
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        public string Get(long id)
        {
            BlobHelper.SetConnectionString(System.Configuration.ConfigurationManager.AppSettings["ConnectionString"]);
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
                maptxt = View.ProcessImageMapBackdrop("/api/Image/" + imageFile);
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

            return maptxt;


        }

        // POST api/values
        public string[] Post([FromBody]LatLonPoint value)
        {
            var config = Config.Juaneta();
            //config.Lat = Angle.FromDecimalDegrees(value.Lat);
            //config.Lon = Angle.FromDecimalDegrees(value.Lon);
            //config.MinAngle = Angle.FromDecimalDegrees(89.0);
            //config.MaxAngle = Angle.FromDecimalDegrees(91.0);

            var chunks = View.GetRelevantChunkKeys(config);
            return chunks.Select(p => p.ToString()).ToArray();
        }
    }
}
