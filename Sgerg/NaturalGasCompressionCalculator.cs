using System;

namespace Sgerg;

/// <summary>
/// Calculates natural-gas compression factor <i>Z</i> using the SGERG-88 virial method
/// (ISO 12213-3 — compression factor from physical properties).
/// </summary>
/// <remarks>
/// Coefficients follow the GERG-88 / SGERG-88 field formulation, aligned with the MIT-licensed
/// pygerg package (FORTRAN source: Michels &amp; Schouten, 1991).
/// </remarks>
public sealed class NaturalGasCompressionCalculator
{
    /// <summary>
    /// Mole-fraction snapshot for virial mixing (SGERG pseudo-groups: fuel gas, N₂, CO₂, H₂,
    /// plus the seventh correlated fraction tied to hydrogen in the standard procedure).
    /// </summary>
    private readonly record struct MoleFractionSnapshot(
        double EquivalentFuelGas,
        double Nitrogen,
        double CarbonDioxide,
        double Hydrogen,
        double AuxiliarySpeciesSeven)
    {
        public double FuelSquared => EquivalentFuelGas * EquivalentFuelGas;
        public double FuelNitrogen => EquivalentFuelGas * Nitrogen;
        public double FuelCarbonDioxide => EquivalentFuelGas * CarbonDioxide;
        public double NitrogenSquared => Nitrogen * Nitrogen;
        public double NitrogenCarbonDioxide => Nitrogen * CarbonDioxide;
        public double NitrogenHydrogen => Nitrogen * Hydrogen;
        public double FuelHydrogen => EquivalentFuelGas * Hydrogen;
        public double FuelAuxiliarySeven => EquivalentFuelGas * AuxiliarySpeciesSeven;
        public double CarbonDioxideSquared => CarbonDioxide * CarbonDioxide;
        public double HydrogenSquared => Hydrogen * Hydrogen;
        public double AuxiliarySevenSquared => AuxiliarySpeciesSeven * AuxiliarySpeciesSeven;
    }

    /// <summary>Coefficients for a₀ + a₁T + a₂T² with T in kelvin (SGERG correlation form).</summary>
    private readonly record struct QuadraticTemperaturePolynomial(double A0, double A1, double A2)
    {
        public double EvaluateKelvin(double kelvin)
        {
            var t2 = kelvin * kelvin;
            return A0 + A1 * kelvin + A2 * t2;
        }
    }

    /// <summary>Fuel (group-1) virial term as quadratic in T, weighted by H⁰, H¹, H² (calorific parameter H).</summary>
    private readonly record struct FuelVirialPolynomialInH(
        QuadraticTemperaturePolynomial IndependentOfH,
        QuadraticTemperaturePolynomial LinearInH,
        QuadraticTemperaturePolynomial QuadraticInH)
    {
        public double Evaluate(double kelvin, double calorificParameterH)
        {
            var p0 = IndependentOfH.EvaluateKelvin(kelvin);
            var p1 = LinearInH.EvaluateKelvin(kelvin);
            var p2 = QuadraticInH.EvaluateKelvin(kelvin);
            return p0 + p1 * calorificParameterH + p2 * calorificParameterH * calorificParameterH;
        }
    }

    /// <summary>Indices into <see cref="Sgerg88VirialCoefficients.SecondVirialBinaryQuadratics"/> (order must match the table).</summary>
    private enum SecondVirialBinaryIndex : byte
    {
        NitrogenNitrogen = 0,
        NitrogenCarbonDioxide = 1,
        CarbonDioxideCarbonDioxide = 2,
        FuelHydrogen = 3,
        FuelAuxiliarySeven = 4,
        HydrogenHydrogen = 5,
        AuxiliarySevenAuxiliarySeven = 6,
    }

