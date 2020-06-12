using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
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
                // disable console quick edit mode
                ConsoleQuickEditMode.Disable();
                // run the service
                Environment.ExitCode = new GcsFuseWinService().Run();
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                throw;
            }
            /*Console.WriteLine("hello!");
            CfgReader cfgReader = new CfgReader(Constants.CONFIG_PATH + Constants.CONFIG_FILE);
            Config config = cfgReader.readCfg();
            Console.WriteLine(config.ProjectId);
            GcsService gcsService = GcsService.Instance();
            System.Object obj = GcmService.Instance().ReadBucketSize("practical-now-257700.appspot.com");
            byte[] content = GcsService.Instance().DownloadByteRange("practical-now-257700.appspot.com", "gcsfuse-master/gcsfuse-master/.gitignore", 0, 128);
            List<Bucket> buckets = gcsService.GetBuckets(config.ProjectId);
            foreach (var bucket in buckets) 
            {   
                Console.WriteLine(bucket.Name);
            }
            List<Google.Apis.Storage.v1.Data.Object> blobs = GcsService.Instance().GetFoldersAndBlobs("practical-now-257700.appspot.com", null, "/");
            foreach (var blob in blobs) {
                Console.WriteLine(blob.Name);
            }
            GcsService.Instance().MoveBlob("practical-now-257700.appspot.com", "New folder/", "practical-now-257700.appspot.com", "3B folder/");
            List<Google.Apis.Storage.v1.Data.Object> blobs = gcsService.GetBlobs("practical-now-257700.appspot.com");
            foreach (var blob in blobs)
            {
                Console.WriteLine(blob.Name);
            }
            GcsReqParmas inParmas = new GcsReqParmas();
            inParmas.Bucket = "practical-now-257700.appspot.com";
            inParmas.Blob = "trader-2020-03-26.log";
            BlobBufferedIns bbIns = new BlobBufferedIns(config, inParmas);
            GcsReqParmas ouParmas = new GcsReqParmas();
            ouParmas.Bucket = "practical-now-257700.appspot.com";
            ouParmas.Blob = "trader-2020-03-28.log";
            BlobBufferedOus bbOus = new BlobBufferedOus(config, ouParmas);
            String line;
            while ((line = bbIns.ReadLine()) != null)
            {
                Console.WriteLine((line));
                bbOus.WriteLine(line);
                Thread.Sleep(100);
            }
            bbIns.Close();
            bbOus.Close();*/
            /* string bucketName = "practical-now-257700.appspot.com";
             string blobName = "trader-2020-03-33.log";
             StorageService storage = GcsService.Instance().GetStorage().Service;
             var uri = new Uri(
                 $"https://storage.googleapis.com/upload/storage/v1/b/{bucketName}/o?uploadType=resumable");
             var initRequest = new HttpRequestMessage() { RequestUri = uri };
             string body = "{\"name\":\"" + blobName + "\"}";
             initRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
             string contentType = MimeMapping.GetMimeMapping(Path.GetFileName(blobName));
             initRequest.Headers.Add("X-Upload-Content-Type", contentType);
             //initRequest.Headers.Add("X-Upload-Content-Length", "*");
             //initRequest.Content.Headers.ContentLength = body.Length;
             initRequest.Method = new HttpMethod("POST");
             var initResponse = storage.HttpClient.SendAsync(initRequest).Result;
             Uri uploadUri = null;
             if ((int)initResponse.StatusCode == 200)
             {
                 uploadUri = initResponse.Headers.Location;
             }
             else 
             {
                 string errMsg = string.Format("Failed to initialize the upload session :{}" + bucketName + "/" + blobName);
                 throw new UploadException(errMsg); 
             }
             //var content = response.Content.ReadAsByteArrayAsync().Result;
             Console.WriteLine(uploadUri.ToString());
             byte[] temp = new byte[256 * 1024];
             temp.Fill((byte)0);

             var uploadRequest = new HttpRequestMessage() { RequestUri = uploadUri };
             //uploadRequest.Headers.Add("x-goog-resumable", "start");
             //uploadRequest.Headers.TryAddWithoutValidation("Content-Length", temp.Length.ToString());
             uploadRequest.Method = new HttpMethod("PUT");
             uploadRequest.Content = new ByteArrayContent(temp);
             uploadRequest.Content.Headers.ContentLength = temp.Length;
             uploadRequest.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(0, temp.Length -1);
             var uploadResponse = storage.HttpClient.SendAsync(uploadRequest).Result;
             if ((int)uploadResponse.StatusCode == 200)
             { }
             else if ((int)uploadResponse.StatusCode != 308) 
             { 
                 string errMsg = string.Format("Failed to the upload :{0}" , bucketName + "/" + blobName);
                 throw new UploadException(errMsg);
             }
             temp.Fill((byte)1);
             var uploadRequest2 = new HttpRequestMessage() { RequestUri = uploadUri };
             //uploadRequest2.Headers.TryAddWithoutValidation("Content-Length", temp.Length.ToString());
             uploadRequest2.Method = new HttpMethod("PUT");
             uploadRequest2.Content = new ByteArrayContent(temp);
             //uploadRequest2.Content.Headers.ContentLength = temp.Length;
             long bytesSent = temp.Length;
             long cnt = temp.Length;
             uploadRequest2.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(bytesSent, bytesSent + cnt - 1);
             var uploadResponse2 = storage.HttpClient.SendAsync(uploadRequest2).Result;
             if ((int)uploadResponse2.StatusCode == 200)
             { }
             else if ((int)uploadResponse2.StatusCode != 308)
             {
                 string errMsg = string.Format("Failed to the upload :{0}", bucketName + "/" + blobName);
                 throw new UploadException(errMsg);
             }
             byte[] temp2 = new byte[384];
             temp2.Fill((byte)0);
             var uploadRequest3 = new HttpRequestMessage() { RequestUri = uploadUri };
             uploadRequest3.Method = new HttpMethod("PUT");
             uploadRequest3.Content = new ByteArrayContent(temp2);
             long bytesSent2 = temp.Length *2;
             long cnt2 = temp2.Length;
             long total = bytesSent2 + cnt2;
             uploadRequest3.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(bytesSent2, bytesSent2 + cnt2 - 1, total);
             var uploadResponse3 = storage.HttpClient.SendAsync(uploadRequest3).Result;
             if ((int)uploadResponse3.StatusCode == 200)
             { }
             else if ((int)uploadResponse3.StatusCode != 308)
             {
                 string errMsg = string.Format("Failed to the upload :{0}", bucketName + "/" + blobName);
                 throw new UploadException(errMsg);
             }*/
            //Console.ReadLine();
        }
    }
}
