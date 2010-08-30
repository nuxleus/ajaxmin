// CommandLine.cs
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
  /// Summary description for CommandLine
  /// </summary>
  [TestClass]
  public class CommandLine
  {
    [TestMethod]
    public void Usage()
    {
      // no input or output files
      TestHelper.Instance.RunTest(false, "-?");
    }

    [TestMethod]
    public void UnknownArg()
    {
      // no input or output files
      TestHelper.Instance.RunTest(false, "-foo");
    }

    [TestMethod]
    public void TermSemi1()
    {
      TestHelper.Instance.RunTest("-term");
    }

    [TestMethod]
    public void TermSemi2()
    {
      TestHelper.Instance.RunTest("-term");
    }

    [TestMethod]
    public void Globals()
    {
      TestHelper.Instance.RunTest("-global:foobar,foo -global:bar");
    }
  }
}
