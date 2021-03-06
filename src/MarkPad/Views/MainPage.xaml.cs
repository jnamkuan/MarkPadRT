﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using GalaSoft.MvvmLight.Messaging;
using MarkPad.Messages;
using MarkPad.ViewModel;
using Windows.System;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace MarkPad.Views
{
	public sealed partial class MainPage
	{
		private MainViewModel ViewModel { get { return (MainViewModel)DataContext; } }
		private readonly DispatcherTimer _timer = new DispatcherTimer();
		private readonly DispatcherTimer _closeWebView = new DispatcherTimer();
		private readonly WebViewBrush _webBrush;
		private bool _isCtrlKeyPressed;

		public MainPage()
		{
			InitializeComponent();
			_timer.Interval = new TimeSpan(0, 0, 0, 0, 500);
			_timer.Tick += TimerTick;
			_closeWebView.Interval = new TimeSpan(0, 0, 0, 0, 500);
			_closeWebView.Tick += (s, e) =>
				{
					_closeWebView.Stop();
					wv.Visibility = Visibility.Visible;
					webRectangle.Visibility = Visibility.Collapsed;

				};
			wv.AllowedScriptNotifyUris = WebView.AnyScriptNotifyUri;
			wv.LoadCompleted += wv_LoadCompleted;
			BottomAppBar.Opened += (s, e) => SwitchWebViewForWebViewBrush();
			BottomAppBar.Closed += (s, e) => SwitchWebViewBrushForWebView();

			_webBrush = new WebViewBrush();
			_webBrush.SetSource(wv);
			webRectangle.Fill = _webBrush;

			Messenger.Default.Register<HideWebviewMessage>(this, o => SwitchWebViewForWebViewBrush());
			Messenger.Default.Register<ShowWebViewMessage>(this, o => SwitchWebViewBrushForWebView());
			Loaded += (s, e) =>
						  {
							  Editor.AddHandler(RightTappedEvent, (RightTappedEventHandler)RichEditBox_Tapped, true);
							  VisualStateManager.GoToState(this, ViewModel.Distraction ? "FullScreenLandscapeOrWide" : "DistractionFree", false);
						  };
		}

		private void RichEditBox_Tapped(object sender, RightTappedRoutedEventArgs e)
		{
			BottomAppBar.IsOpen = true;
		}


		protected override void OnKeyDown(KeyRoutedEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.Key == VirtualKey.Control)
			{
				_isCtrlKeyPressed = true;
				return;
			}

			if (!_isCtrlKeyPressed)
				return;

			switch (e.Key)
			{
				case VirtualKey.N:
					SafelyExecute(ViewModel.NewCommand);
					break;
				case VirtualKey.O:
					SafelyExecute(ViewModel.OpenCommand);
					break;
				case VirtualKey.S:
					SafelyExecute(ViewModel.SaveCommand);
					break;
			}
		}

		private void SafelyExecute(System.Windows.Input.ICommand command)
		{
			if (command.CanExecute(null))
				command.Execute(null);
		}

		protected override void OnKeyUp(KeyRoutedEventArgs e)
		{
			base.OnKeyUp(e);
			if (e.Key == VirtualKey.Control)
				_isCtrlKeyPressed = false;
		}


		private void SwitchWebViewBrushForWebView()
		{
			if (_closeWebView.IsEnabled)
				_closeWebView.Stop();
			_closeWebView.Start();
		}

		private void SwitchWebViewForWebViewBrush()
		{
			if (_closeWebView.IsEnabled)
				_closeWebView.Stop();

			if (_webBrush != null)
				_webBrush.Redraw();
			webRectangle.Visibility = Visibility.Visible;
			wv.Visibility = Visibility.Collapsed;
		}

		void wv_LoadCompleted(object sender, NavigationEventArgs e)
		{
			Editor.Focus(FocusState.Keyboard);

		}

		void TimerTick(object sender, object e)
		{
			_timer.Stop();
			if (ViewModel.SelectedDocument != null)
				wv.NavigateToString(ViewModel.Transform(true));
		}

		private void TextChanged(object sender, RoutedEventArgs e)
		{
			if (ViewModel.SelectedDocument != null)
			{
				string foo;
				Editor.Document.GetText(TextGetOptions.UseCrlf, out foo);
				ViewModel.SelectedDocument.Text = foo;
			}
			_timer.Stop();
			_timer.Start();
		}

		private void DocumentsSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			Editor.Document.SetText(TextSetOptions.None, ViewModel.SelectedDocument != null ? ViewModel.SelectedDocument.Text : string.Empty);
		}

		private void LinkClicked(object sender, RoutedEventArgs e)
		{
			string selection;
			Editor.Document.Selection.GetText(TextGetOptions.None, out selection);

			var dialog = new LinkDialog(selection);
			var viewModel = dialog.DataContext as LinkViewModel;
			dialog.Added = () => TransformText(s => string.Format("[{0}]({1})", viewModel.DisplayText, viewModel.LinkAddress));

			dialog.ShowAsync();
		}

		private void BoldClicked(object sender, RoutedEventArgs e)
		{
			TransformText(s => "**" + s + "**");
		}

		private void ItalicClicked(object sender, RoutedEventArgs e)
		{
			TransformText(s => "*" + s + "*");
		}

		private void TransformText(Func<string, string> transform)
		{
			string selection;
			Editor.Document.Selection.GetText(TextGetOptions.None, out selection);
			selection = transform(selection);
			Editor.Document.Selection.SetText(TextSetOptions.None, selection);
		}

		protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
		{

		}

		protected override void SaveState(Dictionary<String, Object> pageState)
		{

		}


		private void BtnPreviewClicked(object sender, RoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "Snapped_Preview", false);
		}

		private void BtnEditorClicked(object sender, RoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "Snapped", false);
		}

		private void DistractionToggled(object sender, RoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, ViewModel.Distraction ? "DistractionFree" : "FullScreenLandscapeOrWide", false);
			ViewModel.Distraction = !ViewModel.Distraction;
		}

		private bool UsingLogicalPageNavigation(ApplicationViewState? viewState = null)
		{
			if (viewState == null)
				viewState = ApplicationView.Value;

			return viewState == ApplicationViewState.FullScreenPortrait || viewState == ApplicationViewState.Snapped;
		}

		protected override string DetermineVisualState(ApplicationViewState viewState)
		{
			var logicalPageBack = UsingLogicalPageNavigation(viewState);// && this.itemListView.SelectedItem != null;
			var physicalPageBack = Frame != null && Frame.CanGoBack;
			DefaultViewModel["CanGoBack"] = logicalPageBack || physicalPageBack;

			if (viewState == ApplicationViewState.Filled || viewState == ApplicationViewState.FullScreenLandscape)
			{
				var windowWidth = Window.Current.Bounds.Width;
				if (windowWidth >= 1366)
					return "FullScreenLandscapeOrWide";
				return "FilledOrNarrow";
			}

			var defaultStateName = base.DetermineVisualState(viewState);
			return defaultStateName;
			return logicalPageBack ? defaultStateName + "_Detail" : defaultStateName;
		}

		private void EditorKeyDown(object sender, KeyRoutedEventArgs e)
		{
			if (e.Key != VirtualKey.Tab)
				return;

			var t = sender as RichEditBox;
			if (t == null)
				return;

			string selection;
			t.Document.Selection.GetText(TextGetOptions.None, out selection);
			t.Document.Selection.SetText(TextSetOptions.None, "\t" + selection);
			t.Document.Selection.StartPosition += 1;
			t.Document.Selection.EndPosition = t.Document.Selection.StartPosition;
			e.Handled = true;
		}

		/// <summary>
		/// Invoke script from webview
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void Wv_OnScriptNotify(object sender, NotifyEventArgs e)
		{
			try
			{
				// invoke IE.
				string data = e.Value;
				const string urlPrefix = "url:";
				if (data.ToUpper().StartsWith(urlPrefix.ToUpper()))
				{
					// if it is not a valid url type, add http prefix.
					var url = data.Substring(urlPrefix.Length);
					if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute) || !url.Contains("://"))
						url = "http://" + url;
					await Launcher.LaunchUriAsync(new Uri(url, UriKind.RelativeOrAbsolute));
				}
			}
			catch (Exception)
			{
				// Could not build a proper Uri. Abandon.
			}
		}
	}
}
