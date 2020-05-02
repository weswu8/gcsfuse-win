/**
 *
 * @copyright wesley wu 
 * @email jie1975.wu@gmail.com
 */
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Fsp;
using VolumeInfo = Fsp.Interop.VolumeInfo;
using FileInfo = Fsp.Interop.FileInfo;
using System.Collections.Generic;
using System.Threading;

namespace gcsfuse_win
{
    class DateUtils
    {
        /// <summary>
        /// The start of the Windows epoch
        /// </summary>
        public static readonly DateTime windowsEpoch = new DateTime(1601, 1, 1, 0, 0, 0, 0);
        /// <summary>
        /// The start of the Java epoch
        /// </summary>
        public static readonly DateTime javaEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        /// <summary>
        /// The difference between the Windows epoch and the Java epoch
        /// in milliseconds.
        /// </summary>
        public static readonly long epochDiff; /* = 1164447360000L; */

        static DateUtils()
        {
            epochDiff = (javaEpoch.ToFileTimeUtc() - windowsEpoch.ToFileTimeUtc())
                            / TimeSpan.TicksPerMillisecond;
        }
        /// <summary>
        /// change the java time to long
        /// </summary>
        /// <param name="javaTime"></param>
        /// <returns></returns>
        public static long FromJavaTimeToLong(long javaTime)
        {

            DateTime dateTime = DateTime.FromFileTime((javaTime + epochDiff) * TimeSpan.TicksPerMillisecond);
            return dateTime.ToFileTimeUtc();
        }
    }
    class Path
    {
        public static String GetDirectoryName(String Path)
        {
            int Index = Path.LastIndexOf('\\');
            if (0 > Index)
                return Path;
            else if (0 == Index)
                return "\\";
            else
                return Path.Substring(0, Index);
        }

        public static String GetFileName(String Path)
        {
            int Index = Path.LastIndexOf('\\');
            if (0 > Index)
                return Path;
            else
                return Path.Substring(Index + 1);
        }

        internal static object GetTempFileName()
        {
            throw new NotImplementedException();
        }
    }

    class FileNode
    {
        // FileName is not the full path ,is the path without the prefix
        public String FileName;
        public FileInfo FileInfo;
        public bool FileInfoFilled = false;
        public Byte[] FileSecurity;
        public string Openedhandle;
        public FileNode RootNode;
        public List<String> allSubFiles;
        public int ReadIndexInDirectory;
        public UInt64 PreOffset;
        public UInt64 PreNumbOfBytesReaded;
        public int AbnormalOffsetCount;
        public FileNode(String FileName)
        {
            this.FileName = FileName;
        }
        /// <summary>
        /// get the real blob node as the new FileNode object, if the blob doesn't exists, will return null
        /// </summary>
        /// <param name="FileName"></param>
        /// <returns></returns>
        public static FileNode getFileNode(String FileName)
        {
            FileNode FNode = new FileNode(FileName);
            String uxFileName = FileName.Replace("\\", "/");
            GcsPath gcsPath = new GcsPath(Config.RootPrefix, uxFileName);
            gcsPath.SetGcsProperties();
            if (gcsPath.pathType.Equals(GcsPathType.INVALID))
            { return null; }
            return FNode;
        }
        /// <summary>
        /// Initialize the root node, root node will represent the root directory
        /// </summary>

        public void InitTheRootNode()
        {
            if ("\\".Equals(FileName))
            {
                RootNode = GcsFuseWin.RootNode;
            }
        }
        /// <summary>
        /// check if the file attribute is read only
        /// </summary>
        /// <returns></returns>
        public bool IsReadOnly()
        {
            bool cRes = ((FileInfo.FileAttributes & (UInt32)FileAttributes.ReadOnly) == (UInt32)FileAttributes.ReadOnly);
            return cRes;
        }
        /// <summary>
        /// get the current file size from the cache
        /// </summary>
        public void GetFileSizeFromCache()
        {
            /*** put into cache  ***/
            /*** we should tracking the size for Subsequent write operation  ***/
            UInt64 FSize = 0;
            if (null != Openedhandle)
            {
                GcsFuseWin.SizeOfBeingWrittenFilesCache.TryGetValue(Openedhandle, out FSize);
            }
            if (0 != FSize)
            {
                FileInfo.FileSize = FSize;
            }
        }
        /// <summary>
        /// get the read offset of the current file from the cache
        /// </summary>
        /// <returns></returns>
        public UInt64 GetFileOffsetFromCache()
        {
            /*** put into cache  ***/
            /*** we should tracking the size for Subsequent read operation  ***/
            UInt64 FOffset = 0;
            if (null != Openedhandle)
            {
                GcsFuseWin.OffsetOfBeingReadFilesCache.TryGetValue(Openedhandle, out FOffset);
            }
            return FOffset;
        }
        /// <summary>
        /// get the file read and write attribute form the cache
        /// </summary>
        /// <returns></returns>
        public void GetFileRWAttrFromCache()
        {
            /*** put into cache  ***/
            /*** we should tracking the size for Subsequent read operation  ***/
            UInt32 RWAttr = 0;
            if ("" != FileName)
            {
                GcsFuseWin.RWAttrOfBeingDeletedFilesCache.TryGetValue(FileName, out RWAttr);
            }
            if (0 != RWAttr)
            {
                FileInfo.FileAttributes = RWAttr;
            }
        }


        public void SetFileAttributesToNormal()
        {
            this.FileInfo.FileAttributes = (UInt32)System.IO.FileAttributes.Normal | (UInt32)System.IO.FileAttributes.Archive;
        }
        /// <summary>
        /// get the file attribute from the underlying blob object
        /// </summary>
        /// <returns></returns>
        public FileInfo BfsGetFileInfo()
        {
            FileInfo FileInfo = default(FileInfo);
            String uxFileName = this.FileName.Replace("\\", "/");
            GcsPath gcsPath = new GcsPath(Config.RootPrefix, uxFileName);
            // cache logic begin
            string key = Utils.GetFormattedKey(gcsPath.Bucket, gcsPath.Blob);
            GcsPath cachedGcsPath;
            GcsFuseWin.cachedFilesInMemManager.TryGet(key, out cachedGcsPath);
            if (null == cachedGcsPath)
            {
                gcsPath.SetGcsProperties();
                GcsFuseWin.cachedFilesInMemManager.AddOrUpdate(key, gcsPath);
                Utils.WriteLine("put blob into cache>>>>>>>:" + key);
            }
            else
            {
                gcsPath = cachedGcsPath;
                Utils.WriteLine("hit blob from cache++++++:" + key);
            }
            // cache logic end
            gcsPath.SetGcsProperties();
            if (gcsPath.pathType.Equals(GcsPathType.INVALID)) { return FileInfo; }
            if (gcsPath.pathType.Equals(GcsPathType.ROOT))
            {
                /* is the root directory */
                //Utils.WriteLine("GetFileInfo-ROOT:" + FileName);
                FileInfo.FileAttributes = (UInt32)FileAttributes.Directory | (UInt32)System.IO.FileAttributes.Archive | (UInt32)System.IO.FileAttributes.NotContentIndexed;
            }
            else if (gcsPath.pathType.Equals(GcsPathType.BUCKET))
            {
                /* is the container */
                //Utils.WriteLine("GetFileInfo-CONTAINER:" + FileName);
                FileInfo.FileAttributes = (UInt32)FileAttributes.Directory | (UInt32)System.IO.FileAttributes.Archive | (UInt32)System.IO.FileAttributes.NotContentIndexed;
                FileInfo.CreationTime = (UInt64)((DateTime)gcsPath.Created).ToFileTimeUtc();
                FileInfo.LastAccessTime =
                FileInfo.LastWriteTime =
                FileInfo.ChangeTime = (UInt64)((DateTime)gcsPath.Updated).ToFileTimeUtc();
            }
            else if (gcsPath.pathType.Equals(GcsPathType.SUBDIR))
            {
                /* is the virtual directory */
                //Utils.WriteLine("GetFileInfo-SUBDIR:" + FileName);
                FileInfo.FileAttributes = (UInt32)FileAttributes.Directory | (UInt32)System.IO.FileAttributes.Archive | (UInt32)System.IO.FileAttributes.NotContentIndexed;
                FileInfo.CreationTime =
                FileInfo.LastAccessTime =
                FileInfo.LastWriteTime =
                FileInfo.ChangeTime = (UInt64)DateTime.Now.ToFileTimeUtc();
            }
            else if (gcsPath.pathType.Equals(GcsPathType.BLOB))
            {
                /* is the virtual directory */
                /* is the blob */
                FileInfo.FileAttributes = (UInt32)FileAttributes.ReadOnly 
                    | (UInt32)System.IO.FileAttributes.Archive
                    | (UInt32)System.IO.FileAttributes.NoScrubData
                    | (UInt32)System.IO.FileAttributes.NotContentIndexed;
                FileInfo.FileSize = (UInt64)gcsPath.Size;
                FileInfo.AllocationSize = (UInt64)gcsPath.Size;
                FileInfo.CreationTime = (UInt64)((DateTime)gcsPath.Created).ToFileTimeUtc();
                FileInfo.LastAccessTime =
                FileInfo.LastWriteTime =
                FileInfo.ChangeTime = (UInt64)((DateTime)gcsPath.Created).ToFileTimeUtc();
            }
            this.FileInfo = FileInfo;
            FileInfoFilled = true;
            return FileInfo;
        }

