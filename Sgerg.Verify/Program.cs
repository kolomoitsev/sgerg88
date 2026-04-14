using System;
using Sgerg;

namespace Sgerg.Verify
{
    internal static class Program
    {
        private static void Main()
        {
            var gas = new NaturalGasPropertyInputs(
                absolutePressureBar: 60.0,
                temperatureCelsius: -3.15,
                relativeDensity: 0.581,
                carbonDioxideMoleFraction: 0.006,
                hydrogenMoleFraction: 0.000,
                superiorCalorificValueMegajoulesPerCubicMeter: 40.66);

            var result = new NaturalGasCompressionCalculator().Calculate(gas);

            Console.WriteLine(
                $"Pressure {gas.AbsolutePressureBar} bar, temperature {gas.TemperatureCelsius} °C, " +
                $"relative density {gas.RelativeDensity}, Hs {gas.SuperiorCalorificValueMegajoulesPerCubicMeter} MJ/m³, " +
                $"CO₂ {gas.CarbonDioxideMoleFraction}, H₂ {gas.HydrogenMoleFraction}");
            Console.WriteLine($"Inferred nitrogen (mole fraction)     = {result.InferredNitrogenMoleFraction:F6}");
            Console.WriteLine($"Compression factor Z                  = {result.CompressionFactor:F6}");
            Console.WriteLine($"Molar density (mol/m³)                 = {result.MolarDensityMolesPerCubicMeter:F6}");
        }
    }
}
