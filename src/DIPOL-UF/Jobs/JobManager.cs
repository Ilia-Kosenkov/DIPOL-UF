﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ANDOR_CS;
using ANDOR_CS.AcquisitionMetadata;
using ANDOR_CS.Classes;
using ANDOR_CS.Enums;
using DIPOL_UF.Converters;
using DIPOL_UF.Enums;
using DIPOL_UF.Models;
using DIPOL_UF.UserNotifications;
using DynamicData;
using DynamicData.Binding;
using FITS_CS;
using ReactiveUI.Fody.Helpers;
using Serilog.Events;
using Localization = DIPOL_UF.Properties.Localization;
using MessageBox = System.Windows.MessageBox;

namespace DIPOL_UF.Jobs
{
    internal sealed record ObservationScenario(string Light, string Bias, string Dark);
    internal sealed partial class JobManager : ReactiveObjectEx
    {
        // These regimes might produce data that will overflow uint16 format,
        // therefore images should be save as int32
        private static readonly AcquisitionMode[] LongFormatModes =
        {
            AcquisitionMode.Accumulation,
            AcquisitionMode.Kinetic,
            AcquisitionMode.FastKinetics
        };


        private DipolMainWindow _windowRef;
        private byte[] _settingsRep;
        private List<CameraTab> _jobControls;
        private Dictionary<int, IAcquisitionSettings> _settingsCache;
        private Task _jobTask;
        private CancellationTokenSource _tokenSource;
        private Dictionary<int, Request> _requestMap;

        private bool _firstRun;

        public int AcquisitionActionCount { get; private set; }
        public int TotalAcquisitionActionCount { get; private set; }
        public int BiasActionCount { get; private set; }
        public int DarkActionCount { get; private set; }

        internal bool NeedsCalibration { get; set; } = true;

        public IObservableCache<(string, IDevice), string> ConnectedCameras => _windowRef.ConnectedCameras;

        public static JobManager Manager { get; } = new JobManager();
        


        [Reactive]
        public float? MotorPosition { get; private set; }
        [Reactive]
        public int Progress { get; private set; }
        [Reactive]
        public int CumulativeProgress { get; private set; }
        [Reactive]
        public int Total { get; private set; }
        [Reactive]
        public string CurrentJobName { get; private set; }
        [Reactive]
        public bool IsInProcess { get; private set; }
        [Reactive]
        public bool ReadyToRun { get; private set; }
        
        [Reactive]
        public CycleType? CurrentCycleType { get; private set; }
        
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        public bool AnyCameraIsAcquiring { [ObservableAsProperty] get; }
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        public bool IsRegimeSwitching { [ObservableAsProperty] get; }

        // ReSharper disable once UnassignedGetOnlyAutoProperty
        public bool AllCamerasHaveSettings { [ObservableAsProperty] get; }
        public float? ActualMotorPosition { get; private set; }

        [Obsolete("Use " + nameof(CurrentTarget1), true)]
        public Target CurrentTarget { get; private set; } = new Target();
        public Target1 CurrentTarget1 { get; private set; } = new Target1();

        public Job AcquisitionJob { get; private set; }
        public Job BiasJob { get; private set; }
        public Job DarkJob { get; private set; }
        public int AcquisitionRuns { get; private set; }

        public void AttachToMainWindow(DipolMainWindow window)
        {
            if(_windowRef is null)
                _windowRef = window ?? throw new ArgumentNullException(nameof(window));
            else
                throw new InvalidOperationException(Localization.General_ShouldNotHappen);

            _windowRef.ConnectedCameras.Connect()
                .Transform(x => x.Camera)
                .TrueForAny(x => x.WhenPropertyChanged(y => y.IsAcquiring).Select(y => y.Value),
                    z => z)
                .ToPropertyEx(this, x => x.AnyCameraIsAcquiring)
                .DisposeWith(Subscriptions);

            _windowRef.ConnectedCameras.Connect()
                .Transform(x => x.Camera)
                .TrueForAll(
                    x => x.WhenPropertyChanged(y => y.CurrentSettings).Select(y => y.Value is { }),
                    z => z)
                .ToPropertyEx(this, x => x.AllCamerasHaveSettings)
                .DisposeWith(Subscriptions);

            _windowRef.WhenPropertyChanged(x => x.IsSwitchingRegimes)
                .Select(x => x.Value)
                .ToPropertyEx(this, x => x.IsRegimeSwitching)
                .DisposeWith(Subscriptions);

        }

