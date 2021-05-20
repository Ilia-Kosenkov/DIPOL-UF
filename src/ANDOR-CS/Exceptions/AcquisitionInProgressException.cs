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

namespace ANDOR_CS.Exceptions
{
    /// <inheritdoc />
    public class AcquisitionInProgressException : Exception
    {
        /// <inheritdoc />
        public AcquisitionInProgressException(string message) :
            base(message)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cam"></param>
        /// <exception cref="AcquisitionInProgressException"></exception>
        [Obsolete]
        public static void ThrowIfAcquiring(IDevice cam) 
            
        {
            if (cam.IsAcquiring)
                throw new AcquisitionInProgressException("Camera is acquiring image(s) at the moment.");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cam"></param>
        /// <param name="except"></param>
        /// <returns></returns>
        public static bool FailIfAcquiring(IDevice cam, out Exception except)
        {
            except = null;

            if (cam.IsAcquiring)
            {
                except = new AcquisitionInProgressException("Camera is acquiring image(s) at the moment.");
                return true;
            }

            return false;


        }

    }
}