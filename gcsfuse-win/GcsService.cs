using Google.Apis.Storage.v1;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web;

namespace gcsfuse_win
{
    class GcsService
    {
        private static volatile GcsService instance;
        private readonly StorageClient storage;
        public static object SyncRoot = new System.Object();
        /// <summary>
        /// initialize
        /// </summary>
        private GcsService()
        {
            this.storage = Auth.NewStorageClient();
        }
        /// <summary>
        /// Get StorageClient
        /// </summary>
        /// <returns></returns>
        public StorageClient GetStorage()
        {
            return this.storage;
        }
        /// <summary>
        /// Singleton 
        /// </summary>
        public static GcsService Instance()
        {
            if (instance == null)
            {
                lock (SyncRoot)
                {
                    if (instance == null)
                        instance = new GcsService();
                }
            }
            return instance;
        }

        public Bucket CreateBucket(string bucketName, string location, string storageClass)
        {
            Bucket bucket = new Bucket { Location = location, StorageClass = storageClass, Name = bucketName };
            bucket = this.storage.CreateBucket(Config.ProjectId, bucket);
            return bucket;
        }

        public Bucket GetBucket(string bucketName)
        {
            Bucket bucket = null;
            try
            {
                bucket = this.storage.GetBucket(bucketName);
            }
            catch (Google.GoogleApiException e)
            when (e.Error.Code == 404)
            {
                return null;
            }
            return bucket;
        }

        public List<Bucket> GetBuckets(string projectId)
        {
            List<Bucket> buckets = new List<Bucket> { };
            foreach (var bucket in this.storage.ListBuckets(projectId))
            {
                buckets.Add(bucket);
            }
            return buckets;
        }

        public List<string> DeleteBucket(string bucketName)
        {
            List<string> deletedFileKeys = new List<string>();
  
            List<Google.Apis.Storage.v1.Data.Object> blobs = GetFoldersAndBlobs(bucketName, null, null);
            foreach (var blob in blobs)
            {
                this.storage.DeleteObject(blob);
                deletedFileKeys.Add(Utils.GetFormattedKey(bucketName, blob.Name));
            }
            this.storage.DeleteBucket(bucketName);
            deletedFileKeys.Add(Utils.GetFormattedKey(bucketName, null));
            return deletedFileKeys;

        }

        public Google.Apis.Storage.v1.Data.Object CreateEmptyBlob(string bucketName, string blobName) 
        {
            var content = Encoding.UTF8.GetBytes("");
            string contentType = MimeMapping.GetMimeMapping(Path.GetFileName(blobName));
            Google.Apis.Storage.v1.Data.Object blob = this.storage.UploadObject(bucketName, blobName, contentType, new MemoryStream(content));
            return blob;
        }

        public Google.Apis.Storage.v1.Data.Object CreateSubDir(string bucketName, string blobName)
        {
            string subDir = blobName.EndsWith("/") ? blobName : blobName + "/";
            Google.Apis.Storage.v1.Data.Object blob = CreateEmptyBlob(bucketName, subDir);
            return blob;
        }

        public Google.Apis.Storage.v1.Data.Object GetBlob(string bucketName, string blobName)
        {
            Google.Apis.Storage.v1.Data.Object blob = null;
            try
            {
                blob = this.storage.GetObject(bucketName, blobName);
            }
            catch (Google.GoogleApiException e)
            when (e.Error.Code == 404)
            {
                return null;
            }
            return blob;
        }

        public bool SubDirExists(string bucketName, string subDir)
        {
            subDir = subDir.EndsWith("/") ? subDir : subDir + "/";
            Google.Apis.Storage.v1.Data.Object blob = this.GetBlob(bucketName, subDir);
            if (null != blob && blob.Size == 0) { return true; }
            List<Google.Apis.Storage.v1.Data.Object> blobs = this.GetBlobs(bucketName, subDir, "/");
            if (blobs.Count > 0) { return true; }
            return false;
        }


        public Google.Apis.Storage.v1.Data.Objects GetBlobsRaw(string bucketName, string prefix, string delimiter)
        {
            /*
            * Gcs folder behavior, by wesly , 20200501
            * folder name look likes aaa/ and the size is zero, and then if we put a ojbect in the folder, the folder name aaa/
            * will be delete by system automatically, so you can get the object instnce of aaa/ any more.
            * if list the bucket with delimiter, we can get all the direct child objects and folders names(as prefixes)
            * and only these folders which contains no items can get the object instance.
            * if list the bucket with delimiter and IncludeTrailingDelimiter, objects that end in exactly one 
            * instance of delimiter will have their metadata included in items in addition to prefixes.
            * but pls pay attention to the duplicated item both in items and prefixes
            */
            StorageService storageService = this.storage.Service;
            ObjectsResource.ListRequest request = storageService.Objects.List(bucketName);
            if (null != delimiter) { request.Delimiter = delimiter; }
            if (null != prefix) { request.Prefix = prefix; }
            //request.IncludeTrailingDelimiter = true;
            Google.Apis.Storage.v1.Data.Objects response = request.Execute();
            return response;
        }

