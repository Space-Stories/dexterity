using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using System;
// ReSharper disable InconsistentNaming

namespace Content.Shared.Atmos
{
    /// <summary>
    ///     Class to store atmos constants.
    /// </summary>
    public static class Atmospherics
    {
        static Atmospherics()
        {
            AdjustedNumberOfGases = MathHelper.NextMultipleOf(TotalNumberOfGases, 4);
        }

        #region ATMOS
        /// <summary>
        ///     The universal gas constant, in kPa*L/(K*mol)
        /// </summary>
        public const float R = 8.314462618f;

        /// <summary>
        ///     1 ATM in kPA.
        /// </summary>
        public const float OneAtmosphere = 101.325f;

        /// <summary>
        ///     Maximum external pressure (in kPA) a gas miner will, by default, output to.
        ///     This is used to initialize roundstart atmos rooms.
        /// </summary>
        public const float GasMinerDefaultMaxExternalPressure = 6500f;

        /// <summary>
        ///     -270.3ºC in K. CMB stands for Cosmic Microwave Background.
        /// </summary>
        public const float TCMB = 2.7f;

        /// <summary>
        ///     0ºC in K
        /// </summary>
        public const float T0C = 273.15f;

        /// <summary>
        ///     20ºC in K
        /// </summary>
        public const float T20C = 293.15f;

        /// <summary>
        ///     Liters in a cell.
        /// </summary>
        public const float CellVolume = 2500f;

        // Liters in a normal breath
        public const float BreathVolume = 0.5f;

        // Amount of air to take from a tile
        public const float BreathPercentage = BreathVolume / CellVolume;

        /// <summary>
        ///     Moles in a 2.5 m^3 cell at 101.325 kPa and 20ºC
        /// </summary>
        public const float MolesCellStandard = (OneAtmosphere * CellVolume / (T20C * R));

        /// <summary>
        ///     Moles in a 2.5 m^3 cell at GasMinerDefaultMaxExternalPressure kPa and 20ºC
        /// </summary>
        public const float MolesCellGasMiner = (GasMinerDefaultMaxExternalPressure * CellVolume / (T20C * R));

        /// <summary>
        ///     Compared against for superconduction.
        /// </summary>
        public const float MCellWithRatio = (MolesCellStandard * 0.005f);

        public const float OxygenStandard = 0.21f;
        public const float NitrogenStandard = 0.79f;

        public const float OxygenMolesStandard = MolesCellStandard * OxygenStandard;
        public const float NitrogenMolesStandard = MolesCellStandard * NitrogenStandard;

        #endregion

        /// <summary>
        ///     Visible moles multiplied by this factor to get moles at which gas is at max visibility.
        /// </summary>
        public const float FactorGasVisibleMax = 20f;

        /// <summary>
        ///     Minimum number of moles a gas can have.
        /// </summary>
        public const float GasMinMoles = 0.00000005f;

        public const float OpenHeatTransferCoefficient = 0.4f;

        /// <summary>
        ///     Hack to make vacuums cold, sacrificing realism for gameplay.
        /// </summary>
        public const float HeatCapacityVacuum = 7000f;

        /// <summary>
        ///     Ratio of air that must move to/from a tile to reset group processing
        /// </summary>
        public const float MinimumAirRatioToSuspend = 0.1f;

        /// <summary>
        ///     Minimum ratio of air that must move to/from a tile
        /// </summary>
        public const float MinimumAirRatioToMove = 0.001f;

        /// <summary>
        ///     Minimum amount of air that has to move before a group processing can be suspended
        /// </summary>
        public const float MinimumAirToSuspend = (MolesCellStandard * MinimumAirRatioToSuspend);

        public const float MinimumTemperatureToMove = (T20C + 100f);

        public const float MinimumMolesDeltaToMove = (MolesCellStandard * MinimumAirRatioToMove);

        /// <summary>
        ///     Minimum temperature difference before group processing is suspended
        /// </summary>
        public const float MinimumTemperatureDeltaToSuspend = 4.0f;

        /// <summary>
        ///     Minimum temperature difference before the gas temperatures are just set to be equal.
        /// </summary>
        public const float MinimumTemperatureDeltaToConsider = 0.5f;

        /// <summary>
        ///     Minimum temperature for starting superconduction.
        /// </summary>
        public const float MinimumTemperatureStartSuperConduction = (T20C + 400f);
        public const float MinimumTemperatureForSuperconduction = (T20C + 80f);

