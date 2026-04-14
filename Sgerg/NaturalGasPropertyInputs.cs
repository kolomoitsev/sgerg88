using System;

namespace Sgerg
{
    /// <summary>Measured or reported natural-gas properties for SGERG-88 (ISO 12213-3 style).</summary>
    public readonly struct NaturalGasPropertyInputs
    {
        public readonly double AbsolutePressureBar;
        public readonly double TemperatureCelsius;
        public readonly double RelativeDensity;
        public readonly double CarbonDioxideMoleFraction;
        public readonly double HydrogenMoleFraction;
        public readonly double SuperiorCalorificValueMegajoulesPerCubicMeter;

        public NaturalGasPropertyInputs(
            double absolutePressureBar,
            double temperatureCelsius,
            double relativeDensity,
            double carbonDioxideMoleFraction,
            double hydrogenMoleFraction,
            double superiorCalorificValueMegajoulesPerCubicMeter)
        {
            AbsolutePressureBar = absolutePressureBar;
            TemperatureCelsius = temperatureCelsius;
            RelativeDensity = relativeDensity;
            CarbonDioxideMoleFraction = carbonDioxideMoleFraction;
            HydrogenMoleFraction = hydrogenMoleFraction;
            SuperiorCalorificValueMegajoulesPerCubicMeter = superiorCalorificValueMegajoulesPerCubicMeter;
        }
    }
}
