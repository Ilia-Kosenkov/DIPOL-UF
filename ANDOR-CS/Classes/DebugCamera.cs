﻿//    This file is part of Dipol-3 Camera Manager.

//    Dipol-3 Camera Manager is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.

//    Dipol-3 Camera Manager is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//    GNU General Public License for more details.

//    You should have received a copy of the GNU General Public License
//    along with Dipol-3 Camera Manager.  If not, see<http://www.gnu.org/licenses/>.
//
//    Copyright 2017, Ilia Kosenkov, Tuorla Observatory, Finland


using System;
using System.Threading.Tasks;
using System.Threading;
using ANDOR_CS.Enums;
using ANDOR_CS.DataStructures;
using ANDOR_CS.Events;

using DipolImage;
using System.Collections.Concurrent;
using Timer = System.Timers.Timer;

#pragma warning disable 1591
namespace ANDOR_CS.Classes
{
    public sealed class DebugCamera : CameraBase
    {
        private static readonly Random R = new Random();
        private static readonly object Locker = new object();
        private const ConsoleColor Green = ConsoleColor.DarkGreen;
        private const ConsoleColor Red = ConsoleColor.Red;
        private const ConsoleColor Blue = ConsoleColor.Blue;

        public override ConcurrentQueue<Image> AcquiredImages => throw new NotImplementedException();

        public override CameraStatus GetStatus()
        {
            WriteMessage("Status checked.", Blue);
            return CameraStatus.Idle;
        }
        public override (TemperatureStatus Status, float Temperature) GetCurrentTemperature()
        {
            WriteMessage("Current temperature returned.", Blue);
            return (Status: TemperatureStatus.Stabilized, Temperature: R.Next(-10, 10));
        }
        public override void SetActive()
            => WriteMessage("Camera is manually set active.", Green);
        public override void FanControl(FanMode mode)
        {
            FanMode = mode;
            WriteMessage($"Fan mode is set to {mode}", Blue);
        }
        public override void CoolerControl(Switch mode)
        {
            CoolerMode = mode;
            WriteMessage($"Cooler mode is set to {mode}", Blue);
        }
        public override void SetTemperature(int temperature) 
            => WriteMessage($"Temperature was set to {temperature}.", Blue);

        public override void ShutterControl(int clTime, int opTime, ShutterMode inter, ShutterMode extrn = ShutterMode.FullyAuto, TtlShutterSignal type = TtlShutterSignal.Low) 
            => WriteMessage("Shutter settings were changed.", Blue);

       
        public DebugCamera(int camIndex)
        {
            CameraIndex = camIndex;
            SerialNumber = $"XYZ-{R.Next(9999):0000}";
            Capabilities = new DeviceCapabilities()
            {
                CameraType = CameraType.IXonUltra,
                GetFunctions = GetFunction.Temperature | GetFunction.TemperatureRange,
                SetFunctions = SetFunction.Temperature
            };
            Properties = new CameraProperties()
            {
                DetectorSize = new Size(256, 512)
            };
            IsActive = true;
            IsInitialized = true;
            CameraModel = "DEBUG-CAMERA-INTERFACE";
            FanMode = FanMode.Off;
            CoolerMode = Switch.Disabled;

            PropertyChanged += (sender, prop) => WriteMessage($"{prop.PropertyName} was changed.", Blue);
            TemperatureStatusChecked += (sender, args) => WriteMessage($"Temperature: {args.Temperature}\tStatus: {args.Status}", Blue);
            WriteMessage("Camera created.", Green);
        }
        public override SettingsBase GetAcquisitionSettingsTemplate()
        {
            throw new NotImplementedException();
        }

        public override void EnableAutosave(in string pattern)
        {
            throw new NotImplementedException();
        }


        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    IsDisposing = true;
                }
            }
            base.Dispose(disposing);
            WriteMessage("Camera disposed.", Red);
        }


        private void WriteMessage(string message, ConsoleColor col)
        {
            lock (Locker)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("[{0,-3:000}-{1:hh:mm:ss.ff}] > ", CameraIndex, DateTime.Now);
                Console.ForegroundColor = col;
                Console.WriteLine(message);
                Console.ForegroundColor = ConsoleColor.White;
            }

        }

        public override async Task StartAcquisitionAsync(CancellationTokenSource token, int timeout = StatusCheckTimeOutMs)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1));
            throw new NotImplementedException();
        }

        public override void StartAcquisition()
        {
            throw new NotImplementedException();
        }

        public override void AbortAcquisition()
        {
            throw new NotImplementedException();
        }
    }
}