    /// <summary>Indices into <see cref="Sgerg88VirialCoefficients.ThirdVirialTripleQuadratics"/> (order must match the table).</summary>
    private enum ThirdVirialTripleIndex : byte
    {
        NitrogenTriple = 0,
        NitrogenNitrogenCarbonDioxide = 1,
        NitrogenCarbonDioxidePair = 2,
        CarbonDioxideTriple = 3,
        HydrogenTriple = 4,
        FuelFuelAuxiliarySeven = 5,
    }

    /// <summary>Published SGERG-88 numerical coefficients (static, immutable).</summary>
    private static class Sgerg88VirialCoefficients
    {
        /// <summary>Second virial for fuel (group 1), explicit in calorific parameter H.</summary>
        public static readonly FuelVirialPolynomialInH SecondVirialFuelB11 = new(
            IndependentOfH: new(A0: -0.425468, A1: 0.286500e-2, A2: -0.462073e-5),
            LinearInH: new(A0: 0.877118e-3, A1: -0.556281e-5, A2: 0.881510e-8),
            QuadraticInH: new(A0: -0.824747e-6, A1: 0.431436e-8, A2: -0.608319e-11));

        /// <summary>Third virial for fuel triple, explicit in H.</summary>
        public static readonly FuelVirialPolynomialInH ThirdVirialFuelC111 = new(
            IndependentOfH: new(A0: -0.302488, A1: 0.195861e-2, A2: -0.316302e-5),
            LinearInH: new(A0: 0.646422e-3, A1: -0.422876e-5, A2: 0.688157e-8),
            QuadraticInH: new(A0: -0.332805e-6, A1: 0.223160e-8, A2: -0.367713e-11));

        /// <summary>Binary second-virial contributions B_ij(T), one row per <see cref="SecondVirialBinaryIndex"/>.</summary>
        public static readonly QuadraticTemperaturePolynomial[] SecondVirialBinaryQuadratics = new QuadraticTemperaturePolynomial[]
        {
            new(A0: -0.144600, A1: 0.740910e-3, A2: -0.911950e-6),   // N2–N2
            new(A0: -0.339693, A1: 0.161176e-2, A2: -0.204429e-5),   // N2–CO2
            new(A0: -0.868340, A1: 0.403760e-2, A2: -0.516570e-5),   // CO2–CO2
            new(A0: -0.521280e-1, A1: 0.271570e-3, A2: -0.25e-6),    // fuel–H2
            new(A0: -0.687290e-1, A1: -0.239381e-5, A2: 0.518195e-6), // fuel–aux. 7
            new(A0: -0.110596e-2, A1: 0.813385e-4, A2: -0.987220e-7), // H2–H2
            new(A0: -0.130820, A1: 0.602540e-3, A2: -0.644300e-6),    // aux. 7–aux. 7
        };

        /// <summary>Triple third-virial contributions C_ijk(T), one row per <see cref="ThirdVirialTripleIndex"/>.</summary>
        public static readonly QuadraticTemperaturePolynomial[] ThirdVirialTripleQuadratics = new QuadraticTemperaturePolynomial[]
        {
            new(A0: 0.784980e-2, A1: -0.398950e-4, A2: 0.611870e-7),   // N2–N2–N2
            new(A0: 0.552066e-2, A1: -0.168609e-4, A2: 0.157169e-7),   // N2–N2–CO2
            new(A0: 0.358783e-2, A1: 0.806674e-5, A2: -0.325798e-7),  // N2–CO2–CO2
            new(A0: 0.205130e-2, A1: 0.348880e-4, A2: -0.837030e-7),   // CO2–CO2–CO2
            new(A0: 0.104711e-2, A1: -0.364887e-5, A2: 0.467095e-8),   // H2–H2–H2
            new(A0: 0.736748e-2, A1: -0.276578e-4, A2: 0.343051e-7),   // fuel–fuel–aux. 7
        };

        public static QuadraticTemperaturePolynomial SecondBinary(SecondVirialBinaryIndex index) =>
            SecondVirialBinaryQuadratics[(int)index];

        public static QuadraticTemperaturePolynomial ThirdTriple(ThirdVirialTripleIndex index) =>
            ThirdVirialTripleQuadratics[(int)index];
    }

