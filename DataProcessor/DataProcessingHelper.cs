using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;

namespace HeavyFileSorter
{
	internal class DataProcessingHelper
	{
		static readonly long GB = 1024 * 1024 * 1024;
		internal static async Task<(Entity[] entities, long currentSeek, bool isFinished)> ReadLimitedCountEntitiesFromJsonFileAsync(string filePath, long seekStartPos, int limit)
		{
			var limitedEntities = new Entity[limit];
			Entity[] result = new Entity[0];
			bool isFinished = false;

			using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
			{
				int foundCount = 0;
				long currentSeekPos = seekStartPos;
				try
				{
					var bufferSize = limit * (1024+40);
					var buffer = new byte[bufferSize];
					int bytesRead;
					JsonReaderState state = default;
					int bufferOffset = 0;

					if (seekStartPos > 0)
					{
						fs.Seek(seekStartPos + 1, SeekOrigin.Begin);
						bufferOffset = 1;
						buffer[0] = 91; // ASCII value for '[' - to emulate start of JSON array
					}
					
					while ((bytesRead = await fs.ReadAsync(buffer, bufferOffset, buffer.Length - bufferOffset).ConfigureAwait(false)) > 0)
					{
						bufferOffset = 0;//reset buffer offset for next read
						Utf8JsonReader reader = new Utf8JsonReader(buffer.AsSpan(0, bytesRead), isFinalBlock: bytesRead < buffer.Length, state);

						while (reader.Read())
						{
							if (reader.TokenType == JsonTokenType.StartObject)
							{
								var jsonObject = JsonDocument.ParseValue(ref reader).RootElement.GetRawText();
								var entity = JsonSerializer.Deserialize<Entity>(jsonObject);

								limitedEntities[foundCount] = entity;
								foundCount++;

								if (foundCount == limit)
								{
									currentSeekPos += reader.BytesConsumed;
									result = new Entity[foundCount];
									Array.Copy(limitedEntities, result, foundCount);
									return (result, currentSeekPos, isFinished);
								}

								// if the buffer is almost full then break reading to avoid incomplete json objects
								if (bufferSize - reader.BytesConsumed < 2048)
								{
									currentSeekPos += reader.BytesConsumed;
									fs.Seek(currentSeekPos, SeekOrigin.Begin);

									break;
								}

								//if reader consumed all bytes then break reading 
								if (reader.BytesConsumed == bytesRead)
								{
									break;
								}
							}
						}

						state = reader.CurrentState;
					}

					isFinished = true;
					result = new Entity[foundCount];
					Array.Copy(limitedEntities, result, foundCount);
					return (result, currentSeekPos, isFinished);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"File: {filePath}\nseekStart:{seekStartPos}\nfoundCount:{foundCount}\n");
					Console.WriteLine($"{ex.Message}\n");
					throw;
				}
			}
		}
		
		/// <summary>
		/// Reads the source file, sort it using files in temp folder and then saves to the output file.
		/// </summary>
		internal static async Task ProcessDataInFileAsync(string inputFilePath, string tempFolderPath, string outputFilePath)
		{
			long fileSize;
			if (IsEnoughFreeSpace(inputFilePath, out fileSize))
			{
				Console.WriteLine("Not enough free space to process the file. Cleanup the disk and try again.");
				return;
			}

			var chunkSize = CalculateFileChunkSize(fileSize);
			var chunkBorders = await FindPrecisedChunkBordersAsync(inputFilePath, 0, fileSize, chunkSize).ConfigureAwait(false);
			ConcurrentBag<string> chunkFiles = await MakeChunksAsync(chunkBorders, inputFilePath, tempFolderPath);

			await MergeChunksIntoOneFileAsync(chunkFiles.ToList(), outputFilePath).ConfigureAwait(false);
		}

