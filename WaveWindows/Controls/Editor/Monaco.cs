using System;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using WaveWindows.Interfaces;

namespace WaveWindows.Controls.Editor;

internal class Monaco : ChromiumWebBrowser
{
	internal TaskCompletionSource<bool> TaskCompletionSource = new TaskCompletionSource<bool>();

	private static string _cachedHtmlTemplate = null;

	private static string EscapeJsString(string s)
	{
		if (string.IsNullOrEmpty(s)) return "";
		return s
			.Replace("\\", "\x01ESC\x01")
			.Replace("\'", "\x01SQ\x01")
			.Replace("\r\n", "\x01NL\x01")
			.Replace("\r", "\x01NL\x01")
			.Replace("\n", "\x01NL\x01")
			.Replace("\t", "\x01TB\x01")
			.Replace("\x01ESC\x01", "\\\\")
			.Replace("\x01SQ\x01", "\\\'")
			.Replace("\x01NL\x01", "\\n")
			.Replace("\x01TB\x01", "\\t");
	}

	private static string GetMonacoHtml(string initialText)
	{
		if (_cachedHtmlTemplate == null)
		{
			_cachedHtmlTemplate = @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>body{margin:0;padding:0;overflow:hidden;background:#040007;}#container{width:100vw;height:100vh;}</style>
</head>
<body>
<div id='container'></div>
<script src='https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs/loader.min.js'></script>
<script>
require.config({paths:{'vs':'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs'}});
let editor;
require(['vs/editor/editor.main'],function(){
monaco.editor.defineTheme('synapsez-dark',{base:'vs-dark',inherit:true,colors:{'editor.background':'#1d1d1e'},rules:[{token:'keyword',foreground:'cc99cc'},{token:'string',foreground:'ddccff'},{token:'number',foreground:'f99157'},{token:'comment',foreground:'999999'}]});
editor=monaco.editor.create(document.getElementById('container'),{value:'%%INITIAL_TEXT%%',language:'lua',theme:'synapsez-dark',automaticLayout:true,minimap:{enabled:false},fontSize:14,smoothScrolling:true});
window.getText=function(){return editor.getValue()};
window.setText=function(x){editor.setValue(x||'')};
window.goToLine=function(line){editor.revealLineInCenter(line);editor.setPosition({lineNumber:line,column:1});editor.focus()};
window.updateOptions=function(opts){editor.updateOptions(typeof opts==='string'?JSON.parse(opts):opts)};
window.editorReady=true;
});
</script>
</body></html>";
		}

		string escaped = EscapeJsString(initialText ?? "");
		return _cachedHtmlTemplate.Replace("%%INITIAL_TEXT%%", escaped);
	}

	internal Monaco(string addressOrPath, string text, EditorInterface.EditorOptions editorOptions)
	{
		Monaco monaco = this;
		
		((ChromiumWebBrowser)this).BrowserSettings = (IBrowserSettings)new BrowserSettings(false)
		{
			WindowlessFrameRate = 60
		};
		
		string html = GetMonacoHtml(text);
		((ChromiumWebBrowser)this).LoadHtml(html, "http://monaco-editor/");
		
		((ChromiumWebBrowser)this).LoadingStateChanged += async delegate(object sender, LoadingStateChangedEventArgs e)
		{
			if (!e.IsLoading)
			{
				await Task.Delay(300);
				if (!monaco.TaskCompletionSource.Task.IsCompleted)
				{
					monaco.TaskCompletionSource.SetResult(result: true);
				}
				if (editorOptions != null)
				{
					monaco.UpdateOptions(editorOptions);
				}
			}
		};
	}

	internal async void SetBrowserFramerate(int framerate)
	{
		await TaskCompletionSource.Task;
		WebBrowserExtensions.GetBrowserHost((IChromiumWebBrowserBase)(object)this).WindowlessFrameRate = framerate;
	}

	internal Task<T> EvaluateScriptAsync<T>(string method)
	{
		Task<JavascriptResponse> task = WebBrowserExtensions.EvaluateScriptAsync((IChromiumWebBrowserBase)(object)this, method + "();", (TimeSpan?)null, false);
		task.Wait();
		JavascriptResponse result = task.Result;
		object obj = (result.Success ? (result.Result ?? ((object)default(T))) : result.Message);
		return Task.FromResult((T)obj);
	}

	internal async Task<string> GetText()
	{
		return await EvaluateScriptAsync<string>("window.getText");
	}

	internal void SetText(string text)
	{
		try
		{
			WebBrowserExtensions.EvaluateScriptAsync((IChromiumWebBrowserBase)(object)this, "window.setText", new object[1] { text ?? "" });
		}
		catch { }
	}

	internal void GoToLine(int line)
	{
		try
		{
			WebBrowserExtensions.EvaluateScriptAsync((IChromiumWebBrowserBase)(object)this, "window.goToLine", new object[1] { line });
		}
		catch { }
	}

	internal void SetTheme(string theme, bool _default = false)
	{
		try
		{
			WebBrowserExtensions.EvaluateScriptAsync((IChromiumWebBrowserBase)(object)this, "window.setTheme", new object[2] { theme, _default });
		}
		catch { }
	}

	internal void SetFontSize(int fontSize)
	{
		UpdateOptions(new EditorInterface.EditorOptions
		{
			FontSize = fontSize
		});
	}

	internal void SetMinimap(bool enabled)
	{
		UpdateOptions(new EditorInterface.EditorOptions
		{
			Minimap = new EditorInterface.MinimapOptions
			{
				Enabled = enabled
			}
		});
	}

	internal void SetInlayHints(bool enabled)
	{
		UpdateOptions(new EditorInterface.EditorOptions
		{
			InlayHints = new EditorInterface.InlayHintsOptions
			{
				Enabled = enabled
			}
		});
	}

	internal async void UpdateOptions(EditorInterface.EditorOptions editorOptions)
	{
		await TaskCompletionSource.Task;
		try
		{
			WebBrowserExtensions.ExecuteScriptAsync((IChromiumWebBrowserBase)(object)this, $"window.updateOptions({editorOptions})");
		}
		catch { }
	}
}
