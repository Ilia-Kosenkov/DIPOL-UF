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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using SettingsManager;

namespace Tests
{
    [TestClass]
    public class JsonSettingsTests
    {
        private const string Path = "../../../Tests input/test.Json";
        private const string Path2 = "../../../Tests/Tests input/test.Json";

        public JsonSettings Settings;

        [TestInitialize]
        public void Initialize()
        {
            string str;
            try
            {
                using (var reader = new StreamReader(Path))
                    str = reader.ReadToEnd();
            }
            catch
            {
                using (var reader = new StreamReader(Path2))
                    str = reader.ReadToEnd();
            }

            Settings = new JsonSettings(str);
        }

        [TestMethod]
        public void Test_Constructor() => 
            Assert.IsNotNull(Settings);

        [TestMethod]
        public void Test_Get()
        {
            Assert.AreEqual(153, Settings.Get<int>("item_int"));
            Assert.AreEqual(1.23e55, Settings.Get<double>("item_double"));
            Assert.AreEqual(-123.56m, Settings.Get<decimal>("item_decimal"));
            Assert.AreEqual(1e10, Settings.Get<double>("item_float"));
            Assert.AreEqual("string", Settings.Get<string>("item_string"));
            Assert.AreEqual("", Settings.Get<string>("item_string_empty"));
            Assert.AreEqual(null, Settings.Get<object>("item_null"));
        }

        [TestMethod]
        public void Test_Get_MissingKey()
        {
            Assert.AreEqual(default, Settings.Get<int>("missing_item_int"));
            CollectionAssert.AreEqual(new int[] {}, Settings.GetArray<int>("missing_item_array_int"));
        }

        [TestMethod]
        public void Test_GetArray()
        {
            CollectionAssert.AreEqual(new[] {1, 2, 3, 4, 5, 6},
                Settings.GetArray<int>("item_array_int"));

            CollectionAssert.AreEqual(new object[] {}, 
                Settings.GetArray<object>("item_array_empty"));
        }

        [TestMethod]
        public void Test_GetJson()
        {
            var item = Settings.GetJson("item_nested");
            Assert.AreEqual(2, item.Count);
            Assert.AreEqual(153, item.Get<int>("nested_int"));
            Assert.AreEqual(1.53m, item.Get<decimal>("nested_decimal"));
        }

        [TestMethod]
        public void Test_HasKey()
        {
            Assert.IsTrue(Settings.HasKey("item_empty"));
            Assert.IsFalse(Settings.HasKey("missing_item_empty"));

        }

        [TestMethod]
        public void Test_GetAsObject()
        {
            Assert.AreEqual(153L, Settings.Get("item_int"));
            Assert.AreEqual(1.23e55, Settings.Get("item_double"));
            Assert.AreEqual(-123.56, Settings.Get("item_decimal"));
            Assert.AreEqual(1e10, Settings.Get("item_float"));
            Assert.AreEqual("string", Settings.Get("item_string"));
            Assert.AreEqual("", Settings.Get("item_string_empty"));
            Assert.AreEqual(null, Settings.Get("item_null"));
            
        }

        [TestMethod]
        public void Test_TryGet()
        {
            Assert.IsTrue(Settings.TryGet("item_int", out long intVal) && intVal == 153);
            Assert.IsTrue(Settings.TryGet("item_double", out double doubleVal) && Math.Abs(doubleVal - 1.23e55) < double.Epsilon);
            Assert.IsTrue(Settings.TryGet("item_decimal", out double decimalVal) && Math.Abs(decimalVal - (-123.56)) < double.Epsilon);
            Assert.IsTrue(Settings.TryGet("item_float", out double dFloatVal) && Math.Abs(dFloatVal - 1e10f) < float.Epsilon);
            Assert.IsTrue(Settings.TryGet("item_string", out string stringVal) && stringVal == "string");
            Assert.IsTrue(Settings.TryGet("item_string_empty", out string stringValE) && stringValE == "");
            Assert.IsTrue(Settings.TryGet("item_null", out object objectVal) && objectVal == null);
            Assert.IsTrue(!Settings.TryGet("missing_item", out object value) && value == null);
        }

        [TestMethod]
        public void Test_this()
        {
            Assert.AreEqual(153L, Settings["item_int"]);
            Assert.AreEqual(1.23e55, Settings["item_double"]);
            Assert.AreEqual(-123.56, Settings["item_decimal"]);
            Assert.AreEqual(1e10, Settings["item_float"]);
            Assert.AreEqual("string", Settings["item_string"]);
            Assert.AreEqual("", Settings["item_string_empty"]);
            Assert.AreEqual(null, Settings["item_null"]);

        }

        [TestMethod]
        public void Test_Count()
        {
            Assert.AreEqual(2, Settings.GetJson("item_nested").Count);
        }

        [TestMethod]
        public void Test_Set()
        {
            var count = Settings.Count;
            const string intKey = "test_item_int";
            const int intVal = 42;
            Assert.IsFalse(Settings.HasKey(intKey));

            Settings.Set(intKey, intVal);
            Assert.AreEqual(count+1, Settings.Count);
            Assert.AreEqual(intVal, Settings.Get<int>(intKey));

            Settings.Set(intKey, intVal + 1);
            Assert.AreEqual(count + 1, Settings.Count);
            Assert.AreEqual(intVal + 1, Settings.Get<int>(intKey));
        }

        [TestMethod]
        public void Test_ctor()
        {
            var setts = new JsonSettings();
            Assert.AreEqual(0, setts.Count);
        }

        [TestMethod]
        public void Test_Clear()
        {
            Assert.AreEqual(153L, Settings["item_int"]);
            Settings.Clear("item_int");
            Assert.AreEqual(null, Settings["item_int"]);
        }

        [TestMethod]
        public void Test_TryRemove()
        {
            const string key = "test_key";
            Assert.IsFalse(Settings.HasKey(key));
            Settings.Set(key, key);
            Assert.AreEqual(key, Settings.Get<string>(key));
            Assert.IsTrue(Settings.TryRemove(key));
            Assert.IsFalse(Settings.HasKey(key));

        }

        [TestMethod]
        public void Test_WriteString()
        {
            var setts = new JsonSettings(new JObject(new JProperty("test", 123L)));
            var strRep = setts.WriteString();

            var setts2 = new JsonSettings(strRep);

            Assert.AreEqual(setts.Count, setts2.Count);
            Assert.AreEqual(setts.Get<int>("name"), setts2.Get<int>("name"));
        }
    }
}
