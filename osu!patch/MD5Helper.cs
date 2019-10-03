using System;
using System.IO;
using System.Security.Cryptography;

namespace osu_patch
{
	class MD5Helper
	{
		public static string Compute(string filename)
		{
			using (var md5 = MD5.Create())
			{
				using (var stream = File.OpenRead(filename))
				{
					var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
				}
			}
		}
	}
}