    private const double BinaryVirialHydrogenNitrogen = 0.012;

    private const double BinaryInteractionFuelNitrogen = 0.72;
    private const double BinaryInteractionFuelCarbonDioxide = -0.865;
    private const double TernaryCombiningFuelNitrogen = 0.92;
    private const double TernaryCombiningFuelCarbonDioxide = 0.92;
    private const double TernaryCombiningFuelNitrogenCarbonDioxide = 1.10;
    private const double TernaryCombiningFuelHydrogen = 1.2;

    private const double FuelMolarMassIntercept = -2.709328;
    private const double FuelMolarMassSlope = 0.021062199;
    private const double MolarMassNitrogen = 28.0135;
    private const double MolarMassCarbonDioxide = 44.010;
    private const double MolarMassHydrogen = 2.0159;
    private const double MolarMassAuxiliarySeven = 28.010;
    private const double ReferenceMolarVolumeOffset = 22.414097;
    private const double RelativeDensityAirReference = 1.292923;
    private const double CelsiusZeroKelvin = 273.15;
    private const double HydrogenCombustionCorrection = 285.83;
    private const double AuxiliarySevenCombustionCorrection = 282.98;
    private const double GasConstantBarLiterPerMolKelvin = 0.0831451;
    private const double HydrogenToAuxiliarySevenMoleRatio = 0.0964;

    private double _inputSuperiorCalorificValue;
    private double _carbonDioxideMoleFraction;
    private double _hydrogenMoleFraction;
    private double _equivalentFuelMoleFraction;
    private double _nitrogenMoleFraction;

    /// <summary>Calculates <i>Z</i>, inferred nitrogen fraction, and molar density from a single input record.</summary>
    public NaturalGasCompressionResult Calculate(in NaturalGasPropertyInputs gas) =>
        Calculate(
            gas.AbsolutePressureBar,
            gas.TemperatureCelsius,
            gas.RelativeDensity,
            gas.CarbonDioxideMoleFraction,
            gas.HydrogenMoleFraction,
            gas.SuperiorCalorificValueMegajoulesPerCubicMeter);

    /// <summary>
    /// Calculates compression factor and related quantities.
    /// </summary>
    /// <param name="absolutePressureBar">Absolute pressure (bar), 0–120.</param>
    /// <param name="temperatureCelsius">Temperature (°C), −23 to 65.</param>
    /// <param name="relativeDensity">Relative density vs. dry air (1 = air), 0.55–0.90.</param>
    /// <param name="carbonDioxideMoleFraction">Mole fraction CO₂, 0–0.30.</param>
    /// <param name="hydrogenMoleFraction">Mole fraction H₂, 0–0.10.</param>
    /// <param name="superiorCalorificValueMegajoulesPerCubicMeter">
    /// Superior (gross) calorific value (MJ/m³), 20–48; metering basis 0 °C, 1.01325 bar per standard procedure.
    /// </param>
    public NaturalGasCompressionResult Calculate(
        double absolutePressureBar,
        double temperatureCelsius,
        double relativeDensity,
        double carbonDioxideMoleFraction,
        double hydrogenMoleFraction,
        double superiorCalorificValueMegajoulesPerCubicMeter)
    {
        if (absolutePressureBar is < 0 or > 120)
            throw new ArgumentOutOfRangeException(nameof(absolutePressureBar), "Pressure must be 0–120 bar.");
        if (temperatureCelsius is < -23 or > 65)
            throw new ArgumentOutOfRangeException(nameof(temperatureCelsius), "Temperature must be -23 to 65 °C.");

        var (nitrogenMoleFraction, compressionFactor, molarDensityMolPerLiter) = RunSgergIteration(
            absolutePressureBar,
            temperatureCelsius,
            carbonDioxideMoleFraction,
            hydrogenMoleFraction,
            superiorCalorificValueMegajoulesPerCubicMeter,
            relativeDensity);

        // Internal model uses R in bar·L/(mol·K); reciprocal molar volume is mol/L — convert to mol/m³.
        var molarDensitySi = molarDensityMolPerLiter * 1000.0;
        return new NaturalGasCompressionResult(nitrogenMoleFraction, compressionFactor, molarDensitySi);
    }