        public FileInfo GetFileInfo()
        {
            InitTheRootNode();
            if (null != this.RootNode) { return this.RootNode.FileInfo; }
            if (!this.FileInfoFilled)
            {   // should get properties from underlying blob
                this.FileInfo = BfsGetFileInfo();
            }
            GetFileSizeFromCache();
            GetFileRWAttrFromCache();
            return this.FileInfo;
        }
        /// <summary>
        /// get the parent's FileInfo
        /// </summary>
        /// <param name="FileName"></param>
        /// <param name="Result"></param>
        /// <returns></returns>
        public static FileNode GetParent(String FileName, ref Int32 Result)
        {
            FileInfo FInfo;
            FileNode FileNode = new FileNode(Path.GetDirectoryName(FileName));
            FInfo = FileNode.GetFileInfo();
            if (FInfo.Equals(default(FileInfo)))
            {
                Result = FileSystemBase.STATUS_OBJECT_PATH_NOT_FOUND;
                return null;
            }
            if (0 == (FileNode.FileInfo.FileAttributes & (UInt32)FileAttributes.Directory))
            {
                Result = FileSystemBase.STATUS_NOT_A_DIRECTORY;
                return null;
            }
            return FileNode;
        }
        /// <summary>
        /// reset the dir flag , if we don't rest, the directory can not get refreshed
        /// </summary>

        public void ResetDirReader()
        {

            allSubFiles = null;
            ReadIndexInDirectory = 0;

        }

        public override string ToString()
        {
            return "FileName: " + FileName + ", Openedhandle: " + Openedhandle + ", FileSize: " + FileInfo.FileSize;
        }


    }

    class GcsFuseWin : FileSystemBase
    {
        public const UInt16 MEMFS_SECTOR_SIZE = 512;
        public const UInt16 MEMFS_SECTORS_PER_ALLOCATION_UNIT = 1;
        public const uint FILE_OPEN_IF = 0x00000003; //for append blob
        private UInt64 MaxFileNodes;
        private UInt64 MaxFileSize;
        private String VolumeLabel;
        public static FileNode RootNode;
        public static object DirReadLocker = new object();
        // use this the keep the size of files being written
        public static Dictionary<string, UInt64> SizeOfBeingWrittenFilesCache = new Dictionary<string, UInt64> { };
        // normally the block blob is read only, but when we need the delete or over write it
        // we should change it's attribute to normal, use this the keep the normal attributes files being over written
        // due to GetSecurityByName will create a new FileNode, so we need tract this will the file name
        public static Dictionary<string, UInt32> RWAttrOfBeingDeletedFilesCache = new Dictionary<string, UInt32> { };
        public static Dictionary<string, UInt64> OffsetOfBeingReadFilesCache = new Dictionary<string, UInt64> { };
        /* Table of opened files with corresponding InputStreams and OutputStreams */
        private static Cache<OpenedFile> openedFilesManager = new Cache<OpenedFile>(65535, 30 * 60 /* live time in second */) { };
        public static Cache<GcsPath> cachedFilesInMemManager = new Cache<GcsPath>(3000, Config.CacheTTL /* live time in second */) { };
        public static Cache<List<string>> cachedDirsInMemManager = new Cache<List<string>>(100, Config.CacheTTL /* live time in second */) { };
        public static Cache<double> cachedBucketSize = new Cache<double>(10, 60 * 60 /* live time in second */) { };

        public static GcsService GcsService = GcsService.Instance();
        //public static GcmService GcmService = GcmService.Instance();
        public GcsFuseWin(UInt64 MaxFileNodes, UInt64 MaxFileSize, String RootSddl)
        {
            this.MaxFileNodes = MaxFileNodes;
            this.MaxFileSize = MaxFileSize;

            /*
             * Create root directory.
             */

            RootNode = new FileNode("\\");
            RootNode.FileInfo.FileAttributes = (UInt32)FileAttributes.Directory;
            if (null == RootSddl)
                RootSddl = "O:BAG:BAD:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;WD)";
            RawSecurityDescriptor RootSecurityDescriptor = new RawSecurityDescriptor(RootSddl);
            RootNode.FileSecurity = new Byte[RootSecurityDescriptor.BinaryLength];
            RootSecurityDescriptor.GetBinaryForm(RootNode.FileSecurity, 0);
            RootNode.FileInfo.CreationTime =
            RootNode.FileInfo.LastAccessTime =
            RootNode.FileInfo.LastWriteTime =
            RootNode.FileInfo.ChangeTime = (UInt64)DateTime.Now.ToFileTimeUtc();

        }

        public override Int32 Init(Object Host0)
        {
            FileSystemHost Host = (FileSystemHost)Host0;
            Host.SectorSize = GcsFuseWin.MEMFS_SECTOR_SIZE;
            Host.SectorsPerAllocationUnit = GcsFuseWin.MEMFS_SECTORS_PER_ALLOCATION_UNIT;
            Host.VolumeCreationTime = (UInt64)DateTime.Now.ToFileTimeUtc();
            Host.VolumeSerialNumber = (UInt32)(Host.VolumeCreationTime / (10000 * 1000));
            Host.CaseSensitiveSearch = true;
            Host.FileInfoTimeout = 1 * 60 * 60 * 1000;
            Host.CasePreservedNames = true;
            Host.UnicodeOnDisk = true;
            Host.PersistentAcls = true;
            Host.NamedStreams = false;
            Host.PostCleanupWhenModifiedOnly = true;
            Host.PassQueryDirectoryFileName = true;
            return STATUS_SUCCESS;
        }


        public String ChangeWinPathToBlobPath(String WinPath)
        {   
            return WinPath.Replace("\\", "/");
        }

        public String PathConcat(String Parent, String CurrentFile)
        {
            if (Parent.Equals("\\")) { return Parent + CurrentFile; }
            return Parent + "\\" + CurrentFile;
        }

        public override Int32 GetVolumeInfo(
            out VolumeInfo VolumeInfo)
        {
            VolumeInfo = default(VolumeInfo);
            VolumeInfo.TotalSize = MaxFileNodes * (UInt64)MaxFileSize;
            VolumeInfo.FreeSize = (MaxFileNodes - 128) * (UInt64)MaxFileSize;
            VolumeInfo.SetVolumeLabel(VolumeLabel);
            //Utils.WriteLine("===GetVolumeInfo :  TotalSize: " + VolumeInfo.TotalSize + "; FreeSize: " + VolumeInfo.FreeSize);

            return STATUS_SUCCESS;
        }


