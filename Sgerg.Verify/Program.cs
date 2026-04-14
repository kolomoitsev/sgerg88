using Sgerg;

var gas = new NaturalGasPropertyInputs(
    AbsolutePressureBar: 60.0,
    TemperatureCelsius: -3.15,
    RelativeDensity: 0.609,
    CarbonDioxideMoleFraction: 0.005,
    HydrogenMoleFraction: 0.000,
    SuperiorCalorificValueMegajoulesPerCubicMeter: 40.62);

var result = new NaturalGasCompressionCalculator().Calculate(gas);

Console.WriteLine(
    $"Pressure {gas.AbsolutePressureBar} bar, temperature {gas.TemperatureCelsius} °C, " +
    $"relative density {gas.RelativeDensity}, Hs {gas.SuperiorCalorificValueMegajoulesPerCubicMeter} MJ/m³, " +
    $"CO₂ {gas.CarbonDioxideMoleFraction}, H₂ {gas.HydrogenMoleFraction}");
Console.WriteLine($"Inferred nitrogen (mole fraction)     = {result.InferredNitrogenMoleFraction:F6}");
Console.WriteLine($"Compression factor Z                  = {result.CompressionFactor:F6}");
Console.WriteLine($"Molar density (mol/m³)                 = {result.MolarDensityMolesPerCubicMeter:F6}");
