﻿using Content.Client.Mapping.Snapping;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;

namespace Content.Client.Mapping.Tools.Widgets;

[GenerateTypedNameReferences]
public sealed partial class DrawToolWidget : UIWidget, IDrawingLikeToolConfiguration
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly List<string> InitialModes = new()
    {
        "TileSnap",
        "TileSnapQuarter",
        "TileSnapCorner",
        "TileSnapQuarterCorner",
    };

    private float _rotation;
    private Dictionary<string, SnappingModeImpl?> _modes = new();

    public float Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value % 360.0f;
            RotationEdit.OverrideValue((int)_rotation);
        }
    }

    public float RotationAdjust { get; set; } = 90.0f;
    public string Prototype { get; set; } = "WallSolid";
    public SnappingModeImpl? SnappingMode { get; private set; }

    Dictionary<string, SnappingModeImpl?> IDrawingLikeToolConfiguration.Modes => _modes;

    public DrawToolWidget()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);
        ((IDrawingLikeToolConfiguration) this).SetupModes(InitialModes);
        var idx = 0;
        foreach (var (id, _) in _modes)
        {
            SnapOptions.AddItem(id, idx);
            SnapOptions.SetItemMetadata(idx++, id); // can't store nullable metadata...
        }

        RotationEdit.ClearButtons();
        // if someone wants integer versions yell at someone to make FloatSpinBox
        RotationEdit.AddLeftButton (-45, "-45");
        RotationEdit.AddRightButton(+45, "+45");
        RotationEdit.Value = (int)_rotation;
        RotationEdit.ValueChanged += RotationEditOnValueChanged;
        RotationAdjustEdit.Value = (int)RotationAdjust;
        RotationAdjustEdit.ValueChanged += RotationAdjustEditOnValueChanged;
        PrototypeEdit.OnTextChanged += PrototypeEditOnTextChanged;
        SnapOptions.OnItemSelected += SnapOptionsOnItemSelected;
    }

    private void SnapOptionsOnItemSelected(OptionButton.ItemSelectedEventArgs obj)
    {
        SnapOptions.SelectId(obj.Id);
        var id = (string) obj.Button.SelectedMetadata!;
        SnappingMode = _modes[id];
    }

    private void PrototypeEditOnTextChanged(LineEdit.LineEditEventArgs obj)
    {
        if (!_prototype.HasIndex<EntityPrototype>(obj.Text))
            return;
        Prototype = obj.Text;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        RotationEdit.ValueChanged -= RotationEditOnValueChanged;
    }

    private void RotationEditOnValueChanged(object? sender, ValueChangedEventArgs e)
    {
        Rotation = e.Value;
    }

    private void RotationAdjustEditOnValueChanged(object? sender, ValueChangedEventArgs e)
    {
        RotationAdjust = e.Value;
    }

}