    private MoleFractionSnapshot BuildMoleFractionSnapshot() =>
        new(
            EquivalentFuelGas: _equivalentFuelMoleFraction,
            Nitrogen: _nitrogenMoleFraction,
            CarbonDioxide: _carbonDioxideMoleFraction,
            Hydrogen: _hydrogenMoleFraction,
            AuxiliarySpeciesSeven: _hydrogenMoleFraction * HydrogenToAuxiliarySevenMoleRatio);

    private (double nitrogenMoleFraction, double compressionFactor, double molarDensityMolPerLiter) RunSgergIteration(
        double pressureBar,
        double temperatureCelsius,
        double co2MoleFraction,
        double h2MoleFraction,
        double superiorCalorificValueMjPerM3,
        double relativeDensity)
    {
        _inputSuperiorCalorificValue = superiorCalorificValueMjPerM3;
        _carbonDioxideMoleFraction = co2MoleFraction;
        _hydrogenMoleFraction = h2MoleFraction;

        if (relativeDensity is < 0.55 or > 0.90)
            throw new ArgumentOutOfRangeException(nameof(relativeDensity), "Relative density must be 0.55–0.90.");
        if (_carbonDioxideMoleFraction is < 0.0 or > 0.30)
            throw new ArgumentOutOfRangeException(nameof(co2MoleFraction), "CO₂ mole fraction must be 0–0.30.");
        if (_inputSuperiorCalorificValue is < 20.0 or > 48.0)
            throw new ArgumentOutOfRangeException(nameof(superiorCalorificValueMjPerM3), "Calorific value must be 20–48 MJ/m³.");
        if (_hydrogenMoleFraction is < 0.0 or > 0.10)
            throw new ArgumentOutOfRangeException(nameof(h2MoleFraction), "H₂ mole fraction must be 0–0.10.");

        if (0.55 + 0.97 * _carbonDioxideMoleFraction - 0.45 * _hydrogenMoleFraction > relativeDensity)
            throw new ArgumentException("Conflicting input parameters for relative density and inerts.");

        var targetApparentMolarMass = relativeDensity * RelativeDensityAirReference;
        var effectiveSecondVirialGuess = -0.065;
        var calorificParameterH = 1000.0;
        var molarMassClosureFactor = 1.0 / (ReferenceMolarVolumeOffset + effectiveSecondVirialGuess);
        var heatIterationCount = 0;
        var molarMassIterationCount = 0;

        while (true)
        {
            var trialApparentMolarMass = UpdateCompositionForMolarMass(calorificParameterH, ref molarMassClosureFactor);

            if (Math.Abs(targetApparentMolarMass - trialApparentMolarMass) > 1.0e-6)
            {
                var trialAtHPlusOne = UpdateCompositionForMolarMass(calorificParameterH + 1.0, ref molarMassClosureFactor);
                calorificParameterH += (targetApparentMolarMass - trialApparentMolarMass) / (trialAtHPlusOne - trialApparentMolarMass);
                molarMassIterationCount++;
                if (molarMassIterationCount > 20)
                    throw new InvalidOperationException("SGERG-88: no convergence in molar mass iteration.");
                continue;
            }

            var moles = BuildMoleFractionSnapshot();
            var b11AtReference = SecondVirialFuelTerm(CelsiusZeroKelvin, calorificParameterH);
            effectiveSecondVirialGuess = MixtureSecondVirialCoefficient(CelsiusZeroKelvin, b11AtReference, in moles);
            molarMassClosureFactor = 1.0 / (ReferenceMolarVolumeOffset + effectiveSecondVirialGuess);
            var reconstructedCalorificValue =
                _equivalentFuelMoleFraction * calorificParameterH * molarMassClosureFactor
                + (_hydrogenMoleFraction * HydrogenCombustionCorrection
                   + moles.AuxiliarySpeciesSeven * AuxiliarySevenCombustionCorrection) * molarMassClosureFactor;

            if (Math.Abs(_inputSuperiorCalorificValue - reconstructedCalorificValue) > 1.0e-4)
            {
                heatIterationCount++;
                if (heatIterationCount > 20)
                    throw new InvalidOperationException("SGERG-88: no convergence in calorific value iteration.");
                continue;
            }

            break;
        }

        if (_nitrogenMoleFraction is < -0.01 or > 0.5)
            throw new InvalidOperationException("Calculated N₂ fraction out of range.");
        if (_nitrogenMoleFraction + _carbonDioxideMoleFraction > 0.5)
            throw new InvalidOperationException("Sum of N₂ and CO₂ mole fractions out of range.");
        if (0.55 + 0.4 * _nitrogenMoleFraction + 0.97 * _carbonDioxideMoleFraction - 0.45 * _hydrogenMoleFraction > relativeDensity)
            throw new InvalidOperationException("Conflicting result vs. relative density.");

        var inferredNitrogen = _nitrogenMoleFraction;
        var temperatureKelvin = temperatureCelsius + CelsiusZeroKelvin;
        var molesFinal = BuildMoleFractionSnapshot();
        var b11 = SecondVirialFuelTerm(temperatureKelvin, calorificParameterH);
        var mixtureB = MixtureSecondVirialCoefficient(temperatureKelvin, b11, in molesFinal);
        var mixtureC = MixtureThirdVirialCoefficient(temperatureKelvin, calorificParameterH, in molesFinal);
        var (molarVolumeLitersPerMol, z) = SolveMolarVolumeAndCompressionFactor(pressureBar, temperatureKelvin, mixtureB, mixtureC);
        var densityMolPerLiter = 1.0 / molarVolumeLitersPerMol;
        return (inferredNitrogen, z, densityMolPerLiter);
    }

