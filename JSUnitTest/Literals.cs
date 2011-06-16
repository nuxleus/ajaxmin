// Literals.cs
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
  /// Summary description for Literals
  /// </summary>
  [TestClass]
  public class Literals
  {
    [TestMethod]
    public void Boolean()
    {
      TestHelper.Instance.RunTest();
    }

    [TestMethod]
    public void Number()
    {
      TestHelper.Instance.RunTest();
    }

    [TestMethod]
    public void Strings()
    {
      TestHelper.Instance.RunTest("-inline:N");
    }

    [TestMethod]
    public void Strings_utf()
    {
      TestHelper.Instance.RunTest("-inline:F -enc:out utf-8");
    }

    [TestMethod]
    public void Strings_h()
    {
        TestHelper.Instance.RunTest("-inline:false -rename:all");
    }

    [TestMethod]
    public void Strings_k()
    {
      TestHelper.Instance.RunTest("-inline:true");
    }

    [TestMethod]
    public void Combined_h()
    {
        TestHelper.Instance.RunTest("-rename:all");
    }

    [TestMethod]
    public void Combined_hc()
    {
        TestHelper.Instance.RunTest("-rename:all -literals:combine");
    }

    [TestMethod]
    public void NestedCombine()
    {
        TestHelper.Instance.RunTest("-rename:all -literals:combine");
    }

    [TestMethod]
    public void ArrayLiteral()
    {
      TestHelper.Instance.RunTest();
    }

    [TestMethod]
    public void ObjectLiteral()
    {
      TestHelper.Instance.RunTest();
    }

    [TestMethod]
    public void InlineSafe()
    {
        TestHelper.Instance.RunTest();
    }

    [TestMethod]
    public void InlineSafe_no()
    {
        TestHelper.Instance.RunTest("-inline:no");
    }

    [TestMethod]
    public void CombineNegs()
    {
        TestHelper.Instance.RunTest("-literals:combine -rename:all");
    }

    [TestMethod]
    public void StrictEncoding()
    {
        TestHelper.Instance.RunTest();
    }
  }
}
