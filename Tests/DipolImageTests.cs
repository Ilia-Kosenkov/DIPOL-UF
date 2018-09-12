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
using System.Linq;
using System.Reflection;
using DipolImage;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class DipolImageTests
    {
        public Random R;
        public int[] TestArray;
        public byte[] TestByteArray;
        public byte[] VeryLargeByteArray;

        [SetUp]
        public void Test_Initialize()
        {
            R = new Random();
            TestArray = new int[32];
            for (var i = 0; i < TestArray.Length; i++)
            {
                TestArray[i] = R.Next();
            }
            TestByteArray= new byte[512];
            R.NextBytes(TestByteArray);

            VeryLargeByteArray = new byte[1024 * 1024 * 8];
            R.NextBytes(VeryLargeByteArray);

        }

        [Test]
        public void Test_ConstructorThrows()
        {
            Assert.Multiple(() =>
            {
                // ReSharper disable for method ObjectCreationAsStatement

                Assert.Throws<ArgumentNullException>(() => new Image(null, 2, 3));
                Assert.Throws<ArgumentOutOfRangeException>(() => new Image(TestArray, 0, 3));
                Assert.Throws<ArgumentOutOfRangeException>(() => new Image(TestArray, 10, 0));
                Assert.Throws<ArgumentException>(() => new Image(new[] {"s"}, 1, 1));

                Assert.Throws<ArgumentNullException>(() => new Image(null, 1, 1, TypeCode.Int16));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new Image(TestByteArray, 0, 3, TypeCode.Int32));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    new Image(TestByteArray, 10, 0, TypeCode.Int32));
                Assert.Throws<ArgumentException>(() => new Image(TestByteArray, 1, 1, TypeCode.Char));
                Assert.Throws<ArgumentException>(() => new Image(TestByteArray, 1, 1, (TypeCode) 45500));
            });
        }

        [Test]
        public void Test_ImageEqualsToArray()
        {
            var initArray = new[] {1, 2, 3, 4, 5, 6};

            var image = new Image(initArray, 2, 3);

            Assert.That(
                initArray[0] == image.Get<int>(0, 0) &&
                initArray[1] == image.Get<int>(0, 1) &&
                initArray[2] == image.Get<int>(1, 0) &&
                initArray[3] == image.Get<int>(1, 1) &&
                initArray[4] == image.Get<int>(2, 0) &&
                initArray[5] == image.Get<int>(2, 1),
                Is.True);
        }

        [Test]
        public void Test_ImageInitializedFromBytes()
        {
            const ushort value = 23;

            foreach (var code in Image.AllowedPixelTypes)
            {
                var temp = (Convert.ChangeType(value, code)) ?? new object();
                byte[] bytes;
                if (code != TypeCode.Byte)
                {
                    var mi = typeof(BitConverter)
                             .GetMethods(BindingFlags.Public | BindingFlags.Static)
                             .First(m => m.Name == "GetBytes" &&
                                         m.GetParameters().Length == 1 &&
                                         m.GetParameters().First().ParameterType == temp.GetType());
                    bytes = (byte[]) mi.Invoke(null, new[] {temp});
                }
                else
                    bytes = new [] {(byte) value};

                var image = new Image(bytes, 1, 1, code);

                Assert.Multiple(() =>
                {
                    Assert.That(image[0, 0], Is.EqualTo(temp));
                    Assert.That(image.UnderlyingType, Is.EqualTo(code));
                });
            }

        }

        [Test]
        public void Test_GetBytes()
        {
            const int val1 = 1;
            const int val2 = 123;
            foreach (var code in Image.AllowedPixelTypes)
            {
                var type = Type.GetType("System." + code) ?? typeof(void);
                var initArray = Array.CreateInstance(type, 2);
                initArray.SetValue(Convert.ChangeType(val1, code), 0);
                initArray.SetValue(Convert.ChangeType(val2, code), 1);


                var image = new Image(initArray, 2, 1);

                var bytes = image.GetBytes();
                byte[] reconstructed;
                if (code != TypeCode.Byte)
                {
                    var mi = typeof(BitConverter)
                             .GetMethods(BindingFlags.Public | BindingFlags.Static)
                             .First(m => m.Name == "GetBytes" &&
                                         m.GetParameters().Length == 1 &&
                                         m.GetParameters().First().ParameterType == type);
                    var size = System.Runtime.InteropServices.Marshal.SizeOf(type);
                    reconstructed = new byte[2 * size];
                    Array.Copy((byte[]) mi.Invoke(null, new[] {initArray.GetValue(0)}), 0, reconstructed, 0, size);
                    Array.Copy((byte[]) mi.Invoke(null, new[] {initArray.GetValue(1)}), 0, reconstructed, size, size);
                }
                else
                {
                    var size = System.Runtime.InteropServices.Marshal.SizeOf(type);
                    reconstructed = new byte[2 * size];
                    reconstructed[0] = ((byte[])initArray)[0];
                    reconstructed[1] = ((byte[])initArray)[1];
                }

                CollectionAssert.AreEqual(reconstructed, bytes);
            }
        }

        [Test]
        public void Test_Equals()
        {
            var tempArr = new byte[TestByteArray.Length];
            Array.Copy(TestByteArray, tempArr, tempArr.Length);
            tempArr[0] = (byte) (tempArr[0] == 0 ? 127 : 0);

            foreach (var code in Image.AllowedPixelTypes)
            {
                var image1 = new Image(TestByteArray, 2, 2, code);
                var image2 = new Image(TestByteArray, 2, 2, code);

                var wrImage1 = new Image(TestByteArray, 2, 1, code);
                var wrImage2 = new Image(TestByteArray, 1, 2, code);
                var wrImage3 = new Image(TestByteArray, 2, 2,
                    code == TypeCode.Int16 ? TypeCode.UInt16 : TypeCode.Int16);
                var wrImage4 = new Image(tempArr, 2, 2, code);

                Assert.Multiple(() =>
                {
                    Assert.That(image1.Equals(image2), Is.True);
                    Assert.That(image2.Equals(image1), Is.True);
                    Assert.That(image1.Equals((object) image2), Is.True);
                    Assert.That(image1.Equals(image1, image2), Is.True);

                    Assert.That(image1.Equals(null), Is.False);
                    Assert.That(image1.Equals(wrImage1), Is.False);
                    Assert.That(image1.Equals(wrImage2), Is.False);
                    Assert.That(image1.Equals(wrImage3), Is.False);
                    Assert.That(image1.Equals(wrImage4), Is.False);
                    Assert.That(image1.Equals((object) null), Is.False);
                    Assert.That(image1.Equals(image1, wrImage1), Is.False);
                    Assert.That(image1.Equals(image1, null), Is.False);
                    Assert.That(image1.Equals(null, image1), Is.False);
                });
            }
        }

        [Test]
        public void Test_Copy()
        {
            var array = new byte[1024];
            R.NextBytes(array);

            var img = new Image(array, 32, 16, TypeCode.Int16);
            Assert.That(img.Equals(img.Copy()), Is.True);
        }

        [Test]
        public void Test_ThisAccessor()
        {
            var initArray = new[] {1, 2, 3, 4};
            var image = new Image(initArray, 2, 2);

            Assert.Multiple(() =>
            {
                Assert.That(image[0, 1], Is.EqualTo(initArray[1]));
                Assert.That(image[1, 0], Is.EqualTo(initArray[2]));
            });

            image[0, 0] = 430;

            Assert.That(image[0,0], Is.EqualTo(430));
        }

        [Test]
        public void Test_GetHashCode()
        {
            var tempArr = new byte[TestByteArray.Length];
            Array.Copy(TestByteArray, tempArr, tempArr.Length);
            tempArr[0] = (byte)(tempArr[0] == 0 ? 127 : 0);

            foreach (var code in Image.AllowedPixelTypes)
            {
                var image1 = new Image(TestByteArray, 2, 2, code);
                var image2 = new Image(TestByteArray, 2, 2, code);

                var wrImage1 = new Image(tempArr, 2, 2, code);

                Assert.Multiple(() =>
                {
                    Assert.That(image1.GetHashCode(), Is.EqualTo(image2.GetHashCode()));
                    Assert.That(image1.GetHashCode(image1), Is.EqualTo(image1.GetHashCode(image2)));
                    Assert.That(image1.GetHashCode(), Is.Not.EqualTo(wrImage1.GetHashCode()));
                    Assert.That(image1.GetHashCode(image1), Is.Not.EqualTo(image1.GetHashCode(wrImage1)));
                });

            }
        }

        [Test]
        public void Test_Max()
        {
            foreach (var code in Image.AllowedPixelTypes)
            {
                var type = Type.GetType("System." + code) ?? typeof(byte);
                var size = System.Runtime.InteropServices.Marshal.SizeOf(type);

                var max = type
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .First(fi => fi.Name == "MinValue")
                    .GetValue(null);

                var image = new Image(TestByteArray, TestByteArray.Length / size, 1, code);
                for (var i = 0; i < image.Width; i++)
                {
                    var val = image[0, i] as IComparable;
                    if (val?.CompareTo(max) > 0)
                        max = Convert.ChangeType(val, code);
                }


                max = Convert.ChangeType(max, code);

                Assert.That(image.Max(), Is.EqualTo(max));
            }
        
        }

        [Test]
        public void Test_Min()
        {
            foreach (var code in Image.AllowedPixelTypes)
            {
                var type = Type.GetType("System." + code) ?? typeof(byte);
                var size = System.Runtime.InteropServices.Marshal.SizeOf(type);

                var min = type
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .First(fi => fi.Name == "MaxValue")
                    .GetValue(null);

                var image = new Image(TestByteArray, TestByteArray.Length / size, 1, code);
                for (var i = 0; i < image.Width; i++)
                {
                    var val = image[0, i] as IComparable;
                    if (val?.CompareTo(min) < 0)
                    {
                        if(type == typeof(float) && !float.IsNaN((float)val))
                            min = Convert.ChangeType(val, code);
                        else if (type == typeof(double) && !double.IsNaN((double)val))
                            min = Convert.ChangeType(val, code);
                        else if(type != typeof(double) && type != typeof(float))
                            min = Convert.ChangeType(val, code);

                    }
                }
            


                min = Convert.ChangeType(min, code);

                Assert.That(image.Min(), Is.EqualTo(min));
            }
        }

        [Test]
        public void Test_Transpose()
        {
            foreach (var code in Image.AllowedPixelTypes)
            {
                var type = Type.GetType("System." + code) ?? typeof(byte);
                var size = System.Runtime.InteropServices.Marshal.SizeOf(type);

                var image = new Image(TestByteArray, TestByteArray.Length / 2 / size, 2, code);
                var imageT = image.Transpose();

                Assert.Multiple(() =>
                {

                    Assert.That(imageT.Height, Is.EqualTo(image.Width));
                    Assert.That(imageT.Width, Is.EqualTo(image.Height));

                    Assert.That(Enumerable.Range(0, image.Width * image.Height)
                                             .All(i => image[i % 2, i / 2].Equals(imageT[i / 2, i % 2])),
                        Is.True);
                });
            }
        }

        [Test]
        public void Test_Type()
        {
            Assert.Multiple(() =>
            {
                foreach (var code in Image.AllowedPixelTypes)
                    Assert.That(new Image(TestByteArray, 2, 2, code).Type,
                        Is.EqualTo(Type.GetType("System." + code)));
            });
        }

        [Test]
        public void Test_CastTo()
        {
            var image = new Image(TestArray,4, TestArray.Length/4);
            Assert.Multiple(() =>
            {
                Assert.That(image.Equals(image.CastTo<int, int>(x => x)), Is.True);
                Assert.That(() => image.CastTo<int, string>(x => x.ToString()),
                    Throws.InstanceOf<ArgumentException>());
            });
            var otherArray = TestArray.Select(x => (double) x).ToArray();

            var otherImage = new Image(otherArray, 4, otherArray.Length/4);
            Assert.That(otherImage.Equals(image.CastTo<int, double>(x => 1.0 * x)), Is.True);
        }

        [Test]
        public void Test_Clamp()
        {
            // ReSharper disable for method InconsistentNaming
            foreach (var code in Image.AllowedPixelTypes)
            {
                var type = Type.GetType("System." + code) ?? typeof(byte);
                var size = System.Runtime.InteropServices.Marshal.SizeOf(type);
                var image = new Image(TestByteArray, TestByteArray.Length/4/ size, 4, code);
                var f_mx = (Type.GetType("System." + code) ?? typeof(byte))
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .First(fi => fi.Name == "MaxValue");

                dynamic m_max = f_mx.GetValue(null);

                var mx = code.ToString().Contains("U") || code.ToString().Contains("Byte") ? m_max / 2 : 5000;
                var mn = code.ToString().Contains("U") || code.ToString().Contains("Byte") ? m_max/ 4 : -5000;

                Assert.That(() => image.Clamp(100, 10),
                    Throws.InstanceOf<ArgumentException>());

                image.Clamp(mn, mx);

                var min = image.Min() as IComparable;
                var max = image.Max() as IComparable;

                Assert.Multiple(() =>
                {
                    Assert.That(min?.CompareTo(Convert.ChangeType(mn, code)), 
                        Is.GreaterThanOrEqualTo(0));
                    Assert.That(max?.CompareTo(Convert.ChangeType(mx, code)),
                        Is.LessThanOrEqualTo(0));
                });
            }
        }

        [Test]
        [Retry(3)]
        public void Test_Scale()
        {
            foreach (var code in Image.AllowedPixelTypes)
            {
                var f_mx = (Type.GetType("System." + code) ?? typeof(byte))
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .First(fi => fi.Name == "MaxValue");

                var f_mn = (Type.GetType("System." + code) ?? typeof(byte))
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .First(fi => fi.Name == "MinValue");

                dynamic mx = f_mx.GetValue(null);
                dynamic mn = f_mn.GetValue(null);

                var image = new Image(TestByteArray, 
                    TestByteArray.Length / 4 / 
                        System.Runtime.InteropServices.Marshal.SizeOf(
                            Type.GetType("System." + code) ?? throw new InvalidOperationException()),
                            4, code);
                var imageLarge = new Image(VeryLargeByteArray, 
                    VeryLargeByteArray.Length / 4 / 
                        System.Runtime.InteropServices.Marshal.SizeOf(
                            Type.GetType("System." + code) ?? throw new InvalidOperationException()), 
                            4, code);

                image.Clamp(mn / 100, mx / 100);
                imageLarge.Clamp(mn / 100, mx / 100);

                Assert.Throws<ArgumentException>(() => image.Scale(100, 10));

                image.Scale(1, 10);
                imageLarge.Scale(1, 10);

                var min = image.Min() as IComparable ?? throw new ArgumentNullException();
                var max = image.Max() as IComparable ?? throw new ArgumentNullException();

                var minL = imageLarge.Min() as IComparable ?? throw new ArgumentNullException();
                var maxL = imageLarge.Max() as IComparable ?? throw new ArgumentNullException();

                Assert.Multiple(() =>
                {
                    Assert.That((Math.Abs(min.CompareTo(Convert.ChangeType(1, code))) < float.Epsilon) ||
                                   (Math.Abs(max.CompareTo(min)) < float.Epsilon), Is.True);
                    Assert.That((Math.Abs(max.CompareTo(Convert.ChangeType(10, code))) < float.Epsilon) ||
                                   (Math.Abs(max.CompareTo(min)) < float.Epsilon), Is.True);

                    Assert.That(Math.Abs(minL.CompareTo(Convert.ChangeType(1, code))) < float.Epsilon, Is.True);
                    Assert.That(Math.Abs(maxL.CompareTo(Convert.ChangeType(10, code))) < float.Epsilon, Is.True);
                });
            }
        }

        [Test]
        public void Test_Scale_FlatImage()
        {
            foreach (var code in Image.AllowedPixelTypes)
            {
                var image = new Image(new byte[TestByteArray.Length],
                    TestByteArray.Length / 4 / 
                    System.Runtime.InteropServices.Marshal.SizeOf(
                        Type.GetType("System." + code) ?? throw new InvalidOperationException()), 
                        4, code);
                var imageLarge = new Image(new byte[VeryLargeByteArray.Length], 
                    VeryLargeByteArray.Length / 4 / 
                    System.Runtime.InteropServices.Marshal.SizeOf(
                        Type.GetType("System." + code) ?? throw new InvalidOperationException()),
                        4, code);

                image.Scale(1, 10);
                imageLarge.Scale(1, 10);

                var min = image.Min() as IComparable;
                var max = image.Max() as IComparable;

                var minL = imageLarge.Min() as IComparable;
                var maxL = imageLarge.Max() as IComparable;

                Assert.Multiple(() =>
                {
                    Assert.That(min?.CompareTo(Convert.ChangeType(1, code)), Is.EqualTo(0));
                    Assert.That(max?.CompareTo(Convert.ChangeType(1, code)), Is.EqualTo(0));

                    Assert.That(minL?.CompareTo(Convert.ChangeType(1, code)), Is.EqualTo(0));
                    Assert.That(maxL?.CompareTo(Convert.ChangeType(1, code)), Is.EqualTo(0));
                });
            }
        }

        [Test]
        public void Test_AddScalar()
        {
            foreach (var code in Image.AllowedPixelTypes)
            {
                var type = Type.GetType("System." + code) ?? typeof(byte);
                const int N = 1024;
                var array = Array.CreateInstance(type, N);
                for(var i = 0; i < N / 4; i ++)
                    for (var j = 0; j < 4; j++)
                        array.SetValue(Convert.ChangeType((i + j) % 128, code), i * 4 + j);

                var image = new Image(array, 4, N/4);

                const double scalar = 12.0;

                var copyImage = image.Copy();

                image.AddScalar(scalar);


                for(var i  = 0; i < image.Height; i++)
                    for (var j = 0; j < image.Width; j++)
                    {
                        dynamic val1 = image[i, j];
                        var dVal1 = 1.0 * val1;
                        dynamic val2 = copyImage[i, j];
                        var dVal2 = 1.0 * val2;

                        var diff = dVal1 - dVal2 - scalar;


                        Assert.That(Math.Abs(diff), Is.EqualTo(0).Within(double.Epsilon));
                    }
            }
        }

        [Test]
        public void Test_MultiplyByScalar()
        {
            foreach (var code in Image.AllowedPixelTypes)
            {
                var type = Type.GetType("System." + code) ?? typeof(byte);
                const int N = 1024;
                var array = Array.CreateInstance(type, N);
                for (var i = 0; i < N / 4; i++)
                    for (var j = 0; j < 4; j++)
                        array.SetValue(Convert.ChangeType((i + j) % 64, code), i * 4 + j);

                var image = new Image(array, 4, N / 4);

                const double scalar = 2.0;

                var copyImage = image.Copy();

                image.MultiplyByScalar(scalar);


                for (var i = 0; i < image.Height; i++)
                    for (var j = 0; j < image.Width; j++)
                    {
                        dynamic val1 = image[i, j];
                        var dVal1 = 1.0 * val1;
                        dynamic val2 = copyImage[i, j];
                        var dVal2 = 1.0 * val2;

                        var diff = dVal2 - dVal1/scalar;


                        Assert.That(Math.Abs(diff), Is.EqualTo(0).Within(double.Epsilon));
                    }
            }
        }

        [Test]
        public void Test_Percentile()
        {
            foreach (var code in Image.AllowedPixelTypes)
            {
                var type = Type.GetType("System." + code) ?? typeof(byte);

             
                const int N = 1024;
                var array = Array.CreateInstance(type, N);
                var d_array = new double[N];

                for (var i = 0; i < N / 4; i++)
                    for (var j = 0; j < 4; j++)
                    {
                        array.SetValue(Convert.ChangeType((i + j) % 256, code), i * 4 + j);
                        d_array[i * 4 + j] = i + j;
                    }



                var image = new Image(array, 4, N / 4);

                dynamic mn = image.Min();
                dynamic mx = image.Max();

                Assert.Multiple(() =>
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => image.Percentile(-1));
                    Assert.Throws<ArgumentOutOfRangeException>(() => image.Percentile(2));
                    Assert.That(image.Percentile(0), Is.EqualTo(mn));
                    Assert.That(image.Percentile(1), Is.EqualTo(mx));
                });
                //var prcnt = image.Percentile(0.5);
                //var factor = d_array.OrderBy(x => x).Count(x => x < prcnt) - 0.5 * array.Length;
            }
        }
    }
}
