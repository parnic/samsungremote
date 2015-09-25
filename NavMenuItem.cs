using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace UnofficialSamsungRemote
{
    /// <summary>
    /// Data to represent an item in the nav menu.
    /// </summary>
    public class NavMenuItem
    {
        public string Label { get; set; }
        public Symbol Symbol { get; set; }
        public char SymbolAsChar
        {
            get
            {
                return (char)this.Symbol;
            }
        }

        public Type DestPage { get; set; }
        public object Arguments { get; set; }

        public event RoutedEventHandler Selected;

        public bool OnInvoked(Frame frame, object sender)
        {
            if (DestPage != null)
            {
                if (DestPage != frame.CurrentSourcePageType)
                {
                    frame.Navigate(DestPage, Arguments);
                    return true;
                }
            }
            else if (Selected != null)
            {
                Selected(sender, null);
            }

            return false;
        }
    }
}
