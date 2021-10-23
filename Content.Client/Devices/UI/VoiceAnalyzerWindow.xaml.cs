using System;
using Content.Shared.Devices;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Localization;

namespace Content.Client.Devices.UI
{
    [GenerateTypedNameReferences]
    public partial class VoiceAnalyzerWindow : SS14Window
    {
        public VoiceAnalyzerWindow()
        {
            RobustXamlLoader.Load(this);
        }
    }
}
