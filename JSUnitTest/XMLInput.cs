// XmlInput.cs
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
    /// unit tests dealing with the XML input fklag (-x) which can specify a series of
    /// input and output files in an XML file for a single instance of tool execution
    /// </summary>
    [TestClass]
    public class XmlInput
    {
        [TestMethod]
        public void XmlOneOutputFile()
        {
            TestHelper.Instance.RunTest("-xml");
        }

        [TestMethod]
        public void XmlTwoOutputFiles()
        {
            TestHelper.Instance.RunTest("-xml");
        }
    }
}
