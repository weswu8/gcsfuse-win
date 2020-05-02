using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gcsfuse_win
{
    public class GcsReqParmas
    {
        public string Bucket { set; get; }
        public string Blob { set; get; }
        public string Charset { set; get; }
        public long LocalFileSize { set; get; }
        public string BlobFullPath
        {
            get
            {
                return this.Bucket + Constants.PATH_DELIMITER + this.Blob;
            }
        }
        public string ContentType { set; get; }
        public string DestBucket { set; get; }
        public string DestBlob { set; get; }
    }
}
