using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace WaveWindows.Interfaces;

internal static class LanguageServerInterface
{
	internal static readonly List<string> Directories = new List<string> { "server", "shared", "shared\\bin", "shared\\configuration", "shared\\themes" };

	internal static readonly List<string> Files = new List<string> { "https://github.com/dxgi/wave-binaries/raw/main/language-server-protocol/node.exe", "https://github.com/dxgi/wave-binaries/raw/main/language-server-protocol/server/codicon.ttf", "https://github.com/dxgi/wave-binaries/raw/main/language-server-protocol/server/index.js", "https://github.com/dxgi/wave-binaries/raw/main/language-server-protocol/shared/bin/en-us.json", "https://github.com/dxgi/wave-binaries/raw/main/language-server-protocol/shared/bin/globalTypes.d.luau", "https://github.com/dxgi/wave-binaries/raw/main/language-server-protocol/shared/bin/wave-luau.exe", "https://github.com/dxgi/wave-binaries/raw/main/language-server-protocol/shared/bin/wave.d.luau", "https://github.com/dxgi/wave-binaries/raw/main/language-server-protocol/shared/configuration/default.json", "https://github.com/dxgi/wave-binaries/raw/main/language-server-protocol/shared/themes/wave.json" };

    internal static readonly string BaseDirectory = Path.Combine(Directory.GetCurrentDirectory(), "bin");	

    internal static Task<Process> TryLaunch()
	{
		// Don't verify/download - just return a dummy process
		return Task.FromResult(new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = "cmd.exe",
				Arguments = "/c echo Language Server disabled",
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			},
			EnableRaisingEvents = true
		});
	}

	internal static Task Verify()
	{
		// Don't download anything
		return Task.CompletedTask;
	}

	internal static string GetFileDirectory(string path)
	{
		if (path.Contains("server/"))
		{
			return BaseDirectory + "\\server";
		}
		if (path.Contains("shared/bin"))
		{
			return BaseDirectory + "\\shared\\bin";
		}
		if (path.Contains("shared/configuration"))
		{
			return BaseDirectory + "\\shared\\configuration";
		}
		if (path.Contains("shared/themes"))
		{
			return BaseDirectory + "\\shared\\themes";
		}
		return BaseDirectory;
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
