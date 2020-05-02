using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gcsfuse_win
{
    public class Config
    {
        public static string ProjectId { get; set; }
        public static string ServiceAccountFile { get; set; }
        public static string RootPrefix { get; set; }
        public static string MountDrive { get; set; }
        public static int CacheTTL { get; set; }

        static Config()
        {}
    }
}
