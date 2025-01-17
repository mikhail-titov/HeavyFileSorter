class Program
{
	static void Main(string[] args)
	{
		try
		{
			int stringsCount = 0;
			bool countParsed = false;
			bool countProvided = false;
			string defaultOutputPath= string.Empty;
			string? _outputFilePath = null;

			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "--count" || args[i] == "-c" && i + 1 < args.Length)
				{
					countParsed = int.TryParse(args[i + 1], out stringsCount);
					if(!countParsed || stringsCount < 1)
					{
						Console.WriteLine("Invalid value for count parameter. Please try again and input a positive number.");
						return;
					}

					countProvided = true;
				}
				else if (args[i] == "--output" || args[i] == "-o" && i + 1 < args.Length)
				{
					_outputFilePath = args[i + 1];
				}

				i++;
			}

			if (args.Length == 1 && args[0].ToLower() == "help")
			{
				Console.WriteLine("Usage: TestDataGenerator \n[--count] [-c] <count of records>\n[--output] [-o] <outputFile>");
				return;
			}

			if (string.IsNullOrEmpty(_outputFilePath))
			{
				defaultOutputPath = $"generated-{ Path.GetRandomFileName()}.txt";
				Console.WriteLine($"Output file path is not provided. Using default file name: {defaultOutputPath}");
				_outputFilePath = defaultOutputPath;
			}

			if(!countProvided)
			{
				stringsCount =  300;
				Console.WriteLine($"Strings count not provided. Using default value: {stringsCount}");
			}
			

			Console.WriteLine("Test data generator started.");
			Console.WriteLine("Generating data. Please wait...");

			var random = new Random(DateTime.Now.Microsecond);
			using (var writer = new StreamWriter(_outputFilePath))
			{
				for (int i = 0; i < stringsCount; i++)
				{
					long randomLong  = GenerateRandomLong(random);
					int randomInt = random.Next(1, 1024);
					string randomString = GenerateRandomString(random, randomInt);
					writer.WriteLine($"{randomLong}. {randomString}");
				}
			}

			Console.WriteLine("Data generation completed successfully.");
			Console.WriteLine("Press any key to exit.");
			Console.ReadLine();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"An error occurred: {ex.Message}");
		}
	}

	static long GenerateRandomLong(Random random)
	{
		byte[] buffer = new byte[8];
		random.NextBytes(buffer);
		return BitConverter.ToInt64(buffer, 0);
	}

	static string GenerateRandomString(Random random, int length)
	{
		const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ";
		string result = string.Empty;
		
		while(string.IsNullOrEmpty(result.Trim()))
		{
			result = GetRandomChars(random, chars, length);
		}

		return result;
	}

	private static string GetRandomChars(Random random, string chars, int length)
	{
		char[] stringChars = new char[length];
		for (int i = 0; i < stringChars.Length; i++)
		{
			stringChars[i] = chars[random.Next(chars.Length)];
		}

		return new string(stringChars);
	}
}
