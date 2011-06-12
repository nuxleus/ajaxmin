// Operators.cs
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
    /// Summary description for Operators
    /// </summary>
    [TestClass]
    public class Operators
    {
        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void Member()
        {
            TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void In()
        {
            TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void Void()
        {
            TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void Unary()
        {
            TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void Assign()
        {
            TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void Conditional()
        {
            TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void Comma()
        {
            TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void New()
        {
            TestHelper.Instance.RunTest();
        }

        [DeploymentItem("AjaxMin.exe")]
        [TestMethod()]
        public void InstanceOf()
        {
            TestHelper.Instance.RunTest();
        }

        [TestMethod()]
        public void NewPrecedence()
        {
            TestHelper.Instance.RunTest("-new:keep");
        }

        [TestMethod()]
        public void Associative()
        {
            TestHelper.Instance.RunTest();
        }

        [TestMethod()]
        public void Strict()
        {
            TestHelper.Instance.RunTest("-kill:0x0000001000000000");
        }
    }
}
