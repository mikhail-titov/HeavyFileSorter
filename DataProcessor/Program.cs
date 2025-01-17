namespace HeavyFileSorter;

internal class Program
{
	static readonly long GB = 1024 * 1024 * 1024;
	internal static string _inputFilePath = string.Empty;
	internal static string _outputFilePath = string.Empty;
	internal static string _filePathToCheck = string.Empty;

	static  string _tempFolderPath = null;
	static Timer? _timer;

	/// <param name="args">
	/// Command-Line Arguments
	///    --input | -i <inputFile>
	///			Description: Specifies the path to the input file that contains the data to be processed.
	///			Example: --input inputFile.txt or -i inputFile.txt
	///    --output | -o <outputFile>
	///			Description: Specifies the path to the output file where the processed data will be saved.
	///			Example: --output outputFile.txt or -o outputFile.txt
	///		--check | -c <fileNameToCheck>
	///			Description: Specifies the path to a file that should be checked to see if it is sorted.
	///			Example: --check fileToCheck.txt or -c fileToCheck.txt
	///		help | --help | -h
	///			Description: Displays usage information for the program.
	///			Example: help
	/// </param>
	private static async Task Main(string[] args)
	{
		try
		{
			Console.WriteLine("Data sorting utility started.");
			var stopwatch = new System.Diagnostics.Stopwatch();
			stopwatch.Start();

			ProcessCommandLineArgs(args);

			_timer = new Timer(CheckMemoryUsage, null, 0, 2000);

			if (!string.IsNullOrEmpty(_filePathToCheck))
			{
				Console.WriteLine($"Checking if file is sorted: {_filePathToCheck}");
				var sorted  = await DataProcessingHelper.CheckFileSorted(_filePathToCheck);

				Console.WriteLine(sorted ? "File is sorted." : "File has unsorted records.");
				
				_timer.Dispose();

				return;
			}

			_tempFolderPath = FileHelper.CreateTemporaryFolder();

			await DataProcessingHelper.ProcessDataInFileAsync(_inputFilePath, _tempFolderPath, _outputFilePath).ConfigureAwait(false);

			stopwatch.Stop();
			Console.WriteLine($"Data processing completed successfully.\nElapsed time: {stopwatch.Elapsed}");
			
			_timer.Dispose();

			FileHelper.DeleteTemporaryFolder(_tempFolderPath);
			Console.WriteLine($"Press Enter key to exit...");
			Console.ReadLine();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"An error occurred: {ex.Message} \n{ex.StackTrace}");
		}
		finally
		{
			_timer?.Dispose();
			FileHelper.DeleteTemporaryFolder(_tempFolderPath);
		}
	}

	internal static void ProcessCommandLineArgs(string[] args)
	{
		string? inputFilePath = null;
		string? outputFilePath = null;

		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] == "--input" || args[i] == "-i" && i + 1 < args.Length)
			{
				inputFilePath = args[i + 1];
				i++;
			}
			else if (args[i] == "--output" || args[i] == "-o" && i + 1 < args.Length)
			{
				outputFilePath = args[i + 1];
				i++;
			}
			else if (args[i] == "--check" || args[i] == "-c" && i + 1 < args.Length)
			{
				_filePathToCheck = args[i + 1];
				return;
			}

			if (args.Length == 1 && args[0].ToLower() == "help" || args[0].ToLower() == "-h" || args[0].ToLower() == "--help")
			{
				Console.WriteLine("Usage: .\\DataProcessor.exe \n[--input] | [-i] <inputFile>\n[--output] | [-o] <outputFile>\n[--check] | [-c] fileNameToCheck.txt");
				return;
			}
		}

		if (string.IsNullOrEmpty(inputFilePath))
		{
			Console.WriteLine("Input file path is not provided. Using default value: input.txt");
			_inputFilePath = inputFilePath ?? "input.txt";
		}

		if (string.IsNullOrEmpty(outputFilePath))
		{
			_outputFilePath = $"result-{Path.GetRandomFileName()}.txt";
			Console.WriteLine($"Output file path is not provided. Using value:{_outputFilePath}");
		}
	}

	static void CheckMemoryUsage(object state)
	{
		long totalMemory = GC.GetTotalMemory(false);

		if (totalMemory > GB)
		{
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
		}
	}
}