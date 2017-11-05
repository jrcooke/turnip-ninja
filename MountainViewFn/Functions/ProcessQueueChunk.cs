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
    public static class ProcessQueueChunk
    {
        [FunctionName("ProcessQueueChunk")]
        [Singleton(Mode = SingletonMode.Listener)]
        public static void Run([QueueTrigger(Constants.ChunkQueueName, Connection = "ConnectionString2")]string myQueueItem, TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {myQueueItem}");

            var chunkMetadata = JsonConvert.DeserializeObject<ChunkMetadata>(myQueueItem);

            string cs = Environment.GetEnvironmentVariable("ConnectionString", EnvironmentVariableTarget.Process);
            BlobHelper.SetConnectionString(cs);

            var config = Config.Juaneta();
            //var config = Config.JuanetaAll();
            ////config.Lat = homeLat;
            ////config.Lon = homeLon;
            //config.MinAngle = Angle.FromDecimalDegrees(89.0);
            //config.MaxAngle = Angle.FromDecimalDegrees(91.0);

            var imageFile = config.Name + "." + chunkMetadata.ChunkKey + ".png";

            int numParts = (int)((config.ElevationViewMax.Radians - config.ElevationViewMin.Radians) / config.AngularResolution.Radians);

            MyColor[][] resultImage = null;
            View.ColorHeight[][] view = null;
            if (chunkMetadata.ChunkKey == 0)
            {
                resultImage = View.ProcessImageBackdrop(config.NumTheta, numParts);
            }
            else
            {
                view = new View.ColorHeight[config.NumTheta][];
                for (int i = 0; i < view.Length; i++)
                {
                    view[i] = new View.ColorHeight[numParts];
                }

                var chunkView = View.GetPolarData(config, chunkMetadata.ChunkKey, chunkMetadata.HeightOffset);
                foreach (var pixel in chunkView)
                {
                    view[pixel.iTheta][pixel.iViewElev] = pixel.ToColorHeight();
                }

                resultImage = View.ProcessImage(view);
            }

            Utils.WriteImageFile(resultImage, Path.Combine(Path.GetTempPath(), imageFile), a => a, OutputType.PNG);
            string location = BlobHelper.WriteStream("share", imageFile, Path.Combine(Path.GetTempPath(), imageFile));

            string maptxt = "";
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
                TableHelper.Insert(chunkMetadata.SessionId, chunkMetadata.Order, location, maptxt);
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            }

            // return req.CreateResponse(HttpStatusCode.OK, maptxt);
        }
    }
}

