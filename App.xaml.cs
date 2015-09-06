﻿using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace UnofficialSamsungRemote
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private static MainViewModel viewModel = null;

        public static MainViewModel ViewModel
        {
            get
            {
                // Delay creation of the view model until necessary
                if (viewModel == null)
                {
                    viewModel = new MainViewModel();
                }

                return viewModel;
            }
        }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.Resuming += App_Resuming;
            this.UnhandledException += App_UnhandledException;
        }

        private void App_Resuming(object sender, object e)
        {
            MainPage.NotifyAppActivated();
        }

        private void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                System.Diagnostics.Debugger.Break();
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PreviousExecutionState == ApplicationExecutionState.Terminated
                    || e.PreviousExecutionState == ApplicationExecutionState.ClosedByUser
                    || e.PreviousExecutionState == ApplicationExecutionState.NotRunning)
            {
                var settings = ApplicationData.Current.RoamingSettings.Values;
                Settings.LoadedSettings.bShouldVibrateOnKeyPress = settings.ContainsKey(nameof(Settings.LoadedSettings.bShouldVibrateOnKeyPress)) ? (bool)settings[nameof(Settings.LoadedSettings.bShouldVibrateOnKeyPress)] : true;

                Settings.LoadedSettings.OnLoaded();

                MainPage.NotifyAppFreshStart();
            }
            else
            {
                MainPage.NotifyAppActivated();
            }

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }
            // Ensure the current window is active
            Window.Current.Activate();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            MainPage.NotifyAppDeactivated();

            deferral.Complete();
        }

        public static void SaveSetting(string settingName, object settingValue)
        {
            var settings = ApplicationData.Current.RoamingSettings.Values;
            if (!settings.ContainsKey(settingName))
            {
                settings.Add(settingName, settingValue);
            }
            else
            {
                settings[settingName] = settingValue;
            }
        }
    }
}