    /// <summary>Advances fuel and nitrogen mole fractions for a trial calorific parameter <paramref name="h"/>; returns apparent molar mass.</summary>
    private double UpdateCompositionForMolarMass(double h, ref double molarMassClosureFactor)
    {
        var fuelMolarMass = FuelMolarMassIntercept + FuelMolarMassSlope * h;
        var auxiliarySeven = _hydrogenMoleFraction * HydrogenToAuxiliarySevenMoleRatio;
        _equivalentFuelMoleFraction =
            (_inputSuperiorCalorificValue
             - (_hydrogenMoleFraction * HydrogenCombustionCorrection + auxiliarySeven * AuxiliarySevenCombustionCorrection)
             * molarMassClosureFactor) / h / molarMassClosureFactor;
        _nitrogenMoleFraction =
            1.0 - _equivalentFuelMoleFraction - _carbonDioxideMoleFraction - _hydrogenMoleFraction - auxiliarySeven;
        return (_equivalentFuelMoleFraction * fuelMolarMass
                + _nitrogenMoleFraction * MolarMassNitrogen
                + _carbonDioxideMoleFraction * MolarMassCarbonDioxide
                + _hydrogenMoleFraction * MolarMassHydrogen
                + auxiliarySeven * MolarMassAuxiliarySeven) * molarMassClosureFactor;
    }

    private static double SecondVirialFuelTerm(double temperatureKelvin, double calorificParameterH) =>
        Sgerg88VirialCoefficients.SecondVirialFuelB11.Evaluate(temperatureKelvin, calorificParameterH);

