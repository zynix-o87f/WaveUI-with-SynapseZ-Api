using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Newtonsoft.Json;
using RestSharp;
using WaveWindows.Controls;
using WaveWindows.Controls.Card;
using WaveWindows.Controls.Editor;
using WaveWindows.Controls.Settings;
using WaveWindows.Interfaces;
using WaveWindows.Modules;
using WaveWindows.Modules.Behaviour;

namespace WaveWindows;

public partial class MainWindow : Window, IComponentConnector
{
	internal static readonly DependencyProperty CurrentPageSelectionProperty;

	internal OverlayState CurrentOverlaySelection = OverlayState.None;

	private readonly WmiProcessWatcher WmiWatcher = new WmiProcessWatcher("RobloxPlayerBeta");

	private readonly SocketServer SocketServer = new SocketServer("ws://localhost:60137");

	private readonly List<string> SelectedClients = new List<string>();

	private readonly RestClient RobloxThumbnailApi = new RestClient("https://thumbnails.roblox.com/v1", (ConfigureRestClient)null, (ConfigureHeaders)null, (ConfigureSerialization)null);

	private readonly EditorInterface.EditorOptions EditorOptions = new EditorInterface.EditorOptions
	{
		FontSize = 14,
		Minimap = new EditorInterface.MinimapOptions
		{
			Enabled = true
		},
		InlayHints = new EditorInterface.InlayHintsOptions
		{
			Enabled = true
		}
	};

	private int References = 1;

	internal PageState CurrentPageSelection
	{
		get
		{
			return (PageState)GetValue(CurrentPageSelectionProperty);
		}
		set
		{
			SetValue(CurrentPageSelectionProperty, value);
		}
	}

	private Types.WaveAPI.User User { get; set; }

	private Types.WaveAPI.Product Product { get; set; }

	private List<Types.WaveAPI.Message> Messages { get; set; }

	private string Session { get; set; }

	private string SearchQuery { get; set; }

	private SearchResult SearchResult { get; set; }

	public MainWindow()
	{
		InitializeComponent();
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		try
		{
            Types.WaveAPI.Product product = new Types.WaveAPI.Product
            {
                Id = "52",
                Timestamp = 17852734,
                Name = "premium-wave"
            };

			Product = product;
			Messages = new List<Types.WaveAPI.Message>();
			LoadUserData();
		}
		catch
		{
			await Task.Delay(3000);
		}
		
		// Ensure blur and loading overlay are disabled
		BlurEffect.Radius = 0;
		LoadingOverlay.Visibility = Visibility.Hidden;
		LoadingOverlay.Opacity = 0;
		
		IsInjectedText.Text = "Not Injected";
		WmiWatcher.Start(delegate(Instance instance, ProcessState state)
		{
			if (state == ProcessState.Running && !ClientBehaviour.GetAllClients().Contains(instance.Process.Id.ToString()))
			{
				instance.Inject(HandleInjectionCallback, 2500);
			}
		});
		Initializer.Once();
		EditorTabControl.AddTab("Untitled Tab", "print(\"Hello World!\");");
		ContinueOnStartUpCheckBox.IsChecked = WaveWindows.Modules.Registry.Configuration.ContinueOnStartUp;
		TopMostCheckBox.IsChecked = WaveWindows.Modules.Registry.Configuration.TopMost;
		RedirectCompilerErrorCheckBox.IsChecked = WaveWindows.Modules.Registry.Configuration.RedirectCompilerError;
		UsePerformanceModeCheckBox.IsChecked = WaveWindows.Modules.Registry.Configuration.UsePerformanceMode;
		RefreshRateSlider.Value = WaveWindows.Modules.Registry.Configuration.RefreshRate;
		FontSizeSlider.Value = WaveWindows.Modules.Registry.Configuration.FontSize;
		MinimapCheckBox.IsChecked = WaveWindows.Modules.Registry.Configuration.Minimap;
		InlayHintsCheckBox.IsChecked = WaveWindows.Modules.Registry.Configuration.InlayHints;
		SendCurrentDocumentCheckBox.IsChecked = WaveWindows.Modules.Registry.Configuration.SendCurrentDocument;
		EditorOptions.FontSize = WaveWindows.Modules.Registry.Configuration.FontSize;
		EditorOptions.Minimap.Enabled = WaveWindows.Modules.Registry.Configuration.Minimap;
		EditorOptions.InlayHints.Enabled = WaveWindows.Modules.Registry.Configuration.InlayHints;
		EditorTabControl.SetEditorOptions(EditorOptions);
		LoadLocalScripts();
		LoadAutoexecScripts();
		LoadCurrentWorkspace("SynapseZ_Tabs");
		base.Closing += delegate
		{
			SaveCurrentWorkspace("SynapseZ_Tabs");
		};
	}

	private void Window_Closing(object sender, CancelEventArgs e)
	{
		WmiWatcher.Stop();
		SocketServer.Dispose();
	}

	private void EditorToggleButton_Click(object sender, RoutedEventArgs e)
	{
		SwitchPage(PageState.Editor);
	}

	private void ScriptCloudToggleButton_Click(object sender, RoutedEventArgs e)
	{
		SwitchPage(PageState.ScriptCloud);
	}

	private void SettingsToggleButton_Click(object sender, RoutedEventArgs e)
	{
		SwitchPage(PageState.Settings);
	}

	private void ManagerToggleButton_Click(object sender, RoutedEventArgs e)
	{
		PopulateClientList();
		SwitchOverlay(OverlayState.Manager);
	}

