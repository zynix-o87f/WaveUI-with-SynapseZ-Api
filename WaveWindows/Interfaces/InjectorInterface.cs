using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WaveWindows.Interfaces;

internal class InjectorInterface
{
	internal static readonly string[] Files;

	internal static readonly string BaseDirectory;

	internal static Process GetInjector(int processId)
	{
		return new Process
		{
			StartInfo = new ProcessStartInfo
			{
				Verb = "runas",
				FileName = BaseDirectory + "\\Injector.exe",
				WorkingDirectory = "./bin",
				Arguments = $"{processId}",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true,
				RedirectStandardOutput = true
			}
		};
	}

	internal static Task<string> TryGetInjector(Action<string, double> callback)
	{
		// Don't download - just return the path
		return Task.FromResult(BaseDirectory + "\\Injector.exe");
	}

	internal static void VerifyInjector()
	{
		// Don't verify downloads
		return;
	}

	internal static Task TryDownloadAvailableInjector(string version, Action<string, double> callback)
	{
		// Don't download anything
		return Task.CompletedTask;
	}

	internal static Task<string> GetRobloxVersion()
	{
		// Return a dummy version
		return Task.FromResult("dummy-version");
	}

	internal static async Task DownloadFileAsync(string version, string fileName, string filePath, Action<string, double> callback)
	{
	}

	static InjectorInterface()
	{
		Files = new string[2] { "Injector.exe", "Wave.dll" };
		BaseDirectory = Path.GetTempPath();
	}
}
