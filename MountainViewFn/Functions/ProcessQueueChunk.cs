using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using MountainView.Base;
using MountainViewCore.Base;
using MountainViewDesktopCore.Base;
using MountainViewFn.Core;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MountainViewFn
{
    public static class ProcessQueueChunk
    {
        [FunctionName("ProcessQueueChunk")]
        [Singleton(Mode = SingletonMode.Listener)]
        public static async Task Run([QueueTrigger(Constants.ChunkQueueName, Connection = "ConnectionString2")]string myQueueItem, TraceWriter log)
        {
            log.Info($"ProcessQueueChunk processed: {myQueueItem}");

            string computerName = Environment.GetEnvironmentVariable("ComputerName", EnvironmentVariableTarget.Process);
            log.Info($"Computer name: {computerName}");

            var chunkMetadata = JsonConvert.DeserializeObject<ChunkMetadata>(myQueueItem);
            var config = chunkMetadata.Config.GetConfig();

            string cs = Environment.GetEnvironmentVariable("ConnectionString", EnvironmentVariableTarget.Process);
            BlobHelper.SetConnectionString(cs);
            var log2 = new MyTraceLister(log);

            var imageFile = chunkMetadata.SessionId + "." + chunkMetadata.ChunkKey + ".png";

            int numParts = (int)((config.ElevationViewMax.Radians - config.ElevationViewMin.Radians) / config.AngularResolution.Radians);

            MyColor[][] resultImage = null;
            View.ColorHeight[][] view = null;
            if (chunkMetadata.ChunkKey == 0)
            {
                resultImage = View.ProcessImageBackdrop(config.NumTheta, numParts);
            }
            else
            {
                var chunkView = await View.GetPolarData(config, chunkMetadata.ChunkKey, chunkMetadata.HeightOffset, log2);
                if (chunkView.Count() == 0)
                {
                    log.Error($"ERROR: No pixels from GetPolarData");
                }

                view = new View.ColorHeight[config.NumTheta][];
                for (int i = 0; i < view.Length; i++)
                {
                    view[i] = new View.ColorHeight[numParts];
                }

                foreach (var pixel in chunkView)
                {
                    view[pixel.iTheta][pixel.iViewElev] = pixel.ToColorHeight();
                }

                resultImage = await View.ProcessImage(view, log2);

                if (resultImage.SelectMany(p => p).Count(p => p.R != 0 || p.G != 0 || p.B != 0) == 0)
                {
                    log.Error($"ERROR: Only black pixels in result image");
                }
            }

            Utils.WriteImageFile(resultImage, Path.Combine(Path.GetTempPath(), imageFile), a => a, OutputType.PNG);
            string location = await BlobHelper.WriteStream("share", imageFile, Path.Combine(Path.GetTempPath(), imageFile), log2);

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
