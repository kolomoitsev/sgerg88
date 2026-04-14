using System;

namespace Sgerg
{
    /// <summary>Outcome of a SGERG-88 compression-factor calculation.</summary>
    public readonly struct NaturalGasCompressionResult
    {
        public readonly double InferredNitrogenMoleFraction;
        public readonly double CompressionFactor;
        public readonly double MolarDensityMolesPerCubicMeter;

        public NaturalGasCompressionResult(
            double inferredNitrogenMoleFraction,
            double compressionFactor,
            double molarDensityMolesPerCubicMeter)
        {
            InferredNitrogenMoleFraction = inferredNitrogenMoleFraction;
            CompressionFactor = compressionFactor;
            MolarDensityMolesPerCubicMeter = molarDensityMolesPerCubicMeter;
        }
    }
}
