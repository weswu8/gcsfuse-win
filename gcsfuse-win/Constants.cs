
using System.Collections.Generic;
/**
* @copyright wesley wu 
* @email jie1975.wu@gmail.com
*/
namespace gcsfuse_win
{
    /// <summary>
    /// Global variables
    /// </summary>
    public class Constants
    {
        public const string CONFIG_PATH = "conf\\";
        public const string CONFIG_FILE = "config.xml";
        public const string LOG_PATH = "log\\";
        public const string PATH_DELIMITER = "/";
        public const string VIRTUAL_DIRECTORY_NODE_NAME = ".$$$"; //don't use in gcs
        public const string WIN_DESKTOP_FILE_NAME = "";
        public const bool DEBUG_MODE = false;
        public const int BLOB_BUFFERED_INS_DOWNLOAD_SIZE = 8 * 1024 * 1024; //default is 4MB:  4 * 1024 * 1024 = 4194304
        public const int BLOB_BUFFERED_OUTS_BUFFER_SIZE = 8 * 4 * 256 * 1024; //default is 4MB:  4 * 4 * 256 * 1024 = 4194304
        public const int BLOB_BUFFERED_OUTS_CHUNK_SIZE = 8 * 4 * 256 * 1024; //default is 4MB:  4 * 4 * 256 * 1024= 4194304
        public const long BLOB_SIZE_LIMIT = 5L * 1024L * 1024L * 1024L * 1024L; //default is 5TB
    }
}
