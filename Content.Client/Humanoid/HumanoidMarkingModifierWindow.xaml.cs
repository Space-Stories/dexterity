using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using static Content.Shared.Humanoid.HumanoidAppearanceState;

namespace Content.Client.Humanoid;

// hack for a panel that modifies an entity's markings on demand

[GenerateTypedNameReferences]
public sealed partial class HumanoidMarkingModifierWindow : DefaultWindow
{
    public Action<MarkingSet>? OnMarkingAdded;
    public Action<MarkingSet>? OnMarkingRemoved;
    public Action<MarkingSet>? OnMarkingColorChange;
    public Action<MarkingSet>? OnMarkingRankChange;
    public Action<HumanoidVisualLayers, CustomBaseLayerInfo?>? OnLayerInfoModified;

    private readonly Dictionary<HumanoidVisualLayers, HumanoidBaseLayerModifier> _modifiers = new();

    public HumanoidMarkingModifierWindow()
    {
        RobustXamlLoader.Load(this);

        foreach (var layer in Enum.GetValues<HumanoidVisualLayers>())
        {
            var modifier = new HumanoidBaseLayerModifier(layer);
            BaseLayersContainer.AddChild(modifier);
            _modifiers.Add(layer, modifier);

            modifier.OnStateChanged += delegate
            {
                OnLayerInfoModified!(
                    layer,
                    modifier.Enabled
                        ? new CustomBaseLayerInfo(modifier.State, modifier.Color)
                        : null);
            };
        }

        MarkingPickerWidget.OnMarkingAdded += set => OnMarkingAdded!(set);
        MarkingPickerWidget.OnMarkingRemoved += set => OnMarkingRemoved!(set);
        MarkingPickerWidget.OnMarkingColorChange += set => OnMarkingColorChange!(set);
        MarkingPickerWidget.OnMarkingRankChange += set => OnMarkingRankChange!(set);
        MarkingForced.OnToggled += args => MarkingPickerWidget.Forced = args.Pressed;
        MarkingIgnoreSpecies.OnToggled += args => MarkingPickerWidget.Forced = args.Pressed;

        MarkingPickerWidget.Forced = MarkingForced.Pressed;
        MarkingPickerWidget.IgnoreSpecies = MarkingForced.Pressed;
    }

    public void SetState(
        MarkingSet markings,
        string species,
        Color skinColor, Color eyeColor, Color? hairColor, Color? facialHairColor,
        Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> info
    )
    {
        MarkingPickerWidget.SetData(markings, species,
            skinColor,
            eyeColor,
            hairColor,
            facialHairColor
        );

        foreach (var (layer, modifier) in _modifiers)
        {
            if (!info.TryGetValue(layer, out var layerInfo))
            {
                modifier.SetState(false, string.Empty, Color.White);
                continue;
            }

            modifier.SetState(true, layerInfo.ID, layerInfo.Color ?? Color.White);
        }
    }

    private sealed class HumanoidBaseLayerModifier : BoxContainer
    {
        private CheckBox _enable;
        private LineEdit _lineEdit;
        private ColorSelectorSliders _colorSliders;
        private BoxContainer _infoBox;

        public bool Enabled => _enable.Pressed;
        public string State => _lineEdit.Text;
        public Color Color => _colorSliders.Color;

        public Action? OnStateChanged;

        public HumanoidBaseLayerModifier(HumanoidVisualLayers layer)
        {
            HorizontalExpand = true;
            Orientation = LayoutOrientation.Vertical;
            var labelBox = new BoxContainer
            {
                MinWidth = 250,
                HorizontalExpand = true
            };
            AddChild(labelBox);

            labelBox.AddChild(new Label
            {
                HorizontalExpand = true,
                Text = layer.ToString()
            });
            _enable = new CheckBox
            {
                Text = "Enable",
                HorizontalAlignment = HAlignment.Right
            };

            labelBox.AddChild(_enable);
            _infoBox = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Visible = false
            };
            _enable.OnToggled += args =>
            {
                _infoBox.Visible = args.Pressed;
                OnStateChanged!();
            };

            var lineEditBox = new BoxContainer();
            lineEditBox.AddChild(new Label { Text = "Prototype id: "});
            _lineEdit = new();
            _lineEdit.OnTextEntered += args => OnStateChanged!();
            lineEditBox.AddChild(_lineEdit);
            _infoBox.AddChild(lineEditBox);

            _colorSliders = new();
            _colorSliders.OnColorChanged += color => OnStateChanged!();
            _infoBox.AddChild(_colorSliders);
            AddChild(_infoBox);
        }

        public void SetState(bool enabled, string state, Color color)
        {
            _enable.Pressed = enabled;
            _infoBox.Visible = enabled;
            _lineEdit.Text = state;
            _colorSliders.Color = color;
        }
    }
}
