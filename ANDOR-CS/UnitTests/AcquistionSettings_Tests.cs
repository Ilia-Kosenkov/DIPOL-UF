﻿using System;
using System.Collections.Generic;
using System.Xml;
using System.Text;
using System.Linq;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ANDOR_CS.Classes;


namespace ANDOR_CS.UnitTests
{
    [TestClass]
    public class AcquistionSettings_Tests
    {
        private Camera camera;

        public static void Main()
        {

            var setts = new AcquisitionSettings();
            var sb = new StringBuilder();

            using (var xml = XmlWriter.Create(sb, new XmlWriterSettings() { Indent = true, NewLineOnAttributes = true }))
                setts.WriteXml(xml);


            using (var xml = XmlReader.Create(new System.IO.StringReader(sb.ToString()), new XmlReaderSettings()))
                foreach (var item in XMLParser.ReadXml(xml).Where(x => x.Value != null))
                    Console.WriteLine(item);

            Console.WriteLine(sb.ToString());
            Console.ReadKey();
        }
        [TestInitialize]
        public void Initiazlie()
        {
            camera = new Camera();
        }
        [TestCleanup]
        public void Cleanup()
        {
            camera.Dispose();
        }
        [TestMethod]
        public void AcquisitionSettings_Serialize_Deserialize()
        {
            var settings_output = new AcquisitionSettings();
            using (var str = new StreamWriter("AcquistionSettings_Serialize_Deserizalie.test"))
                settings_output.Serialize(str.BaseStream);

            var settings_input = new AcquisitionSettings(camera);

            var publicProps = typeof(AcquisitionSettings)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod != null);

            var initialVals = publicProps.Select(p => p.GetValue(settings_output)).ToArray();
            var intermVals = publicProps.Select(p => p.GetValue(settings_input)).ToArray();

            CollectionAssert.AreNotEquivalent(initialVals, intermVals);

            using (var str = new StreamReader("AcquistionSettings_Serialize_Deserizalie.test"))
                settings_input.Deserialize(str.BaseStream);

            var finalVals = publicProps.Select(p => p.GetValue(settings_input)).ToArray();

            CollectionAssert.AreEquivalent(initialVals, finalVals);
        }
    }
}