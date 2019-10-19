using System.Collections.Generic;
using System.IO;

namespace DictionaryProcessorLib
{
	public static class DictionaryProcessor
	{
		private static readonly long DICTIONARY_SIGNATURE = 0x544349445055534F; // OSUPDICT

		public static byte[] Pack(Dictionary<string, string> dict)
		{
			using (var ms = new MemoryStream())
			{
				using (var w = new BinaryWriter(ms))
				{
					w.Write(DICTIONARY_SIGNATURE);
					w.Write(dict.Count);

					foreach (var kvp in dict)
					{
						w.Write(kvp.Key);
						w.Write(kvp.Value);
					}

					return ms.ToArray();
				}
			}
		}

		public static Dictionary<string, string> Unpack(string path)
		{
			using (var fs = File.OpenRead(path))
				return Unpack(fs);
		}

		public static Dictionary<string, string> Unpack(byte[] bytes)
		{
			using (var ms = new MemoryStream(bytes))
				return Unpack(ms);
		}

		public static Dictionary<string, string> Unpack(Stream stream)
		{
			using (BinaryReader r = new BinaryReader(stream))
			{
				try
				{
					if (r.ReadInt64() != DICTIONARY_SIGNATURE)
						throw new DictionaryProcessorException("Given file is not a valid osu!patch dictionary.");
				}
				catch (IOException) { throw new DictionaryProcessorException("Given file is not a valid osu!patch dictionary."); }

				var capacity = r.ReadInt32();
				var names = new Dictionary<string, string>(capacity);

				for (int i = 0; i < capacity; i++)
					names.Add(r.ReadString(), r.ReadString());

				return names;
			}
		}
	}
}