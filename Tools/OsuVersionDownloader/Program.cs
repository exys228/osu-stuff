using Newtonsoft.Json.Linq;
using osu_patch;
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

namespace OsuVersionDownloader
{
	static class Program
	{
		private const string CHECK_UPDATES_TEMPLATE = "https://osu.ppy.sh/web/check-updates.php?action=check&stream={0}&time={1}";
		private const string DOWNLOAD_TARGET_TEMPLATE = "https://osu.ppy.sh/web/check-updates.php?action=path&stream={0}&target={1}";
		private const int RETRY_SLEEP_TIME = 2000;

		private const string DEFAULT_BACKUP_FOLDER = "osu!bak";

		private static readonly string ExecutingAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

		public static int Main(string[] args)
		{
			if (args.Length < 2)
				return Message("OsuVersionDownloader - download osu! versions from osu.ppy.sh\n" +
								   "by exys, 2019\n" +
								   "\n" +
								   "Usage:\n" +
								   "OsuVersionDownloader [stream] [out file]\n" +
								   "OsuVersionDownloader [stream] [target] [out file]\n" +
								   "\n" +
								   "Streams:\n" +
								   "Stable - fallback (!)\n" +
								   "Stable40 - current stable\n" +
								   "Beta\n" +
								   "cuttingedge");

			string stream;
			string targetFile;

			int target = -1;

			if (args.Length == 2) // OsuVersionDownloader [stream] [out file]
			{
				stream = args[0];
				targetFile = args[1];
			}
			else
			{
				stream = args[0];
				targetFile = args[2];

				if (!int.TryParse(args[1], out target))
					return Message("Invalid target.");
			}

			var ret = GetDataFromStream(stream, targetFile, target);

			if (ret is null)
			{
				Message("Data == null (no updates/failed to update), exiting.");
				return 0;
			}

			if (File.Exists(targetFile))
				MakeBackup(targetFile);

			File.WriteAllBytes(targetFile, ret);
			return 0;
		}

		private static void MakeBackup(string path)
		{
			var directory = Path.GetDirectoryName(path);
			var fileName = Path.GetFileNameWithoutExtension(path);
			var extension = Path.GetExtension(path);

			directory = Path.Combine(directory, DEFAULT_BACKUP_FOLDER);

			if (!Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			var i = 0L;
			string backupPath;

			while (File.Exists(backupPath = Path.Combine(directory, $"{fileName}-{i}{extension}")))
				i++;

			File.Copy(path, backupPath);
		}

		/// <summary>
		/// Get osu! assembly by stream and target filename
		/// </summary>
		/// <param name="streamName">Target stream name (Stable/Beta/cuttingedge)</param>
		/// <param name="targetFile">Target filename (ex. osu!.exe, bass_fx.dll etc.)</param>
		/// <param name="target">Target number (file_version), default is latest</param>
		/// <returns>Dictionary (file name to write with extension; file data)</returns>
		private static byte[] GetDataFromStream(string streamName, string targetFile, long target = -1)
		{
			byte[] ret = null;

			HttpWebRequest request;

			if (target == -1)
				request = (HttpWebRequest)WebRequest.Create(string.Format(CHECK_UPDATES_TEMPLATE, streamName, DateTime.Now.Ticks));
			else
				request = (HttpWebRequest)WebRequest.Create(string.Format(DOWNLOAD_TARGET_TEMPLATE, streamName, target));

			request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

			string jsonResponse;

			using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
			using (Stream stream = response.GetResponseStream())
			using (StreamReader reader = new StreamReader(stream))
			{
				jsonResponse = reader.ReadToEnd();
			}

			dynamic updateFiles = JToken.Parse(jsonResponse);

			if (updateFiles is JObject)
			{
				Message("Error response received from server:\n" + updateFiles.response.Value);
				return null;
			}

			if (updateFiles is JArray)
			{
				foreach (var updateFile in updateFiles)
				{
					if (updateFile.filename.Value != targetFile)
						continue;

					if (File.Exists(targetFile))
						if (updateFile.file_hash.Value.Equals(MD5Helper.Compute(targetFile), StringComparison.OrdinalIgnoreCase))
							continue;

					var cachedAssemblyPath = Path.Combine(ExecutingAssemblyLocation, updateFile.file_hash.Value + Path.GetExtension(targetFile));

					if (File.Exists(cachedAssemblyPath))
					{
						Message($"Found cached copy of {targetFile}!");
						return File.ReadAllBytes(cachedAssemblyPath);
					}

					int retryCount = 0;

					while (true)
					{
						try
						{
							ret = GetDataFromUrl(updateFile.url_full.Value);
							break;
						}
						catch (Exception ex)
						{
							Message($"Failed to download \"{target} - {updateFile.filename.Value}\", retrying in {RETRY_SLEEP_TIME / 1000} seconds. More info:\n{ex}");
							Thread.Sleep(RETRY_SLEEP_TIME);
						}

						if (++retryCount > 3)
							return ret;
					}

					File.WriteAllBytes(cachedAssemblyPath, ret);
					Message($"Successfully downloaded {targetFile}!");
					break;
				}
			}

			return ret;
		}

		private static byte[] GetDataFromUrl(string url) => new WebClient().DownloadData(url);

		private static int Message(string msg)
		{
			XConsole.WriteLine(msg);
			return 1;
		}
	}
}
