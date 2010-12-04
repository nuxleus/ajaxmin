using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSUnitTest
{
    [TestClass]
    public class Renaming
    {
        [TestMethod]
        public void NestedGlobals()
        {
            // no input or output files
            TestHelper.Instance.RunTest("-rename:all");
        }
    }
}