        public override Int32 SetVolumeLabel(
            String VolumeLabel,
            out VolumeInfo VolumeInfo)
        {
            this.VolumeLabel = VolumeLabel;
            return GetVolumeInfo(out VolumeInfo);
        }
        /// <summary>
        /// get the basic info of the blob
        /// </summary>
        /// <param name="FileName"></param>
        /// <param name="FileAttributes"></param>
        /// <param name="SecurityDescriptor"></param>
        /// <returns></returns>
        public override Int32 GetSecurityByName(
            String FileName,
            out UInt32 FileAttributes/* or ReparsePointIndex */,
            ref Byte[] SecurityDescriptor)
        {
            FileAttributes = default(UInt32);
            try
            {
                // FileName is full win name without the prefix: \\dir\filename
                FileNode FileNode = new FileNode(FileName);
                //Utils.WriteLine("GetSecurityByName: FileName: " + FileName +
                //                    " , TID: " + Thread.CurrentThread.ManagedThreadId);
                // check the RootNode firstly without actually check the underling blob
                FileNode.InitTheRootNode();
                if (null != FileNode.RootNode)
                {
                    FileNode = FileNode.RootNode;
                    FileAttributes = FileNode.FileInfo.FileAttributes;
                    return STATUS_SUCCESS;
                }
                FileNode = FileNode.getFileNode(FileName);
                if (null == FileNode)
                {
                    Int32 Result = STATUS_OBJECT_NAME_NOT_FOUND;
                    if (FindReparsePoint(FileName, out FileAttributes))
                        Result = STATUS_REPARSE;
                    else
                        FileNode.GetParent(FileName, ref Result);
                    return Result;
                }
                FileNode.GetFileInfo();
                FileAttributes = FileNode.FileInfo.FileAttributes;
                Utils.WriteLine("GetSecurityByName: FileNode: " + FileNode.ToString() +
                                "; Attributes: " + FileAttributes.ToString() + 
                                ", TID: " + Thread.CurrentThread.ManagedThreadId);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                return STATUS_UNEXPECTED_IO_ERROR;
            }
            return STATUS_SUCCESS;
        }
        /// <summary>
        /// creat the container, virtual directory or container
        /// </summary>
        /// <param name="FileName"></param>
        /// <param name="CreateOptions"></param>
        /// <returns></returns>
        public Int32 BfsCreate(String FileName, UInt32 CreateOptions)
        {
            try
            {
                String uxFileName = FileName.Replace("\\", "/");
                GcsPath gcsPath = new GcsPath(Config.RootPrefix, uxFileName);
                Utils.WriteLine("Create:" + FileName + "||" + uxFileName);
                // it is a file
                if (0 == (CreateOptions & FILE_DIRECTORY_FILE))
                {
                    //Utils.WriteLine("Create-FILE:" + FileName + "||" + uxFileName + "||" + bfsPath.getContainer() + "||" + bfsPath.getBlob());
                    if (!gcsPath.Bucket.Equals("") && gcsPath.Blob.Equals(""))
                    {
                        // try to create new file in the cotainer level
                        return STATUS_INVALID_PARAMETER;
                    }


                    /* the blob exists*/
                    if (null != GcsService.GetBlob(gcsPath.Bucket, gcsPath.Blob)) 
                    {
                        return STATUS_OBJECT_NAME_COLLISION;
                    }
                    if (null == GcsService.CreateEmptyBlob(gcsPath.Bucket, gcsPath.Blob))
                    {
                        return STATUS_INVALID_PARAMETER;
                    }
                }
                else /* it is a directory */
                {
                    //Utils.WriteLine("Create-Dir:" + FileName + "||" + uxFileName + "||" + bfsPath.getContainer() + "||" + bfsPath.getBlob());

                    if (!gcsPath.Bucket.Equals("") && gcsPath.Blob.Equals(""))
                    {
                        //Utils.WriteLine("Create-Container:" + FileName + "||" + uxFileName);
                        //  does not support creating buckets on GCS due to complexity in cmd mode
                        return STATUS_DIRECTORY_NOT_SUPPORTED;

                    }
                    if (!gcsPath.Bucket.Equals("") && !gcsPath.Blob.Equals(""))
                    {
                        //Utils.WriteLine("Create-VDIR:" + FileName + "||" + uxFileName);
                        // virtual directory
                        if (GcsService.SubDirExists(gcsPath.Bucket, gcsPath.Blob))
                        {
                            return STATUS_OBJECT_NAME_COLLISION;
                        }
                        else
                        {
                            if (null == GcsService.CreateSubDir(gcsPath.Bucket, gcsPath.Blob))
                            {
                                return STATUS_UNEXPECTED_IO_ERROR;
                            }
                        }

                    }
                    // remove the dir cache
                    cachedDirsInMemManager.Remove(gcsPath.Parent);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                return STATUS_UNEXPECTED_IO_ERROR;
            }
            return STATUS_SUCCESS;
        }
        /// <summary>
        /// create the blob
        /// </summary>
        /// <param name="FileName"></param>
        /// <param name="CreateOptions"></param>
        /// <param name="GrantedAccess"></param>
        /// <param name="FileAttributes"></param>
        /// <param name="SecurityDescriptor"></param>
        /// <param name="AllocationSize"></param>
        /// <param name="FileNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="FileInfo"></param>
        /// <param name="NormalizedName"></param>
        /// <returns></returns>
        public override Int32 Create(
            String FileName,
            UInt32 CreateOptions,
            UInt32 GrantedAccess,
            UInt32 FileAttributes,
            Byte[] SecurityDescriptor,
            UInt64 AllocationSize,
            out Object FileNode0,
            out Object FileDesc,
            out FileInfo FileInfo,
            out String NormalizedName)
        {
            FileNode0 = default(Object);
            FileDesc = default(Object);
            FileInfo = default(FileInfo);
            NormalizedName = default(String);

            FileNode FileNode;
            try
            {
                // FileName is full win name without the prefix: \\dir\filename
                // check if the file exists already
                FileNode = FileNode.getFileNode(FileName);
                Utils.WriteLine("Create: FileName: " + FileName +
                                    " , TID: " + Thread.CurrentThread.ManagedThreadId);
                if (null != FileNode)
                {
                    return STATUS_OBJECT_NAME_COLLISION;
                }

                Int32 cResult = BfsCreate(FileName, CreateOptions);
                if (STATUS_SUCCESS != cResult) { return cResult; }
                FileNode = new FileNode(FileName);
                FileInfo = FileNode.GetFileInfo();
                FileNode.FileInfo.FileAttributes = FileAttributes;
                //FileNode.FileSecurity = SecurityDescriptor;
                FileNode.Openedhandle = FileName;
                FileNode0 = FileNode;
                NormalizedName = FileNode.FileName;
                //Utils.WriteLine("Create: FileNode: " + FileNode.ToString() +
                //                   " , TID: " + Thread.CurrentThread.ManagedThreadId);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                return STATUS_UNEXPECTED_IO_ERROR;
            }
            return STATUS_SUCCESS;
        }
        /// <summary>
        /// open the blob
        /// </summary>
        /// <param name="FileName"></param>
        /// <param name="CreateOptions"></param>
        /// <param name="GrantedAccess"></param>
        /// <param name="FileNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="FileInfo"></param>
        /// <param name="NormalizedName"></param>
        /// <returns></returns>

        public override Int32 Open(
            String FileName,
            UInt32 CreateOptions,
            UInt32 GrantedAccess,
            out Object FileNode0,
            out Object FileDesc,
            out FileInfo FileInfo,
            out String NormalizedName)
        {
            FileNode0 = default(Object);
            FileDesc = default(Object);
            FileInfo = default(FileInfo);
            NormalizedName = default(String);

            FileNode FileNode;
            try
            {
                FileNode = new FileNode(FileName);
                FileNode.InitTheRootNode();
                Utils.WriteLine("Open: FileName: " + FileName +
                                   " , TID: " + Thread.CurrentThread.ManagedThreadId);

                if (null != FileNode.RootNode)
                {
                    FileNode0 = FileNode.RootNode;
                    FileInfo = FileNode.RootNode.FileInfo;
                    NormalizedName = FileNode.FileName;
                    return STATUS_SUCCESS;
                }
                FileNode = FileNode.getFileNode(FileName);
                if (null == FileNode)
                {
                    return STATUS_OBJECT_NAME_NOT_FOUND;
                }
                FileInfo = FileNode.GetFileInfo();
                FileNode0 = FileNode;
                NormalizedName = FileNode.FileName;
                /* add handle */
                FileNode.Openedhandle = FileName;

                Utils.WriteLine("Open: FileNode: " + FileNode.ToString() +
                                  " , TID: " + Thread.CurrentThread.ManagedThreadId);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                return STATUS_UNEXPECTED_IO_ERROR;
            }

            return STATUS_SUCCESS;
        }

        /// <summary>
        /// clean up the file handle
        /// </summary>
        /// <param name="FileNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="FileName"></param>
        /// <param name="Flags"></param>
        public override void Cleanup(
            Object FileNode0,
            Object FileDesc,
            String FileName,
            UInt32 Flags)
        {
            FileNode FileNode = (FileNode)FileNode0;
            Utils.WriteLine("Cleanup: FileNode: " + FileNode.ToString() +
                                   " , TID: " + Thread.CurrentThread.ManagedThreadId);
            ClearIOHandleInCache(FileNode);
            FileNode.ResetDirReader();
            if (0 != (Flags & CleanupDelete))
            {
                BfsDelete(FileNode.FileName);
                //clear the read wirte cache
                RemoveItemFromRWAttrOfBeingDeletedFilesCache(FileNode.FileName);
            }

        }

        public void ClearIOHandleInCache(FileNode FileNode)
        {
            if (null == FileNode.Openedhandle) { return; }
            OpenedFile ofm;
            openedFilesManager.TryGet(FileNode.Openedhandle, out ofm);
            if (ofm == null)
            {
                return;
            }
            try
            {
                /* if it is the write operation, we should update the time */
                if (ofm.BOut != null)
                {
                    ofm.BOut.Flush();
                    ofm.BOut.Close();
                    //clear file properties in the cache, so we can see the write result immediately
                    //note: for the txt file, the notepad will not open the file from it's cache
                    string key = Utils.GetFormattedKey(ofm.Bucket, ofm.Blob);
                    cachedFilesInMemManager.Remove(key);
                    // clear the file size in the cache
                    RemoveItemFromSizeOfBeingWrittenFilesCache(FileNode.Openedhandle);


                }
                if (ofm.BIn != null)
                {
                    ofm.BIn.Close();
                    // clear the file size in the cache
                    RemoveItemFromOffsetOfBeingReadFilesCache(FileNode.Openedhandle);

                }
                ofm.close();
                lock (openedFilesManager)
                {
                    /* clean the table */
                    openedFilesManager.Remove(FileNode.Openedhandle.ToString());
                }

            }
            catch (IOException ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
            }
        }

        public override void Close(
            Object FileNode0,
            Object FileDesc)
        {
            FileNode FileNode = (FileNode)FileNode0;
            //Utils.WriteLine("Close: FileNode: " + FileNode.ToString() +
            //                       " , TID: " + Thread.CurrentThread.ManagedThreadId);
            ClearIOHandleInCache(FileNode);
            FileNode.ResetDirReader();
        }
        /// <summary>
        /// create the read stream handle by file name
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public BlobBufferedIns CreateReadStreamHandle(String fileName)
        {
            BlobBufferedIns bbIns = null;
            try
            {
                String uxFileName = ChangeWinPathToBlobPath(fileName);
                GcsPath gcsPath = new GcsPath(Config.RootPrefix, uxFileName);
                gcsPath.SetGcsProperties();
                GcsReqParmas insParams = new GcsReqParmas();
                insParams.Bucket = gcsPath.Bucket;
                insParams.Blob = gcsPath.Blob;
                bbIns = new BlobBufferedIns(insParams);

            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
            }
            return bbIns;

        }
        /// <summary>
        /// get or create the read stream handle by the file name and opend file ID
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="openedFileHandle"></param>
        /// <returns></returns>
        public BlobBufferedIns GetReadStreamHandle(String fileName, string openedFileHandle)
        {
            OpenedFile ofm;
            BlobBufferedIns bbIns = null;
            openedFilesManager.TryGet(openedFileHandle.ToString(), out ofm);
            if (ofm == null)
            {
                bbIns = CreateReadStreamHandle(fileName);
                OpenedFile ofe = new OpenedFile(bbIns, null);
                lock (openedFilesManager)
                {
                    openedFilesManager.AddOrUpdate(openedFileHandle.ToString(), ofe);
                }
                return bbIns;
            }
            bbIns = ofm.BIn;
            if (bbIns == null)
            {
                bbIns = CreateReadStreamHandle(fileName);
                OpenedFile newOfe = ofm;
                newOfe.BIn = bbIns;
                lock (openedFilesManager)
                {
                    openedFilesManager.AddOrUpdate(openedFileHandle.ToString(), newOfe);
                }
            }
            return bbIns;
        }
        /// <summary>
        /// create the write stream handle by file name
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public BlobBufferedOus CreateWriteStreamHandle(String fileName)
        {
            BlobBufferedOus bbOus = null;
            try
            {
                String uxFileName = ChangeWinPathToBlobPath(fileName);
                GcsPath gcsPath = new GcsPath(Config.RootPrefix, uxFileName);
                gcsPath.SetGcsProperties();
                GcsReqParmas ousParams = new GcsReqParmas();
                ousParams.Bucket = gcsPath.Bucket;
                ousParams.Blob = gcsPath.Blob;
                bbOus = new BlobBufferedOus(ousParams);

            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
            }
            return bbOus;

        }
        /// <summary>
        ///  get or create the write stream handle by file name and opened ID
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="openedFileHandle"></param>
        /// <returns></returns>
        public BlobBufferedOus GetWriteStreamHandle(String fileName, string openedFileHandle)
        {
            OpenedFile ofm;
            BlobBufferedOus bbOus = null;
            openedFilesManager.TryGet(openedFileHandle.ToString(), out ofm);
            if (ofm == null)
            {
                bbOus = CreateWriteStreamHandle(fileName);
                OpenedFile ofe = new OpenedFile(null, bbOus);
                String uxFileName = ChangeWinPathToBlobPath(fileName);
                lock (openedFilesManager)
                {
                    openedFilesManager.AddOrUpdate(openedFileHandle.ToString(), ofe);
                }
                return bbOus;
            }
            bbOus = ofm.BOut;
            if (bbOus == null)
            {
                bbOus = CreateWriteStreamHandle(fileName);
                OpenedFile newOfe = ofm;
                newOfe.BOut = bbOus;
                lock (openedFilesManager)
                {
                    openedFilesManager.AddOrUpdate(openedFileHandle.ToString(), newOfe);
                }
            }
            return bbOus;
        }
        /// <summary>
        ///  the read stream of the blob
        /// </summary>
        /// <param name="FileNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="Buffer"></param>
        /// <param name="Offset"></param>
        /// <param name="Length"></param>
        /// <param name="BytesTransferred"></param>
        /// <returns></returns>

        public override Int32 Read(
             Object FileNode0,
             Object FileDesc,
             IntPtr Buffer,
             UInt64 Offset,
             UInt32 Length,
             out UInt32 BytesTransferred)
        {
            BytesTransferred = default(UInt32);
            FileNode FileNode = (FileNode)FileNode0;
            try
            {
                FileNode fileNode = (FileNode)FileNode0;
                Utils.WriteLine("Read: FileNode: " + FileNode.ToString() +
                                   ", Buffer: " + Buffer +
                                   ", Offset: " + Offset +
                                   ", Length: " + Length +
                                   ", TID: " + Thread.CurrentThread.ManagedThreadId);
                // get the Read Offset from cache, overwrite the incoming parameter
                //UInt64 PreOffset = fileNode.GetFileOffsetFromCache();
                if (FileNode.PreNumbOfBytesReaded > Offset - FileNode.PreOffset)
                {
                    FileNode.AbnormalOffsetCount++;
                }
                //if (FileNode.AbnormalOffsetCount > 5){ return STATUS_SUCCESS; }
                BlobBufferedIns bbIns = GetReadStreamHandle(fileNode.FileName, FileNode.Openedhandle);
                if (bbIns == null)
                {
                    Logger.Log(Logger.Level.Error, String.Format("{0} was not open for reading", FileNode.FileName));
                    return STATUS_INVALID_HANDLE;
                }
                /* avoid the out of boundary error */
                int bytesToRead = (int)Math.Min((ulong)bbIns.GetBlobSize() - Offset, Length);
                Byte[] bytesRead = new byte[bytesToRead];
                int bytesReaded = 0;
                lock (bbIns)
                {
                    if ((bytesReaded = bbIns.Read(bytesRead, (int)Offset, bytesToRead)) > 0)
                    {
                        BytesTransferred = (UInt32)bytesReaded;
                        Marshal.Copy(bytesRead, 0, Buffer, bytesRead.Length);
                        // update the read offset
                        FileNode.PreOffset = Offset;
                        FileNode.PreNumbOfBytesReaded = BytesTransferred;
                        AddItemToOffestOfBeingReadFilesCache(fileNode.Openedhandle, Offset + BytesTransferred);
                    }
                    else
                    {
                        /*  reach the end of file */
                        BytesTransferred = 0;
                        return STATUS_END_OF_FILE;
                    }
                }
                bytesRead = null;
                Utils.WriteLine("Read: FileNode: " + FileNode.ToString() +
                                    ", Buffer: " + Buffer +
                                    ", Offset: " + Offset +
                                    ", Length: " + Length +
                                    ", bytesReaded: " + bytesReaded +
                                    ", TID: " + Thread.CurrentThread.ManagedThreadId);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                return STATUS_UNEXPECTED_IO_ERROR;
            }
            return STATUS_SUCCESS;
        }
        /// <summary>
        /// the write steam of blob
        /// </summary>
        /// <param name="FileNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="Buffer"></param>
        /// <param name="Offset"></param>
        /// <param name="Length"></param>
        /// <param name="WriteToEndOfFile"></param>
        /// <param name="ConstrainedIo"></param>
        /// <param name="BytesTransferred"></param>
        /// <param name="FileInfo"></param>
        /// <returns></returns>
        public override Int32 Write(
            Object FileNode0,
            Object FileDesc,
            IntPtr Buffer,
            UInt64 Offset,
            UInt32 Length,
            Boolean WriteToEndOfFile,
            Boolean ConstrainedIo,
            out UInt32 BytesTransferred,
            out FileInfo FileInfo)
        {
            BytesTransferred = default(UInt32);
            FileInfo = default(FileInfo);
            FileNode FileNode = (FileNode)FileNode0;
            try
            {
                FileNode fileNode = (FileNode)FileNode0;
                // the FileNode0 contains the old file size, should refresh it from cache
                FileNode.GetFileInfo();
                Utils.WriteLine("Write: FileNode: " + FileNode.ToString() +
                                   ", Buffer: " + Buffer +
                                   ", Offset: " + Offset +
                                   ", Length: " + Length +
                                   ", TID: " + Thread.CurrentThread.ManagedThreadId);
                BlobBufferedOus bbOus = GetWriteStreamHandle(fileNode.FileName, FileNode.Openedhandle);
                if (bbOus == null)
                {
                    Logger.Log(Logger.Level.Error, String.Format("{0} was not open for writing", FileNode.FileName));
                    return STATUS_INVALID_HANDLE;
                }
                lock (bbOus)
                {
                    byte[] Bytes = new byte[Length];
                    Marshal.Copy(Buffer, Bytes, 0, Bytes.Length);
                    bbOus.Write(Bytes);
                    BytesTransferred = (UInt32)Bytes.Length;
                }
                FileInfo = FileNode.FileInfo;
                // remove the parent dir cache
                String uxFileName = fileNode.FileName.Replace("\\", "/");
                GcsPath gcsPath = new GcsPath(Config.RootPrefix, uxFileName);
                cachedDirsInMemManager.Remove(gcsPath.Parent);
                Utils.WriteLine("Write: FileNode: " + FileNode.ToString() +
                                   ", BytesTransferred: " + BytesTransferred +
                                   ", TID: " + Thread.CurrentThread.ManagedThreadId);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                return STATUS_UNEXPECTED_IO_ERROR;
            }
            return STATUS_SUCCESS;
        }
        /// <summary>
        /// use to flush the blob write steam
        /// </summary>
        /// <param name="FileNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="FileInfo"></param>
        /// <returns></returns>
        public override Int32 Flush(
            Object FileNode0,
            Object FileDesc,
            out FileInfo FileInfo)
        {
            FileInfo = default(FileInfo);
            FileNode FileNode = (FileNode)FileNode0;
            Utils.WriteLine("Flush: FileNode: " + FileNode.ToString() +
                                                   " , TID: " + Thread.CurrentThread.ManagedThreadId);
            BlobBufferedOus bbOus = GetWriteStreamHandle(FileNode.FileName, FileNode.Openedhandle);
            OpenedFile ofm;
            lock (openedFilesManager)
            {
                openedFilesManager.TryGet(FileNode.Openedhandle, out ofm);
            }
            if (ofm == null)
            {
                Logger.Log(Logger.Level.Error, string.Format("Cannot find fd for {0} in table", FileNode.FileName));
                return STATUS_INVALID_HANDLE;
            }
            if (ofm.BOut != null)
            {
                try
                {
                    ofm.BOut.Flush();
                }
                catch (IOException ex)
                {
                    Logger.Log(Logger.Level.Error, ex.Message);
                    return STATUS_UNEXPECTED_IO_ERROR;
                }
            }
            /*  nothing to flush, since we do not cache anything */
            FileInfo = null != FileNode ? FileNode.GetFileInfo() : default(FileInfo);

            return STATUS_SUCCESS;
        }
        /// <summary>
        /// get the basic file attributes from the blob
        /// </summary>
        /// <param name="FileNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="FileInfo"></param>
        /// <returns></returns>

        public override Int32 GetFileInfo(
            Object FileNode0,
            Object FileDesc,
            out FileInfo FileInfo)
        {
            FileNode FileNode = (FileNode)FileNode0;
            Utils.WriteLine("Flush: FileNode: " + FileNode.ToString() +
                              " , TID: " + Thread.CurrentThread.ManagedThreadId);
            FileInfo = FileNode.GetFileInfo();

            return STATUS_SUCCESS;
        }
        /// <summary>
        /// set the basic attribute of the blob
        /// </summary>
        /// <param name="FileNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="FileAttributes"></param>
        /// <param name="CreationTime"></param>
        /// <param name="LastAccessTime"></param>
        /// <param name="LastWriteTime"></param>
        /// <param name="ChangeTime"></param>
        /// <param name="FileInfo"></param>
        /// <returns></returns>
        public override Int32 SetBasicInfo(
            Object FileNode0,
            Object FileDesc,
            UInt32 FileAttributes,
            UInt64 CreationTime,
            UInt64 LastAccessTime,
            UInt64 LastWriteTime,
            UInt64 ChangeTime,
            out FileInfo FileInfo)
        {
            FileNode FileNode = (FileNode)FileNode0;
            //Utils.WriteLine("SetBasicInfo: FileNode: " + FileNode.ToString() +
            //                                      " , TID: " + Thread.CurrentThread.ManagedThreadId);
            FileInfo = default(FileInfo);
            // change blockblob to append blob
            try
            {
                // use this to change the blob from block to append
                if (FileNode.IsReadOnly())
                {
                    // change file attribute to normal subsequence delete and overwrite operation
                    FileNode.SetFileAttributesToNormal();
                    // put it in to cache
                    AddItemToRWAttrOfBeingDeletedFilesCache(FileNode.FileName, (UInt32)System.IO.FileAttributes.Normal);

                }
                // retrieve the read and write attribute from the cache
                FileInfo = FileNode.GetFileInfo();
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                return STATUS_UNEXPECTED_IO_ERROR;
            }
            return STATUS_SUCCESS;
        }
        /// <summary>
        /// set the file size of the blob
        /// </summary>
        /// <param name="FileNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="NewSize"></param>
        /// <param name="SetAllocationSize"></param>
        /// <param name="FileInfo"></param>
        /// <returns></returns>
        public override Int32 SetFileSize(
            Object FileNode0,
            Object FileDesc,
            UInt64 NewSize,
            Boolean SetAllocationSize,
            out FileInfo FileInfo)
        {
            FileNode FileNode = (FileNode)FileNode0;
            //Utils.WriteLine("SetFileSize: FileNode: " + FileNode.ToString() +
            //                   ", NewSize: " + NewSize +  ", TID: " + Thread.CurrentThread.ManagedThreadId);
            FileInfo = FileNode.GetFileInfo();
            FileInfo.FileSize = NewSize;
            /*** put into cache  ***/
            /*** we should tracking the size for Subsequent write operation  ***/
            if (null != FileNode.Openedhandle)
            {
                AddItemToSizeOfBeingWrittenFilesCache(FileNode.Openedhandle, FileInfo.FileSize);
            }
            return STATUS_SUCCESS;
        }
        /// <summary>
        /// add new item to the Size Of Being Written Files Cache
        /// </summary>
        /// <param name="Openedhandle"></param>
        /// <param name="FileSize"></param>
        public static void AddItemToSizeOfBeingWrittenFilesCache(string Openedhandle, UInt64 FileSize)
        {
            UInt64 oValue;
            if (!GcsFuseWin.SizeOfBeingWrittenFilesCache.TryGetValue(Openedhandle, out oValue))
            {
                GcsFuseWin.SizeOfBeingWrittenFilesCache.Add(Openedhandle, FileSize);
            }
            else
            {
                GcsFuseWin.SizeOfBeingWrittenFilesCache[Openedhandle] = FileSize;
            }
        }
        /// <summary>
        /// remove item from the Size Of Being Written Files Cache
        /// </summary>
        /// <param name="Openedhandle"></param>

        public static void RemoveItemFromSizeOfBeingWrittenFilesCache(string Openedhandle)
        {
            if (null != Openedhandle)
            {
                GcsFuseWin.SizeOfBeingWrittenFilesCache.Remove(Openedhandle);
            }
        }
        /// <summary>
        /// add new item to the Offest Of Being Read Files Cache
        /// </summary>
        /// <param name="Openedhandle"></param>
        /// <param name="FileSize"></param>
        public static void AddItemToOffestOfBeingReadFilesCache(string Openedhandle, UInt64 FileSize)
        {
            UInt64 oValue;
            if (!GcsFuseWin.OffsetOfBeingReadFilesCache.TryGetValue(Openedhandle, out oValue))
            {
                // begin of reading
                GcsFuseWin.OffsetOfBeingReadFilesCache.Add(Openedhandle, FileSize);
            }
            else
            {
                GcsFuseWin.OffsetOfBeingReadFilesCache[Openedhandle] = FileSize;
            }
        }
        /// <summary>
        ///  removeitem to the Offest Of Being Read Files Cache
        /// </summary>
        /// <param name="Openedhandle"></param>

        public static void RemoveItemFromOffsetOfBeingReadFilesCache(string Openedhandle)
        {
            if (null != Openedhandle)
            {
                GcsFuseWin.OffsetOfBeingReadFilesCache.Remove(Openedhandle);
            }
        }
        /// <summary>
        /// add new item to the attribute Of Being deleted Files Cache
        /// </summary>
        /// <param name="Openedhandle"></param>
        /// <param name="FileAttribute"></param>
        public static void AddItemToRWAttrOfBeingDeletedFilesCache(string FileName, UInt32 FileAttribute)
        {
            UInt32 oValue;
            if (!GcsFuseWin.RWAttrOfBeingDeletedFilesCache.TryGetValue(FileName, out oValue))
            {
                GcsFuseWin.RWAttrOfBeingDeletedFilesCache.Add(FileName, FileAttribute);
            }
            else
            {
                GcsFuseWin.RWAttrOfBeingDeletedFilesCache[FileName] = FileAttribute;
            }
        }
        /// <summary>
        /// removeitem to the attribute Of Being deleted Files Cache
        /// </summary>
        /// <param name="Openedhandle"></param>
        public static void RemoveItemFromRWAttrOfBeingDeletedFilesCache(string FileName)
        {
            if ("" != FileName)
            {
                GcsFuseWin.RWAttrOfBeingDeletedFilesCache.Remove(FileName);
            }
        }
        // this used to check whether this file can be delete 
        // doesn't to perform the delete action, windows delete file during the cleanup process
        public override Int32 CanDelete(
            Object FileNode0,
            Object FileDesc,
            String FileName)
        {
            FileNode FileNode = (FileNode)FileNode0;
            return STATUS_SUCCESS;
        }

        /// <summary>
        /// delete the underlying blobs
        /// </summary>
        /// <param name="FileName"></param>
        /// <returns></returns>
        public Int32 BfsDelete(String FileName)
        {
            try
            {
                String uxFileName = FileName.Replace("\\", "/");
                GcsPath gcsPath = new GcsPath(Config.RootPrefix, uxFileName);
                //Utils.WriteLine("CanDelete:" + FileName + "||" + uxFileName + "||" + bfsPath.getContainer() + "||" + bfsPath.getBlob());
                gcsPath.SetGcsProperties();
                // clear the cache
                List<string> deletedFileKeys = new List<string>() { };
                if (gcsPath.pathType.Equals(GcsPathType.INVALID))
                {
                    return STATUS_OBJECT_NAME_NOT_FOUND;
                }

                if (gcsPath.pathType.Equals(GcsPathType.BLOB))
                {
                    GcsService.DeletBlob(gcsPath.Bucket, gcsPath.Blob);
                    string deletedFile = Utils.GetFormattedKey(gcsPath.Bucket, gcsPath.Blob);
                    deletedFileKeys.Add(deletedFile);
                }
                else if (gcsPath.pathType.Equals(GcsPathType.BUCKET))
                {
                    deletedFileKeys.AddRange(GcsService.DeleteBucket(gcsPath.Bucket));
                }
                else if (gcsPath.pathType.Equals(GcsPathType.SUBDIR))
                {
                    /* set the directory params */
                    deletedFileKeys.AddRange(GcsService.DeleteDirectory(gcsPath));
                }

                // clear the cache
                deletedFileKeys.ForEach(key => cachedFilesInMemManager.Remove(key));
                cachedDirsInMemManager.Remove(gcsPath.Parent);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                return STATUS_UNEXPECTED_IO_ERROR;
            }
            return STATUS_SUCCESS;
        }

        /// <summary>
        /// rename the blobs
        /// </summary>
        /// <param name="FileName"></param>
        /// <param name="NewFileName"></param>
        /// <returns></returns>
        public Int32 BfsRename(String FileName, String NewFileName)
        {
            try
            {
                String srcFileName = ChangeWinPathToBlobPath(FileName);
                GcsPath srcPath = new GcsPath(Config.RootPrefix, srcFileName);
                srcPath.SetGcsProperties();

                String newFileName = ChangeWinPathToBlobPath(NewFileName);
                GcsPath destPath = new GcsPath(Config.RootPrefix, newFileName);
                Utils.WriteLine("BfsRename:" + FileName + "||" + srcFileName + "||" + newFileName);
                // clear the dir cache
                string key = Utils.GetFormattedKey(srcPath.Bucket, srcPath.Blob);
                cachedDirsInMemManager.Remove(key);
                if (srcPath.pathType.Equals(GcsPathType.INVALID))
                {
                    return STATUS_OBJECT_NAME_NOT_FOUND;
                }
                // clear the cache
                List<string> changedFileKeys = new List<string>() { };
                changedFileKeys = GcsService.MoveOrCopyDirectory(srcPath, destPath, true);
                // clear the cache
                changedFileKeys.ForEach(bKey => cachedFilesInMemManager.Remove(bKey));
                cachedDirsInMemManager.Remove(srcPath.Parent);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
            }
            return STATUS_SUCCESS;
        }
        /// <summary>
        /// rename the blob or virtual directory
        /// </summary>
        /// <param name="FileNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="FileName"></param>
        /// <param name="NewFileName"></param>
        /// <param name="ReplaceIfExists"></param>
        /// <returns></returns>

        public override Int32 Rename(
            Object FileNode0,
            Object FileDesc,
            String FileName,
            String NewFileName,
            Boolean ReplaceIfExists)
        {
            FileNode FileNode = (FileNode)FileNode0;
            Int32 rResult = BfsRename(FileName, NewFileName);
            if (STATUS_SUCCESS != rResult) { return rResult; }
            return STATUS_SUCCESS;
        }
        private Int32 BfsTruncate(
            FileNode FileNode,
            UInt64 NewSize,
            Boolean SetAllocationSize)
        {
            try
            {
                String uxFileName = FileNode.FileName.Replace("\\", "/");
                GcsPath gcsPath = new GcsPath(Config.RootPrefix, uxFileName);
                gcsPath.SetGcsProperties();
                //Utils.WriteLine("bfsTruncate:" + FileNode.FileName + "||" + uxFileName + "||" + trctFielPath.getContainer() + "||" + trctFielPath.getBlob());
                // clear the cache
                string key = Utils.GetFormattedKey(gcsPath.Bucket, gcsPath.Blob);
                GcsService.CreateEmptyBlob(gcsPath.Bucket, gcsPath.Blob);
                // clear the cache
                cachedFilesInMemManager.Remove(key);
                cachedDirsInMemManager.Remove(gcsPath.Parent);
                FileNode.FileInfo.FileSize = NewSize;
                FileNode.FileInfo.AllocationSize = NewSize;

            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                return STATUS_UNEXPECTED_IO_ERROR;

            }

            return STATUS_SUCCESS;
        }
        public override Int32 Overwrite(
            Object FileNode0,
            Object FileDesc,
            UInt32 FileAttributes,
            Boolean ReplaceFileAttributes,
            UInt64 AllocationSize,
            out FileInfo FileInfo)
        {
            FileInfo = default(FileInfo);

            FileNode FileNode = (FileNode)FileNode0;
            Int32 Result;


            Result = BfsTruncate(FileNode, AllocationSize, true);
            if (0 > Result)
                return Result;
            //clear the read wirte cache
            RemoveItemFromRWAttrOfBeingDeletedFilesCache(FileNode.FileName);

            FileNode.FileInfo.FileSize = 0;
            FileNode.FileInfo.LastAccessTime =
            FileNode.FileInfo.LastWriteTime =
            FileNode.FileInfo.ChangeTime = (UInt64)DateTime.Now.ToFileTimeUtc();

            FileInfo = FileNode.GetFileInfo();

            return STATUS_SUCCESS;
        }
        // we can fetech the properties here using the parallel threding pool , and catche it
        // later we can get the properties form local db cache
        /// <summary>
        /// read the directroy
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public List<String> BfsReaddir(String path)
        {
            List<String> allSubFiles = new List<String> { };
            try
            {
                string bPath = ChangeWinPathToBlobPath(path);
                //Utils.WriteLine("read dir:" + path);
                GcsPath gcsPath = new GcsPath(Config.RootPrefix, bPath);
                string key = Utils.GetFormattedKey(gcsPath.Bucket, gcsPath.Blob);
                // blob cache logic begin
                GcsPath cachedGcsPath;
                cachedFilesInMemManager.TryGet(key, out cachedGcsPath);
                if (null == cachedGcsPath)
                {
                    gcsPath.SetGcsProperties();
                    cachedFilesInMemManager.AddOrUpdate(key, gcsPath);
                    Utils.WriteLine("put blob into cache>>>>>>>:" + key);
                }
                else {
                    gcsPath = cachedGcsPath;
                    Utils.WriteLine("hit blob from cache++++++:" + key);
                }
                // dir cache logic begin
                List<String> cachedDir;
                cachedDirsInMemManager.TryGet(key, out cachedDir);
                if (null != cachedDir)
                {
                    allSubFiles = cachedDir;
                    Utils.WriteLine("hit dir from cache++++++:" + key);
                    return allSubFiles;
                }
                // dir cache logic end
                if (gcsPath.pathType.Equals(GcsPathType.ROOT))
                {
                    /* is the root directory */
                    List<Google.Apis.Storage.v1.Data.Bucket> buckets = GcsService.GetBuckets(Config.ProjectId);
                    buckets.ForEach(b => allSubFiles.Add(b.Name));
                    /* put dir into cache */
                    cachedDirsInMemManager.AddOrUpdate(key, allSubFiles);
                    Utils.WriteLine("put dir into cache>>>>>>>:" + key);
                }
                else if (gcsPath.pathType.Equals(GcsPathType.BUCKET) || gcsPath.pathType.Equals(GcsPathType.SUBDIR))
                {
                    /* is the container or virtual directory */
                    List<string> blobs = GcsService.GetFolderAndBlobNamesInSubDir(gcsPath);
                    blobs.ForEach(blob =>
                    {
                        if (!blob.EndsWith(Constants.VIRTUAL_DIRECTORY_NODE_NAME))
                        {   

                            allSubFiles.Add(Utils.RemoveLastSlash(blob));
                        }
                    });
                    /* put dir into cache */
                    cachedDirsInMemManager.AddOrUpdate(key, allSubFiles);
                    Utils.WriteLine("put dir into cache>>>>>>>:" + key);
                }

            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
            }
            return allSubFiles;
        }
        /// <summary>
        /// read the sub files or folder in the directory
        /// </summary>
        /// <param name="FileNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="Pattern"></param>
        /// <param name="Marker"></param>
        /// <param name="Context"></param>
        /// <param name="FileName"></param>
        /// <param name="FileInfo"></param>
        /// <returns></returns>
        public override Boolean ReadDirectoryEntry(
            System.Object FileNode0,
             System.Object FileDesc,
            String Pattern,
            String Marker,
            ref System.Object Context,
            out String FileName,
            out FileInfo FileInfo)
        {
            FileNode ParentNode = (FileNode)FileNode0;
            ParentNode.InitTheRootNode();
            if (null == ParentNode.allSubFiles)
            {
                ParentNode.allSubFiles = BfsReaddir(ParentNode.FileName);
                if (null == ParentNode.RootNode)
                {
                    /* if this is not the root directory add the dot entries */
                    if (null == Marker)
                        ParentNode.allSubFiles.Add(".");
                    if (null == Marker || "." == Marker)
                        ParentNode.allSubFiles.Add("..");
                }
            }

            if (ParentNode.allSubFiles.Count > ParentNode.ReadIndexInDirectory)
            {
                int Index = ParentNode.ReadIndexInDirectory;
                lock (GcsFuseWin.DirReadLocker)
                {
                    ParentNode.ReadIndexInDirectory++;
                }
                String thisFileName = (String)ParentNode.allSubFiles[Index];
                if ("." == thisFileName)
                {
                    FileName = ".";
                    FileInfo = ParentNode.GetFileInfo();
                    return true;
                }
                else if (".." == thisFileName)
                {
                    FileNode ChildNode = new FileNode(Directory.GetParent(ParentNode.FileName).FullName);
                    if (null != ChildNode)
                    {
                        FileName = "..";
                        FileInfo = ChildNode.GetFileInfo();
                        return true;
                    }
                }
                else
                {
                    //BfsPath bfsPath = new BfsPath(FileNameWithoutPrefix);
                    String FileNameWithoutPrefix = PathConcat(ParentNode.FileName, thisFileName);
                    FileNode ChildNode = new FileNode(FileNameWithoutPrefix);
                    // this is the display name in the file explorer
                    FileName = thisFileName;
                    FileInfo = ChildNode.GetFileInfo();
                    return true;
                }
            }
            FileName = default(String);
            FileInfo = default(FileInfo);
            return false;

        }
        /// <summary>
        /// get the directroy info
        /// </summary>
        /// <param name="ParentNode0"></param>
        /// <param name="FileDesc"></param>
        /// <param name="FileName"></param>
        /// <param name="NormalizedName"></param>
        /// <param name="FileInfo"></param>
        /// <returns></returns>

        public override int GetDirInfoByName(
            Object ParentNode0,
            Object FileDesc,
            String FileName,
            out String NormalizedName,
            out FileInfo FileInfo)
        {
            FileNode ParentNode = (FileNode)ParentNode0;
            FileNode FileNode;
            NormalizedName = default(String);
            FileInfo = default(FileInfo);
            try
            {
                FileName = ParentNode.FileName + ("\\" == ParentNode.FileName ? "" : "\\") + Path.GetFileName(FileName);
                FileNode = new FileNode(FileName);
                FileNode.InitTheRootNode();
                if (null != FileNode.RootNode)
                {
                    FileInfo = FileNode.GetFileInfo();
                    NormalizedName = FileNode.FileName;
                    return STATUS_SUCCESS;
                }
                FileNode = FileNode.getFileNode(FileName);
                if (null == FileNode)
                {
                    return STATUS_OBJECT_NAME_NOT_FOUND;
                }
                NormalizedName = Path.GetFileName(FileNode.FileName);
                FileInfo = FileNode.GetFileInfo();
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
            }
            return STATUS_SUCCESS;
        }

    }
    class GcsFuseWinService : Service
    {
        private class CommandLineUsageException : Exception
        {
            public CommandLineUsageException(String Message = null) : base(Message)
            {
                HasMessage = null != Message;
            }

