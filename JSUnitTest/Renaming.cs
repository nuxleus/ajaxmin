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
            TestHelper.Instance.RunTest("-rename:all");
        }

        [TestMethod]
        public void ManualRename()
        {
            TestHelper.Instance.RunTest();
        }

        [TestMethod]
        public void ManualRename_cmd()
        {
            TestHelper.Instance.RunTest("-rename:globalFunction=_g,oneGlobal=g1,oneLocal=l1 -rename:oneParam=p1,twoParam=p2,nameOne=n1,你好=中文,while=for");
        }

        [TestMethod]
        public void ManualRename_rename()
        {
            TestHelper.Instance.RunTest("-rename Rename.xml");
        }

        [TestMethod]
        public void ManualRename_noprops()
        {
            TestHelper.Instance.RunTest("-rename Rename.xml -rename:noprops");
        }

        [TestMethod]
        public void ManualRename_all()
        {
            TestHelper.Instance.RunTest("-rename:all");
        }

        [TestMethod]
        public void ManualRename_collide()
        {
            TestHelper.Instance.RunTest("-rename:all -rename Collide.xml");
        }
    }
}