		/// <summary>
		/// Checks if the file is sorted.
		/// </summary>
		/// <param name="filePathToCheck"></param>
		/// <returns></returns>
		internal static async Task<bool> CheckFileSorted(string filePathToCheck)
		{
			long counter = 0;
			Entity? previousEntity = null;
			using (FileStream fs = new FileStream(filePathToCheck, FileMode.Open, FileAccess.Read))
			using (StreamReader reader = new StreamReader(fs))
			{
				string line;
				while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
				{
					var entity = ParseEntityFromString(line);
					if (previousEntity != null && entity.CompareTo(previousEntity) < 0)
					{
						return false;
					}
					previousEntity = entity;
					counter++;
					if (counter % 1000000 == 0)
					{
						Console.WriteLine($"Checked {counter} records.");
					}
				}
			}

			return true;
		}

		/// <summary>
		/// Reads specified chunk files page by page and  merges it into one file.
		/// </summary>
		/// <param name="chunkFiles">List of file paths of chunk files</param>
		private static async Task MergeChunksIntoOneFileAsync(List<string> chunkFiles, string outputFilePath)
		{
			var semaphore = new SemaphoreSlim(SystemInfo.OptimalIoThreadCount);
			var tasks = new List<Task>();
			var chunkReaders = new ConcurrentBag<SortedChunkReader>();

			for (int i = 0; i < chunkFiles.Count; i++)
			{
				var index = i;
				var chunkReader = new SortedChunkReader(chunkFiles[index]);
				chunkReaders.Add(chunkReader);

				var task = Task.Run(async () =>
				{
					try
					{
						await semaphore.WaitAsync().ConfigureAwait(false);
						await chunkReader.ReadChunkAsync().ConfigureAwait(false);
					}
					finally
					{
						semaphore.Release();
					}
				});

				tasks.Add(task);
			}

			await Task.WhenAll(tasks);

			Entity[] mergedEntities = new Entity[10000000];
			int mergedIndex = -1;
			var readerList = chunkReaders.ToList();
			long totalCount = 0;

			while (readerList.Count > 0)
			{
				var chunk = readerList.MinBy(x => x.Entity);

				mergedIndex++;
				mergedEntities[mergedIndex] = await chunk.TakeEntity();

				totalCount++;

				if (chunk.IsCompleted)
				{
					readerList.Remove(chunk);
				}

				if (mergedIndex == mergedEntities.Length - 1)
				{
					await AppendEntitesToFileAsync(mergedEntities, outputFilePath, append: true);
					Array.Clear(mergedEntities, 0, mergedEntities.Length);
					mergedIndex = -1;
				}
			}


			if (mergedIndex != -1)
			{
				await AppendEntitesToFileAsync(mergedEntities.Take(mergedIndex + 1), outputFilePath, append: true);
			}

			Console.WriteLine($"Total records in merged file: {totalCount}");
		}

		private static async Task<List<Entity>> ReadEntitiesChunkFromFileAsync(FileStream fileStream, long startPosition, long endPosition)
		{
			var entities = new List<Entity>();
			const int bufferSize = 2048; 
			char[] buffer = new char[bufferSize];
			StringBuilder stringBuilder = new StringBuilder();
			long currentPosition = startPosition;

			using (StreamReader reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: bufferSize, leaveOpen: true))
			{
				reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);
				reader.DiscardBufferedData();

				while (reader.BaseStream.Position <= endPosition)
				{
					int charsToRead = (int)Math.Min(bufferSize, endPosition - reader.BaseStream.Position);
					int charsRead = await reader.ReadAsync(buffer, 0, charsToRead).ConfigureAwait(false);

					if (charsRead == 0)
					{
						break;
					}

					stringBuilder.Append(buffer, 0, charsRead);

					string[] lines = stringBuilder.ToString().Split('\n');
					for (int i = 0; i < lines.Length - 1; i++)
					{
						if (!string.IsNullOrWhiteSpace(lines[i]))
						{
							entities.Add(ParseEntityFromString(lines[i]));
						}
					}

					// Keep the last line in the buffer (it might be incomplete)
					stringBuilder.Clear();
					stringBuilder.Append(lines[^1]);
				}

