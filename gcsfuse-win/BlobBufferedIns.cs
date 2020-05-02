using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace gcsfuse_win
{
    public class BlobBufferedIns
    {
        private Google.Apis.Storage.v1.Data.Object blob;
        /* the central buffer */
        private byte[] centralBuffer = new byte[] { };
        /* the pointer of the central buffer */
        private int centralBufOffset = 0;
        /* the available bytes in central buffer */
        private int numOfBtsAalInCentralBuf = 0;
        private long blobSize = -1;
        private long blobOffset = 0;
        private long numOfBlobBtsLeft = 0;
        private int dwLocalBufferSize = 0;
        private string fullBlobPath;
        /* count used by readline function */
        private long readOffset = 0;

        private bool isBlobEOF = false;

        public BlobBufferedIns(GcsReqParmas reqParams)
            {
            this.blob = GcsService.Instance().GetBlob(reqParams.Bucket, reqParams.Blob);
            this.blobSize = (long)this.blob.Size;
            this.numOfBlobBtsLeft = this.blobSize;
            this.fullBlobPath = reqParams.BlobFullPath;
            this.dwLocalBufferSize = (int) Math.Min(Constants.BLOB_BUFFERED_INS_DOWNLOAD_SIZE* 2, this.blobSize);
        }
        public Google.Apis.Storage.v1.Data.Object GetBlob()
        {
            return this.blob;
        }

        public long GetBlobSize() 
        {
            return this.blobSize;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public int Read(byte[] outputBuffer, int offset, int len)
        {
            int numOfBytesReaded = 0;
            /* for multiple threads, When the subsequent thread runs slower than the previous multiple threads, the read offset
                * of the slower old thread may small than the global read offset. so we need reset the global offset. this may cause
                * download the same data multiple times.
                */
            bool resetFlag = false;
            /* the index of the offset for the read operation */
            int numOfBtsSubBatchDwed= 0;
            /* the skipped bytes for read */
            int numOfBtsSkiped = 0;
            /* If len is zero return 0 per the InputStream contract */
            if (len == 0) {
                return 0;
            }
            /* clean the output buffer, avoid dirty data */
            outputBuffer.Fill((byte)0);

            /* check the offset */
            numOfBtsSkiped = (int) (offset - readOffset);
            //Console.WriteLine(String.Format("numOfBtsSkiped {0}, readOffset {1}" , numOfBtsSkiped, readOffset));
            /* the offset may decrease in some condition, such as for tail command
                * in this condition, we should reset all the global variable
                */
            if (numOfBtsSkiped < 0){
                ResetGlobalVariables(offset);
                resetFlag = true;
                /* reset this variable again */
                numOfBtsSkiped = 0;
            }
            numOfBtsAalInCentralBuf = ((numOfBtsAalInCentralBuf - numOfBtsSkiped) > 0) ? (numOfBtsAalInCentralBuf - numOfBtsSkiped) : 0;

            /* read buffer if buffered data is enough */
            if (numOfBtsAalInCentralBuf >= len){
                byte[] btsReadedTempBuf1 = ReadFromBuffer(numOfBtsSkiped, len);
                numOfBytesReaded = btsReadedTempBuf1.Length;
                Array.Copy(btsReadedTempBuf1, 0, outputBuffer, 0, numOfBytesReaded);
                readOffset += numOfBytesReaded + numOfBtsSkiped;
                return numOfBytesReaded;
            }
            /* if stream is closed */
            if (isBlobEOF & !resetFlag) {
                /* reach the end of the buffer */
                if (numOfBtsAalInCentralBuf == 0)
                {
                    return -1;
                }
                byte[] btsReadedTempBuf2 = ReadFromBuffer(numOfBtsSkiped, numOfBtsAalInCentralBuf);
                numOfBytesReaded = btsReadedTempBuf2.Length;
                Array.Copy(btsReadedTempBuf2, 0, outputBuffer, 0, numOfBytesReaded);
                readOffset += numOfBytesReaded + numOfBtsSkiped;
                return numOfBytesReaded;
            }
            /* numOfBtsSkiped should be used only one times in the loop */
            int numOfBtsSkipedInLoop = numOfBtsSkiped;
            /* download new data in chunks and consume it */
            while (numOfBtsAalInCentralBuf < len){
                if ((this.isBlobEOF && !resetFlag) || blobSize - offset <= 0) { break; }
                /* avoid out of boundary */
                long bytesToDown = (int)Math.Min(dwLocalBufferSize, blobSize - offset);
                int numOfBytesDwed = UpdateCentralBuffer(numOfBtsSkipedInLoop, offset + numOfBtsSubBatchDwed, (int)bytesToDown);
                /* reset to zero, otherwise this will cause the error for sequenced steps */
                numOfBtsSkipedInLoop = 0;
                numOfBtsSubBatchDwed += numOfBytesDwed;
            }
            /* read the data from bytesUnreadedInBuffer and store in outputBuffer */
            len =  Math.Min(numOfBtsAalInCentralBuf, len);
            byte[] btsReadedTempBuf = ReadFromBuffer(0, len);
            numOfBytesReaded = btsReadedTempBuf.Length;
            Array.Copy(btsReadedTempBuf, 0, outputBuffer, 0, numOfBytesReaded);
            readOffset += numOfBytesReaded + numOfBtsSkiped;
            btsReadedTempBuf = null;
            return numOfBytesReaded;
        }

        public int Read()
        {
            byte[]
            oneByte = new byte[1];
            int result = Read(oneByte, (int)readOffset, oneByte.Length);
            if (result <= 0) {
                return -1;
            }
            return oneByte[0];
        }
        /* Read a \r\n terminated line from an InputStream.
            * @return line without the newline or empty String if InputStream is empty
            */
        public String ReadLine()
        {
            StringBuilder builder = new StringBuilder();
            /* readOffset is updated in read(), reach the end of file normally */
            if (readOffset == blobSize){
                return null;
            }
            while (true) {
                int ch = Read();
                if (ch == '\r') {
                    ch = Read();
                    if (ch == '\n') {
                        break;
                    } else {
                        throw new IOException("unexpected char after \\r: " + ch);
                    }
                } else if (ch == -1) {
                    if (builder.Length > 0) {
                        return builder.ToString();
                    }else{
                        return null;
                    }
                }
                builder.Append((char) ch);
            }
            return builder.ToString();
        }
        /* reset all the index to zero, specially reset the  read offset as the new value */
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void ResetGlobalVariables(int readOffset)
        {
            this.blobOffset = 0;
            this.numOfBlobBtsLeft = 0;
            this.centralBuffer = new byte[] { };
            this.centralBufOffset = 0;
            this.numOfBtsAalInCentralBuf = 0;
            this.readOffset = readOffset;
            this.isBlobEOF = false;
        }
        /**
         * update the central buffer
         * @param len
         * @return the number of bytes added to the central buffer
         * @throws GcsException
         */
        [MethodImpl(MethodImplOptions.Synchronized)]
        private int UpdateCentralBuffer(int bufferOffset, int dwOffset, int len)
        {
                int numOfBytesDwed = 0;
                byte[]
                bytesDwedBuf = DownloadBlobChunk(dwOffset, len);
                if (null != bytesDwedBuf && bytesDwedBuf.Length > 0)
                {
                    byte[] bytesTotalDwedBuf = new byte[numOfBtsAalInCentralBuf + bytesDwedBuf.Length];
                    /* the available data in central buffer */
                    if (numOfBtsAalInCentralBuf > 0)
                    {
                        /* update the central buffer offset */
                        centralBufOffset += bufferOffset;
                        /* copy data in buffer first */
                        Array.Copy(centralBuffer, centralBufOffset,
                                bytesTotalDwedBuf, 0, numOfBtsAalInCentralBuf);
                    }
                    /* copy the new downloaded data */
                    Array.Copy(bytesDwedBuf, 0,
                            bytesTotalDwedBuf, numOfBtsAalInCentralBuf, bytesDwedBuf.Length);
                    /* refresh the bytesUnreadedInBuffer pointer */
                    centralBuffer = bytesTotalDwedBuf;
                    /* reset the offset*/
                    centralBufOffset = 0;
                    numOfBtsAalInCentralBuf = centralBuffer.Length - centralBufOffset;
                    numOfBytesDwed = bytesDwedBuf.Length;
                    bytesDwedBuf = null;
                }
                else{
                    this.isBlobEOF = true;
                }
                return numOfBytesDwed;
        }

        /* download a chunk of data from the blob */
        [MethodImpl(MethodImplOptions.Synchronized)]
        private byte[] DownloadBlobChunk(long offset, int len)
        {
            long bytesDownloaded = 0;
            byte[] dwLocalBuffer;
            if (isBlobEOF){ return null; }
            /* how much to read (only last chunk may be smaller) */
            len = Math.Min((int)(blobSize - offset), len) ;
            dwLocalBuffer = GcsService.Instance().DownloadByteRange(this.blob.Bucket, this.blob.Name, offset, offset + len -1);
            bytesDownloaded = dwLocalBuffer.Length;
            numOfBlobBtsLeft = blobSize - offset - bytesDownloaded;
            blobOffset = (offset + bytesDownloaded);
            if (numOfBlobBtsLeft <= 0){ this.isBlobEOF = true; }
            Utils.WriteLine(String.Format("Downloaded from {0} , size of this chunk : {1}, " +
                "total downloaded size: {2}, TID: {3}",
                fullBlobPath, bytesDownloaded, blobOffset, ", TID: " + Thread.CurrentThread.ManagedThreadId));
            return dwLocalBuffer;

        }
        /* read the date from the buffer */
        [MethodImpl(MethodImplOptions.Synchronized)]
        private byte[] ReadFromBuffer(int offset, int numOfbytesToRead)
        {
            byte[] chunkBytesReaded;
            /* update the offset of the buffer */
            centralBufOffset += offset;
            if (numOfBtsAalInCentralBuf <= numOfbytesToRead)
            {
                chunkBytesReaded = new byte[numOfBtsAalInCentralBuf];
            }
            else
            {
                chunkBytesReaded = new byte[numOfbytesToRead];
            }
            Array.Copy(centralBuffer, centralBufOffset, chunkBytesReaded, 0, numOfbytesToRead);
            centralBufOffset += chunkBytesReaded.Length;
            numOfBtsAalInCentralBuf = centralBuffer.Length - centralBufOffset;
            return chunkBytesReaded;
        }
        /* close the channel */
        public void Close () 
        {
        }
    }
}
