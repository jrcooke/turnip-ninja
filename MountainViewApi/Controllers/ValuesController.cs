using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Imaging;
using MountainViewCore.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Http;

namespace MountainViewWeb.Controllers
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
            var imageFile = config.Name + ".jpg";

            var view = View.GetPolarData(config);
            var xxx = View.ProcessImage(view);
            Utils.WriteImageFile(xxx, Path.Combine(Path.GetTempPath(), imageFile), a => a, OutputType.JPEG);

            var maptxt = View.ProcessImageMap(view, "/api/Image/" + imageFile);
            return maptxt;
        }
    }
}