        public void StartAllAcquisitions()
        {
            var tabs = GetCameraTabs();
            foreach (var tab in tabs)
            {
                var command = tab.StartAcquisitionCommand as ICommand;
                if(command.CanExecute(Unit.Default))
                    command.Execute(Unit.Default);
            }

        }

        public void StopAllAcquisitions()
        {
            var tabs = GetCameraTabs(true);
            foreach (var tab in tabs)
            {
                var command = tab.StartAcquisitionCommand as ICommand;
                if (command.CanExecute(Unit.Default))
                    command.Execute(Unit.Default);
            }
        }

        public void StartJob(int nRepeats)
        {
            BiasActionCount = (_firstRun || NeedsCalibration) ? BiasJob?.NumberOfActions<CameraAction>() ?? 0 : 0;
            DarkActionCount = (_firstRun || NeedsCalibration) ? DarkJob?.NumberOfActions<CameraAction>() ?? 0 : 0;

            AcquisitionRuns = nRepeats <= 1 ? 1 : nRepeats;

            AcquisitionActionCount = AcquisitionJob.NumberOfActions<CameraAction>();
            TotalAcquisitionActionCount = AcquisitionActionCount * AcquisitionRuns;

            CumulativeProgress = 0;
            IsInProcess = true;
            ReadyToRun = false;
            _tokenSource?.Dispose();
            _tokenSource = new CancellationTokenSource();
            _jobTask = StartJobAsync(_tokenSource.Token);
        }

        public void StopJob()
        {
            if (_jobTask is null)
                return;
            if (!_jobTask.IsCompleted)
            {
                if (_firstRun && !NeedsCalibration)
                    NeedsCalibration = true;

                _tokenSource?.Cancel();

            }
        }

        [Obsolete("Use " + nameof(CurrentTarget1), true)]
        public Task SubmitNewTarget(Target target)
        {
            ReadyToRun = false;
            CurrentTarget = target ?? throw new ArgumentNullException(
                                Localization.General_ShouldNotHappen,
                                nameof(target));
            return SetupNewTarget();
        }

        public IImmutableDictionary<string, IDevice> GetCameras() 
            => _windowRef.ConnectedCameras.Items.ToImmutableDictionary(
                x => ConverterImplementations.CameraToFilterConversion(x.Camera), x => x.Camera);

        public async Task SubmitNewTarget1(Target1 target)
        {
            var oldTarget = CurrentTarget1;
            ReadyToRun = false;
            CurrentTarget1 = target ?? throw new ArgumentNullException(
                Localization.General_ShouldNotHappen,
                nameof(target));

            try
            {
                await SetupNewTarget1();
                // If cycle type has changed and both old and new are polarimetric,
                // this is likely an error, because change of cycle type requires 
                // manual replacement of the plate inside of the polarimeter
                if (
                    oldTarget.CycleType != target.CycleType &&
                    oldTarget.CycleType.IsPolarimetric() &&
                    target.CycleType.IsPolarimetric()
                )
                {
                    Injector.LocateOrDefault<IUserNotifier>()?.Info(
                        Localization.CalciteWarning_Caption, 
                        string.Format(
                            Localization.CalciteWarning_Message, 
                            oldTarget.CycleType.GetEnumNameRep().Full,
                            target.CycleType.GetEnumNameRep().Full
                        )
                    );
                }
            }
            catch (Exception)
            {
                CurrentTarget1 = oldTarget;
                throw;
            }
        }

        public Target1 GenerateTarget1()
        {
            var result = CurrentTarget1.Clone();
            var setts = GetCameras().ToImmutableDictionary(x => x.Key, x => x.Value.CurrentSettings);
            if (setts.All(x => x.Value is {}))
            {
                // If all settings are present, recalculating target value
                result = Target1.FromSettings(setts, result.StarName, result.Description, result.CycleType);
            }

            return result;
        }

        private IReadOnlyList<CameraTab> GetCameraTabs(bool acquiring = false) =>
            _windowRef.CameraTabs.Items.Where(x => acquiring ? x.Tab.Camera.IsAcquiring : !x.Tab.Camera.IsAcquiring).Select(x => x.Tab).ToList();

