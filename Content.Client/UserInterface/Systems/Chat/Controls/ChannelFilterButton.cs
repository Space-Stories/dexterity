﻿using Content.Client.Resources;
using Content.Shared.Chat;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class FilterButton : ContainerButton
    {
        private static readonly Color ColorNormal = Color.FromHex("#7b7e9e");
        private static readonly Color ColorHovered = Color.FromHex("#9699bb");
        private static readonly Color ColorPressed = Color.FromHex("#789B8C");
        private readonly TextureRect _textureRect;
        private readonly ChannelFilterPopup _chatFilterPopup;
        private IUserInterfaceManager _interfaceManager = IoCManager.Resolve<IUserInterfaceManager>();
        private ChatUIController _chatUIController;
        private const int FilterDropdownOffset = 120;
        public FilterButton()
        {
            _chatUIController = _interfaceManager.GetUIController<ChatUIController>();
            var filterTexture = IoCManager.Resolve<IResourceCache>()
                .GetTexture("/Textures/Interface/Nano/filter.svg.96dpi.png");

            // needed for same reason as ChannelSelectorButton
            Mode = ActionMode.Press;
            EnableAllKeybinds = true;

            AddChild(
                (_textureRect = new TextureRect
                {
                    Texture = filterTexture,
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center
                })
            );
            ToggleMode = true;
            OnToggled += OnFilterButtonToggled;
            _chatFilterPopup = _interfaceManager.CreateNamedPopup<ChannelFilterPopup>("ChatFilterPopup", (0, 0)) ??
                               throw new Exception("Tried to add chat filter popup while one already exists");

            _chatUIController.RegisterOnChannelsAdd(_chatFilterPopup.ShowChannels);
            _chatUIController.RegisterOnChannelsRemove(_chatFilterPopup.HideChannels);
        }

        private void OnFilterButtonToggled(ButtonToggledEventArgs args)
        {
            if (args.Pressed)
            {
                var globalPos = GlobalPosition;
                var (minX, minY) = _chatFilterPopup.MinSize;
                var box = UIBox2.FromDimensions(globalPos - (FilterDropdownOffset, 0),
                    (Math.Max(minX, _chatFilterPopup.MinWidth), minY));
                _chatFilterPopup.Open(box);
            }
            else
            {
                _chatFilterPopup.Close();
            }

        }
        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            // needed since we need EnableAllKeybinds - don't double-send both UI click and Use
            if (args.Function == EngineKeyFunctions.Use) return;
            base.KeyBindDown(args);
        }

        private void UpdateChildColors()
        {
            if (_textureRect == null) return;
            switch (DrawMode)
            {
                case DrawModeEnum.Normal:
                    _textureRect.ModulateSelfOverride = ColorNormal;
                    break;

                case DrawModeEnum.Pressed:
                    _textureRect.ModulateSelfOverride = ColorPressed;
                    break;

                case DrawModeEnum.Hover:
                    _textureRect.ModulateSelfOverride = ColorHovered;
                    break;

                case DrawModeEnum.Disabled:
                    break;
            }
        }

        protected override void DrawModeChanged()
        {
            base.DrawModeChanged();
            UpdateChildColors();
        }

        protected override void StylePropertiesChanged()
        {
            base.StylePropertiesChanged();
            UpdateChildColors();
        }
    }
