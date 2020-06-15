﻿using Content.Shared.GameObjects.Components.Power;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;
using System;

namespace Content.Server.GameObjects.Components.NewPower
{
    [RegisterComponent]
    public class BatteryComponent : Component
    {
        public override string Name => "Battery";

        [ViewVariables(VVAccess.ReadWrite)]
        public int MaxCharge { get => _maxCharge; set => SetMaxCharge(value); }
        private int _maxCharge;

        [ViewVariables(VVAccess.ReadWrite)]
        public float CurrentCharge { get => _currentCharge; set => SetCurrentCharge(value); }
        private float _currentCharge;

        /// <summary>
        ///     How full the battery is.
        /// </summary>
        [ViewVariables]
        public BatteryState BatteryState { get; private set; }

        /// <summary>
        ///     What direction the battery's charge is currently going.
        /// </summary>
        [ViewVariables]
        public ChargeState LastChargeState { get => _lastChargeState; set => SetLastChargeState(value); }
        private ChargeState _lastChargeState = ChargeState.Still;

        public DateTime LastChargeStateChange { get; private set; }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref _maxCharge, "maxCharge", 1000);
            serializer.DataField(ref _currentCharge, "startingCharge", 500);
        }

        public override void Initialize()
        {
            base.Initialize();
            UpdateStorageState();
        }

        public ChargeState GetChargeState()
        {
            if (LastChargeStateChange + TimeSpan.FromSeconds(1) > DateTime.Now)
            {
                return LastChargeState;
            }
            return ChargeState.Still;
        }

        /// <summary>
        ///     If sufficient charge is avaiable on the battery, use it. Otherwise, don't.
        /// </summary>
        public bool TryUseCharge(float chargeToUse)
        {
            if (chargeToUse > CurrentCharge)
            {
                return false;
            }
            else
            {
                CurrentCharge -= chargeToUse;
                return true;
            }
        }

        private void UpdateStorageState()
        {
            if (CurrentCharge == MaxCharge)
            {
                BatteryState = BatteryState.Full;
            }
            else if (CurrentCharge == 0)
            {
                BatteryState = BatteryState.Empty;
            }
            else
            {
                BatteryState = BatteryState.PartlyFull;
            }
        }

        private void SetMaxCharge(int newMax)
        {
            _maxCharge = Math.Max(newMax, 0);
            _currentCharge = Math.Min( _currentCharge, MaxCharge);
            UpdateStorageState();
        }

        private void SetCurrentCharge(float newChargeAmount)
        {
            var oldCharge = _currentCharge;
            _currentCharge = FloatMath.Clamp(newChargeAmount, 0, MaxCharge);
            if (_currentCharge > oldCharge)
            {
                LastChargeState = ChargeState.Charging;
            }
            else if (_currentCharge < oldCharge)
            {
                LastChargeState = ChargeState.Discharging;
            }
            UpdateStorageState();
        }

        private void SetLastChargeState(ChargeState newChargeState)
        {
            _lastChargeState = newChargeState;
            LastChargeStateChange = DateTime.Now;
        }
    }

    public enum BatteryState
    {
        Full,
        PartlyFull,
        Empty
    }
}
