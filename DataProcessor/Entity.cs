using System.Globalization;
using System.Runtime.Serialization;

namespace HeavyFileSorter
{
	[DataContract]
	internal class Entity : IComparable<Entity>
	{
		[DataMember]
		public long Number { get; set; }

		[DataMember]
		public required string Text { get; set; }

		public Entity() { }

		public override string ToString()
		{
			return $"{Number}. {Text}";
		}

		public override int GetHashCode()
		{
			return Text.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (obj == null || GetType() != obj.GetType())
			{
				return false;
			}

			Entity entity = (Entity)obj;
			return string.Equals(Text, entity.Text, StringComparison.Ordinal)
				&& Number.CompareTo(entity.Number)==0;
		}

		public int CompareTo(Entity? other)
		{
			if (other == null) return -1;

			var stringCompareRestult = string.Compare(Text, other.Text, new CultureInfo("en-US"), CompareOptions.OrdinalIgnoreCase);

			if (stringCompareRestult != 0)
			{
				return stringCompareRestult;
			}
			else
			{
				return Number.CompareTo(other.Number);
			}
		}
	}
}
