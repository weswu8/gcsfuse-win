using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gcsfuse_win
{
    [Serializable]
    public class InvalidCongfigurationException : Exception
    {
        public InvalidCongfigurationException(string message): base(message)
        {
        }
    }
    [Serializable]
    public class BucketAlreadyExists : Exception
    {
        public BucketAlreadyExists(string message) : base(message)
        {
        }
    }
    [Serializable]
    public class BucketNotExists : Exception
    {
        public BucketNotExists(string message) : base(message)
        {
        }
    }
    [Serializable]
    public class BlobAlreadyExists : Exception
    {
        public BlobAlreadyExists(string message) : base(message)
        {
        }
    }

    [Serializable]
    public class BlobNotExists : Exception
    {
        public BlobNotExists(string message) : base(message)
        {
        }
    }

    [Serializable]
    public class IndexOutOfBoundsException : Exception
    {
        public IndexOutOfBoundsException(string message) : base(message)
        {
        }
    }
    [Serializable]
    public class UnsupportedFunctionException : Exception
    {
        public UnsupportedFunctionException(string message) : base(message)
        {
        }
    }
    [Serializable]
    public class BlobBufferedOusException : Exception
    {
        public BlobBufferedOusException(string message) : base(message)
        {
        }
    }
    public class UploadException : Exception
    {
        public UploadException(string message) : base(message)
        {
        }
    }
    public class DownloadException : Exception
    {
        public DownloadException(string message) : base(message)
        {
        }
    }
}
