using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;

namespace MountainViewApi.Controllers
{
    public class ImageController : ApiController
    {
        // GET api/values
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        public HttpResponseMessage Get(string id)
        {
            var imageBytes = File.ReadAllBytes(Path.Combine(Path.GetTempPath(), id));
            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(imageBytes)
            };
            result.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return result;
        }

        //public string /* HttpResponseMessage */ Get(string imageName)
        //{
        //    var imageBytes = File.ReadAllBytes(Path.Combine(Path.GetTempPath(), imageName));
        //    HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK)
        //    {
        //        Content = new ByteArrayContent(imageBytes)
        //    };
        //    result.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        //    return result.ToString();
        //}
    }
}
