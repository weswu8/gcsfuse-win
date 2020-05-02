using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace gcsfuse_win
{
    public class BlobBufferedOus
    {
        private Google.Apis.Storage.v1.Data.Object blob;
        private GcsReqParmas reqParmas;
        /* the central buffer */
        private byte[] centralBuffer;
        /* the pointer of the central buffer */
        private int centralBufOffset = 0;
        private int centralBufferSize = Constants.BLOB_BUFFERED_OUTS_BUFFER_SIZE;
        private long totalDataWBuffered = 0;
        private long totalDataUploaded = 0;
        private long localFileSize = 0;
        /* the upload chunk size, the should be smaller than the buffer size */
        private int chunkSizeOfBB = Constants.BLOB_BUFFERED_OUTS_CHUNK_SIZE;
        private int chunkNumber = 0;
        /* the path of the blob */
        private String fullBlobPath;
        /* the flag represent the state of local stream */
        private bool isBlobClosed = false;
        private int numOfCommitedBlocks = 0;
        private Uri uploadUri;

        public BlobBufferedOus(GcsReqParmas reqParams){
            this.blob = GcsService.Instance().CreateEmptyBlob(reqParams.Bucket, reqParams.Blob);
            this.reqParmas = reqParams;
            this.fullBlobPath = reqParams.BlobFullPath;
            this.centralBuffer = new byte[centralBufferSize];
            this.localFileSize = (0 != reqParams.LocalFileSize) ? reqParams.LocalFileSize : 0;
            this.uploadUri = GcsService.Instance().InitializeResumableUploader(reqParams.Bucket, reqParams.Blob);
        }

        public Google.Apis.Storage.v1.Data.Object GetBlob()
        {
            return blob;
        }

        public void Write(int b)
        {
            /* simply call the write function */
            byte[]
            oneByte = new byte[1];
            oneByte[0] = (byte) b;
            Write(oneByte, 0, 1);
        }

        public void Write(byte[] data)
        {
            Write(data, 0, data.Length);
        }

        public void Write( byte[] data, int offset, int length)
        {
            /* throw the parameters error */
            if (offset < 0 || length < 0 || length > data.Length - offset) 
            {
                throw new IndexOutOfBoundsException("write error.");
            }
            /* test the upload contions */
            VerifyUploadConditions();

            /* write data to the buffer */
            WriteToBuffer(data, offset, length);
            /* check the buffered data and the chunk size threshold */
            if (IsBufferedDataReadyToUpload())
            {
                int numOfdataUploaded = 0;
                if ((numOfdataUploaded = UploadBlobChunk(centralBuffer, 0, centralBufOffset)) > 0)
                {
                    totalDataUploaded += numOfdataUploaded;
                    /* clean the buffer */
                    byte[] tempBuffer = new byte[centralBufferSize];
                    centralBuffer = tempBuffer;
                    /* reset the chunk count */
                    centralBufOffset = 0;
                }               
            }
        }
        /* write line function */
        public void WriteLine(String line)
        {
            /* simply call the write function */
            byte[]
            lineBytes = new byte[0];
            try 
            {
                line = line.Contains((char)13) ? line : line + Environment.NewLine;
                lineBytes = Encoding.UTF8.GetBytes(line);
                Write(lineBytes, 0, lineBytes.Length);
            } catch (Exception ex) {
                throw new UnsupportedFunctionException(ex.Message);
            }

        }
        /* push the data from buffer to blob */
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Flush()
        {
            try {
                if (centralBufOffset > 0)
                {
                    int numOfdataUploaded = 0;
                    if ((numOfdataUploaded = UploadBlobChunk(centralBuffer, 0, centralBufOffset)) > 0)
                    {
                        totalDataUploaded += numOfdataUploaded;
                        /* reset the chunk count */
                        centralBufOffset = 0;
                    }
                }
            } catch (Exception ex) {
                String errMessage = "Unexpected exception occurred when flush to buffered data to the blob: " + fullBlobPath + ". " + ex.Message;
                throw new BlobBufferedOusException(errMessage);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Close()
        {
            try 
            {
                if (isBlobClosed)
                {
                    return;
                }
                /* flush the data */
                Flush();
                /* clean the buffer */
                centralBuffer = null;
                /* close the write channel */
                //Console.WriteLine("Closed the blob output stream.");
            } catch (Exception ex) {
                String errMessage = "Unexpected exception occurred when closing the blob output stream " + fullBlobPath + ". " + ex.Message;
                throw new BlobBufferedOusException(errMessage);
            }
            finally{
                isBlobClosed = true;
            }

        }
        /* write data to buffer */
        [MethodImpl(MethodImplOptions.Synchronized)]
        private int WriteToBuffer(byte[] rawData, int offset, int length)
        {
            int numOfDataWrited = 0;
            /* the capacity of central buffer is ok */
            if ((centralBuffer.Length - centralBufOffset) > length)
            {
                Array.Copy(rawData, offset, centralBuffer, centralBufOffset, length);
            }
            else
            {
                byte[] tempBuffer = new byte[centralBufOffset + length];
                Array.Copy(centralBuffer, 0, tempBuffer, 0, centralBufOffset);
                Array.Copy(rawData, offset, tempBuffer, centralBufOffset, length);
                centralBuffer = tempBuffer;
            }
            numOfDataWrited = length;
            centralBufOffset += numOfDataWrited;
            totalDataWBuffered += numOfDataWrited;
            rawData = null;
            return numOfDataWrited;
        }
        /* upload a chunk of data from to blob */
        [MethodImpl(MethodImplOptions.Synchronized)]
        private int UploadBlobChunk(byte[] rawData, int offset, int length)
        {
            int dataUploadedThisChunk = 0;
            try 
            {
                byte[] targetData = new byte[length];
                Buffer.BlockCopy(rawData, 0, targetData, 0, length);
                GcsService.Instance().UploadBlobChunk(this.uploadUri, targetData, totalDataUploaded, length);
                /* update the chunk counter */
                chunkNumber++;
                dataUploadedThisChunk = length;
                targetData = null;

            } catch (Exception ex) {
                String errMessage = "Unexpected exception occurred when uploading to the blob : "
                        + this.fullBlobPath + ", No. of chunk: " + chunkNumber + "." + ex.Message;
                throw new BlobBufferedOusException(errMessage);
            }
            //Console.WriteLine(String.Format("Uploading to {0} , blockId: {1}, size: {2}", fullBlobPath, chunkNumber, length));
            return dataUploadedThisChunk;
        }
        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool IsBufferedDataReadyToUpload()
        {
            bool result = false;
            if (centralBufOffset > chunkSizeOfBB) { result = true; }
            return result;
        }
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void VerifyUploadConditions()
        {
            long blobSizeLimit = 0;
            blobSizeLimit = Constants.BLOB_SIZE_LIMIT;
            if (0 == numOfCommitedBlocks){ numOfCommitedBlocks = 0; }
            /* verify the size of local file is under the limit */
            if (localFileSize > blobSizeLimit)
            {
                String errMessage = "The size of the source file exceeds the size limit: " + blobSizeLimit + ".";
                throw new BlobBufferedOusException(errMessage);
            }
            return;
        }
    }
}
