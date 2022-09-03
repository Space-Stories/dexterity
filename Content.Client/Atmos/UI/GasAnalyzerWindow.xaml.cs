using Content.Shared.Atmos;
using Content.Shared.Temperature;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.XAML;
using Serilog;
using static Content.Shared.Atmos.Components.SharedGasAnalyzerComponent;

namespace Content.Client.Atmos.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class GasAnalyzerWindow : DefaultWindow
    {
        public GasAnalyzerBoundUserInterface Owner { get; }
        private IEntityManager _entityManager;

        public GasAnalyzerWindow(GasAnalyzerBoundUserInterface owner)
        {
            RobustXamlLoader.Load(this);
            _entityManager = IoCManager.Resolve<IEntityManager>();
            Owner = owner;
        }

        public void Populate(GasAnalyzerUserMessage msg)
        {
            if (msg.Error != null)
            {
                CTopBox.AddChild(new Label
                {
                    Text = Loc.GetString("gas-analyzer-window-error-text", ("errorText", msg.Error)),
                    FontColorOverride = Color.Red
                });
                return;
            }

            if (msg.NodeGasMixes.Length == 0)
            {
                CTopBox.AddChild(new Label
                {
                    Text = Loc.GetString("gas-analyzer-window-no-data")
                });
                return;
            }

            // Device Tab
            if (msg.NodeGasMixes.Length > 1)
            {
                CTabContainer.SetTabVisible(0, true);
                CTabContainer.SetTabTitle(0, Loc.GetString("gas-analyzer-window-tab-title-capitalized", ("title", msg.DeviceName)));
                // Set up Grid
                GridIcon.OverrideDirection = msg.NodeGasMixes.Length switch
                {
                    // Unary layout
                    2 => Direction.South,
                    // Binary layout
                    3 => Direction.East,
                    // Trinary layout
                    4 => Direction.East,
                    _ => GridIcon.OverrideDirection
                };

                GridIcon.Sprite = _entityManager.GetComponent<SpriteComponent>(msg.DeviceUid);
                LeftPanel.RemoveAllChildren();
                MiddlePanel.RemoveAllChildren();
                RightPanel.RemoveAllChildren();
                if (msg.NodeGasMixes.Length == 2)
                {
                    // Unary, use middle
                    LeftPanelLabel.Text = string.Empty;
                    MiddlePanelLabel.Text = Loc.GetString("gas-analyzer-window-tab-title-capitalized", ("title", msg.NodeGasMixes[1].Name));
                    RightPanelLabel.Text = string.Empty;

                    GenerateGasDisplay(msg.NodeGasMixes[1], MiddlePanel);
                }
                else if (msg.NodeGasMixes.Length == 3)
                {
                    // Binary, use left and right
                    LeftPanelLabel.Text = Loc.GetString("gas-analyzer-window-tab-title-capitalized", ("title", msg.NodeGasMixes[1].Name));
                    MiddlePanelLabel.Text = string.Empty;
                    RightPanelLabel.Text = Loc.GetString("gas-analyzer-window-tab-title-capitalized", ("title", msg.NodeGasMixes[2].Name));

                    GenerateGasDisplay(msg.NodeGasMixes[1], LeftPanel);
                    GenerateGasDisplay(msg.NodeGasMixes[2], RightPanel);
                }
                else if (msg.NodeGasMixes.Length == 4)
                {
                    // Trinary, use all three
                    LeftPanelLabel.Text = Loc.GetString("gas-analyzer-window-tab-title-capitalized", ("title", msg.NodeGasMixes[1].Name));
                    MiddlePanelLabel.Text = Loc.GetString("gas-analyzer-window-tab-title-capitalized", ("title", msg.NodeGasMixes[2].Name));
                    RightPanelLabel.Text = Loc.GetString("gas-analyzer-window-tab-title-capitalized", ("title", msg.NodeGasMixes[3].Name));

                    GenerateGasDisplay(msg.NodeGasMixes[1], LeftPanel);
                    GenerateGasDisplay(msg.NodeGasMixes[2], MiddlePanel);
                    GenerateGasDisplay(msg.NodeGasMixes[3], RightPanel);
                }
                else
                {
                    // oh shit of fuck its more than 4 this ui isn't gonna look pretty anymore
                    for (var i = 1; i < msg.NodeGasMixes.Length; i++)
                    {
                        GenerateGasDisplay(msg.NodeGasMixes[i], CDeviceMixes);
                    }
                }
            }
            else
            {
                // Hide device tab, no device selected
                CTabContainer.SetTabVisible(0, false);
                CTabContainer.CurrentTab = 1;
            }
            // Environment Tab
            var envMix = msg.NodeGasMixes[0];

            CTabContainer.SetTabTitle(1, envMix.Name);
            CEnvironmentMix.RemoveAllChildren();
            GenerateGasDisplay(envMix, CEnvironmentMix);
            MinSize = new Vector2(CDeviceGrid.DesiredSize.X + 40, MinSize.Y);
        }

        private void GenerateGasDisplay(GasMixEntry gasMix, Control parent)
        {
            var panel = new PanelContainer
            {
                VerticalExpand = true,
                HorizontalExpand = true,
                Margin = new Thickness(4),
                PanelOverride = new StyleBoxFlat{BorderColor = Color.FromHex("#4f4f4f"), BorderThickness = new Thickness(1)}
            };
            var dataContainer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };


            parent.AddChild(panel);
            panel.AddChild(dataContainer);

            // Pressure label
            dataContainer.AddChild(new Label
            {
                Text = Loc.GetString("gas-analyzer-window-pressure-text", ("pressure", $"{gasMix.Pressure:0.##}"))
            });

            // If there is no gas, it doesn't really have a temperature, so skip displaying it
            if (gasMix.Pressure > Atmospherics.GasMinMoles)
            {
                // Temperature label
                dataContainer.AddChild(new Label
                {
                    Text = Loc.GetString("gas-analyzer-window-temperature-text",
                        ("tempK", $"{gasMix.Temperature:0.#}"),
                        ("tempC", $"{TemperatureHelpers.KelvinToCelsius(gasMix.Temperature):0.#}"))
                });
            }

            if (gasMix.Gases == null || gasMix.Gases?.Length == 0)
            {
                // Separator
                dataContainer.AddChild(new Control
                {
                    MinSize = new Vector2(0, 10)
                });

                // Add a label that there are no gases so it's less confusing
                dataContainer.AddChild(new Label
                {
                    Text = Loc.GetString("gas-analyzer-window-no-gas-text"),
                    FontColorOverride = Color.Gray
                });
                // return, everything below is for the fancy gas display stuff
                return;
            }
            // Separator
            dataContainer.AddChild(new Control
            {
                MinSize = new Vector2(0, 10)
            });

            // Add a table with all the gases
            var tableKey = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical
            };
            var tableVal = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical
            };
            dataContainer.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children =
                {
                    tableKey,
                    new Control
                    {
                        MinSize = new Vector2(20, 0)
                    },
                    tableVal
                }
            });
            // This is the gas bar thingy
            var height = 30;
            var minSize = 24; // This basically allows gases which are too small, to be shown properly
            var gasBar = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                MinSize = new Vector2(0, height)
            };
            // Separator
            dataContainer.AddChild(new Control
            {
                MinSize = new Vector2(0, 10),
                VerticalExpand = true
            });

            var totalGasAmount = 0f;
            foreach (var gas in gasMix.Gases!)
            {
                totalGasAmount += gas.Amount;
            }

            for (var j = 0; j < gasMix.Gases.Length; j++)
            {
                var gas = gasMix.Gases[j];
                var color = Color.FromHex($"#{gas.Color}", Color.White);
                // Add to the table
                tableKey.AddChild(new Label
                {
                    Text = Loc.GetString(gas.Name)
                });
                tableVal.AddChild(new Label
                {
                    Text = Loc.GetString("gas-analyzer-window-molarity-text", ("mol", $"{gas.Amount:0.##}"))
                });

                // Add to the gas bar //TODO: highlight the currently hover one
                var left = (j == 0) ? 0f : 2f;
                var right = (j == gasMix.Gases.Length - 1) ? 0f : 2f;
                gasBar.AddChild(new PanelContainer
                {
                    ToolTip = Loc.GetString("gas-analyzer-window-molarity-percentage-text",
                        ("gasName", gas.Name),
                        ("amount", $"{gas.Amount:0.##}"),
                        ("percentage", $"{(gas.Amount / totalGasAmount * 100):0.#}")),
                    HorizontalExpand = true,
                    SizeFlagsStretchRatio = gas.Amount,
                    MouseFilter = MouseFilterMode.Stop,
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = color,
                        PaddingLeft = left,
                        PaddingRight = right
                    },
                    MinSize = new Vector2(minSize, 0)
                });
            }

            dataContainer.AddChild(gasBar);
        }
    }
}
