// Declarations.cs
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
  public class Declarations
  {
    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void New()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void This()
    {
      TestHelper.Instance.RunTest("-unused:keep");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Var()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void With()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void With_H()
    {
        TestHelper.Instance.RunTest("-rename:all -unused:keep");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Function()
    {
      TestHelper.Instance.RunTest("-unused:keep");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Identifiers()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void LocalizationVars()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void LocalizationVars_H()
    {
        TestHelper.Instance.RunTest("-rename:all");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void LocalizationVars_HL()
    {
      TestHelper.Instance.RunTest("-rename:localization");
    }
  }
}
