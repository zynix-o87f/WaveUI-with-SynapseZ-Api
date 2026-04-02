using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using WaveWindows.Modules;

namespace WaveWindows.Interfaces;

internal static class BloxstrapInterface
{
	internal static readonly List<string> Files = new List<string> { "https://github.com/dxgi/wave-binaries/raw/main/bloxstrap-setup/Bloxstrap.dll", "https://github.com/dxgi/wave-binaries/raw/main/bloxstrap-setup/Bloxstrap.exe", "https://github.com/dxgi/wave-binaries/raw/main/bloxstrap-setup/Bloxstrap.runtimeconfig.json", "https://github.com/dxgi/wave-binaries/raw/main/bloxstrap-setup/Wave-Blue.ico" };

	internal static readonly string Path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Bloxstrap";

	internal static readonly string Hash = "2F88EA7E1183D320FB2B7483DE2E860DA13DC0C0CAAF58F41A888528D78C809F";

	internal static void Install()
	{
		// Don't download or install anything
		return;
	}

	internal static async Task DownloadFileAsync(string fileName, string filePath, string fileUrl)
	{
		HttpClient client = new HttpClient();
		try
		{
			HttpResponseMessage response = await client.GetAsync(fileUrl);
			if (response.StatusCode != HttpStatusCode.OK)
			{
				throw new Exception("Failed to download " + fileName);
			}
			File.WriteAllBytes(filePath, await response.Content.ReadAsByteArrayAsync());
		}
		finally
		{
			((IDisposable)client)?.Dispose();
		}
	}
}