	private void PopulateClientList()
	{
		ClientList.Children.Clear();
		SelectedClients.Clear();
		
		var processes = SynapseZ.SynapseZAPI.GetRobloxProcesses();
		
		if (processes.Length == 0)
		{
			System.Windows.Controls.TextBlock noClients = new System.Windows.Controls.TextBlock
			{
				Text = "No Roblox instances found",
				Foreground = new SolidColorBrush(Color.FromRgb(168, 168, 168)),
				FontFamily = new FontFamily("Inter"),
				FontSize = 12,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				Margin = new Thickness(0, 20, 0, 0)
			};
			ClientList.Children.Add(noClients);
			return;
		}
		
		foreach (var process in processes)
		{
			bool isInjected = CheckIfInjected(process.Id);
			string status = isInjected ? "Injected" : "Not Injected";
			
			Client client = new Client
			{
				Id = process.Id.ToString(),
				Player = $"Roblox ({process.Id})",
				Game = status,
				Image = null,
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
			};
			client.Checked = (EventHandler<string>)Delegate.Combine(client.Checked, (EventHandler<string>)delegate
			{
				if (SelectedClients.Contains(client.Id))
				{
					SelectedClients.Remove(client.Id);
				}
				else
				{
					SelectedClients.Add(client.Id);
				}
			});
			if (SelectedClients.Count < 1)
			{
				client.IsChecked = true;
			}
			ClientList.Children.Add(client);
		}
	}

	private bool CheckIfInjected(int pid)
	{
		try
		{
			string pipeName = $"\\\\.\\pipe\\synz-{pid}";
			return System.IO.File.Exists(pipeName);
		}
		catch
		{
			return false;
		}
	}

	private void LicenseToggleButton_Click(object sender, RoutedEventArgs e)
	{
    string savedKey = SynapseZ.SynapseZAPI.GetAccountKey();
    if (!string.IsNullOrEmpty(savedKey))
    {
        LicenseKeyBox.Text = savedKey;
    }
    SwitchOverlay(OverlayState.License);
	}


