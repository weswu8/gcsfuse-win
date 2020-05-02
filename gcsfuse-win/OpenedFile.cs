
namespace gcsfuse_win
{
	public class OpenedFile
	{
		public string Bucket { get; set; }
		public string Blob { get; set; }
		public BlobBufferedIns BIn { get; set; }
		public BlobBufferedOus BOut { get; set; }
	
		public OpenedFile(BlobBufferedIns bIn, BlobBufferedOus bOut)
		{
			this.BIn = bIn;
			this.BOut = bOut;
			if (null != bIn)
			{
				this.Bucket = this.BIn.GetBlob().Bucket;
				this.Blob = this.BIn.GetBlob().Name;
			}
			else {
				this.Bucket = this.BOut.GetBlob().Bucket;
				this.Blob = this.BOut.GetBlob().Name;
			}
		}

		public void close()
		{
			if (this.BIn != null)
			{
				this.BIn.Close();
			}

			if (this.BOut != null)
			{
				this.BOut.Close();
			}

		}
	}
}
