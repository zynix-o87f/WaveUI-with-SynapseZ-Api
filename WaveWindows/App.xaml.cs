using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CefSharp;
using CefSharp.Wpf;
using WaveWindows.Interfaces;

namespace WaveWindows;

public partial class App
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		_ = AppDomain.CurrentDomain.BaseDirectory;
		AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
		{
			InterceptException(args.ExceptionObject as Exception);
		};
		base.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs args)
		{
			InterceptException(args.Exception);
			args.Handled = true;
		};
		TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs args)
		{
			InterceptException(args.Exception);
			args.SetObserved();
		};
		try
		{
			if (Process.GetProcessesByName("WaveWindows").Length > 1)
			{
				throw new Exception("Another instance of Wave is already running.");
			}
			if (Assembly.GetEntryAssembly().Location.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
			{
				throw new Exception("Please extract the Wave archive to a folder.");
			}
			string CefSharpPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\CefSharp";
		}
		catch (Exception ex2)
		{
			Exception ex = ex2;
			InterceptException(ex);
		}
		try
		{
			// Language server disabled - no download needed
		}
		catch
		{
			// Language server launch failed - continue without it
		}
	}

	private void InterceptException(Exception ex)
	{
		if (ex != null)
		{
			Console.WriteLine(ex);
			InvokeError(ex.Message, GetUnhandledExceptionErrorType(ex));
		}
	}

	private UnhandledExceptionErrorType GetUnhandledExceptionErrorType(Exception ex)
	{
		if (ex.InnerException is FileNotFoundException)
		{
			return UnhandledExceptionErrorType.RegistryError;
		}
		return UnhandledExceptionErrorType.ApplicationError;
	}

	private void InvokeError(string message, UnhandledExceptionErrorType unhandledExceptionErrorType)
	{
		Current.Dispatcher.Invoke(delegate
		{
			dynamic val = Current.MainWindow as MainWindow;
			if (val == null)
			{
				Environment.FailFast("An unhandled exception occurred.", new Exception(message));
			}
			val.ShowUnhandledExceptionError(unhandledExceptionErrorType, message);
		});
	}
}