        private async Task SetupNewTarget1()
        {

            try
            {
                var jobScenario = GetJobScenario(CurrentTarget1.CycleType);

                AcquisitionJob = await ConstructJob(jobScenario.Light);

                if(CurrentTarget1.CycleType.IsPolarimetric()
                    && (!AcquisitionJob.ContainsActionOfType<MotorAction>() || _windowRef.PolarimeterMotor is null))
                    throw new InvalidOperationException("Cannot execute current control with no motor connected.");

                BiasJob = await ConstructJob(jobScenario.Bias);
                BiasActionCount = BiasJob?.NumberOfActions<CameraAction>() ?? 0;

                DarkJob = await ConstructJob(jobScenario.Dark);
                DarkActionCount = DarkJob?.NumberOfActions<CameraAction>() ?? 0;
                
                ApplySettingsTemplate1();
                
                _firstRun = true;
                ReadyToRun = true;
                NeedsCalibration = true;
                CurrentCycleType = CurrentTarget1.CycleType;
                Helper.WriteLog(
                    LogEventLevel.Information, "Set up new target {StarName} in {CycleType} regime",
                    CurrentTarget1.StarName, 
                    CurrentTarget1.CycleType.GetEnumNameRep().Full
                );

            }
            catch (Exception ex)
            {
                CurrentTarget1 = new Target1();
                AcquisitionJob = null;
                BiasJob = null;
                DarkJob = null;
                _settingsRep = null;
                Helper.WriteLog(LogEventLevel.Error, ex,  @"Failed to apply new target");
                throw;
            }
        }

