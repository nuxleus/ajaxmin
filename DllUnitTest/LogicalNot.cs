using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Microsoft.Ajax.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DllUnitTest
{
    /// <summary>
    /// Summary description for LogicalNot
    /// </summary>
    [TestClass]
    public class LogicalNot
    {
        public LogicalNot()
        {
        }

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        public static string ExpectedFolder { get; private set; }
        public static string InputFolder { get; private set; }

        #region Additional test attributes
        
        // Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext) 
        {
            var dataFolder = Path.Combine(testContext.TestDeploymentDir, @"Dll");
            var className = testContext.FullyQualifiedTestClassName.Substring(testContext.FullyQualifiedTestClassName.LastIndexOf('.') + 1);

            ExpectedFolder = Path.Combine(Path.Combine(dataFolder, "Expected"), className);
            InputFolder = Path.Combine(Path.Combine(dataFolder, "Input"), className);
        }
        
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        private string[] GetSource(string extension)
        {
            var lines = new List<string>();
            var ndxUnderscore = TestContext.TestName.IndexOf('_');
            var testName = ndxUnderscore < 0 ? TestContext.TestName : TestContext.TestName.Substring(0, ndxUnderscore);

            var path = Path.ChangeExtension(Path.Combine(InputFolder, testName), extension);
            Trace.WriteLine(string.Format("Source path: {0}", path));

            using (var reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            return lines.ToArray();
        }

        private string[] GetExpected(string extension)
        {
            var lines = new List<string>();

            var path = Path.ChangeExtension(Path.Combine(ExpectedFolder, TestContext.TestName), extension);
            Trace.Write("Expected path: ");
            Trace.WriteLine(path);

            using (var reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            return lines.ToArray();
        }

        private void RunTest(CodeSettings settings)
        {
            var source = GetSource(".js");
            var expected = GetExpected(".js");

            settings = settings ?? new CodeSettings() { MinifyCode = false };

            if (source.Length == expected.Length)
            {
                for (var ndx = 0; ndx < source.Length; ++ndx)
                {
                    Trace.WriteLine("");
                    Trace.WriteLine("----------------------------------------------------------------------------");
                    Trace.WriteLine("");

                    // parse the source into an AST
                    var parser = new JSParser(source[ndx]);
                    var block = parser.Parse(settings);

                    // there should only be one statement in the block
                    if (block.Count == 1)
                    {
                        var expression = block[0];

                        // create the logical-not visitor on the expression
                        var logicalNot = new Microsoft.Ajax.Utilities.LogicalNot(expression, parser);

                        // get the original code
                        var original = expression.ToCode();

                        Trace.Write("ORIGINAL EXPRESSION:    ");
                        Trace.WriteLine(original);

                        // get the measured delta
                        var measuredDelta = logicalNot.Measure();

                        // perform the logical-not operation
                        logicalNot.Apply();

                        // get the resulting code -- should still be only one statement in the block
                        var notted = block[0].ToCode();

                        Trace.Write("LOGICAL-NOT EXPRESSION: ");
                        Trace.WriteLine(notted);

                        Trace.Write("EXPECTED EXPRESSION:    ");
                        Trace.WriteLine(expected[ndx]);

                        Trace.Write("DELTA: ");
                        Trace.WriteLine(measuredDelta);

                        // what's the actual difference
                        var actualDelta = notted.Length - original.Length;
                        Assert.IsTrue(actualDelta == measuredDelta,
                            "Measurement was off; calculated {0} but was actually {1}",
                            measuredDelta,
                            actualDelta);

                        Assert.IsTrue(string.CompareOrdinal(expected[ndx], notted) == 0, "Expected output is not the same!!!!");
                    }
                    else
                    {
                        Assert.Fail(string.Format("Source line {0} parsed to more than one statement!", ndx + 1));
                    }
                }
            }
            else
            {
                Assert.Fail("Input and Expected files have different number of lines!");
            }
        }

        [TestMethod]
        public void Test1()
        {
            RunTest(null);
        }
    }
}
