using System;

namespace Sgerg;

/// <summary>Outcome of a SGERG-88 compression-factor calculation.</summary>
public readonly record struct NaturalGasCompressionResult(
    double InferredNitrogenMoleFraction,
    double CompressionFactor,
    double MolarDensityMolesPerCubicMeter);
