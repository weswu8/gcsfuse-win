
using System;
using System.Threading;

namespace gcsfuse_win
{
    public static class ArrayExtensions
    {
        public static void Fill<T>(this T[] originalArray, T with)
        {
            for (int i = 0; i < originalArray.Length; i++)
            {
                originalArray[i] = with;
            }
        }
    }

    public class Utils
    {
        public static string GetFormattedKey(string bucket, string blob)
        {
            if (null == blob || "" == blob.Trim())
            {
                return "/" + bucket;
            }
            return "/" + bucket + "/" + blob;
        }

        public static string RemoveLastSlash(string path)
        {
            string tmpPath = "";
            if (path.EndsWith("/"))
            {
                tmpPath = path.Substring(0, path.Length - 1);
            }
            else
            {
                tmpPath = path;
            }
            return tmpPath;
        }

        public static void WriteLine(string line)
        {
            if (Constants.DEBUG_MODE)
            {
                Console.WriteLine(line);
            }
        }

        public static string TrimPrefix(string str, string prefix)
        {
            if (str.StartsWith(prefix))
            {
                str = str.Remove(0, prefix.Length);
            }
            return str;
        }
        public static string TrimSuffix(string str, string suffix)
        {
            if (str.EndsWith(suffix))
            {
                str = str.Remove(str.Length - suffix.Length, suffix.Length);
            }
            return str;
        }

        public static string GetFileKey(string filename) {
            return filename + "-" + Thread.CurrentThread.ManagedThreadId;
        }
    }
}
