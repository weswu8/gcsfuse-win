using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;

namespace gcsfuse_win
{


    public class Auth
    {
        private Auth() { }
        public static StorageClient NewStorageClient()
        {
            var credential = GoogleCredential.FromFile(Constants.CONFIG_PATH + Config.ServiceAccountFile);
            var storage = Google.Cloud.Storage.V1.StorageClient.Create(credential);
            return storage;
        }

       /* public static MetricServiceClient NewMetricServiceClient()
        {
            var credential = GoogleCredential.FromFile(Constants.CONFIG_PATH + Config.ServiceAccountFile);
            var builder = new MetricServiceClientBuilder();
            builder.CredentialsPath = Constants.CONFIG_PATH + Config.ServiceAccountFile;
            var metric = builder.Build();
            return metric;
        }*/
    }
}
