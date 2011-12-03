using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;

namespace SamsungRemoteWP7
{
    public partial class App : Application
    {
        private static MainViewModel viewModel = null;

        /// <summary>
        /// A static ViewModel used by the views to bind against.
        /// </summary>
        /// <returns>The MainViewModel object.</returns>
        public static MainViewModel ViewModel
        {
            get
            {
                // Delay creation of the view model until necessary
                if (viewModel == null)
                    viewModel = new MainViewModel();

                return viewModel;
            }
        }

        /// <summary>
        /// Provides easy access to the root frame of the Phone Application.
        /// </summary>
        /// <returns>The root frame of the Phone Application.</returns>
        public PhoneApplicationFrame RootFrame { get; private set; }

        /// <summary>
        /// Constructor for the Application object.
        /// </summary>
        public App()
        {
            // Global handler for uncaught exceptions. 
            UnhandledException += Application_UnhandledException;

            // Standard Silverlight initialization
            InitializeComponent();

            // Phone-specific initialization
            InitializePhoneApplication();

            // Show graphics profiling information while debugging.
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // Display the current frame rate counters
                Application.Current.Host.Settings.EnableFrameRateCounter = true;

                // Show the areas of the app that are being redrawn in each frame.
                //Application.Current.Host.Settings.EnableRedrawRegions = true;

                // Enable non-production analysis visualization mode, 
                // which shows areas of a page that are handed off to GPU with a colored overlay.
                //Application.Current.Host.Settings.EnableCacheVisualization = true;

                // Disable the application idle detection by setting the UserIdleDetectionMode property of the
                // application's PhoneApplicationService object to Disabled.
                // Caution:- Use this under debug mode only. Application that disables user idle detection will continue to run
                // and consume battery power when the user is not using the phone.
                PhoneApplicationService.Current.UserIdleDetectionMode = IdleDetectionMode.Disabled;
            }
        }

        // Code to execute when the application is launching (eg, from Start)
        // This code will not execute when the application is reactivated
        private void Application_Launching(object sender, LaunchingEventArgs e)
        {
            SavedSettings.Load();
            MainPage.NotifyAppFreshStart();
            PhoneHome();
        }

        // Code to execute when the application is activated (brought to foreground)
        // This code will not execute when the application is first launched
        private void Application_Activated(object sender, ActivatedEventArgs e)
        {
            // Ensure that application state is restored appropriately
            if (!App.ViewModel.IsDataLoaded)
            {
                App.ViewModel.LoadData();
            }

            SavedSettings.Load();

            MainPage.NotifyAppActivated();
            PhoneHome(true);
        }

        // Code to execute when the application is deactivated (sent to background)
        // This code will not execute when the application is closing
        private void Application_Deactivated(object sender, DeactivatedEventArgs e)
        {
            SavedSettings.LoadedSettings.Save();
            MainPage.NotifyAppDeactivated();
        }

        // Code to execute when the application is closing (eg, user hit Back)
        // This code will not execute when the application is deactivated
        private void Application_Closing(object sender, ClosingEventArgs e)
        {
            SavedSettings.LoadedSettings.Save();
        }

        // Code to execute if a navigation fails
        private void RootFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // A navigation has failed; break into the debugger
                System.Diagnostics.Debugger.Break();
            }
        }

        // Code to execute on Unhandled Exceptions
        private void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                System.Diagnostics.Debugger.Break();
            }
        }

        private void PhoneHome(bool bActivation = false)
        {
            try
            {
                string url = "http://samsungremotewp7.perniciousgames.com/checkin.php?user=" + PhoneInfoExtendedProperties.GetWindowsLiveAnonymousID();
                if (bActivation)
                {
                    url += "&re=1";
                }
                url += "&v=" + MainPage.GetVersionNumber();

                HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(url);
                wr.BeginGetResponse(asyncRequest =>
                {
                    try
                    {
                        HttpWebRequest wr2 = asyncRequest.AsyncState as HttpWebRequest;
                        using (var response = wr2.EndGetResponse(asyncRequest))
                        {
                            try
                            {
                                string respMsg = new StreamReader(response.GetResponseStream()).ReadToEnd();
                                System.Diagnostics.Debug.WriteLine("received response from check-in server: " + respMsg);
                                if (respMsg == "nv")
                                {
                                    RootFrame.Dispatcher.BeginInvoke(() =>
                                    {
                                        if (MessageBox.Show("A new version of this app is now available! Press 'Ok' to open up the marketplace and download it.",
                                            "New version available", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                                        {
                                            ShowMyMarketplacePage();
                                        }
                                    });
                                }
                            }
                            catch (Exception)
                            {
                                System.Diagnostics.Debug.WriteLine("unable to parse check-in response");
                            }
                        }
                    }
                    catch (System.Exception)
                    {
                        System.Diagnostics.Debug.WriteLine("saying hi failed.");
                    }
                }, wr);
            }
            catch (System.Exception)
            {
                System.Diagnostics.Debug.WriteLine("asking to say hi failed.");
            }
        }

        public static void ShowMyMarketplacePage()
        {
            MarketplaceDetailTask mktp = new MarketplaceDetailTask();
            mktp.ContentType = MarketplaceContentType.Applications;
            mktp.Show();
        }

        #region Phone application initialization

        // Avoid double-initialization
        private bool phoneApplicationInitialized = false;

        // Do not add any additional code to this method
        private void InitializePhoneApplication()
        {
            if (phoneApplicationInitialized)
                return;

            // Create the frame but don't set it as RootVisual yet; this allows the splash
            // screen to remain active until the application is ready to render.
            //RootFrame = new PhoneApplicationFrame();
            RootFrame = new TransitionFrame();
            RootFrame.Navigated += CompleteInitializePhoneApplication;

            // Handle navigation failures
            RootFrame.NavigationFailed += RootFrame_NavigationFailed;

            // Ensure we don't initialize again
            phoneApplicationInitialized = true;
        }

        // Do not add any additional code to this method
        private void CompleteInitializePhoneApplication(object sender, NavigationEventArgs e)
        {
            // Set the root visual to allow the application to render
            if (RootVisual != RootFrame)
                RootVisual = RootFrame;

            // Remove this handler since it is no longer needed
            RootFrame.Navigated -= CompleteInitializePhoneApplication;
        }

        #endregion
    }
}