	private void Border_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
		{
			if (base.WindowState == WindowState.Maximized)
			{
				base.WindowState = WindowState.Normal;
				base.Top = PointToScreen(new Point(0.0, 0.0)).Y / 2.0 - base.ActualHeight / 6.0;
			}
			DragMove();
		}
	}

	private void ExitButton_Click(object sender, RoutedEventArgs e)
	{
		Environment.Exit(0);
	}

	private void MaximizeButton_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private void MinimizeButton_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
	{
		TabItem tab = EditorTabControl.SelectedItem as TabItem;
		Monaco editor = EditorTabControl.CurrentEditor;
		if (editor == null)
		{
			return;
		}
		
		string script = await editor.GetText();
		int result = SynapseZ.SynapseZAPI.Execute(script);
		
		if (result != 0)
		{
			string errorMsg = SynapseZ.SynapseZAPI.GetLatestErrorMessage();
			ToastNotification.Error("Execution Failed", errorMsg);
		}
		else
		{
			ToastNotification.Success("Script executed successfully.");
		}
	}

	private void ClearButton_Click(object sender, RoutedEventArgs e)
	{
		Monaco currentEditor = EditorTabControl.CurrentEditor;
		currentEditor.SetText("");
	}

	private async void SaveFileButton_Click(object sender, RoutedEventArgs e)
	{
		SaveFileDialog dialog = new SaveFileDialog
		{
			Filter = "LuaU Files (*.luau)|*.luau|Lua Files (*.lua)|*.lua|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
			Title = "SynapseZ - Save File",
			DefaultExt = "luau",
			AddExtension = true
		};
		if (dialog.ShowDialog() == true)
		{
			Monaco editor = EditorTabControl.CurrentEditor;
			string fileName = dialog.FileName;
			File.WriteAllText(fileName, await editor.GetText());
		}
	}

	private void OpenFileButton_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "LuaU Files (*.luau)|*.luau|Lua Files (*.lua)|*.lua|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
			Title = "SynapseZ - Open File",
			Multiselect = false
		};
		if (openFileDialog.ShowDialog() == true)
		{
			Monaco currentEditor = EditorTabControl.CurrentEditor;
			currentEditor.SetText(File.ReadAllText(openFileDialog.FileName));
		}
	}

	private async void SearchBox_Loaded(object sender, RoutedEventArgs e)
	{
		SearchResult = await ScriptBloxInterface.Trending();
		PopulateSearchResult(SearchResult);
	}

	private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.IsDown && e.Key == Key.Return)
		{
			SearchQuery = SearchBox.Text;
			if (!string.IsNullOrEmpty(SearchQuery))
			{
				SearchResult = await ScriptBloxInterface.Search(SearchQuery, 1);
				PopulateSearchResult(SearchResult);
			}
		}
	}

	private void PreviousPageButton_Click(object sender, RoutedEventArgs e)
	{
		NavigateSearchResult(SearchResult, SearchResult.NextPage - 2);
	}

	private void NextPageButton_Click(object sender, RoutedEventArgs e)
	{
		NavigateSearchResult(SearchResult, SearchResult.NextPage);
	}

	private void SettingsExecutorToggleButton_Click(object sender, RoutedEventArgs e)
	{
		SettingsExecutorSection.BringIntoView();
	}

	private void SettingsEditorToggleButton_Click(object sender, RoutedEventArgs e)
	{
		SettingsEditorSection.BringIntoView();
	}

	private void SettingsArtificialIntelligenceToggleButton_Click(object sender, RoutedEventArgs e)
	{
		SettingsArtificialIntelligenceSection.BringIntoView();
	}

	private void ContinueOnStartUpCheckBox_Checked(object sender, CheckBoxChangedEvent e)
	{
		WaveWindows.Modules.Registry.Configuration.ContinueOnStartUp = e.Value;
	}

	private void TopMostCheckBox_Checked(object sender, CheckBoxChangedEvent e)
	{
		WaveWindows.Modules.Registry.Configuration.TopMost = e.Value;
		base.Topmost = e.Value;
	}

	private void RedirectCompilerErrorCheckBox_Checked(object sender, CheckBoxChangedEvent e)
	{
		WaveWindows.Modules.Registry.Configuration.RedirectCompilerError = e.Value;
	}

	private void UsePerformanceModeCheckBox_Checked(object sender, CheckBoxChangedEvent e)
	{
		WaveWindows.Modules.Registry.Configuration.UsePerformanceMode = e.Value;
	}

	private void RefreshRateSlider_Changed(object sender, SliderChangedEvent e)
	{
		WaveWindows.Modules.Registry.Configuration.RefreshRate = e.Value;
		Monaco[] allEditors = EditorTabControl.GetAllEditors();
		foreach (Monaco monaco in allEditors)
		{
			monaco.SetBrowserFramerate(e.Value);
		}
	}

	private void FontSizeSlider_Changed(object sender, SliderChangedEvent e)
	{
		WaveWindows.Modules.Registry.Configuration.FontSize = e.Value;
		EditorOptions.FontSize = e.Value;
		Monaco[] allEditors = EditorTabControl.GetAllEditors();
		foreach (Monaco monaco in allEditors)
		{
			monaco.SetFontSize(e.Value);
		}
	}

	private void MinimapCheckBox_Checked(object sender, CheckBoxChangedEvent e)
	{
		WaveWindows.Modules.Registry.Configuration.Minimap = e.Value;
		EditorOptions.Minimap.Enabled = e.Value;
		Monaco[] allEditors = EditorTabControl.GetAllEditors();
		foreach (Monaco monaco in allEditors)
		{
			monaco.SetMinimap(e.Value);
		}
	}

	private void InlayHintsCheckBox_Checked(object sender, CheckBoxChangedEvent e)
	{
		WaveWindows.Modules.Registry.Configuration.InlayHints = e.Value;
		EditorOptions.InlayHints.Enabled = e.Value;
		Monaco[] allEditors = EditorTabControl.GetAllEditors();
		foreach (Monaco monaco in allEditors)
		{
			monaco.SetInlayHints(e.Value);
		}
	}

	private void SendCurrentDocumentCheckBox_Checked(object sender, CheckBoxChangedEvent e)
	{
		WaveWindows.Modules.Registry.Configuration.SendCurrentDocument = e.Value;
	}

	private void JoinNowButton_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrEmpty(LicenseKeyBox.Text))
		{
			ToastNotification.Warning("Please provide a license key to redeem.");
			return;
		}
		try
		{
			ToggleLoading(show: true, hasOverlay: true);
			int result = SynapseZ.SynapseZAPI.Redeem(LicenseKeyBox.Text);
			if (result == 0)
			{
				ToastNotification.Success("License redeemed successfully.");
			}
			else
			{
				string errorMsg = SynapseZ.SynapseZAPI.GetLatestErrorMessage();
				ToastNotification.Error(errorMsg);
			}
		}
		catch (Exception ex2)
		{
			Exception ex = ex2;
			ToastNotification.Error(ex.Message);
		}
		finally
		{
			LicenseKeyBox.Text = string.Empty;
			ToggleLoading(show: false, hasOverlay: true);
		}
	}

	private void DurationText_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (Product == null)
		{
			ToastNotification.Info("Free mode is not available until 7 days. Please try again later or consider upgrading to a premium plan for immediate access.");
		}
	}

	private void ManagerOverlay_MouseDown(object sender, MouseButtonEventArgs e)
	{
		HideManagerOrLicenseOverlay();
	}

	private void LicenseOverlay_MouseDown(object sender, MouseButtonEventArgs e)
	{
		HideManagerOrLicenseOverlay();
	}

	public void ShowUnhandledExceptionError(UnhandledExceptionErrorType type, string message)
	{
		string unhandledExceptionErrorTitle = GetUnhandledExceptionErrorTitle(type);
		UnhandledExceptionError.Show(BlurEffect, unhandledExceptionErrorTitle, message);
	}

	private string GetUnhandledExceptionErrorTitle(UnhandledExceptionErrorType type)
	{
		return type switch
		{
			UnhandledExceptionErrorType.ApplicationError => "Application Error", 
			UnhandledExceptionErrorType.SecurityError => "Security Error", 
			UnhandledExceptionErrorType.RegistryError => "Registry Error", 
			_ => throw new ArgumentException("GetUnhandledExceptionErrorTitle.UnhandledExceptionErrorType"), 
		};
	}

	private string GetInjectionMessage(InjectionStatus status, object data)
	{
		return status switch
		{
			InjectionStatus.Waiting => "Waiting for the client to be ready.", 
			InjectionStatus.Injecting => "Attempting to inject the client.", 
			InjectionStatus.Failed => $"The Injector process has exited with a non-zero exit code. ({data})", 
			InjectionStatus.Outdated => "The Injector is currently outdated. Please try again later.", 
			_ => string.Empty, 
		};
	}

	private void HandleInjectionCallback(InjectionStatus status, object data)
	{
	}

	private async void OnClientBehaviourAdded(Types.ClientBehaviour.ClientIdentity identity)
	{
		if (identity == null)
		{
			return;
		}
		ImageSource imageSource = ((!string.IsNullOrEmpty(identity.Player.Id) && !(identity.Player.Id == "0")) ? (await GetClientAvatarHeadshot(identity.Player.Id)) : null);
		ImageSource headshot = imageSource;
		Client client = new Client
		{
			Id = identity.Process.Id,
			Player = identity.Player.Name,
			Game = identity.Game.Name,
			Image = headshot,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		Client client2 = client;
		client2.Checked = (EventHandler<string>)Delegate.Combine(client2.Checked, (EventHandler<string>)delegate
		{
			if (SelectedClients.Contains(client.Id))
			{
				SelectedClients.Remove(client.Id);
			}
			else
			{
				SelectedClients.Add(client.Id);
			}
		});
		if (SelectedClients.Count < 1)
		{
			client.IsChecked = true;
		}
		ClientList.Children.Add(client);
	}

	private async void OnClientBehaviourUpdated(Types.ClientBehaviour.ClientIdentity identity)
	{
		if (identity != null)
		{
			Client client = ClientList.Children.OfType<Client>().FirstOrDefault((Client x) => x.Id == identity.Process.Id);
			if (client != null)
			{
				ImageSource imageSource = ((!string.IsNullOrEmpty(identity.Player.Id) && !(identity.Player.Id == "0")) ? (await GetClientAvatarHeadshot(identity.Player.Id)) : null);
				ImageSource headshot = imageSource;
				client.Player = identity.Player.Name;
				client.Game = identity.Game.Name;
				client.Image = headshot;
			}
		}
	}

	private void OnClientBehaviourRemoved(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return;
		}
		Client client = ClientList.Children.OfType<Client>().FirstOrDefault((Client x) => x.Id == id);
		if (client != null)
		{
			if (SelectedClients.Contains(client.Id))
			{
				SelectedClients.Remove(client.Id);
			}
			ClientList.Children.Remove(client);
		}
	}

	private void OnClientBehaviourScript(Types.ClientBehaviour.ClientScript script)
	{
		if (script != null)
		{
		}
	}
    private void OnClientBehaviourError(string OpCode, Types.ClientBehaviour.ClientError error)
	{
		if (error == null)
		{
			return;
		}
		if (!(OpCode == "OP_AUTH"))
		{
			if (OpCode == "OP_ERROR")
			{
				string message = error.Message;
				int num = message.IndexOf(':');
				int num2 = message.IndexOf(':', num + 1);
				string lineInfo = message.Substring(num + 1, num2 - num - 1).Trim();
				string description = message.Substring(num2 + 1).Trim();
				TabItem tab = EditorTabControl.SelectedItem as TabItem;
				ToastNotification.Error("Compiler Error", description, $"{tab.Header}:{lineInfo}", delegate
				{
					EditorTabControl.SelectById(tab.Id)?.GoToLine(int.Parse(lineInfo));
				});
			}
		}
		else
		{
			ToastNotification.Error("Authentication Failed", error.Message);
		}
	}

	private async Task<ImageSource> GetClientAvatarHeadshot(string playerId)
	{
		RestRequest request = RestRequestExtensions.AddParameter(RestRequestExtensions.AddParameter(RestRequestExtensions.AddParameter(RestRequestExtensions.AddParameter(RestRequestExtensions.AddParameter(new RestRequest("users/avatar-headshot", (Method)0), "userIds", playerId, true), "size", "100x100", true), "format", "Png", true), "isCircular", "false", true), "accept", "application/json", true);
		RestResponse response = await RobloxThumbnailApi.ExecuteAsync(request, default(CancellationToken));
		if (((RestResponseBase)response).StatusCode != HttpStatusCode.OK)
		{
			throw new HttpRequestException("RobloxThumbnailApi");
		}
		string data = ((RestResponseBase)response).Content;
		if (string.IsNullOrEmpty(data))
		{
			throw new NullReferenceException("ThumbnailResponse");
		}
		Types.RobloxThumbnail.ThumbnailResponse thumbnail = JsonConvert.DeserializeObject<Types.RobloxThumbnail.ThumbnailResponse>(data) ?? throw new NullReferenceException("ThumbnailResponse");
		if (thumbnail.Data.Count < 1)
		{
			throw new NullReferenceException("ThumbnailResponse");
		}
		Types.RobloxThumbnail.ThumbnailData avatar = thumbnail.Data.FirstOrDefault() ?? throw new NullReferenceException("Avatar");
		if (avatar.Image == null)
		{
			throw new NullReferenceException("Headshot");
		}
		BitmapImage image = new BitmapImage();
		image.BeginInit();
		image.UriSource = new Uri(avatar.Image);
		image.EndInit();
		return image;
	}

	private void LoadCurrentWorkspace(string path)
	{
		List<string> list = new List<string>();
		string path2 = Path.Combine(Path.GetTempPath(), path);
		if (!Directory.Exists(path2))
		{
			return;
		}
		string[] files = Directory.GetFiles(path2, "*", SearchOption.AllDirectories);
		foreach (string item in files)
		{
			list.Add(item);
		}
		foreach (string item2 in list)
		{
			string text = File.ReadAllText(item2);
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			string header = Path.GetFileName(item2);
			if (string.IsNullOrEmpty(Path.GetExtension(item2)))
			{
				header = text.Split('\n', '\r')[0];
			}
			EditorTabControl.AddTab(header, text);
		}
		if (EditorTabControl.Items.Count > 1)
		{
			EditorTabControl.Items.RemoveAt(0);
		}
		Directory.Delete(path2, recursive: true);
	}

	private async void SaveCurrentWorkspace(string path)
	{
		TabItem[] tabs = EditorTabControl.GetAllTabs();
		string directory = Path.Combine(Path.GetTempPath(), path);
		if (!Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}
		TabItem[] array = tabs;
		foreach (TabItem tab in array)
		{
			string header = tab.Header as string;
			Monaco editor = tab.GetEditor();
			if (!header.EndsWith(".luau") || !header.EndsWith(".lua") || !header.EndsWith(".txt"))
			{
				header = Cryptography.SHA1.Compute(await editor.GetText());
			}
			string path2 = Path.Combine(directory, header);
			File.WriteAllText(path2, await editor.GetText());
		}
	}

	private string LoadMessageCodeBlocks(string message)
	{
		MatchCollection matchCollection = Regex.Matches(message, "```lua\\s*([^`]+)\\s*```", RegexOptions.Singleline);
		string text = message;
		for (int i = 0; i < matchCollection.Count; i++)
		{
			string value = matchCollection[i].Groups[1].Value;
			text = text.Replace(matchCollection[i].Value, $"Reference {References}.lua");
			EditorTabControl.AddTab($"Reference {References}.lua", value);
			References++;
		}
		return text.Trim();
	}

	private void LoadUserData()
	{
		try
		{
			string savedKey = SynapseZ.SynapseZAPI.GetAccountKey();
			if (!string.IsNullOrEmpty(savedKey))
			{
				LicenseKeyBox.Text = savedKey;
			}
			
			var expireDate = SynapseZ.SynapseZAPI.GetExpireDate();
			if (expireDate != null)
			{
				DateTime expire = expireDate.Value;
				TimeSpan remaining = expire - DateTime.UtcNow;
				if (remaining.TotalDays > 0)
				{
					DurationText.Text = $"{(int)remaining.TotalDays} days remaining";
				}
				else
				{
					DurationText.Text = "Expired";
				}
			}
			else
			{
				DurationText.Text = "No license found";
			}
		}
		catch
		{
			DurationText.Text = "Unable to check";
		}
		ToggleAds(false);
		OnceReady();
	}

	private void ResetHwidButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		try
		{
			ToggleLoading(show: true, hasOverlay: true);
			int result = SynapseZ.SynapseZAPI.ResetHwid();
			if (result == 0)
			{
				ToastNotification.Success("HWID reset successfully.");
			}
			else
			{
				string errorMsg = SynapseZ.SynapseZAPI.GetLatestErrorMessage();
				ToastNotification.Error(errorMsg);
			}
		}
		catch (Exception ex)
		{
			ToastNotification.Error(ex.Message);
		}
		finally
		{
			ToggleLoading(show: false, hasOverlay: true);
		}
	}

	private string NormalizeTimestamp(long timestamp)
	{
		return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.ToString("MMMM dd, yyyy");
	}

	private void ToggleLoading(bool show, bool hasOverlay = false)
	{
		DoubleAnimation animation = new DoubleAnimation
		{
			To = (show ? 1 : 0),
			Duration = TimeSpan.FromSeconds(0.25),
			EasingFunction = new QuarticEase
			{
				EasingMode = ((!show) ? EasingMode.EaseOut : EasingMode.EaseIn)
			}
		};
		if (hasOverlay)
		{
			LoadingOverlay.Background = Brushes.Transparent;
		}
		else
		{
			LoadingOverlay.Background = Brushes.Black;
		}
		LoadingOverlay.IsHitTestVisible = show;
		BlurEffect.BeginAnimation(BlurEffect.RadiusProperty, animation);
		LoadingOverlay.BeginAnimation(UIElement.OpacityProperty, animation);
	}

	private void ToggleAds(bool show)
	{
		AdContainer.Visibility = ((!show) ? Visibility.Collapsed : Visibility.Visible);
		base.MinHeight = (show ? 570 : 446);
		BackgroundBorder.Margin = new Thickness(4.0, 0.0, 4.0, show ? 124 : 0);
		ManagerOverlayContainer.Margin = new Thickness(4.0, 0.0, 4.0, show ? 124 : 0);
		LicenseOverlayContainer.Margin = new Thickness(4.0, 0.0, 4.0, show ? 124 : 0);
		ToastNotification.Margin = new Thickness(4.0, 25.0, 4.0, show ? 124 : 0);
		UnhandledExceptionError.Margin = new Thickness(4.0, 0.0, 4.0, show ? 124 : 0);
		LoadingOverlay.Margin = new Thickness(4.0, 0.0, 4.0, show ? 124 : 0);
		base.Left = SystemParameters.PrimaryScreenWidth / 2.0 - base.Width / 2.0;
		base.Top = SystemParameters.PrimaryScreenHeight / 2.0 - base.Height / 2.0;
	}

	private void OnceReady()
	{
	}

	private void BackToLogin()
	{
	}

	private void SwitchPage(PageState PageState)
	{
		if (CurrentPageSelection != PageState)
		{
			EditorToggleButton.IsChecked = false;
			ScriptCloudToggleButton.IsChecked = false;
			SettingsToggleButton.IsChecked = false;
			switch (PageState)
			{
			case PageState.Editor:
				MoveSelectionBar(51.0);
				CurrentPageSelection = PageState.Editor;
				break;
			case PageState.ScriptCloud:
				MoveSelectionBar(105.0);
				CurrentPageSelection = PageState.ScriptCloud;
				break;
			case PageState.Settings:
				MoveSelectionBar(159.0);
				CurrentPageSelection = PageState.Settings;
				break;
			default:
				throw new NotImplementedException();
			}
		}
	}

	private void MoveSelectionBar(double Offset)
	{
		ThicknessAnimation animation = new ThicknessAnimation
		{
			To = new Thickness(0.0, Offset, 0.0, 0.0),
			Duration = TimeSpan.FromSeconds(1.5),
			EasingFunction = new ElasticEase
			{
				Springiness = 17.5
			}
		};
		PageSelectionBar.BeginAnimation(FrameworkElement.MarginProperty, animation);
	}

	private void SwitchOverlay(OverlayState OverlayState)
	{
		if (CurrentOverlaySelection != OverlayState)
		{
			DoubleAnimation animation = new DoubleAnimation
			{
				To = 2.0,
				Duration = TimeSpan.FromSeconds(0.25),
				EasingFunction = new QuarticEase()
			};
			DoubleAnimation animation2 = new DoubleAnimation
			{
				To = 1.0,
				Duration = TimeSpan.FromSeconds(0.25)
			};
			switch (OverlayState)
			{
			case OverlayState.Manager:
				ManagerOverlayContainer.IsHitTestVisible = true;
				ManagerOverlayContainer.BeginAnimation(UIElement.OpacityProperty, animation2);
				break;
			case OverlayState.License:
				LicenseOverlayContainer.IsHitTestVisible = true;
				LicenseOverlayContainer.BeginAnimation(UIElement.OpacityProperty, animation2);
				break;
			}
			BlurEffect.BeginAnimation(BlurEffect.RadiusProperty, animation);
			CurrentOverlaySelection = OverlayState;
		}
	}

	private async void PopulateSearchResult(SearchResult result)
	{
		if (result == null)
		{
			return;
		}
		int currentPage = result.NextPage - 1;
		if (currentPage == -1)
		{
			currentPage = ((result.Scripts.Count > 0) ? 1 : 0);
		}
		ShowOrHideScriptCloudNoResult(result.Scripts.Count < 1);
		ShowOrHideScriptCloudNavigationButtons(result, currentPage);
		CurrentPageText.Text = $"{currentPage} of {result.TotalPages}";
		ScriptList.Children.Clear();
		foreach (WaveWindows.Interfaces.Script item in result.Scripts)
		{
			WaveWindows.Controls.Card.Script script2 = new WaveWindows.Controls.Card.Script
			{
				Title = item.Title,
				Description = item.Game.Name
			};
			WaveWindows.Controls.Card.Script script3 = script2;
			script3.ImageSource = await GetImageAsync(item.Game.Image);
			WaveWindows.Controls.Card.Script script = script2;
			script.MouseDown += async delegate
			{
				WaveWindows.Interfaces.Script context = await ScriptBloxInterface.GetScript(item);
				if (!context.Verified)
				{
					ToastNotification.Warning("Caution: Security Alert", "The selected context has not been verified.");
				}
				SwitchPage(PageState.Editor);
				EditorTabControl.AddTab(item.Title, context.Source);
			};
			ScriptList.Children.Add(script);
		}
	}

	private async void NavigateSearchResult(SearchResult result, int page)
	{
		if (result != null && page >= 1)
		{
			SearchResult newSearchResult = await ScriptBloxInterface.Search(SearchQuery, page);
			PopulateSearchResult(newSearchResult);
			SearchResult = newSearchResult;
		}
	}

	private void ShowOrHideScriptCloudNoResult(bool Show)
	{
		DoubleAnimation animation = new DoubleAnimation
		{
			To = (Show ? 1 : 0),
			Duration = TimeSpan.FromSeconds(0.25),
			EasingFunction = new QuarticEase()
		};
		NoResultSection.BeginAnimation(UIElement.OpacityProperty, animation);
	}

	private void ShowOrHideScriptCloudNavigationButtons(SearchResult result, int currentPage)
	{
		if (result != null)
		{
			bool flag = currentPage > 1;
			bool flag2 = currentPage < result.TotalPages;
			DoubleAnimation animation = new DoubleAnimation
			{
				To = (flag ? 1.0 : 0.5),
				Duration = TimeSpan.FromSeconds(0.25),
				EasingFunction = new QuarticEase()
			};
			DoubleAnimation animation2 = new DoubleAnimation
			{
				To = (flag2 ? 1.0 : 0.5),
				Duration = TimeSpan.FromSeconds(0.25),
				EasingFunction = new QuarticEase()
			};
			PreviousPageButton.IsHitTestVisible = flag;
			NextPageButton.IsHitTestVisible = flag2;
			PreviousPageButton.BeginAnimation(UIElement.OpacityProperty, animation);
			NextPageButton.BeginAnimation(UIElement.OpacityProperty, animation2);
		}
	}

	private Task<ImageSource> GetImageAsync(string image)
	{
		if (string.IsNullOrEmpty(image))
		{
			return Task.FromResult<ImageSource>(null);
		}
		string url = ScriptBloxInterface.GetImageUrl(image);
		if (url == null)
		{
			return Task.FromResult<ImageSource>(null);
		}
		return Task.FromResult(ScriptBloxInterface.ToImage(url));
	}

	private void HideManagerOrLicenseOverlay()
	{
		if (CurrentOverlaySelection == OverlayState.Manager || CurrentOverlaySelection == OverlayState.License)
		{
			DoubleAnimation animation = new DoubleAnimation
			{
				To = 0.0,
				Duration = TimeSpan.FromSeconds(0.5),
				EasingFunction = new QuarticEase()
			};
			DoubleAnimation animation2 = new DoubleAnimation
			{
				To = 0.0,
				Duration = TimeSpan.FromSeconds(0.25)
			};
			switch (CurrentOverlaySelection)
			{
			case OverlayState.Manager:
				ManagerToggleButton.IsChecked = false;
				ManagerOverlayContainer.IsHitTestVisible = false;
				ManagerOverlayContainer.BeginAnimation(UIElement.OpacityProperty, animation2);
				break;
			case OverlayState.License:
				LicenseToggleButton.IsChecked = false;
				LicenseOverlayContainer.IsHitTestVisible = false;
				LicenseOverlayContainer.BeginAnimation(UIElement.OpacityProperty, animation2);
				break;
			}
			BlurEffect.BeginAnimation(BlurEffect.RadiusProperty, animation);
			CurrentOverlaySelection = OverlayState.None;
		}
	}

	static MainWindow()
	{
		CurrentPageSelectionProperty = DependencyProperty.Register("CurrentPageSelection", typeof(PageState), typeof(MainWindow));
	}

    private void InjectButton_Click(object sender, RoutedEventArgs e)
    {
		var processes = SynapseZ.SynapseZAPI.GetRobloxProcesses();
		if (processes.Length == 0)
		{
			ToastNotification.Info("No Roblox instances found. Please start Roblox first.");
			return;
		}
		
		var injected = SynapseZ.SynapseZAPI.GetSynzRobloxInstances();
		ToastNotification.Info($"Found {processes.Length} Roblox instance(s), {injected.Count} injected with SynapseZ.");
    }

	private static string GetSynapseZPath()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Synapse Z");
	}

	private static string GetScriptsPath()
	{
		return Path.Combine(GetSynapseZPath(), "scripts");
	}

	private static string GetAutoexecPath()
	{
		return Path.Combine(GetSynapseZPath(), "autoexec");
	}

	private static string GetAutoexecDisabledPath()
	{
		return Path.Combine(GetSynapseZPath(), "autoexec_disabled");
	}

	private void OpenSynapseZFolder_Click(object sender, RoutedEventArgs e)
	{
		string path = GetSynapseZPath();
		if (!Directory.Exists(path))
		{
			Directory.CreateDirectory(path);
		}
		Process.Start("explorer.exe", path);
	}

	private void LoadLocalScripts()
	{
		string scriptsPath = GetScriptsPath();
		if (!Directory.Exists(scriptsPath))
		{
			return;
		}

		ScriptSidebarList.Children.Clear();
		
		var files = Directory.GetFiles(scriptsPath)
			.Where(f => f.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) || 
			            f.EndsWith(".luau", StringComparison.OrdinalIgnoreCase))
			.OrderBy(f => Path.GetFileName(f))
			.ToList();
		
		if (files.Count == 0)
		{
			System.Windows.Controls.TextBlock noScripts = new System.Windows.Controls.TextBlock
			{
				Text = "No scripts found",
				Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
				FontFamily = new FontFamily("Izmir"),
				FontSize = 10,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				Margin = new Thickness(0, 10, 0, 0)
			};
			ScriptSidebarList.Children.Add(noScripts);
			return;
		}
		
		foreach (string file in files)
		{
			string fileName = Path.GetFileName(file);
			CreateScriptSidebarItem(ScriptSidebarList, fileName, file);
		}
	}

	private void LoadAutoexecScripts()
	{
		string autoexecPath = GetAutoexecPath();
		string disabledPath = GetAutoexecDisabledPath();

		AutoexecSidebarList.Children.Clear();

		var activeFiles = Directory.Exists(autoexecPath)
			? Directory.GetFiles(autoexecPath)
				.Where(f => f.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
				            f.EndsWith(".luau", StringComparison.OrdinalIgnoreCase))
				.OrderBy(f => Path.GetFileName(f))
				.ToList()
			: new List<string>();

		var disabledFiles = Directory.Exists(disabledPath)
			? Directory.GetFiles(disabledPath)
				.Where(f => f.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
				            f.EndsWith(".luau", StringComparison.OrdinalIgnoreCase))
				.OrderBy(f => Path.GetFileName(f))
				.ToList()
			: new List<string>();

		if (activeFiles.Count == 0 && disabledFiles.Count == 0)
		{
			System.Windows.Controls.TextBlock noScripts = new System.Windows.Controls.TextBlock
			{
				Text = "No autoexec scripts",
				Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
				FontFamily = new FontFamily("Izmir"),
				FontSize = 10,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
				Margin = new Thickness(0, 10, 0, 0)
			};
			AutoexecSidebarList.Children.Add(noScripts);
			return;
		}

		foreach (string file in activeFiles)
		{
			string fileName = Path.GetFileName(file);
			CreateAutoexecSidebarItem(AutoexecSidebarList, fileName, file, disabled: false);
		}

		foreach (string file in disabledFiles)
		{
			string fileName = Path.GetFileName(file);
			CreateAutoexecSidebarItem(AutoexecSidebarList, fileName, file, disabled: true);
		}
	}

	private void CreateScriptSidebarItem(System.Windows.Controls.Panel panel, string name, string path)
	{
		System.Windows.Controls.Border border = new System.Windows.Controls.Border
		{
			Background = new SolidColorBrush(Color.FromRgb(29, 29, 30)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(44, 44, 45)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(4),
			Margin = new Thickness(0, 0, 0, 3),
			Padding = new Thickness(8, 7, 8, 7),
			Cursor = Cursors.Hand
		};
		
		System.Windows.Controls.TextBlock textBlock = new System.Windows.Controls.TextBlock
		{
			Text = name,
			Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
			FontFamily = new FontFamily("Inter"),
			FontSize = 10,
			TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
		};
		
		border.Child = textBlock;
		
		border.MouseEnter += (s, e) =>
		{
			border.Background = new SolidColorBrush(Color.FromRgb(42, 42, 46));
			border.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 64));
			textBlock.Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220));
		};
		
		border.MouseLeave += (s, e) =>
		{
			border.Background = new SolidColorBrush(Color.FromRgb(29, 29, 30));
			border.BorderBrush = new SolidColorBrush(Color.FromRgb(44, 44, 45));
			textBlock.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160));
		};
		
		border.MouseLeftButtonDown += (s, e) =>
		{
			try
			{
				string content = File.ReadAllText(path);
				EditorTabControl.AddTab(name, content);
			}
			catch (Exception ex)
			{
				ToastNotification.Error("Failed to open script", ex.Message);
			}
		};
		
		panel.Children.Add(border);
	}

	private void CreateAutoexecSidebarItem(System.Windows.Controls.Panel panel, string name, string path, bool disabled)
	{
		System.Windows.Controls.Grid grid = new System.Windows.Controls.Grid();
		grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

		System.Windows.Controls.Border border = new System.Windows.Controls.Border
		{
			Background = new SolidColorBrush(disabled ? Color.FromRgb(20, 20, 21) : Color.FromRgb(29, 29, 30)),
			BorderBrush = new SolidColorBrush(disabled ? Color.FromRgb(35, 35, 36) : Color.FromRgb(44, 44, 45)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(4),
			Margin = new Thickness(0, 0, 0, 3),
			Padding = new Thickness(8, 7, 8, 7),
			Cursor = Cursors.Hand
		};

		System.Windows.Controls.TextBlock textBlock = new System.Windows.Controls.TextBlock
		{
			Text = name,
			Foreground = new SolidColorBrush(disabled ? Color.FromRgb(80, 80, 80) : Color.FromRgb(160, 160, 160)),
			FontFamily = new FontFamily("Inter"),
			FontSize = 10,
			TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
		};

		border.Child = textBlock;
		System.Windows.Controls.Grid.SetColumn(border, 0);
		grid.Children.Add(border);

		System.Windows.Controls.Border toggleBorder = new System.Windows.Controls.Border
		{
			Background = new SolidColorBrush(Color.FromRgb(29, 29, 30)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(44, 44, 45)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(4),
			Width = 26,
			Height = 26,
			Margin = new Thickness(3, 0, 0, 3),
			Cursor = Cursors.Hand,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = System.Windows.VerticalAlignment.Top
		};

		System.Windows.Controls.TextBlock toggleIcon = new System.Windows.Controls.TextBlock
		{
			Text = disabled ? "+" : "/",
			Foreground = new SolidColorBrush(disabled ? Color.FromRgb(100, 100, 100) : Color.FromRgb(160, 160, 160)),
			FontFamily = new FontFamily("Inter"),
			FontSize = 12,
			FontWeight = FontWeights.Bold,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = System.Windows.VerticalAlignment.Center
		};

		toggleBorder.Child = toggleIcon;
		System.Windows.Controls.Grid.SetColumn(toggleBorder, 1);
		grid.Children.Add(toggleBorder);

		border.MouseEnter += (s, e) =>
		{
			border.Background = new SolidColorBrush(disabled ? Color.FromRgb(28, 28, 29) : Color.FromRgb(42, 42, 46));
			border.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 64));
			textBlock.Foreground = new SolidColorBrush(disabled ? Color.FromRgb(100, 100, 100) : Color.FromRgb(220, 220, 220));
			toggleBorder.Background = new SolidColorBrush(Color.FromRgb(42, 42, 46));
			toggleBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 64));
		};

		border.MouseLeave += (s, e) =>
		{
			border.Background = new SolidColorBrush(disabled ? Color.FromRgb(20, 20, 21) : Color.FromRgb(29, 29, 30));
			border.BorderBrush = new SolidColorBrush(disabled ? Color.FromRgb(35, 35, 36) : Color.FromRgb(44, 44, 45));
			textBlock.Foreground = new SolidColorBrush(disabled ? Color.FromRgb(80, 80, 80) : Color.FromRgb(160, 160, 160));
			toggleBorder.Background = new SolidColorBrush(Color.FromRgb(29, 29, 30));
			toggleBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(44, 44, 45));
		};

		border.MouseLeftButtonDown += (s, e) =>
		{
			try
			{
				string content = File.ReadAllText(path);
				EditorTabControl.AddTab(name, content);
			}
			catch (Exception ex)
			{
				ToastNotification.Error("Failed to open script", ex.Message);
			}
		};

		toggleBorder.MouseEnter += (s, e) =>
		{
			toggleBorder.Background = new SolidColorBrush(Color.FromRgb(55, 55, 60));
			toggleBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75));
			toggleIcon.Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220));
		};

		toggleBorder.MouseLeave += (s, e) =>
		{
			toggleBorder.Background = new SolidColorBrush(Color.FromRgb(29, 29, 30));
			toggleBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(44, 44, 45));
			toggleIcon.Foreground = new SolidColorBrush(disabled ? Color.FromRgb(100, 100, 100) : Color.FromRgb(160, 160, 160));
		};

		toggleBorder.MouseLeftButtonDown += (s, e) =>
		{
			e.Handled = true;
			try
			{
				string autoexecPath = GetAutoexecPath();
				string disabledPath = GetAutoexecDisabledPath();

				if (disabled)
				{
					if (!Directory.Exists(autoexecPath))
						Directory.CreateDirectory(autoexecPath);

					string destPath = Path.Combine(autoexecPath, name);
					File.Move(path, destPath);
				}
				else
				{
					if (!Directory.Exists(disabledPath))
						Directory.CreateDirectory(disabledPath);

					string destPath = Path.Combine(disabledPath, name);
					File.Move(path, destPath);
				}

				LoadAutoexecScripts();
			}
			catch (Exception ex)
			{
				ToastNotification.Error("Failed to toggle script", ex.Message);
			}
		};

		panel.Children.Add(grid);
	}

	private void LoadScriptsFolder_Click(object sender, RoutedEventArgs e)
	{
		LoadLocalScripts();
	}

	private void LoadAutoexecFolder_Click(object sender, RoutedEventArgs e)
	{
		LoadAutoexecScripts();
	}
}
