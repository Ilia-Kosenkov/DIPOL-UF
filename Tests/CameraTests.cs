﻿//    This file is part of Dipol-3 Camera Manager.

//     MIT License
//     
//     Copyright(c) 2018 Ilia Kosenkov
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
using ANDOR_CS.Classes;
using ANDOR_CS.Exceptions;
using DIPOL_Remote.Classes;
using DIPOL_Remote.Faults;
using NUnit.Framework;
using Assert2 = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests
{
    [TestFixture]
    public class CameraTests
    {

        [Test]
#if X86
        [DeployItem("../../../../atmcd32d.dll")]
#endif
#if X64
        [DeployItem("../../../../atmcd64d.dll")]
#endif
        public void Test_GetNumberOfCameras_IsNotNegative()
            => Assert.That(Camera.GetNumberOfCameras(), Is.GreaterThanOrEqualTo(0),
                "Number of cameras should always be greater than or equal to 0.");

        [Theory]
#if X86
        [DeployItem("../../../../atmcd32d.dll")]
#endif
#if X64
        [DeployItem("../../../../atmcd64d.dll")]
#endif
        public void Test_CameraCtor()
        {
            Assume.That(Camera.GetNumberOfCameras(), Is.GreaterThan(0),
                "Camera tests require a camera connected to the computer.");

            CameraBase cam = null;
            Assert.That(() => cam = new Camera(), Throws.Nothing, 
                "Camera should be created.");

            cam.Dispose();
            Assert.That(cam.IsDisposed, Is.True, 
                "Camera should be properly disposed.");
        }

        [Test]
#if X86
        [DeployItem("../../../../atmcd32d.dll")]
#endif
#if X64
        [DeployItem("../../../../atmcd64d.dll")]
#endif
        public void Test_CameraBaseCreate_AlwaysThrows()
        {
            Assert.That(() => CameraBase.Create(), 
                Throws.InstanceOf<NotSupportedException>(),
                $"{nameof(CameraBase.Create)} should always throw.");
        }

        [Theory]
#if X86
        [DeployItem("../../../../atmcd32d.dll")]
#endif
#if X64
        [DeployItem("../../../../atmcd64d.dll")]
#endif
        public void Test_CameraCreate()
        {
            Assume.That(Camera.GetNumberOfCameras(), Is.GreaterThan(0),
                "Camera tests require a camera connected to the computer.");

            CameraBase cam = null;

            Assert.That(() => cam = Camera.Create(), Throws.Nothing, 
                $"Camera should be created using static method {nameof(Camera.Create)}.");

            cam.Dispose();
            Assert.That(cam.IsDisposed, Is.True, 
                "Camera should be properly disposed.");

        }


        [Test]
#if X86
        [DeployItem("../../../../atmcd32d.dll")]
#endif
#if X64
        [DeployItem("../../../../atmcd64d.dll")]
#endif
        public void Test_CameraBaseCreateAsync_AlwaysThrows()
        {

            Assert.That(() => CameraBase.CreateAsync().Wait(),
                Throws.InstanceOf<AggregateException>()
                      .With
                      .InnerException.InstanceOf<NotSupportedException>(),
                $"{nameof(CameraBase.CreateAsync)} should always throw.");
        }

        [Theory]
#if X86
        [DeployItem("../../../../atmcd32d.dll")]
#endif
#if X64
        [DeployItem("../../../../atmcd64d.dll")]
#endif
        public void Test_CameraCreateAsync()
        {
            Assume.That(Camera.GetNumberOfCameras(), Is.GreaterThan(0),
                "Camera tests require a camera connected to the computer.");


            CameraBase cam = null;

            Assert.That(() => cam = Camera.CreateAsync().Result, Throws.Nothing,
                $"Camera should be created using static async method {nameof(Camera.CreateAsync)}.");

            cam.Dispose();
            Assert.That(cam.IsDisposed, Is.True,
                "Camera should be properly disposed.");
        }
        
    }
}
