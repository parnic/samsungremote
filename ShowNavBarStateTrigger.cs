using System;
using AdaptiveTriggerLibrary.ConditionModifiers.ComparableModifiers;
using AdaptiveTriggerLibrary.Triggers;
using Windows.UI.Xaml;

namespace UnofficialSamsungRemote
{
    class ShowNavBarStateTrigger : AdaptiveTriggerBase<bool, IComparableModifier>, IAdaptiveTrigger
    {
        public ShowNavBarStateTrigger()
            : base(new EqualToModifier())
        {
            Settings.LoadedSettings.PropertyChanged += LoadedSettings_PropertyChanged;
            CurrentValue = Settings.LoadedSettings.bAlwaysShowNavBar;
        }

        private void LoadedSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.LoadedSettings.bAlwaysShowNavBar))
            {
                CurrentValue = Settings.LoadedSettings.bAlwaysShowNavBar;
            }
        }
    }
}
