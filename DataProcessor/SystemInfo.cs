namespace HeavyFileSorter
{
	public static class SystemInfo
	{
		/// <summary>
		/// Returns the optimal number of I/O threads to use for parallel processing.
		/// </summary>
		public static int OptimalIoThreadCount
		{
			get
			{
				return Environment.ProcessorCount;
			}
		}

		/// <summary>
		/// Returns the total available free space in bytes on the specified drive or current drive if it is not specified.
		/// </summary>
		/// <param name="driveName"></param>
		public static long GetAvailableFreeSpace(string driveName = "")
		{
			if (string.IsNullOrEmpty(driveName))
			{
				driveName = Path.GetPathRoot(Environment.CurrentDirectory);
			}

			var di = new DriveInfo(driveName);
			return di.AvailableFreeSpace;
		}

		/// <summary>
		/// Returns the total size of the specified file in bytes.
		/// </summary>
		/// <param name="filePath"></param>
		/// <exception cref="FileNotFoundException">If file not found</exception>
		public static long GetFileSize(string filePath)
		{
			if (File.Exists(filePath))
			{
				filePath = Path.GetFullPath(filePath);
				FileInfo fileInfo = new FileInfo(filePath);
				return fileInfo.Length;
			}
			else
			{
				throw new FileNotFoundException($"File {filePath} does not exist.");
			}
		}
	}
}
