using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gcsfuse_win
{
    public enum GcsPathType : int
    {
        INVALID = 9,
        ROOT = 1,
        BUCKET = 2,
        SUBDIR = 3,
        BLOB = 4,
        LINK = 5,
    }


}