        public List<string> GetFolderAndBlobNames(string bucketName, string prefix) 
        {
            List<string> rawNames = new List<string>() { };
            List<string> folderAndBlobs = new List<string>() { };
            List<Google.Apis.Storage.v1.Data.Object> blobs = null;
            List<string> folders = null;
            Google.Apis.Storage.v1.Data.Objects response = GetBlobsRaw(bucketName, prefix, "/");
            if (response.Prefixes != null)
            {
                folders = response.Prefixes.ToList();
                rawNames.AddRange(folders);
            }
            if (response.Items != null)
            {
                blobs = response.Items.ToList();
                blobs.ForEach(b => {rawNames.Add(b.Name);});
            }
            rawNames.ForEach(b => {
                if (!b.Equals(prefix))
                {
                    string bName = (null == prefix) ? b : Utils.TrimPrefix(b, prefix);
                    folderAndBlobs.Add(bName);
                }
            });
            return folderAndBlobs;
        }


        public List<Google.Apis.Storage.v1.Data.Object> GetFoldersAndBlobs(string bucketName, string prefix, string delimiter)
        {
            List<string> folderNames = new List<string>() { };
            List<Google.Apis.Storage.v1.Data.Object> blobs = new List<Google.Apis.Storage.v1.Data.Object>() { };
            Google.Apis.Storage.v1.Data.Objects response = GetBlobsRaw(bucketName, prefix, delimiter);
            if (response.Prefixes != null)
            {
                folderNames = response.Prefixes.ToList();
                folderNames.ForEach(fn =>
                {
                    Google.Apis.Storage.v1.Data.Object folderObj = GetBlob(bucketName, fn);
                    if (null != folderObj) { blobs.Add(folderObj); }
                }); 
            }
            if (response.Items != null)
            {
                blobs.AddRange(response.Items.ToList());
            }
            return blobs;
        }

        public List<Google.Apis.Storage.v1.Data.Object> GetBlobs(string bucketName, string prefix, string delimiter)
        { 
            List<Google.Apis.Storage.v1.Data.Object> blobs = new List<Google.Apis.Storage.v1.Data.Object>();
            var options = new ListObjectsOptions() { Delimiter = delimiter };
            foreach (var blob in this.storage.ListObjects(bucketName, prefix, options))
            {
                blobs.Add(blob);
            }
            return blobs;
        }

        public void DeletBlob(string bucketName, string blobName)
        {
             this.storage.DeleteObject(bucketName, blobName);
        }

        public void DeleteBlobs(string bucketName, IEnumerable<string> blobNames)
        {
            foreach (string blobName in blobNames)
            {
                this.storage.DeleteObject(bucketName, blobName);
            }
        }

        public void MoveBlob(string srcBucketName, string srcBlobName, string destBucketName,
           string destBlobName)
        {
            try
            {
                this.storage.CopyObject(srcBucketName, srcBlobName, destBucketName,
                        destBlobName);
                this.storage.DeleteObject(srcBucketName, srcBlobName);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
            }
        }

        public void CopyBlob(string srcBucketName, string srcBlobName,
            string destBucketName, string destBlobName)
        {
            try
            {
                this.storage.CopyObject(srcBucketName, srcBlobName,
                        destBucketName, destBlobName);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
            }
        }


        public List<string> GetFolderAndBlobNamesInSubDir(GcsPath gcsPath)
        {
            List<string> blobs = new List<string>();
            if (gcsPath.pathType.Equals(GcsPathType.BUCKET))
            {
                blobs.AddRange(GetFolderAndBlobNames(gcsPath.Bucket, null));
            }
            else {
                string blob= gcsPath.Blob.EndsWith("/") ? gcsPath.Blob : gcsPath.Blob + "/";
                blobs.AddRange(GetFolderAndBlobNames(gcsPath.Bucket, blob));
            }            
            return blobs;
        }

        public List<string> MoveOrCopyDirectory(GcsPath srcPath, GcsPath destPath, bool isMove)
        {
            List<string> changedFileKeys = new List<string>();
            List<Google.Apis.Storage.v1.Data.Object> blobs = new List<Google.Apis.Storage.v1.Data.Object>();
            List<Google.Apis.Storage.v1.Data.Object> folders = new List<Google.Apis.Storage.v1.Data.Object>();
            if (srcPath.pathType.Equals(GcsPathType.BUCKET))
            {
                blobs.AddRange(GetFoldersAndBlobs(srcPath.Bucket, null, null));
            }
            else if (srcPath.pathType.Equals(GcsPathType.SUBDIR))
            {
                string blobName = srcPath.Blob.EndsWith("/") ? srcPath.Blob : srcPath.Blob + "/";
                folders.AddRange(GetFoldersAndBlobs(srcPath.Bucket, blobName, null));
            }
            else if (srcPath.pathType.Equals(GcsPathType.BLOB)) { 
                blobs.Add(GetBlob(srcPath.Bucket, srcPath.Blob));
            }
            else {  }

            if (isMove)
            {
                folders.ForEach(blob => {
                    string blobName = destPath.Blob.EndsWith("/") ? destPath.Blob : destPath.Blob + "/";
                    MoveBlob(blob.Bucket, blob.Name, destPath.Bucket, blobName);
                    changedFileKeys.Add(Utils.GetFormattedKey(blob.Bucket, blob.Name));
                });

                blobs.ForEach(blob => {
                    MoveBlob(blob.Bucket, blob.Name, destPath.Bucket, destPath.Blob);
                    changedFileKeys.Add(Utils.GetFormattedKey(blob.Bucket, blob.Name));
                });

            }
            else 
            {
                folders.ForEach(blob => {
                    string blobName = destPath.Blob.EndsWith("/") ? destPath.Blob : destPath.Blob + "/";
                    CopyBlob(blob.Bucket, blob.Name, destPath.Bucket, blobName);
                    changedFileKeys.Add(Utils.GetFormattedKey(blob.Bucket, blob.Name));
                });

                blobs.ForEach(blob => {
                    CopyBlob(blob.Bucket, blob.Name, destPath.Bucket, destPath.Blob);
                    changedFileKeys.Add(Utils.GetFormattedKey(blob.Bucket, blob.Name));
                });
            }
            return changedFileKeys;


        }

