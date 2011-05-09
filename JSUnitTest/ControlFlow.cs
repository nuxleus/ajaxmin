// ControlFlow.cs
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
  public class ControlFlow
  {
    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Break()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Continue()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Debugger()
    {
        TestHelper.Instance.RunTest("-debug:Y");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Debugger_D()
    {
        TestHelper.Instance.RunTest("-debug:N");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Debugger_OnCustom()
    {
        // no flag is the same as Y -- and since turned on the debug
        // means no replacement of debug lookups, it doesn't really matter what
        // comes after the comma. We'll process them, but we won't be replacing anything
        // anyway!
        TestHelper.Instance.RunTest("-debug:,AckBar,FooBar");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Debugger_OffCustom()
    {
        TestHelper.Instance.RunTest("-debug:N,AckBar,FooBar,Debug,$Debug,Web.Debug");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Debugger_OffNone()
    {
        // adding the comma after means we want to specify the debug lookups.
        // but since we have nothing after the comma, we replace the defaults
        // ($Debug, Debug, WAssert) with nothing.
        TestHelper.Instance.RunTest("-debug:N,");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void DoWhile()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void ForWhile()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void ForIn()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void ForVar()
    {
        TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void If()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Labels()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Labels_H()
    {
      TestHelper.Instance.RunTest("-rename:all");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Return()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Switch()
    {
      TestHelper.Instance.RunTest("-unused:keep");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Switch_h()
    {
        TestHelper.Instance.RunTest("-rename:all");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void TryCatch()
    {
      TestHelper.Instance.RunTest("-mac:FALSE");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void TryCatch_m()
    {
      TestHelper.Instance.RunTest("-mac:Y");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void CatchScope()
    {
        TestHelper.Instance.RunTest("-rename:none"); // we see the difference when hypercrunch is on
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void CatchScope_Local()
    {
        TestHelper.Instance.RunTest("-rename:all"); // catch-local switch and hypercrunch
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void EncloseBlock()
    {
      TestHelper.Instance.RunTest();
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Throw()
    {
        TestHelper.Instance.RunTest("-mac:N");
    }

    [DeploymentItem("AjaxMin.exe")]
    [TestMethod()]
    public void Throw_M()
    {
        TestHelper.Instance.RunTest();
    }
  }
}
