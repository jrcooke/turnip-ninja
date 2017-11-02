using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using MountainView.Base;
using MountainViewCore.Base;
using MountainViewDesktopCore.Base;
using Newtonsoft.Json;
using System;
using System.IO;

namespace MountainViewFn
{
    public static class Constants
    {
        public const string ChunkQueueName = "chunkqueue";
        public const string FanOutQueue = "fanoutqueue";
    }

    public static class ProcessQueueChunk
    {
        [FunctionName("ProcessQueueChunk")]
        [Singleton(Mode = SingletonMode.Listener)]
        public static void Run([QueueTrigger(Constants.ChunkQueueName, Connection = "ConnectionString2")]string myQueueItem, TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {myQueueItem}");

            var data = JsonConvert.DeserializeObject<QueueRelevantChunkKeys.ChunkMetadata>(myQueueItem);

            string cs = Environment.GetEnvironmentVariable("ConnectionString", EnvironmentVariableTarget.Process);
            BlobHelper.SetConnectionString(cs);

            var config = Config.Juaneta();
            //var config = Config.JuanetaAll();
            ////config.Lat = homeLat;
            ////config.Lon = homeLon;
            //config.MinAngle = Angle.FromDecimalDegrees(89.0);
            //config.MaxAngle = Angle.FromDecimalDegrees(91.0);

            var imageFile = config.Name + "." + data.ChunkKey + ".png";

            int numParts = (int)((config.ElevationViewMax.Radians - config.ElevationViewMin.Radians) / config.AngularResolution.Radians);

            MyColor[][] www3 = null;
            View.ColorHeight[][] view3 = null;
            string maptxt = "";
            if (data.ChunkKey == 0)
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

                var view2 = View.GetPolarData(config, data.ChunkKey);
                foreach (var elem in view2)
                {
                    view3[elem.iTheta][elem.iViewElev] = elem.ToColorHeight();
                }

                www3 = View.ProcessImage(view3);
            }

            Utils.WriteImageFile(www3, Path.Combine(Path.GetTempPath(), imageFile), a => a, OutputType.PNG);
            string location = BlobHelper.WriteStream("share", imageFile, Path.Combine(Path.GetTempPath(), imageFile));

            ////    id = 0;
            //    if (id == 0)
            //    {
            //        maptxt = View.ProcessImageMapBackdrop(location);
            //    }
            //    else
            //    {
            //        maptxt = View.ProcessImageMap(view3, location);
            //    }

            string cs2 = Environment.GetEnvironmentVariable("ConnectionString2", EnvironmentVariableTarget.Process);
            TableHelper.SetConnectionString(cs2);
            try
            {
                TableHelper.Insert(data.SessionId, data.Order, location, maptxt);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }

            // return req.CreateResponse(HttpStatusCode.OK, maptxt);
        }
    }
}

