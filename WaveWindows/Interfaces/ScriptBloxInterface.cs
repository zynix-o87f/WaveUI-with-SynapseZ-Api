using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using RestSharp;

namespace WaveWindows.Interfaces;

internal class ScriptBloxInterface
{
	internal static RestClient Client = new RestClient("https://scriptblox.com/api", (ConfigureRestClient)null, (ConfigureHeaders)null, (ConfigureSerialization)null);

	internal static async Task<SearchResult> Fetch(int page = 1, int max = 20)
	{
		RestRequest request = new RestRequest("script/fetch", Method.Get)
			.AddParameter("page", page)
			.AddParameter("max", max);

		RestResponse response = await RestClientExtensions.GetAsync((IRestClient)(object)Client, request, default(CancellationToken));
		if (!((RestResponseBase)response).IsSuccessStatusCode)
		{
			throw new HttpRequestException(((RestResponseBase)response).ErrorMessage);
		}
		SearchResponse result = JsonConvert.DeserializeObject<SearchResponse>(((RestResponseBase)response).Content, new JsonSerializerSettings
		{
			NullValueHandling = (NullValueHandling)1,
			MissingMemberHandling = (MissingMemberHandling)0
		});
		return result.Result;
	}

	internal static async Task<SearchResult> Search(string query, int page = 1, int max = 20)
	{
		RestRequest request = new RestRequest("script/search", Method.Get)
			.AddParameter("q", query)
			.AddParameter("page", page)
			.AddParameter("max", max);

		RestResponse response = await RestClientExtensions.GetAsync((IRestClient)(object)Client, request, default(CancellationToken));
		if (!((RestResponseBase)response).IsSuccessStatusCode)
		{
			throw new HttpRequestException(((RestResponseBase)response).ErrorMessage);
		}
		SearchResponse result = JsonConvert.DeserializeObject<SearchResponse>(((RestResponseBase)response).Content, new JsonSerializerSettings
		{
			NullValueHandling = (NullValueHandling)1,
			MissingMemberHandling = (MissingMemberHandling)0
		});
		return result.Result;
	}

	internal static async Task<SearchResult> Trending()
	{
		RestRequest request = new RestRequest("script/trending", Method.Get);

		RestResponse response = await RestClientExtensions.GetAsync((IRestClient)(object)Client, request, default(CancellationToken));
		if (!((RestResponseBase)response).IsSuccessStatusCode)
		{
			throw new HttpRequestException(((RestResponseBase)response).ErrorMessage);
		}
		SearchResponse result = JsonConvert.DeserializeObject<SearchResponse>(((RestResponseBase)response).Content, new JsonSerializerSettings
		{
			NullValueHandling = (NullValueHandling)1,
			MissingMemberHandling = (MissingMemberHandling)0
		});
		return result.Result;
	}

	internal static async Task<Script> GetScript(Script script)
	{
		RestRequest request = new RestRequest("script/" + script.Slug, (Method)0);
		RestResponse response = await RestClientExtensions.GetAsync((IRestClient)(object)Client, request, default(CancellationToken));
		if (!((RestResponseBase)response).IsSuccessStatusCode)
		{
			throw new HttpRequestException(((RestResponseBase)response).ErrorException.ToString());
		}
		ScriptResult result = JsonConvert.DeserializeObject<ScriptResult>(((RestResponseBase)response).Content, new JsonSerializerSettings
		{
			NullValueHandling = (NullValueHandling)1,
			MissingMemberHandling = (MissingMemberHandling)0
		});
		return result.Script;
	}

	internal static string GetImageUrl(string image)
	{
		if (string.IsNullOrEmpty(image))
		{
			return null;
		}
		if (image.StartsWith("http://") || image.StartsWith("https://"))
		{
			return image;
		}
		return "https://scriptblox.com" + (image.StartsWith("/") ? "" : "/") + image;
	}

	internal static ImageSource ToImage(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return null;
		}
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(url);
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
			bitmapImage.EndInit();
			if (bitmapImage.CanFreeze)
			{
				bitmapImage.Freeze();
			}
			return bitmapImage;
		}
		catch
		{
			return GetDefaultImage();
		}
	}

	private static ImageSource GetDefaultImage()
	{
		try
		{
			BitmapImage defaultImage = new BitmapImage();
			defaultImage.BeginInit();
			defaultImage.UriSource = new Uri("https://scriptblox.com/favicon.ico");
			defaultImage.CacheOption = BitmapCacheOption.OnLoad;
			defaultImage.EndInit();
			if (defaultImage.CanFreeze)
			{
				defaultImage.Freeze();
			}
			return defaultImage;
		}
		catch
		{
			return null;
		}
	}

	internal static async Task<string> ImageToBase64(string image)
	{
		HttpClient client = new HttpClient();
		try
		{
			return "data:image/webp;base64," + Convert.ToBase64String(await client.GetByteArrayAsync(image));
		}
		finally
		{
			((IDisposable)client)?.Dispose();
		}
	}

	internal static ImageSource Base64ToImage(string base64)
	{
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.StreamSource = new MemoryStream(Convert.FromBase64String(base64));
		bitmapImage.EndInit();
		return bitmapImage;
	}
}
