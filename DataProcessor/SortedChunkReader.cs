namespace HeavyFileSorter
{
	internal class SortedChunkReader
	{
		public Entity[] Entities { get; private set; } = [];
		private int Index { get; set; } = 0;
		private long CurrentSeekPos { get; set; } = 0;
		private bool FileFinished { get; set; } = false;
		public bool IsCompleted { get; private set; } = false;

		private readonly int limit = 5000;//increasing this value speed up the process, but consumes more RAM
		private readonly string filePath;

		public SortedChunkReader(string chunkPath) 
		{
			filePath= chunkPath;
		}

		public async Task ReadChunkAsync()
		{
			var (entities, currentSeekPos, isFinished) = await DataProcessingHelper.ReadLimitedCountEntitiesFromJsonFileAsync(filePath, CurrentSeekPos, limit).ConfigureAwait(false);
			Entities = entities;
			CurrentSeekPos = currentSeekPos;
			FileFinished = isFinished;
			IsCompleted = FileFinished && Entities.Length == 0;
		}

		public async Task<Entity> TakeEntity()
		{
			var entity = Entities[Index];
			Index++;
			if (!FileFinished && Index == Entities.Length)
			{
				await ReadChunkAsync().ConfigureAwait(false);
				Index = 0;
			}
			else if (FileFinished && Index == Entities.Length)
			{
				IsCompleted = true;
			}
			
			return entity;
		}

		public Entity? Entity
		{
			get
			{
				return Entities[Index];
			}
		}
	}
}
