using System;

namespace Sgerg;

/// <summary>Measured or reported natural-gas properties for SGERG-88 (ISO 12213-3 style).</summary>
public readonly record struct NaturalGasPropertyInputs(
    double AbsolutePressureBar,
    double TemperatureCelsius,
    double RelativeDensity,
    double CarbonDioxideMoleFraction,
    double HydrogenMoleFraction,
    double SuperiorCalorificValueMegajoulesPerCubicMeter);
