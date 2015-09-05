using System;
using System.Threading.Tasks;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace UnofficialSamsungRemote
{
    public sealed partial class TvKeyControl : UserControl
    {
        public EKey MyKey { get; set; }
        //public string KeyName;
        public BitmapImage NormalImage { get; private set; }
        public BitmapImage PressedImage { get; private set; }
        public string ImageLocation { get; set; }
        public string Text { get; set; }
        public bool AllowRepeats { get; set; }

        public delegate void KeyPressedDelegate(object sender, EventArgs args);
        public event KeyPressedDelegate OnKeyPressed;

        public delegate void KeyRepeatedDelegate(object sender, EventArgs args);
        public event KeyRepeatedDelegate OnKeyRepeated;

        private DateTime PressStartTime;
        private DateTime LastRepeatTime;
        private const int RepeatDelayMs = 500;
        private const int RepeatRateMs = 350;

        public TvKeyControl()
        {
            this.InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ImageLocation))
            {
                NormalImage = new BitmapImage(new Uri("ms-appx:///" + ImageLocation, UriKind.Absolute));
                PressedImage = new BitmapImage(new Uri("ms-appx:///" + ImageLocation.Replace(System.IO.Path.GetExtension(ImageLocation), "_focus" + System.IO.Path.GetExtension(ImageLocation)), UriKind.Absolute));

                Img.Source = NormalImage;
            }
            else
            {
                Img.Source = null;
            }

            if (!string.IsNullOrWhiteSpace(Text))
            {
                Contents.Text = Text;
            }
        }

        private async void CheckKeyRepeat()
        {
            if (AllowRepeats)
            {
                while (PressStartTime > DateTime.MinValue)
                {
                    if ((DateTime.Now - PressStartTime).TotalMilliseconds > RepeatDelayMs)
                    {
                        if (LastRepeatTime == DateTime.MinValue
                            || (DateTime.Now - LastRepeatTime).TotalMilliseconds > RepeatRateMs)
                        {
                            LastRepeatTime = DateTime.Now;
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                ConditionalSendMyKey();

                                if (OnKeyRepeated != null)
                                {
                                    OnKeyRepeated(this, EventArgs.Empty);
                                }
                            });
                        }
                    }

                    await Task.Delay(33);
                }
            }
        }

        private void UserControl_Tap(object sender, TappedRoutedEventArgs e)
        {
            ConditionalSendMyKey();

            if (OnKeyPressed != null)
            {
                OnKeyPressed(this, EventArgs.Empty);
            }
        }

        private void ConditionalSendMyKey()
        {
            ConditionalConfirmationVibration();
            if (MyKey != EKey.KEY_INVALID)
            {
                MainPage.SendKey(MyKey);
            }
        }

        public static void ConditionalConfirmationVibration()
        {
            if (Settings.LoadedSettings.bShouldVibrateOnKeyPress)
            {
                //VibrateController.Default.Start(TimeSpan.FromMilliseconds(50));
            }
        }

        private async void UserControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Img.Source = PressedImage;

            if (AllowRepeats)
            {
                PressStartTime = DateTime.Now;
                await ThreadPool.RunAsync((workItem) => { CheckKeyRepeat(); });
            }
        }

        private void UserControl_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Img.Source = NormalImage;
            PressStartTime = DateTime.MinValue;
        }
    }
}