        private async Task StartJobAsync(CancellationToken token)
        {
            async Task DoCameraJobAsync(Job job, string file, FrameType type)
            {
                if (job is null)
                    return;
                try
                {
                    if (job.ContainsActionOfType<CameraAction>())
                        foreach (var control in _jobControls)
                            control.Camera.StartImageSavingSequence(
                                CurrentTarget1.StarName!, 
                                file,
                                ConverterImplementations.CameraToFilterConversion(control.Camera),
                                type,
                                new[] { FitsKey.CreateDate("STDATE", DateTimeOffset.Now.UtcDateTime, format: @"yyyy-MM-ddTHH:mm:ss.fff") });
                    MotorPosition = job.ContainsActionOfType<MotorAction>()
                        ? new float?(0)
                        : null;
                    ActualMotorPosition = MotorPosition;


                    await job.Run(token);
                }
                finally
                {
                    if (job.ContainsActionOfType<CameraAction>())
                        foreach (var control in _jobControls)
                            await control.Camera.FinishImageSavingSequenceAsync();

                    MotorPosition = null;
                    ActualMotorPosition = null;
                }
            }

            try
            {
                // To avoid random NREs
                CurrentTarget1.StarName ??= $"star_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

                _jobControls = _windowRef.CameraTabs.Items.Select(x => x.Tab).ToList();
                _settingsCache = _jobControls.ToDictionary(x => x.Camera.GetHashCode(),
                    y => y.Camera.CurrentSettings.MakeCopy());
                if (_jobControls.Any(x => x.Camera?.CurrentSettings is null))
                    throw new InvalidOperationException("At least one camera has no settings applied to it.");

                _requestMap = _jobControls.ToDictionary(
                    x => x.Camera.GetHashCode(),
                    y => new Request(LongFormatModes.Any(z => y.Camera?.CurrentSettings.AcquisitionMode?.HasFlag(z) == true)
                        ? ImageFormat.SignedInt32
                        : ImageFormat.UnsignedInt16));

                if (CurrentTarget1.CycleType.IsPolarimetric()
                    && (_windowRef.Regime != InstrumentRegime.Polarimeter ||
                        _windowRef.PolarimeterMotor is null))
                {
                    throw new InvalidOperationException(Localization.JobManager_NotPolarimetry);
                }

                if (CurrentTarget1.CycleType.IsPhotometric()
                    && _windowRef.Regime == InstrumentRegime.Polarimeter)
                {
                    throw new InvalidOperationException(Localization.JobManager_NotPhotometry);
                }

                var calibrationsMade = false;
                //await Task.Factory.StartNew(async ()  =>
                {
                    var fileName = CurrentTarget1.StarName;
                    Progress = 0;
                    Total = TotalAcquisitionActionCount;
                    CurrentJobName = Localization.JobManager_AcquisitionJobName;
                    try
                    {
                        if (AcquisitionJob.ContainsActionOfType<CameraAction>())
                            foreach (var control in _jobControls)
                                control.Camera.StartImageSavingSequence(
                                    CurrentTarget1.StarName, fileName,
                                    ConverterImplementations.CameraToFilterConversion(control.Camera),
                                    FrameType.Light,
                                    new [] {  FitsKey.CreateDate("STDATE", DateTimeOffset.Now.UtcDateTime, format: @"yyyy-MM-ddTHH:mm:ss.fff") });

                        MotorPosition = AcquisitionJob.ContainsActionOfType<MotorAction>()
                            ? new float?(0)
                            : null;
                        ActualMotorPosition = MotorPosition;

                        Helper.WriteLog(LogEventLevel.Information, @"Initializing system before the new cycle.");
                        await AcquisitionJob.Initialize(token);

                        for (var i = 0; i < AcquisitionRuns; i++)
                        {
                            Helper.WriteLog(LogEventLevel.Information, "Running {i} cycle", i + 1);
                            await AcquisitionJob.Run(token);
                        }
                    }
                    finally
                    {
                        if (AcquisitionJob.ContainsActionOfType<CameraAction>())
                            foreach (var control in _jobControls)
                                await control.Camera.FinishImageSavingSequenceAsync();
                        MotorPosition = null;
                        ActualMotorPosition = MotorPosition;
                    }

                    var areCalibrationsNeeded = false;

                    if (_firstRun)
                        areCalibrationsNeeded = MessageBox.Show(
                            string.Format(
                                Localization.JobManager_TakeCalibrationsFirstTime_Text, CurrentTarget1.StarName
                            ),
                            Localization.JobManager_TakeCalibrationsFirstTime_Header,
                            MessageBoxButton.YesNo, MessageBoxImage.Question
                        ) == MessageBoxResult.Yes;
                    else if (NeedsCalibration)
                        areCalibrationsNeeded = MessageBox.Show(
                            Localization.JobManager_TakeCalibrations_Text,
                            Localization.JobManager_TakeCalibrations_Header,
                            MessageBoxButton.YesNo, MessageBoxImage.Question
                        ) == MessageBoxResult.Yes;

                    if (areCalibrationsNeeded)
                    {
                        Progress = 0;
                        Total = BiasActionCount;
                        CurrentJobName = Localization.JobManager_BiasJobName;
                        await DoCameraJobAsync(BiasJob, $"{CurrentTarget1.StarName}_bias", FrameType.Bias);
                    

                        Progress = 0;
                        Total = DarkActionCount;
                        CurrentJobName = Localization.JobManager_DarkJobName;
                        await DoCameraJobAsync(DarkJob, $"{CurrentTarget1.StarName}_dark", FrameType.Dark);

                        calibrationsMade = true;
                        NeedsCalibration = false;
                    }

                }//, token, );

                var report = $"{Environment.NewLine}{TotalAcquisitionActionCount} {Localization.JobManager_AcquisitionJobName}";

                if(calibrationsMade)
                    report += $",{Environment.NewLine}{BiasActionCount} {Localization.JobManager_BiasJobName},{Environment.NewLine}{BiasActionCount} {Localization.JobManager_DarkJobName}";
                
                MessageBox.Show(
                    string.Format(Localization.JobManager_MB_Finished_Text, report),
                    Localization.JobManager_MB_Finished_Header,
                    MessageBoxButton.OK, MessageBoxImage.Information);

                _firstRun = false;
            }
            catch (TaskCanceledException)
            {
                Helper.WriteLog(LogEventLevel.Warning, "Acquisition has been cancelled.");
                MessageBox.Show(
                    Localization.JobManager_MB_Acq_Cancelled_Text,
                    Localization.JobManager_MB_Acq_Cancelled_Header,
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                NeedsCalibration = true;
            }
            catch (Exception e)
            {
                Helper.WriteLog(LogEventLevel.Error, e, "Acquisition sequence has failed");
                MessageBox.Show(
                    string.Format(Localization.JobManager_MB_Failed_Text, e.Message),
                    Localization.JobManager_MB_Failed_Header,
                    MessageBoxButton.OK, MessageBoxImage.Error);

                NeedsCalibration = true;
            }
            finally
            {
                ReadyToRun = true;
                IsInProcess = false;
                Progress = 0;
                CurrentJobName = string.Empty;
                Total = 0;
                MotorPosition = null;
                ActualMotorPosition = null;
                _jobControls.Clear();
                _jobControls = null;
                foreach( var sett in _settingsCache)
                    sett.Value.Dispose();
                _settingsCache.Clear();
                _settingsCache = null;
            }
        }

        [Obsolete("Use " + nameof(CurrentTarget1), true)]
        private async Task SetupNewTarget()
        {
            try
            {
                AcquisitionJob = await ConstructJob(CurrentTarget.JobPath);
                AcquisitionRuns = CurrentTarget.Repeats > 0 
                    ? CurrentTarget.Repeats
                    : throw new InvalidOperationException(Localization.General_ShouldNotHappen);
                AcquisitionActionCount = AcquisitionJob.NumberOfActions<CameraAction>();
                TotalAcquisitionActionCount = AcquisitionActionCount * AcquisitionRuns;

                if (AcquisitionJob.ContainsActionOfType<MotorAction>()
                    && _windowRef.PolarimeterMotor is null)
                    throw new InvalidOperationException("Cannot execute current control with no motor connected.");

                BiasJob = CurrentTarget.BiasPath is null
                    ? null
                    : await ConstructJob(CurrentTarget.BiasPath);
                BiasActionCount = BiasJob?.NumberOfActions<CameraAction>() ?? 0;  

                DarkJob = CurrentTarget.DarkPath is null
                    ? null
                    : await ConstructJob(CurrentTarget.DarkPath);
                DarkActionCount = DarkJob?.NumberOfActions<CameraAction>() ?? 0;


                await LoadSettingsTemplate();
                await ApplySettingsTemplate();
                ReadyToRun = true;
            }
            catch (Exception)
            {
                CurrentTarget = new Target();
                AcquisitionJob = null;
                BiasJob = null;
                DarkJob = null;
                _settingsRep = null;
                throw;
            }
        }

        [Obsolete("Use " + nameof(CurrentTarget1), true)]
        private async Task LoadSettingsTemplate()
        {
            if(!File.Exists(CurrentTarget.SettingsPath))
                throw new FileNotFoundException("Settings file is not found", CurrentTarget.SettingsPath);

            if(_windowRef.ConnectedCameras.Count < 1)
                throw new InvalidOperationException("No connected cameras to work with.");


            using var str = new FileStream(CurrentTarget.SettingsPath, FileMode.Open, FileAccess.Read);
            _settingsRep = new byte[str.Length];
            await str.ReadAsync(_settingsRep, 0, _settingsRep.Length);
        }

        [Obsolete("Use " + nameof(CurrentTarget1), true)]
        private async Task ApplySettingsTemplate()
        {
            if (_settingsRep is null)
                throw new InvalidOperationException(Localization.General_ShouldNotHappen);

            var cameras = _windowRef.ConnectedCameras.Items.Select(x => x.Camera).ToList();

            if (cameras.Count < 1)
                throw new InvalidOperationException("No connected cameras to work with.");

            using var memory = new MemoryStream(_settingsRep, false);
            foreach (var cam in cameras)
            {
                memory.Position = 0;
                var template = cam.GetAcquisitionSettingsTemplate();
                await template.DeserializeAsync(memory, Encoding.ASCII, CancellationToken.None);


                cam.ApplySettings(template);
            }
        }

        private void ApplySettingsTemplate1()
        {
            var setts = CurrentTarget1.CreateTemplatesForCameras(GetCameras());
            foreach(var (_, s) in setts)
                s.Camera.ApplySettings(s);
        }


        private static async Task<Job> ConstructJob(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Job file is not found", path);

            using var str = new FileStream(path, FileMode.Open, FileAccess.Read);
            return await Job.CreateAsync(str);
        }
#nullable enable
        private static ObservationScenario GetJobScenario(CycleType type)
        {
            var scenarios = UiSettingsProvider.Settings.Get<Dictionary<string, ObservationScenario>>(@"Scenarios");

            ObservationScenario? scenario = null;
            scenarios?.TryGetValue(type.ToEnumName(), out scenario);
            scenario ??= new ObservationScenario(null, null, null);


            if (string.IsNullOrWhiteSpace(scenario.Light))
            {
                throw new InvalidOperationException("Unable to load job scenario.");
            }

            if (string.IsNullOrWhiteSpace(scenario.Bias))
            {
                scenario = scenario with {Bias = Path.ChangeExtension(scenario.Light, ".bias")};
            }

            if (string.IsNullOrWhiteSpace(scenario.Dark))
            {
                scenario = scenario with {Dark = Path.ChangeExtension(scenario.Light, ".dark")};
            }
            return scenario;
        }
#nullable restore
        private JobManager() { }


    }
}