    private static double MixtureSecondVirialCoefficient(double temperatureKelvin, double b11Fuel, in MoleFractionSnapshot moles)
    {
        var b22 = Sgerg88VirialCoefficients.SecondBinary(SecondVirialBinaryIndex.NitrogenNitrogen).EvaluateKelvin(temperatureKelvin);
        var b23 = Sgerg88VirialCoefficients.SecondBinary(SecondVirialBinaryIndex.NitrogenCarbonDioxide).EvaluateKelvin(temperatureKelvin);
        var b33 = Sgerg88VirialCoefficients.SecondBinary(SecondVirialBinaryIndex.CarbonDioxideCarbonDioxide).EvaluateKelvin(temperatureKelvin);
        var b15 = Sgerg88VirialCoefficients.SecondBinary(SecondVirialBinaryIndex.FuelHydrogen).EvaluateKelvin(temperatureKelvin);
        var b55 = Sgerg88VirialCoefficients.SecondBinary(SecondVirialBinaryIndex.HydrogenHydrogen).EvaluateKelvin(temperatureKelvin);
        var b17 = Sgerg88VirialCoefficients.SecondBinary(SecondVirialBinaryIndex.FuelAuxiliarySeven).EvaluateKelvin(temperatureKelvin);
        var b77 = Sgerg88VirialCoefficients.SecondBinary(SecondVirialBinaryIndex.AuxiliarySevenAuxiliarySeven).EvaluateKelvin(temperatureKelvin);

        var productFuelTimesCo2SecondVirial = b11Fuel * b33;
        if (productFuelTimesCo2SecondVirial < 0.0)
            throw new InvalidOperationException("No viable solution for second virial mixture term (B).");

        var temperatureDependentBinaryFuelNitrogen =
            BinaryInteractionFuelNitrogen + (320.0 - temperatureKelvin) * (320.0 - temperatureKelvin) * 1.875e-5;

        return moles.FuelSquared * b11Fuel
               + moles.FuelNitrogen * temperatureDependentBinaryFuelNitrogen * (b11Fuel + b22)
               + 2.0 * moles.FuelCarbonDioxide * BinaryInteractionFuelCarbonDioxide * Math.Sqrt(productFuelTimesCo2SecondVirial)
               + moles.NitrogenSquared * b22
               + 2.0 * moles.NitrogenCarbonDioxide * b23
               + moles.CarbonDioxideSquared * b33
               + moles.HydrogenSquared * b55
               + 2.0 * moles.FuelHydrogen * b15
               + 2.0 * moles.NitrogenHydrogen * BinaryVirialHydrogenNitrogen
               + 2.0 * moles.FuelAuxiliarySeven * b17
               + moles.AuxiliarySevenSquared * b77;
    }