				// Process any remaining data in the buffer
				if (stringBuilder.Length > 0 && !string.IsNullOrWhiteSpace(stringBuilder.ToString()))
				{
					entities.Add(ParseEntityFromString(stringBuilder.ToString()));
				}
			}

			return entities;
		}

		internal static List<Entity> DeserializeEntitiesFromBinaryFile(string filePath)
		{
			using (FileStream fs = new FileStream(filePath, FileMode.Open))
			{
				DataContractSerializer serializer = new DataContractSerializer(typeof(List<Entity>));
				return (List<Entity>)serializer.ReadObject(fs);
			}
		}

		private static async Task SerializeEntitiesToJsonFileAsync(List<Entity> entities, string filePath)
		{
			using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
			{
				await JsonSerializer.SerializeAsync(fs, entities);
			}

			Console.WriteLine($"Serialized {entities.Count} entities to file: {Path.GetFileName(filePath)}");
		}

		private static async Task AppendEntitesToFileAsync(IEnumerable<Entity> entities, string fileName, bool append = false)
		{
			using (StreamWriter writer = new StreamWriter(fileName, append: true))
			{
				foreach (var entity in entities)
				{
					await writer.WriteLineAsync(entity.ToString()).ConfigureAwait(false);
				}
			}

			Console.WriteLine($"Appended {entities.Count()} entities to file: {Path.GetFileName(fileName)}");
		}

		private static  Entity ParseEntityFromString(string str)
		{
			int dotIndex = str.IndexOf('.');

			if (dotIndex == -1)
			{
				throw new ArgumentException("Invalid format. Input string does not contain required dot.");
			}

			if (!long.TryParse(str.Substring(0, dotIndex), out var number))
			{
				throw new ArgumentException($"Invalid format. String does not contain a valid number.  Source string: '{str}' .");
			}

			string text = str.Substring(dotIndex + 1).Trim();

			if(text.Length == 0)
			{
				throw new ArgumentException($"Invalid format. Text is empty. Source string: \'{str}\' .");
			} 
			else if (text.Length > 1024)
			{
				throw new ArgumentException($"Invalid format. Text is too long. Source string: \"{str}\"");
			}

			return new Entity
			{
				Number = number,
				Text = text
			};
		}

		/// <summary>
		/// Picks a chunk from the source file and writes it to new json file.
		/// </summary>
		/// <param name="startPosition">Start byte position of the chunk.</param>
		/// <param name="endPosition">End byte position of the chunk.</param>
		/// <param name="chunkNumber">Chunk identidier</param>
		/// <returns>Path of  new file.</returns>
		private static async Task<(string filePath, long count)> PickChunkAndWriteToJsonAsync(string inputFilePath, string tempFolderPath, long startPosition, long endPosition, int chunkNumber)
		{
			var currentPostion = startPosition;
			var lastPosition = currentPostion;
			long totalCount = 0;
			string chunkFilePath = GetTempFilePath($"chunk{chunkNumber}.json", tempFolderPath);
			using (FileStream fileStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (StreamReader reader = new StreamReader(fileStream))
			{
				Console.WriteLine($"Reading chunk {chunkNumber} from {startPosition} to {endPosition}.");
				var enities = await ReadEntitiesChunkFromFileAsync(fileStream, currentPostion, endPosition).ConfigureAwait(false);

				enities.Sort();

				//var tempFilePath = GetTempFilePath($"chunk{chunkNumber}.json", tempFolderPath);

				await SerializeEntitiesToJsonFileAsync(enities, chunkFilePath).ConfigureAwait(false);
				totalCount += enities.Count;
			}

			return (chunkFilePath, totalCount);
		}

		private static bool IsEnoughFreeSpace(string inputFilePath, out long fileSize)
		{
			fileSize = SystemInfo.GetFileSize(inputFilePath);
			long freeSpace = SystemInfo.GetAvailableFreeSpace();

			if (freeSpace < fileSize * 3)
			{
				Console.WriteLine("Not enough free space to process the file. Cleanup the disk and try again.");
				return true;
			}

			return false;
		}

		private static async Task<List<long>> FindPrecisedChunkBordersAsync(string inputFilePath, long startPosition, long endPosition, long chunkSize)
		{
			long chunkBorder = 0;
			List<long> chunkBorders = [chunkBorder];

			using (FileStream fileStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (StreamReader reader = new StreamReader(fileStream))
			{
				while (startPosition < endPosition)
				{
					startPosition += chunkSize;
					if (startPosition >= endPosition)
					{
						chunkBorders.Add(await FindNearestNewLinePositionAsync(reader, endPosition - 1).ConfigureAwait(false));
						break;
					}

					if (chunkBorder > endPosition)
					{
						chunkBorders.Add(await FindNearestNewLinePositionAsync(reader, endPosition - 1).ConfigureAwait(false));
					}
					else
					{
						chunkBorder = await FindNearestNewLinePositionAsync(reader, startPosition).ConfigureAwait(false);
						if (chunkBorder == -1)
						{
							break;
						}

						chunkBorders.Add(chunkBorder);
					}

					startPosition = ++chunkBorder;
				}
			}

			return chunkBorders;
		}

		private static async Task<long> FindNearestNewLinePositionAsync(StreamReader reader, long startPosition)
		{
			const int bufferSize = 1024;
			char[] buffer = new char[bufferSize];

			reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);
			reader.DiscardBufferedData();

			int bytesRead = await reader.ReadAsync(buffer, 0, bufferSize);
			if (bytesRead == 0)
			{
				// End of file reached without finding a newline
				return -1;
			}

			for (int i = 0; i < bytesRead; i++)
			{
				if (buffer[i] == '\n')
				{
					return startPosition + i;
				}
			}

			return -1;
		}

		private static async Task<ConcurrentBag<string>> MakeChunksAsync(List<long> chunkBorders, string inputFilePath, string tempFolderPath)
		{
			var semaphore = new SemaphoreSlim(SystemInfo.OptimalIoThreadCount);
			var tasks = new List<Task>();

			var chunkFiles = new ConcurrentBag<string>();
			long totalCount = 0;
			for (int i = 0; i < chunkBorders.Count - 1; i++)
			{
				var startPosition = chunkBorders[i];
				var endPosition = chunkBorders[i + 1];
				var chunkNumber = i;

				var task = Task.Run(async () =>
				{
					try
					{
						await semaphore.WaitAsync().ConfigureAwait(false);
						var chunkResult = await PickChunkAndWriteToJsonAsync(inputFilePath, tempFolderPath, startPosition, endPosition, chunkNumber).ConfigureAwait(false);
						chunkFiles.Add(chunkResult.filePath);
						totalCount += chunkResult.count;
					}
					finally
					{
						semaphore.Release();
					}
				});

				tasks.Add(task);
			}

			await Task.WhenAll(tasks);
			Console.WriteLine($"Total records in chunks: {totalCount}");
			return chunkFiles;
		}

		private static long CalculateFileChunkSize(long sourceFileSize)
		{
			var maxChunkSize = 2 * GB / (SystemInfo.OptimalIoThreadCount / 2);
			var optimalChunkSize = sourceFileSize / (SystemInfo.OptimalIoThreadCount * 2);

			if (optimalChunkSize <= maxChunkSize)
			{
				return optimalChunkSize;
			}

			return maxChunkSize;
		}

		private static string GetTempFilePath(string fileName, string tempFolderPath)
		{
			return Path.Combine(tempFolderPath, fileName);
		}
	}
}