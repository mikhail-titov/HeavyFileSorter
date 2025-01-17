namespace HeavyFileSorter
{
	internal class FileHelper
	{
		private static readonly string _tempFolderPath = Path.Combine(Directory.GetCurrentDirectory(), $"temp-{Path.GetRandomFileName()}");

		public static string CreateTemporaryFolder()
		{
			Directory.CreateDirectory(_tempFolderPath);

			return _tempFolderPath;
		}

		public static void DeleteTemporaryFolder(string folderPath)
		{
			try
			{
				if (Directory.Exists(folderPath))
				{
					Directory.Delete(_tempFolderPath, true);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Failed to delete temporary folder: {ex.Message}");
			}
		}
	}
}