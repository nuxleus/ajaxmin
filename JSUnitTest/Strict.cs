// Strict.cs
//
// Copyright 2011 Microsoft Corporation
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
    /// Summary description for Strict
    /// </summary>
    [TestClass]
    public class Strict
    {
        [TestMethod]
        public void EvalArgsAssign()
        {
            TestHelper.Instance.RunTest();
        }

        [TestMethod]
        public void InvalidVarName()
        {
            TestHelper.Instance.RunTest();
        }

        [TestMethod]
        public void DupArg()
        {
            TestHelper.Instance.RunTest();
        }

        [TestMethod]
        public void With()
        {
            TestHelper.Instance.RunTest();
        }

        [TestMethod]
        public void DupProperty()
        {
            TestHelper.Instance.RunTest();
        }

        [TestMethod]
        public void InvalidDelete()
        {
            TestHelper.Instance.RunTest();
        }
    }
}
