using System;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Monitor;
using Content.Shared.Atmos.Monitor.Components;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;


namespace Content.Client.Atmos.Monitor.UI.Widgets
{
    [GenerateTypedNameReferences]
    public partial class ThresholdControl : BoxContainer
    {
        private AtmosAlarmThreshold _threshold;
        private AtmosMonitorThresholdType _type;
        private Gas? _gas;

        public event Action<AtmosMonitorThresholdType, AtmosAlarmThreshold, Gas?>? ThresholdDataChanged;

        private CheckBox _enabled => CEnableDevice;
        private FloatSpinBox _upperBound => CUpperBound;
        private CheckBox _upperBoundEnabled => CEnableUpperBound;
        private FloatSpinBox _lowerBound => CLowerBound;
        private FloatSpinBox _upperWarningBound => CUpperWarningBound;
        private CheckBox _upperWarningBoundEnabled => CEnableUpperWarningBound;
        private FloatSpinBox _lowerWarningBound => CLowerWarningBound;
        private CheckBox _lowerWarningBoundEnabled => CEnableLowerWarningBound;

        private float _lastUpperBound = 0;
        private float _lastLowerBound = 0;
        private float _lastUpperWarningBound = 0;
        private float _lastLowerWarningBound = 0;

        // i have played myself by making threshold values nullable to
        // indicate validity/disabled status, with several layers of side effect
        // dependent on the other three values when you change one :HECK:
        public ThresholdControl(AtmosAlarmThreshold threshold, AtmosMonitorThresholdType type, Gas? gas = null)
        {
            RobustXamlLoader.Load(this);

            _threshold = threshold;
            _type = type;
            _gas = gas;

            // i miss rust macros

            var upperBoundControl = new ThresholdBoundControl(_threshold.UpperBound);
            upperBoundControl.OnBoundChanged += value =>
            {
                // a lot of threshold logic is baked into the properties,
                // so setting this just returns if a change occurred or not
                _threshold.UpperBound = value;
                return _threshold.UpperBound;
            };
            upperBoundControl.OnBoundEnabled += () =>
            {
                var value = 0f;

                if (_threshold.LowerWarningBound != null) value = (float) _threshold.LowerWarningBound + 0.1f;
                else if (_threshold.LowerBound != null)   value = (float) _threshold.LowerBound + 0.1f;

                return value;
            };
            upperBoundControl.OnValidBoundChanged += () =>
            {
                ThresholdDataChanged!.Invoke(_type, _threshold, _gas);
            };

            var lowerBoundControl = new ThresholdBoundControl(_threshold.LowerBound);
            lowerBoundControl.OnBoundChanged += value =>
            {
                _threshold.LowerBound = value;
                return _threshold.LowerBound;
            };
            lowerBoundControl.OnBoundEnabled += () =>
            {
                var value = 0f;

                if (_threshold.UpperWarningBound != null) value = (float) _threshold.UpperWarningBound - 0.1f;
                else if (_threshold.UpperBound != null)   value = (float) _threshold.UpperBound - 0.1f;

                return value;
            };
            lowerBoundControl.OnValidBoundChanged += () =>
            {
                ThresholdDataChanged!.Invoke(_type, _threshold, _gas);
            };
       }


        private class ThresholdBoundControl : BoxContainer
        {
            private float? _value;
            private float _lastValue;

            private FloatSpinBox _bound;
            private CheckBox _boundEnabled;

            public event Action? OnValidBoundChanged;
            public Func<float, float?>? OnBoundChanged;
            public Func<float>? OnBoundEnabled;

            public ThresholdBoundControl(float? value)
            {
                _value = value;

                this.Orientation = LayoutOrientation.Vertical;

                _bound = new FloatSpinBox();
                this.AddChild(_bound);

                _boundEnabled = new CheckBox();
                this.AddChild(_boundEnabled);

                _bound.Value = _value ?? 0;
                _lastValue = _value ?? 0;
                _boundEnabled.Pressed = _value != null;

                _bound.OnValueChanged += ChangeValue;
                _bound.IsValid += ValidateThreshold;
                _boundEnabled.OnToggled += ToggleBound;
            }

            private void ChangeValue(FloatSpinBox.FloatSpinBoxEventArgs args)
            {
                var value = OnBoundChanged!(args.Value);
                if (value != null || value != _lastValue)
                {
                    _value = value;
                    OnValidBoundChanged!.Invoke();
                }
                else
                {
                    _bound.Value = _lastValue;
                }
            }

            private void ToggleBound(BaseButton.ButtonToggledEventArgs args)
            {
                if (args.Pressed)
                {
                    var value = OnBoundChanged!(OnBoundEnabled!());
                    if (value == null || value < 0)
                    {
                        // TODO: Improve UX here, this is ass
                        _boundEnabled.Pressed = false;
                        return;
                    }

                    _value = value;

                    _bound.Value = (float) _value;
                    _lastValue = (float) _value;
                }
                else
                {
                    _value = null;
                }

                OnValidBoundChanged!.Invoke();
            }

            private bool ValidateThreshold(float value)
            {
                if (_value == null) return false;
                if (value < 0) return false;

                return true;
            }

        }
    }
}
