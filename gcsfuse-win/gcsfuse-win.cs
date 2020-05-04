using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace gcsfuse_win
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // set up the configuration
                CfgReader cfgReader = new CfgReader(Constants.CONFIG_PATH + Constants.CONFIG_FILE);
                Config config = cfgReader.readCfg();
                // start the logger
                Logger.LoggerHandlerManager.AddHandler(new FileLoggerHandler())
                                           .AddHandler(new ConsoleLoggerHandler());
                string url = HttpUtility.UrlEncode("foo??bar");
                // run the service
                Environment.ExitCode = new GcsFuseWinService().Run();
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                throw;
            }
        }
    }
}
