// FunctionCreation.cs
//
// Copyright 2010 Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JSUnitTest
{
    /// <summary>
    ///This is a test class for Microsoft.Ajax.Utilities.MainClass and is intended
    ///to contain all Microsoft.Ajax.Utilities.MainClass Unit Tests
    ///</summary>
    [TestClass()]
    public class FunctionCreation
    {
        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void NewFunction()
        {
            TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void Arguments()
        {
          TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void FuncExpr()
        {
          TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void NamedFuncExpr()
        {
            TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void NamedFuncExpr_nfe()
        {
            TestHelper.Instance.RunTest("-fnames:keep");
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void NamedFuncExpr_h()
        {
            TestHelper.Instance.RunTest("-rename:all");
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void NamedFuncExpr_hnfe()
        {
            TestHelper.Instance.RunTest("-rename:all -fnames:keep");
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void NamedFuncExpr_hlock()
        {
            TestHelper.Instance.RunTest("-rename:all -fnames:lock");
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void BadSemicolon()
        {
          TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void VarArgList()
        {
          TestHelper.Instance.RunTest();
        }

        [TestMethod()]
        public void BadLocation()
        {
            TestHelper.Instance.RunTest();
        }

        [TestMethod()]
        public void FunctionNames()
        {
            TestHelper.Instance.RunTest();
        }
    }
}