        /// <summary>
        ///     Minimum heat capacity.
        /// </summary>
        public const float MinimumHeatCapacity = 0.0003f;

        #region Excited Groups

        /// <summary>
        ///     Number of full atmos updates ticks before an excited group breaks down (averages gas contents across turfs)
        /// </summary>
        public const int ExcitedGroupBreakdownCycles = 4;

        /// <summary>
        ///     Number of full atmos updates before an excited group dismantles and removes its turfs from active
        /// </summary>
        public const int ExcitedGroupsDismantleCycles = 16;

        #endregion

        /// <summary>
        ///     Hard limit for zone-based tile equalization.
        /// </summary>
        public const int MonstermosHardTileLimit = 2000;

        /// <summary>
        ///     Limit for zone-based tile equalization.
        /// </summary>
        public const int MonstermosTileLimit = 200;

        /// <summary>
        ///     Total number of gases. Increase this if you want to add more!
        /// </summary>
        public const int TotalNumberOfGases = 6;

        /// <summary>
        ///     This is the actual length of the gases arrays in mixtures.
        ///     Set to the closest multiple of 4 relative to <see cref="TotalNumberOfGases"/> for SIMD reasons.
        /// </summary>
        public static readonly int AdjustedNumberOfGases;

        /// <summary>
        ///     Amount of heat released per mole of burnt hydrogen or tritium (hydrogen isotope)
        /// </summary>
        public const float FireHydrogenEnergyReleased = 560000f;
        public const float FireMinimumTemperatureToExist = T0C + 100f;
        public const float FireMinimumTemperatureToSpread = T0C + 150f;
        public const float FireSpreadRadiosityScale = 0.85f;
        public const float FirePlasmaEnergyReleased = 3000000f;
        public const float FireGrowthRate = 40000f;

        public const float SuperSaturationThreshold = 96f;

        public const float OxygenBurnRateBase = 1.4f;
        public const float PlasmaMinimumBurnTemperature = (100f+T0C);
        public const float PlasmaUpperTemperature = (1370f+T0C);
        public const float PlasmaOxygenFullburn = 10f;
        public const float PlasmaBurnRateDelta = 9f;

        /// <summary>
        ///     This is calculated to help prevent singlecap bombs (Overpowered tritium/oxygen single tank bombs)
        /// </summary>
        public const float MinimumTritiumOxyburnEnergy = 2000000f;

        public const float TritiumBurnOxyFactor = 100f;
        public const float TritiumBurnTritFactor = 10f;

        /// <summary>
        ///     Determines at what pressure the ultra-high pressure red icon is displayed.
        /// </summary>
        public const float HazardHighPressure = 550f;

        /// <summary>
        ///     Determines when the orange pressure icon is displayed.
        /// </summary>
        public const float WarningHighPressure = 0.7f * HazardHighPressure;

        /// <summary>
        ///     Determines when the gray low pressure icon is displayed.
        /// </summary>
        public const float WarningLowPressure = 2.5f * HazardLowPressure;

        /// <summary>
        ///     Determines when the black ultra-low pressure icon is displayed.
        /// </summary>
        public const float HazardLowPressure = 20f;

        /// <summary>
        ///    The amount of pressure damage someone takes is equal to (pressure / HAZARD_HIGH_PRESSURE)*PRESSURE_DAMAGE_COEFFICIENT,
        ///     with the maximum of MaxHighPressureDamage.
        /// </summary>
        public const float PressureDamageCoefficient = 4;

        /// <summary>
        ///     Maximum amount of damage that can be endured with high pressure.
        /// </summary>
        public const int MaxHighPressureDamage = 4;

        /// <summary>
        ///     The amount of damage someone takes when in a low pressure area
        ///     (The pressure threshold is so low that it doesn't make sense to do any calculations,
        ///     so it just applies this flat value).
        /// </summary>
        public const int LowPressureDamage = 4;

        public const float WindowHeatTransferCoefficient = 0.1f;

        /// <summary>
        ///     Directions that atmos currently supports. Modify in case of multi-z.
        ///     See <see cref="AtmosDirection"/> on the server.
        /// </summary>
        public const int Directions = 4;

        /// <summary>
        ///     The normal body temperature in degrees Celsius.
        /// </summary>
        public const float NormalBodyTemperature = 37f;

