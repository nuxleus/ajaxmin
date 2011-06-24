// Switch.cs
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
  /// Summary description for Switch
  /// </summary>
  [TestClass]
  public class Switch
  {
    [TestMethod]
    public void EmptyDefault()
    {
      TestHelper.Instance.RunTest("-unused:keep");
    }

    [TestMethod]
    public void EmptyDefault_h()
    {
        TestHelper.Instance.RunTest("-rename:all");
    }

    [TestMethod]
    public void NoDefaultEmptyCases()
    {
      TestHelper.Instance.RunTest("-unused:keep");
    }

    [TestMethod]
    public void NoDefaultEmptyCases_h()
    {
        TestHelper.Instance.RunTest("-rename:all");
    }

    [TestMethod]
    public void PromoteBreak()
    {
      TestHelper.Instance.RunTest("-unused:keep");
    }

    [TestMethod]
    public void PromoteBreak_h()
    {
        TestHelper.Instance.RunTest("-rename:all");
    }

    [TestMethod]
    public void SwitchLabels()
    {
      TestHelper.Instance.RunTest("-unused:keep");
    }

    [TestMethod]
    public void SwitchLabels_h()
    {
        TestHelper.Instance.RunTest("-rename:all");
    }

    [TestMethod]
    public void PrettyPrint()
    {
      TestHelper.Instance.RunTest();
    }

    [TestMethod]
    public void PrettyPrint_P()
    {
      TestHelper.Instance.RunTest("-pretty");
    }

    [TestMethod]
    public void MacQuirks()
    {
      TestHelper.Instance.RunTest("-mac:No");
    }

    [TestMethod]
    public void MacQuirks_M()
    {
        TestHelper.Instance.RunTest();
    }

    [TestMethod]
    public void Unreachable()
    {
        TestHelper.Instance.RunTest();
    }
  }
}