            public bool HasMessage;
        }

        private const String PROGNAME = "GcsFuse-Win";

        public GcsFuseWinService() : base("GcsFuseWinService")
        {
        }
        protected override void OnStart(String[] Args)
        {
            try
            {
                String DebugLogFile = null;
                UInt32 DebugFlags = 0;
                UInt32 FileInfoTimeout = unchecked((UInt32)(-1));
                UInt64 MaxFileNodes = 1024 * 1024 * 1024;
                UInt64 MaxFileSize = 100 * 1024 * 1024;
                String FileSystemName = null;
                String UncPrefix = null;
                String MountDrive = Config.MountDrive;
                String RootSddl = null;
                FileSystemHost Host = null;
                GcsFuseWin bfsWin = null;
                int I;

                for (I = 1; Args.Length > I; I++)
                {
                    String Arg = Args[I];
                    if ('-' != Arg[0])
                        break;
                    switch (Arg[1])
                    {
                        case '?':
                            throw new CommandLineUsageException();
                        case 'D':
                            argtos(Args, ref I, ref DebugLogFile);
                            break;
                        case 'd':
                            argtol(Args, ref I, ref DebugFlags);
                            break;
                        case 'F':
                            argtos(Args, ref I, ref FileSystemName);
                            break;
                        case 'm':
                            argtos(Args, ref I, ref MountDrive);
                            break;
                        case 'S':
                            argtos(Args, ref I, ref RootSddl);
                            break;
                        case 't':
                            argtol(Args, ref I, ref FileInfoTimeout);
                            break;
                        case 'u':
                            argtos(Args, ref I, ref UncPrefix);
                            break;
                        default:
                            throw new CommandLineUsageException();
                    }
                }

                if (Args.Length > I)
                    throw new CommandLineUsageException();

                if ((null == UncPrefix || 0 == UncPrefix.Length) && null == MountDrive)
                    throw new CommandLineUsageException();

                if (null != DebugLogFile)
                    if (0 > FileSystemHost.SetDebugLogFile(DebugLogFile))
                        throw new CommandLineUsageException("cannot open debug log file");

                Host = new FileSystemHost(bfsWin = new GcsFuseWin(MaxFileNodes, MaxFileSize, RootSddl));
                Host.FileInfoTimeout = FileInfoTimeout;
                Host.Prefix = UncPrefix;
                Host.FileSystemName = null != FileSystemName ? FileSystemName : "-GcsFuse-Win";
                if (0 > Host.Mount(MountDrive, null, false, DebugFlags))
                    throw new IOException("cannot mount file system");
                MountDrive = Host.MountPoint();
                _Host = Host;
                Log(EVENTLOG_INFORMATION_TYPE, String.Format("{0} -t {1} -n {2} -s {3}{4}{5}{6}{7}{8}{9}",
                    PROGNAME, (Int32)FileInfoTimeout, MaxFileNodes, MaxFileSize,
                    null != RootSddl ? " -S " : "", null != RootSddl ? RootSddl : "",
                    null != UncPrefix && 0 < UncPrefix.Length ? " -u " : "",
                        null != UncPrefix && 0 < UncPrefix.Length ? UncPrefix : "",
                    null != MountDrive ? " -m " : "", null != MountDrive ? MountDrive : ""));
                // write log file
                Logger.Log(Logger.Level.Info, "The Gcs Fuse Win Service has started.");
            }
            catch (CommandLineUsageException ex)
            {
                Log(EVENTLOG_ERROR_TYPE, String.Format(
                    "{0}" +
                    "usage: {1} OPTIONS\n" +
                    "\n" +
                    "options:\n" +
                    "    -d DebugFlags       [-1: enable all debug logs]\n" +
                    "    -D DebugLogFile     [file path; use - for stderr]\n" +
                    "    -i                  [case insensitive file system]\n" +
                    "    -t FileInfoTimeout  [millis]\n" +
                    "    -F FileSystemName\n" +
                    "    -u \\Server\\Share    [UNC prefix (single backslash)]\n" +
                    "    -m MountPoint       [X:|* (required if no UNC prefix)]\n",
                    ex.HasMessage ? ex.Message + "\n" : "",
                    PROGNAME));
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, ex.Message);
                throw;
            }
        }
        protected override void OnStop()
        {
            _Host.Unmount();
            _Host = null;
            Logger.Log(Logger.Level.Info, "The Gcs Fuse Win Service has stopped.");
        }

        private static void argtos(String[] Args, ref int I, ref String V)
        {
            if (Args.Length > ++I)
                V = Args[I];
            else
                throw new CommandLineUsageException();
        }
        private static void argtol(String[] Args, ref int I, ref UInt32 V)
        {
            Int32 R;
            if (Args.Length > ++I)
                V = Int32.TryParse(Args[I], out R) ? (UInt32)R : V;
            else
                throw new CommandLineUsageException();
        }

        private FileSystemHost _Host;
    }

}