        public const float HumanNeededOxygen = MolesCellStandard * BreathPercentage * 0.16f;

        public const float HumanProducedOxygen = HumanNeededOxygen * 0.75f;

        public const float HumanProducedCarbonDioxide = HumanNeededOxygen * 0.25f;

        #region Pipes

        /// <summary>
        ///     The pressure pumps and powered equipment max out at, in kPa.
        /// </summary>
        public const float MaxOutputPressure = 4500;

        /// <summary>
        ///     The maximum speed powered equipment can work at, in L/s.
        /// </summary>
        public const float MaxTransferRate = 200;

        #endregion

        #region Supermatter

        /// <summary>
        ///     Standard Effect on PowerMixRatio
        /// </summary>
        public const float OxygenPowerMixRatio = 1f;

        /// <summary>
        ///     Standard Effect on PowerMixRatio
        /// </summary>
        public const float WaterPowerMixRatio = 1f;

        /// <summary>
        ///     Standard Effect on PowerMixRatio
        /// </summary>
        public const float PlasmaPowerMixRatio = 1f;

        /// <summary>
        ///     Standard Effect on PowerMixRatio
        /// </summary>
        public const float  CO2PowerMixRatio = 1f;

        /// <summary>
        ///     Reduces PowerMixRatio
        /// </summary>
        public const float  NitrogenPowerMixRatio = -1f;

        /// <summary>
        ///     Reduces PowerMixRatio
        /// </summary>
        public const float  PluxoiumPowerMixRatio = -1f;

        /// <summary>
        ///     Standard Effect on PowerMixRatio
        /// </summary>
        public const float  TritiumPowerMixRatio = 1f;

        /// <summary>
        ///     Standard Effect on PowerMixRatio
        /// </summary>
        public const float  BZPowerMixRatio = 1f;

        /// <summary>
        ///     Standard Effect on PowerMixRatio
        /// </summary>
        public const float  FreonPowerMixRatio = -1f;

        /// <summary>
        ///     Standard Effect on PowerMixRatio
        /// </summary>
        public const float  HydrogenPowerMixRatio = 1f;

        /// <summary>
        ///     Standard Effect on PowerMixRatio
        /// </summary>
        public const float  HealiumPowerMixRatio = 1f;

        /// <summary>
        ///     Standard Effect on PowerMixRatio
        /// </summary>
        public const float  ProtoNitratePowerMixRatio = 1f;

        /// <summary>
        ///     Standard Effect on PowerMixRatio
        /// </summary>
        public const float  ZaukerPowerMixRatio = 1f;

        /// <summary>
        ///     Half as effective as other gasses
        /// </summary>
        public const float  MiasmaPowerMixRatio = 0.5f;



        /// <summary>
        ///     Higher == Bigger heat and waste penalty from having the crystal surrounded by this gas. Negative numbers reduce penalty.
        /// </summary>
        public const float PlasmaHeatPenalty = 15f;

        /// <summary>
        ///
        /// </summary>
        public const float OxygenHeatPenalty = 1f;

        /// <summary>
        ///
        /// </summary>
        public const float PluoxiumHeatPenalty = -0.5f;

        /// <summary>
        ///
        /// </summary>
        public const float TritiumHeatPenalty = 10f;

        /// <summary>
        ///
        /// </summary>
        public const float CO2HeatPenalty = 0.1f;

        /// <summary>
        ///
        /// </summary>
        public const float NitrogenHeatPenalty = -1.5f;

        /// <summary>
        ///
        /// </summary>
        public const float BZHeatPenalty = 5f;

        /// <summary>
        ///     This'll get made slowly over time, I want my spice rock spicy god damnit
        /// </summary>
        public const float WaterHeatPenalty = 12f;

        /// <summary>
        ///     Very good heat absorbtion and less plasma and o2 generation
        /// </summary>
        public const float FreonHeatPenalty = -10f;

        /// <summary>
        ///     Same heat penalty as tritium (dangerous)
        /// </summary>
        public const float HydrogenHeatPenalty = 10f;

        /// <summary>
        ///
        /// </summary>
        public const float HealiumHeatPenalty = 4f;

        public const float ProtonitrateHeatPenalty = -3f;

        /// <summary>
        ///
        /// </summary>
        public const float ZaukerHeatPenalty = 8f;



