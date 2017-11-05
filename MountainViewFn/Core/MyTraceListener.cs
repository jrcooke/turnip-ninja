using Microsoft.Azure.WebJobs.Host;
using System.Diagnostics;

namespace MountainViewFn.Core
{
    public class MyTraceLister : TraceListener
    {
        private TraceWriter log;

        public MyTraceLister(TraceWriter log)
        {
            this.log = log;
        }

        public override void Write(string message)
        {
            WriteLine(message);
        }

        public override void WriteLine(string message)
        {
            log.Info(message);
        }
    }
}
