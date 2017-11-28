﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DIPOL_UF.Models;
using DIPOL_UF.Commands;

using ANDOR_CS.Classes;
using ANDOR_CS.Enums;

namespace DIPOL_UF.ViewModels
{
    class ConnectedCameraViewModel : ViewModel<ConnectedCamera>
    {
        public CameraBase Camera => model.Camera;
        
        /// <summary>
        /// Minimum allowed cooling temperature
        /// </summary>
        public float MinimumAllowedTemperature => model.Camera.Properties.AllowedTemperatures.Minimum;
        /// <summary>
        /// Maximum allowed cooling temperature
        /// </summary>
        public float MaximumAllowedTemperature => model.Camera.Properties.AllowedTemperatures.Maximum;

        /// <summary>
        /// Indicates if camera supports temperature queries (gets).
        /// </summary>
        public bool CanQueryTemperature => model.Camera.Capabilities.GetFunctions.HasFlag(GetFunction.Temperature);
        /// <summary>
        /// Indicates if camera supports active cooling (set temperature and cooler control)
        /// </summary>
        public bool CanControlCooler => model.Camera.Capabilities.SetFunctions.HasFlag(SetFunction.Temperature);

        public bool CanControlTemperature => model.CanControlTemperature;

        /// <summary>
        /// Target temperature for camera's cooler.
        /// </summary>
        public float TargetTemperature
        {
            get => model.TargetTemperature;
            set
            {
                if (CanControlCooler &&
                    value != model.TargetTemperature &&
                    value <= MaximumAllowedTemperature &&
                    value >= MinimumAllowedTemperature)
                    model.TargetTemperature = value;
            }
        }

        public bool IsCoolerEnabled => model.Camera.CoolerMode == Switch.Enabled;

        public DelegateCommand VerifyTextInputCommand => model.VerifyTextInputCommand;
        public DelegateCommand ControlCoolerCommand => model.ControlCoolerCommand;


        public ConnectedCameraViewModel(ConnectedCamera model) : base(model)
        {
            model.Camera.PropertyChanged += (sender, e) =>
            {
                    if (e.PropertyName == nameof(model.Camera.CoolerMode))
                        RaisePropertyChanged(nameof(IsCoolerEnabled));
            };
        }
    }
}
