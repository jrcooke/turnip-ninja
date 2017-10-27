using MountainView.Base;
using MountainViewCore.Base;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Web.Http;
using static MountainViewCore.Base.View;

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
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        public string Post([FromBody]LatLonPoint value)
        {
            BlobHelper.SetConnectionString(System.Configuration.ConfigurationManager.AppSettings["ConnectionString"]);
            BlobHelper.CacheLocally = true;

            var homeLat = Angle.FromDecimalDegrees(value.Lat);
            var homeLon = Angle.FromDecimalDegrees(value.Lon);

            var config = Config.JuanetaAll();
            config.Lat = homeLat;
            config.Lon = homeLon;
            config.MinAngle = Angle.FromDecimalDegrees(89.0);
            config.MaxAngle = Angle.FromDecimalDegrees(91.0);

            var imageFile = config.Name + ".jpg";

            var chunks = View.GetRelevantChunkKeys(config);

            int numParts = (int)((config.ElevationViewMax.Radians - config.ElevationViewMin.Radians) / config.AngularResolution.Radians);
            ColorHeight[][] view = new ColorHeight[config.NumTheta][];
            for (int i = 0; i < view.Length; i++)
            {
                view[i] = new ColorHeight[numParts];
            }

            int counter = 0;
            foreach (var chunk in chunks)
            {
                var view2 = View.GetPolarData(config, chunk);
                foreach (var elem in view2)
                {
                    view[elem.iTheta][elem.iViewElev] = elem.ToColorHeight();
                }

                ColorHeight[][] view3 = new ColorHeight[config.NumTheta][];
                for (int i = 0; i < view3.Length; i++)
                {
                    view3[i] = new ColorHeight[numParts];
                }
                foreach (var elem in view2)
                {
                    view3[elem.iTheta][elem.iViewElev] = elem.ToColorHeight();
                }

                var www = View.ProcessImage(view);
                Utils.WriteImageFile(www, Path.Combine(Path.GetTempPath(), imageFile + "." + counter + ".jpg"), a => a, OutputType.JPEG);
                var www3 = View.ProcessImage(view3);
                Utils.WriteImageFile(www3, Path.Combine(Path.GetTempPath(), imageFile + ".X" + counter + ".jpg"), a => a, OutputType.JPEG);

                counter++;
                Debug.WriteLine(counter);
            }

            var xxx = View.ProcessImage(view);
            Utils.WriteImageFile(xxx, Path.Combine(Path.GetTempPath(), imageFile), a => a, OutputType.JPEG);

            var maptxt = View.ProcessImageMap(view, "/api/Image/" + imageFile);
            return maptxt;
        }
    }
}
