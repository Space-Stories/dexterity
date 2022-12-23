using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;
using System.Threading;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using Timer = Robust.Shared.Timing.Timer;
using Robust.Client.AutoGenerated;

namespace Content.Client.Communications.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class CommunicationsConsoleMenu : FancyWindow
    {
        private CommunicationsConsoleBoundUserInterface Owner { get; set; }
        private readonly CancellationTokenSource _timerCancelTokenSource = new();
        private string UserSelectedAlertLevel = "";
        private string CurrentStationAlertLevel = "";
        private bool _alertLevelSelectable = true;
        public bool AlertLevelSelectable { get { return _alertLevelSelectable; }
            set
            {
                _alertLevelSelectable = value;
                CheckSetLevelButton();
            }
        }

        public CommunicationsConsoleMenu(CommunicationsConsoleBoundUserInterface owner)
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);

            Owner = owner;

            AnnounceButton.OnPressed += (_) =>
            {
                // Try to avoid sending an empty announcement; not totally bullet-proof because
                // of the way CommunicationsConsoleBoundUserInterface.AnnounceButtonPressed
                // trims long messages, but good enough to avoid users accidentally sending an
                // empty announcement and having to wait for the timeout.
                var trimmed = Rope.Collapse(MessageInput.TextRope).Trim();
                if (trimmed.Length > 0)
                {
                    Owner.AnnounceButtonPressed(trimmed);
                }
            };

            AnnounceButton.Disabled = !owner.CanAnnounce;

            AlertLevelOptions.OnItemSelected += args =>
            {
                var metadata = AlertLevelOptions.GetItemMetadata(args.Id);
                if (metadata != null && metadata is string cast)
                {
                    UserSelectedAlertLevel = cast;
                }
                AlertLevelOptions.Select(args.Id);
                CheckSetLevelButton();
            };

            SetAlertLevelButton.OnPressed += args =>
            {
                var metadata = AlertLevelOptions.GetItemMetadata(AlertLevelOptions.SelectedId);
                if (metadata != null && metadata is string cast)
                {
                    Owner.AlertLevelSelected(cast);
                }
            };
            SetAlertLevelButton.Disabled = !owner.AlertLevelSelectable;

            EmergencyShuttleCallButton.OnPressed += (_) => Owner.EmergencyShuttleButtonPressed();
            EmergencyShuttleCallButton.Disabled = !(owner.CanCall && !owner.CountdownStarted);
            EmergencyShuttleRecallButton.OnPressed += (_) => Owner.EmergencyShuttleButtonPressed();
            EmergencyShuttleRecallButton.Disabled = !(owner.CanCall && owner.CountdownStarted);

            UpdateCountdown();
            Timer.SpawnRepeating(1000, UpdateCountdown, _timerCancelTokenSource.Token);
        }

        // The current alert could make levels unselectable, so we need to ensure that the UI reacts properly.
        // If the current alert is unselectable, the only item in the alerts list will be
        // the current alert. Otherwise, it will be the list of alerts, with the current alert
        // selected.
        public void UpdateAlertLevels(List<string>? alerts, List<Color>? colors, string currentAlert)
        {
            AlertLevelOptions.Clear();

            if (UserSelectedAlertLevel.Length == 0)
            {
                // This is the first time we've got the list of alert levels from the UI
                // User can't have made a change here, so we can initialize the user-selected
                // level to match the currentLevel
                UserSelectedAlertLevel = currentAlert;
            }
            CurrentStationAlertLevel = currentAlert;

            if (alerts == null)
            {
                var name = currentAlert;
                if (Loc.TryGetString($"alert-level-{currentAlert}", out var locName))
                {
                    name = locName;
                }
                AlertLevelOptions.AddItem(name);
                AlertLevelOptions.SetItemMetadata(AlertLevelOptions.ItemCount - 1, currentAlert);
            }
            else
            {
                // If the user has selected a new alert level when we get this message, try to
                // remember their old selection. If not found, select the current station level.
                var foundUserSelectedAlert = false;
                for(int i = 0; i < alerts.Count; i++)
                {
                    var alert = alerts[i];
                    var name = alert;
                    if (Loc.TryGetString($"alert-level-{alert}", out var locName))
                    {
                        name = locName;
                    }
                    AlertLevelOptions.AddItem(name);
                    AlertLevelOptions.SetItemMetadata(AlertLevelOptions.ItemCount - 1, alert);

                    if (alert == UserSelectedAlertLevel)
                    {
                        AlertLevelOptions.Select(AlertLevelOptions.ItemCount - 1);
                        foundUserSelectedAlert = true;
                    }

                    if (alert == currentAlert)
                    {
                        if (!foundUserSelectedAlert)
                        {
                            AlertLevelOptions.Select(AlertLevelOptions.ItemCount - 1);
                        }

                        if (colors != null)
                        {
                            AlertLevelLight.PanelOverride = new StyleBoxFlat
                            {
                                BackgroundColor = colors[i],
                            };
                        }
                    }

                }
            }

            CheckSetLevelButton();
        }

        private void CheckSetLevelButton()
        {
            bool canChange = AlertLevelSelectable && UserSelectedAlertLevel != CurrentStationAlertLevel;
            SetAlertLevelButton.Disabled = !canChange;
        }

        public void UpdateCountdown()
        {
            int remaining = Owner.CountdownStarted ? Owner.Countdown : 0;
            var message = remaining.ToString("D4");
            CountdownLabel.Text = message;

            if(Owner.CountdownStarted)
            {
                ShuttleIncomingLight.SetOnlyStyleClass("LightLitGreen");
            }
            else
            {
                ShuttleIncomingLight.SetOnlyStyleClass("LightUnlit");
            }
        }

        public override void Close()
        {
            base.Close();

            _timerCancelTokenSource.Cancel();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                _timerCancelTokenSource.Cancel();
        }
    }
}
