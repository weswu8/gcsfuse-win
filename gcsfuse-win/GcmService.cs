using Google.Api.Gax;
using Google.Api.Gax.ResourceNames;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gcsfuse_win
{
    class GcmService
    {
        private static volatile GcmService instance;
        private readonly MetricServiceClient metric;
        public static object SyncRoot = new System.Object();
        private static readonly DateTime s_unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        /// <summary>
        /// initialize
        /// </summary>
        private GcmService()
        {
            this.metric = Auth.NewMetricServiceClient();
        }
        /// <summary>
        /// Get MetricServiceClient
        /// </summary>
        /// <returns></returns>
        public MetricServiceClient GetMetic()
        {
            return this.metric;
        }
        /// <summary>
        /// Singleton 
        /// </summary>
        public static GcmService Instance()
        {
            if (instance == null)
            {
                lock (SyncRoot)
                {
                    if (instance == null)
                        instance = new GcmService();
                }
            }
            return instance;
        }

        public double ReadBucketSize(string bucketName)
        {
            // Initialize request argument(s)
            string metricType = "storage.googleapis.com/storage/total_bytes";
            string filter = $"metric.type=\"{metricType}\"";
            filter += $" AND resource.labels.bucket_name =\"{bucketName}\"";
            ListTimeSeriesRequest request = new ListTimeSeriesRequest
            {
                ProjectName = new ProjectName(Config.ProjectId),
                Filter = filter,
                Interval = new TimeInterval(),
                View = ListTimeSeriesRequest.Types.TimeSeriesView.Full,
            };
            // Create timestamp for current time formatted in seconds.
            long timeStamp = (long)(DateTime.UtcNow - s_unixEpoch).TotalSeconds;
            Timestamp startTimeStamp = new Timestamp();
            // Set startTime to limit results to the last 8*60 minutes.
            // Gcs bucket size will be caculated every 24 hours.
            startTimeStamp.Seconds = timeStamp - (1 * 60 * 60);
            Timestamp endTimeStamp = new Timestamp();
            // Set endTime to current time.
            endTimeStamp.Seconds = timeStamp;
            TimeInterval interval = new TimeInterval();
            interval.StartTime = startTimeStamp;
            interval.EndTime = endTimeStamp;
            request.Interval = interval;
            // Make the request.
            PagedEnumerable<ListTimeSeriesResponse, TimeSeries> response =
                this.metric.ListTimeSeries(request);
            // Iterate over all response items, lazily performing RPCs as required.
            if (response.Count() == 0)
            {
                throw new Exception(string.Format("failed to get bucket: {0} size", bucketName));
            }
            TimeSeries item = response.First();
            var points = item.Points;
            double size = points[0].Value.DoubleValue;
            return size;
        }
    }
   

}
