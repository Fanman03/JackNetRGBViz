﻿using System;
using KeyboardAudioVisualizer.AudioProcessing.Equalizer;
using KeyboardAudioVisualizer.AudioProcessing.Spectrum;
using KeyboardAudioVisualizer.Configuration;

namespace KeyboardAudioVisualizer.AudioProcessing.VisualizationProvider
{
    #region Configuration

    public enum ValueMode { Max, Average, Sum }
    public enum SpectrumMode { Gamma, Logarithmic, Linear }

    public class FrequencyBarsVisualizationProviderConfiguration : AbstractConfiguration
    {
        private ValueMode _valueMode = ValueMode.Sum;
        public ValueMode ValueMode
        {
            get => _valueMode;
            set => SetProperty(ref _valueMode, value);
        }

        private SpectrumMode _spectrumMode = SpectrumMode.Logarithmic;
        public SpectrumMode SpectrumMode
        {
            get => _spectrumMode;
            set => SetProperty(ref _spectrumMode, value);
        }

        private int _bars = 48;
        public int Bars
        {
            get => _bars;
            set => SetProperty(ref _bars, value);
        }

        private double _smoothing = 3;
        public double Smoothing
        {
            get => _smoothing;
            set => SetProperty(ref _smoothing, value);
        }

        private double _minFrequency = 60;
        public double MinFrequency
        {
            get => _minFrequency;
            set => SetProperty(ref _minFrequency, value);
        }

        private double _maxFrequency = 15000;
        public double MaxFrequency
        {
            get => _maxFrequency;
            set => SetProperty(ref _maxFrequency, value);
        }

        private double _referenceLevel = 90;
        public double ReferenceLevel
        {
            get => _referenceLevel;
            set => SetProperty(ref _referenceLevel, value);
        }

        private double _emphasisePeaks = 0.5f;
        public double EmphasisePeaks
        {
            get => _emphasisePeaks;
            set => SetProperty(ref _emphasisePeaks, value);
        }

        private int _gamma = 2;
        public int Gamma
        {
            get => _gamma;
            set => SetProperty(ref _gamma, value);
        }
    }

    #endregion

    public class FrequencyBarsVisualizationProvider : AbstractAudioProcessor, IVisualizationProvider
    {
        #region Properties & Fields

        private readonly FrequencyBarsVisualizationProviderConfiguration _configuration;
        private readonly ISpectrumProvider _spectrumProvider;

        private double _smoothingFactor;
        private double _emphasiseFactor;

        public IEqualizer Equalizer { get; set; }
        public IConfiguration Configuration => _configuration;
        public float[] VisualizationData { get; private set; }

        #endregion

        #region Constructors

        public FrequencyBarsVisualizationProvider(FrequencyBarsVisualizationProviderConfiguration configuration, ISpectrumProvider spectrumProvider)
        {
            this._configuration = configuration;
            this._spectrumProvider = spectrumProvider;

            configuration.PropertyChanged += (sender, args) => RecalculateConfigValues(args.PropertyName);
        }

        #endregion

        #region Methods

        public override void Initialize() => RecalculateConfigValues(null);

        private void RecalculateConfigValues(string changedPropertyName)
        {
            if ((changedPropertyName == null) || (changedPropertyName == nameof(FrequencyBarsVisualizationProviderConfiguration.Bars)))
                VisualizationData = new float[_configuration.Bars];

            if ((changedPropertyName == null) || (changedPropertyName == nameof(FrequencyBarsVisualizationProviderConfiguration.Smoothing)))
                _smoothingFactor = Math.Log10(_configuration.Smoothing);

            if ((changedPropertyName == null) || (changedPropertyName == nameof(FrequencyBarsVisualizationProviderConfiguration.EmphasisePeaks)))
                _emphasiseFactor = (0.75 * (1 + _configuration.EmphasisePeaks));
        }

        public override void Update()
        {
            ISpectrum spectrum = GetSpectrum();
            if (spectrum == null) return;

            float[] equalizerValues = Equalizer?.IsEnabled == true ? Equalizer.CalculateValues(spectrum.BandCount) : null;

            for (int i = 0; i < spectrum.BandCount; i++)
            {
                double binPower = GetBandValue(spectrum[i]);

                if (equalizerValues != null)
                {
                    float equalizerValue = equalizerValues[i];
                    equalizerValue *= 10; //TODO DarthAffe 13.08.2017: Equalizer-Scale through setting?
                    if (Math.Abs(equalizerValue) > 0.000001)
                    {
                        bool lower = equalizerValue < 0;
                        equalizerValue = 1 + (equalizerValue * equalizerValue);
                        binPower *= lower ? 1f / equalizerValue : equalizerValue;
                    }
                }

                binPower = Math.Max(0, 20 * Math.Log10(binPower));

                binPower = Math.Max(0, binPower);
                binPower /= _configuration.ReferenceLevel;
                if (_configuration.EmphasisePeaks > 0.001)
                    binPower = Math.Pow(binPower, 1 + _configuration.EmphasisePeaks) * _emphasiseFactor;

                if (i < VisualizationData.Length)
                {
                    VisualizationData[i] = (float)((VisualizationData[i] * _smoothingFactor) + (binPower * (1.0 - _smoothingFactor)));
                    if (float.IsNaN(VisualizationData[i])) VisualizationData[i] = 0;
                }
            }
        }

        private ISpectrum GetSpectrum()
        {
            switch (_configuration.SpectrumMode)
            {
                case SpectrumMode.Gamma:
                    return _spectrumProvider.GetGammaSpectrum(_configuration.Bars, _configuration.Gamma, (float)_configuration.MinFrequency, (float)_configuration.MaxFrequency);
                case SpectrumMode.Logarithmic:
                    return _spectrumProvider.GetLogarithmicSpectrum(_configuration.Bars, (float)_configuration.MinFrequency, (float)_configuration.MaxFrequency);
                case SpectrumMode.Linear:
                    return _spectrumProvider.GetLinearSpectrum(_configuration.Bars, (float)_configuration.MinFrequency, (float)_configuration.MaxFrequency);
                default:
                    return null;
            }
        }

        private float GetBandValue(Band band)
        {
            switch (_configuration.ValueMode)
            {
                case ValueMode.Max: return band.Max;
                case ValueMode.Average: return band.Average;
                case ValueMode.Sum: return band.Sum;
                default: return 0;
            }
        }

        #endregion
    }
}
