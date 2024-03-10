﻿using System.Numerics;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.CriminalRecords;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.StatusIcon;

namespace Content.Client.CartridgeLoader.Cartridges;

[GenerateTypedNameReferences]
public sealed partial class CriminalRecordsCartridgeUiFragment : BoxContainer
{
    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _prototypeManager;
    private readonly SpriteSystem _spriteSystem;
    public CriminalRecordsCartridgeUiFragment()
    {
        RobustXamlLoader.Load(this);

        _entManager = IoCManager.Resolve<IEntityManager>();
        _prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        _spriteSystem = _entManager.System<SpriteSystem>();

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;

        UpdateState(new CriminalRecordsCartridgeUiState(new List<(GeneralStationRecord, CriminalRecord)>()));
    }


    public void UpdateState(CriminalRecordsCartridgeUiState state)
    {
        foreach (var (stationRecord, criminalRecord) in state.Criminals)
        {
            AddCriminal(stationRecord, criminalRecord);
        }
    }

    private void AddCriminal(GeneralStationRecord stationRecord, CriminalRecord criminalRecord)
    {
        var nameLabel = new Label()
        {
            Text = stationRecord.Name,
            HorizontalExpand = true,
            ClipText = true,
        };

        var jobContainer = new BoxContainer()
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        var statusLabel = new Label()
        {
            Text = Enum.GetName(typeof(SecurityStatus), criminalRecord.Status),
            HorizontalExpand = true,
            ClipText = true,
        };

        Criminals.AddChild(nameLabel);
        Criminals.AddChild(jobContainer);
        Criminals.AddChild(statusLabel);

        var jobLabel = new Label()
        {
            Text = stationRecord.JobTitle,
            HorizontalExpand = true,
            ClipText = true,
        };


        if (!_prototypeManager.TryIndex<StatusIconPrototype>(
              stationRecord.JobIcon,
              out var proto
              ))
        {
            jobContainer.AddChild(jobLabel);
            return;
        }

        var jobIcon = new TextureRect()
        {
            TextureScale = new Vector2(2f, 2f),
            VerticalAlignment = VAlignment.Center,
            Texture = _spriteSystem.Frame0(proto.Icon),
            Margin = new Thickness(5, 0, 5, 0),
        };

        jobContainer.AddChild(jobIcon);
        jobContainer.AddChild(jobLabel);
    }
}