        public List<string> DeleteDirectory(GcsPath gcsPath)
        {
            List<string> deletedFileKeys = new List<string>();
            if (gcsPath.pathType.Equals(GcsPathType.BUCKET))
            {
                deletedFileKeys = DeleteBucket(gcsPath.Bucket);
            }
            else
            {
                List<Google.Apis.Storage.v1.Data.Object> blobs = GetFoldersAndBlobs(gcsPath.Bucket, gcsPath.Blob, null);
                foreach (var blob in blobs)
                {
                    DeletBlob(gcsPath.Bucket, blob.Name);
                    deletedFileKeys.Add(Utils.GetFormattedKey(gcsPath.Bucket, gcsPath.Blob));
                }
            }
            return deletedFileKeys;
        }

        public byte[] DownloadByteRange(string bucketName, string blobName,
            long firstByte, long lastByte)
        {
            byte[] content;
            // Create an HTTP request for the media, for a limited byte range.
            StorageService storage = this.storage.Service;
            var uri = new Uri(
                $"{storage.BaseUri}b/{bucketName}/o/{HttpUtility.UrlEncode(blobName)}?alt=media");
            var request = new HttpRequestMessage() { RequestUri = uri };
            request.Headers.Range =
                new System.Net.Http.Headers.RangeHeaderValue(firstByte,
                lastByte);
            var response = storage.HttpClient.SendAsync(request).Result;
            if ((int)response.StatusCode == 200 | (int)response.StatusCode == 206)
            {
                content = response.Content.ReadAsByteArrayAsync().Result;
                return content;
            }
            else
            {
                string errMsg = string.Format("Failed to download the blob:{}" + bucketName + "/" + blobName);
                throw new DownloadException(errMsg);
            }
           
        }

        public Uri InitializeResumableUploader(string bucketName, string blobName)
        {
            StorageService storageService = this.storage.Service;
            var uri = new Uri(
                $"https://storage.googleapis.com/upload/storage/v1/b/{bucketName}/o?uploadType=resumable");
            var initRequest = new HttpRequestMessage() { RequestUri = uri };
            string body = "{\"name\":\"" + blobName + "\"}";
            initRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
            string contentType = MimeMapping.GetMimeMapping(Path.GetFileName(blobName));
            initRequest.Headers.Add("X-Upload-Content-Type", contentType);
            initRequest.Method = new HttpMethod("POST");
            var initResponse = storageService.HttpClient.SendAsync(initRequest).Result;
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
            return uploadUri;
        }
        /// <summary>
        /// resumable upload
        /// </summary>
        /// <param name="uploadUri"></param>
        /// <param name="rawData"> multiples of 256 KB (256 x 1024 bytes),except for the final chunk </param>
        /// <param name="byteSent"></param>
        /// <param name="length"></param>
        public long UploadBlobChunk(Uri uploadUri, byte[] rawData, long byteSent, long length) 
        {
            bool isEof = false;
            StorageService storageService = this.storage.Service;
            var uploadRequest = new HttpRequestMessage() { RequestUri = uploadUri };
            uploadRequest.Method = new HttpMethod("PUT");
            uploadRequest.Content = new ByteArrayContent(rawData);
            if (length % (256 * 1024) != 0)
            {
                long totalLength = byteSent + length;
                uploadRequest.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(byteSent, byteSent + length - 1, totalLength);
                isEof = true;
            }
            else {
                uploadRequest.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(byteSent, byteSent + length - 1);
            }
            var uploadResponse = storageService.HttpClient.SendAsync(uploadRequest).Result;
            if (isEof)
            {
                if ((int)uploadResponse.StatusCode != 200)
                {
                    string errMsg = string.Format("Failed to the upload :{0}", uploadUri.ToString());
                    throw new UploadException(errMsg);
                }
            }
            else {
                if ((int)uploadResponse.StatusCode != 308)
                {
                    string errMsg = string.Format("Failed to the upload :{0}", uploadUri.ToString());
                    throw new UploadException(errMsg);
                }
            }
            return length;
        }

    }
}
