﻿//    This file is part of Dipol-3 Camera Manager.

//     MIT License
//     
//     Copyright(c) 2018-2019 Ilia Kosenkov
//     
//     Permission is hereby granted, free of charge, to any person obtaining a copy
//     of this software and associated documentation files (the "Software"), to deal
//     in the Software without restriction, including without limitation the rights
//     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//     copies of the Software, and to permit persons to whom the Software is
//     furnished to do so, subject to the following conditions:
//     
//     The above copyright notice and this permission notice shall be included in all
//     copies or substantial portions of the Software.
//     
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//     FITNESS FOR A PARTICULAR PURPOSE AND NONINFINGEMENT. IN NO EVENT SHALL THE
//     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//     SOFTWARE.


using System;
using System.Linq;
using System.ServiceModel;

using ANDOR_CS.Events;
using DIPOL_Remote.Enums;
using DIPOL_Remote.Interfaces;

namespace DIPOL_Remote.Classes
{
    [CallbackBehavior(ConcurrencyMode = ConcurrencyMode.Multiple,
        IncludeExceptionDetailInFaults = true)]
    class RemoteCallbackHandler : IRemoteCallback
    {

        public RemoteCallbackHandler()
        {

        }

        public void NotifyRemoteAcquisitionEventHappened(int camIndex, string session,
            AcquisitionEventType type, AcquisitionStatusEventArgs args)
       => RemoteCamera.NotifyRemoteAcquisitionEventHappened(camIndex, session, type, args);

        public void NotifyRemotePropertyChanged(int camIndex, string session, string property)
            => RemoteCamera.NotifyRemotePropertyChanged(camIndex, session, property);

        public void NotifyRemoteTemperatureStatusChecked(
            int camIndex, string session, TemperatureStatusEventArgs args)
            => RemoteCamera.NotifyRemoteTemperatureStatusChecked(camIndex, session, args);

        public void NotifyRemoteNewImageReceivedEventHappened(int camIndex, string session, NewImageReceivedEventArgs e)
            => RemoteCamera.NotifyRemoteNewImageReceivedEventHappened(camIndex, session, e);

        public bool NotifyCameraCreatedAsynchronously(int camIndex, string session, bool success)
        {
            var resetEvent = DipolClient.CameraCreatedEvents
                .FirstOrDefault(x => x.Key.Equals((session, camIndex)))
                .Value.Event;

            if (resetEvent != null)
            {
                DipolClient.CameraCreatedEvents.AddOrUpdate((session, camIndex), (resetEvent, true), (x, y) => (resetEvent, success));
            }

            resetEvent?.Set();

            return resetEvent != null;
        }
    }
}
