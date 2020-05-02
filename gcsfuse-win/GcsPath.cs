using Google.Apis.Storage.v1.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gcsfuse_win
{
    class GcsPath
    {
        public string Path { set; get; }
        public string FullPath { set; get; }
        public string Bucket { set; get; }
        public string Blob { set; get; }
        public string Parent { set; get; }
        public GcsPathType pathType { set; get; }
        public DateTime? Created { set; get; }
        public DateTime? Updated { set; get; }
        public ulong? Size { set; get; }

        //public List<string> WinSpecialFiles = new List<string>() { "desktop.ini", "autorun.inf" };

        public GcsPath(string rootPrefix, string path) 
        {
            this.Path = path;
            this.FullPath = this._getFullPath(rootPrefix, path);
            this.Bucket = this._getBucket(this.FullPath);
            this.Blob = this._getBlob(this.FullPath, this.Bucket);
            this.Parent = this._getParent();
        }

        public string _getFullPath(string rootPrefix, string path)
        {
            if (rootPrefix.Length >= 1 && !rootPrefix.EndsWith("/"))
            {
                rootPrefix = rootPrefix + "/";
            }
            string fullPath = (rootPrefix + path).Replace(@"//", @"/");
            Utils.WriteLine("RootPrefix:" + rootPrefix);
            Utils.WriteLine("path:" + path);
            Utils.WriteLine("FullPath:" + fullPath);
            return fullPath;
        }

        public string _getBucket(string fullPath) 
        {
            return fullPath.Substring(1).Split('/')[0];
        }

        public string _getBlob(string fullPath, string bucket) 
        {
            string blob = "";
            if (fullPath.Contains("/") && fullPath.IndexOf("/") != fullPath.LastIndexOf("/"))
            {
                int beginIndex = fullPath.Substring(1).IndexOf("/") + 2;
                blob = fullPath.Substring(beginIndex);
            }
            return blob;
        }

        private string _getParent()
        {
            string parent = null;
            try
            {
                /* over write the Paht.getParent */
                if ("/".Equals(this.FullPath))
                {
                    return null;
                }
                string fileName = System.IO.Path.GetFileName(this.FullPath);
                parent = Utils.TrimSuffix(this.FullPath, "/"+fileName);
            }
            catch (Exception)
            {
                this.pathType = GcsPathType.INVALID;
                parent = null;
            }
            Utils.WriteLine("Parent Path:" + parent);
            return parent;
        }

        public void SetGcsProperties()
        {
			GcsService gcsService = GcsService.Instance();
			this.pathType = GcsPathType.INVALID;
			try
			{

                if ("/".Equals(this.FullPath))
                {
                    /*  root directory : /:*/
                    this.pathType = GcsPathType.ROOT;
                    return;
                }
                /* validate the bucket name */
                if (!this.Bucket.Equals("") && Uri.CheckHostName(this.Bucket) == 0) 
                {
                    return;
                }
                if (!this.Bucket.Equals("") && this.Blob.Equals(""))
                {

                    /* bucket: /bucket */
                    Bucket bucket = gcsService.GetBucket(this.Bucket);
                    if (null != bucket)
                    {
                        this.pathType = GcsPathType.BUCKET;
                        this.Created = bucket.TimeCreated;
                        this.Updated = bucket.Updated;
                        this.Size = 0L;
                    }
                }
                else if (!this.Bucket.Equals("") && !this.Blob.Equals(""))
                {
                    Google.Apis.Storage.v1.Data.Object blob = gcsService.GetBlob(this.Bucket, this.Blob);
                    if (null != blob && !blob.Name.EndsWith("/"))
                    {
                        /* blob: /container/folder/file1 */
                        this.pathType = GcsPathType.BLOB;
                        this.Created = blob.TimeCreated;
                        this.Updated = blob.Updated;
                        this.Size = blob.Size;
                    }
                    else if (gcsService.SubDirExists(this.Bucket, this.Blob))
                    {
                        /* virtual directory : /container/folder/ */
                        this.pathType = GcsPathType.SUBDIR;
                    }
                }
                //Utils.WriteLine("pathType:" + this.pathType);
			}
			catch (Exception ex)
			{
                Logger.Log(Logger.Level.Error, ex.Message);
            }


		}

    }
}