        //All of these get divided by 10-bzcomp * 5 before having 1 added and being multiplied with power to determine rads
        //Keep the negative values here above -10 and we won't get negative rads
        //Higher == Bigger bonus to power generation.

        /// <summary>
        ///
        /// </summary>
        public const float OxygenTransmitModifier = 1.5f;

        /// <summary>
        ///
        /// </summary>
        public const float NitrogenTransmitModifier = 0f;

        /// <summary>
        ///
        /// </summary>
        public const float CO2TransmitModifier = 0f;

        /// <summary>
        ///
        /// </summary>
        public const float PlasmaTransmitModifier = 4f;

        /// <summary>
        ///
        /// </summary>
        public const float WaterTransmitModifier = 2f;

        /// <summary>
        ///
        /// </summary>
        public const float BZTransmitModifier = -2f;

        /// <summary>
        ///     The SpiceGas makes the Spicerock spicy
        /// </summary>
        public const float TritiumTransmitModifier = 30f;

        /// <summary>
        /// Should halve the power output
        /// </summary>
        public const float PluoxiumTransmitModifier = -5f;

        /// <summary>
        ///     increase the radiation emission, but less than the trit
        /// </summary>
        public const float HydrogenTransmitModifier = 25f;

        /// <summary>
        ///
        /// </summary>
        public const float HealiumTransmitModifier = 2.4f;

        /// <summary>
        ///
        /// </summary>
        public const float ProtonitrateTransmitModifier = 15f;

        /// <summary>
        ///
        /// </summary>
        public const float ZaukerTransmitModifier = 20f;



        /// <summary>
        ///     Improves the effect of transmit modifiers
        /// </summary>
        public const float BZRadioactivityModifier = 5f;



        /// <summary>
        ///     Higher == Gas makes the crystal more resistant against heat damage.
        /// </summary>
        public const float N2OHeatResistance = 6f;

        /// <summary>
        ///     just a bit of heat resistance to spice it up
        /// </summary>
        public const float HydrogenHeatResistance = 2f;

        /// <summary>
        ///
        /// </summary>
        public const float ProtoNitrateHeatResistance = 5f;



        /// <summary>
        ///     The minimum portion of the miasma in the air that will be consumed. Higher values mean more miasma will be consumed be default.
        /// </summary>
        public const float MiasmaConsumptionRatioMin = 0f;

        /// <summary>
        ///     The maximum portion of the miasma in the air that will be consumed. Lower values mean the miasma consumption rate caps earlier.
        /// </summary>
        public const float MiasmaConsumptionRatioMax = 1f;

        /// <summary>
        ///     The minimum pressure for a pure miasma atmosphere to begin being consumed. Higher values mean it takes more miasma pressure to make miasma start being consumed. Should be >= 0
        /// </summary>
        public const float MiasmaConsumptionPP = (Atmospherics.OneAtmosphere*0.01f);

        /// <summary>
        ///     How the amount of miasma consumed per tick scales with partial pressure. Higher values decrease the rate miasma consumption scales with partial pressure. Should be >0
        /// </summary>
        public const float MiasmaPressureScaling = (Atmospherics.OneAtmosphere*0.5f);

        /// <summary>
        ///     How much the amount of miasma consumed per tick scales with gasmix power ratio. Higher values means gasmix has a greater effect on the miasma consumed.
        /// </summary>
        public const float MiasmaGasMixScaling = (0.3f);

        /// <summary>
        ///     The amount of matter power generated for every mole of miasma consumed. Higher values mean miasma generates more power.
        /// </summary>
        public const float MiasmaPowerGain = 10f;


        /// <summary>
        ///     Higher == less heat released during reaction
        /// </summary>
        public const float  ThermalReleaseModifier = 5f;

        /// <summary>
        ///     Higher == less plasma released by reaction
        /// </summary>
        public const float  PlasmaReleaseModifier = 750f;

        /// <summary>
        ///     Higher == less oxygen released at high temperature/power
        /// </summary>
        public const float  OxygenReleaseModifier = 325f;

        #endregion
    }

    /// <summary>
    ///     Gases to Ids. Keep these updated with the prototypes!
    /// </summary>
    [Serializable, NetSerializable]
    public enum Gas : sbyte
    {
        Oxygen = 0,
        Nitrogen = 1,
        CarbonDioxide = 2,
        Plasma = 3,
        Tritium = 4,
        WaterVapor = 5,
    }
}