    private static double MixtureThirdVirialCoefficient(double temperatureKelvin, double calorificParameterH, in MoleFractionSnapshot moles)
    {
        var c111 = Sgerg88VirialCoefficients.ThirdVirialFuelC111.Evaluate(temperatureKelvin, calorificParameterH);
        var c222 = Sgerg88VirialCoefficients.ThirdTriple(ThirdVirialTripleIndex.NitrogenTriple).EvaluateKelvin(temperatureKelvin);
        var c223 = Sgerg88VirialCoefficients.ThirdTriple(ThirdVirialTripleIndex.NitrogenNitrogenCarbonDioxide).EvaluateKelvin(temperatureKelvin);
        var c233 = Sgerg88VirialCoefficients.ThirdTriple(ThirdVirialTripleIndex.NitrogenCarbonDioxidePair).EvaluateKelvin(temperatureKelvin);
        var c333 = Sgerg88VirialCoefficients.ThirdTriple(ThirdVirialTripleIndex.CarbonDioxideTriple).EvaluateKelvin(temperatureKelvin);
        var c555 = Sgerg88VirialCoefficients.ThirdTriple(ThirdVirialTripleIndex.HydrogenTriple).EvaluateKelvin(temperatureKelvin);
        var c117 = Sgerg88VirialCoefficients.ThirdTriple(ThirdVirialTripleIndex.FuelFuelAuxiliarySeven).EvaluateKelvin(temperatureKelvin);

        var tripleProductFuelFuelNitrogen = c111 * c111 * c222;
        var tripleProductFuelFuelCo2 = c111 * c111 * c333;
        var tripleProductFuelNitrogenNitrogen = c111 * c222 * c222;
        var tripleProductFuelNitrogenCo2 = c111 * c222 * c333;
        var tripleProductFuelCo2Co2 = c111 * c333 * c333;
        var tripleProductFuelFuelHydrogen = c111 * c111 * c555;

        if (tripleProductFuelFuelNitrogen < 0.0 || tripleProductFuelFuelCo2 < 0.0 || tripleProductFuelNitrogenNitrogen < 0.0 ||
            tripleProductFuelNitrogenCo2 < 0.0 || tripleProductFuelCo2Co2 < 0.0 || tripleProductFuelFuelHydrogen < 0.0)
            throw new InvalidOperationException("No viable solution for third virial mixture term (C).");

        var y12TemperatureAdjustment = TernaryCombiningFuelNitrogen + (temperatureKelvin - 270.0) * 0.0013;

        return moles.EquivalentFuelGas * moles.FuelSquared * c111
               + 3.0 * moles.FuelSquared * moles.Nitrogen * Math.Cbrt(tripleProductFuelFuelNitrogen) * y12TemperatureAdjustment
               + 3.0 * moles.FuelSquared * moles.CarbonDioxide * Math.Cbrt(tripleProductFuelFuelCo2) * TernaryCombiningFuelCarbonDioxide
               + 3.0 * moles.EquivalentFuelGas * moles.FuelHydrogen * Math.Cbrt(tripleProductFuelFuelHydrogen) * TernaryCombiningFuelHydrogen
               + 3.0 * moles.EquivalentFuelGas * moles.NitrogenSquared * Math.Cbrt(tripleProductFuelNitrogenNitrogen) * y12TemperatureAdjustment
               + 6.0 * moles.EquivalentFuelGas * moles.Nitrogen * moles.CarbonDioxide * Math.Cbrt(tripleProductFuelNitrogenCo2)
               * TernaryCombiningFuelNitrogenCarbonDioxide
               + 3.0 * moles.EquivalentFuelGas * moles.CarbonDioxideSquared * Math.Cbrt(tripleProductFuelCo2Co2) * TernaryCombiningFuelCarbonDioxide
               + moles.NitrogenSquared * moles.Nitrogen * c222
               + 3.0 * moles.NitrogenSquared * moles.CarbonDioxide * c223
               + 3.0 * moles.Nitrogen * moles.CarbonDioxideSquared * c233
               + moles.CarbonDioxide * moles.CarbonDioxideSquared * c333
               + moles.Hydrogen * moles.HydrogenSquared * c555
               + 3.0 * moles.FuelSquared * moles.AuxiliarySpeciesSeven * c117;
    }

    private static (double molarVolumeLitersPerMol, double compressionFactor) SolveMolarVolumeAndCompressionFactor(
        double pressureBar,
        double temperatureKelvin,
        double mixtureSecondVirial,
        double mixtureThirdVirial)
    {
        var rt = GasConstantBarLiterPerMolKelvin * temperatureKelvin;
        var idealGasMolarVolume = rt / pressureBar;
        var molarVolumeLitersPerMol = idealGasMolarVolume + mixtureSecondVirial;
        var iteration = 0;

        while (true)
        {
            molarVolumeLitersPerMol = idealGasMolarVolume * (1.0 + mixtureSecondVirial / molarVolumeLitersPerMol
                                                            + mixtureThirdVirial / (molarVolumeLitersPerMol * molarVolumeLitersPerMol));
            iteration++;
            if (iteration > 20)
                throw new InvalidOperationException("SGERG-88: no convergence in compressibility iteration.");

            var compressionFactor =
                1.0 + mixtureSecondVirial / molarVolumeLitersPerMol
                    + mixtureThirdVirial / (molarVolumeLitersPerMol * molarVolumeLitersPerMol);
            var pressureFromVirial = rt / molarVolumeLitersPerMol * compressionFactor;

            if (Math.Abs(pressureFromVirial - pressureBar) < 1.0e-5)
                return (molarVolumeLitersPerMol, compressionFactor);
        }
    }
}
