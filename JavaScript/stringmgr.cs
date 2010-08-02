// stringmgr.cs
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Microsoft.Ajax.Utilities
{
  internal static class StringMgr
  {
    private const string c_contextStringDelimiter = ";;";

    // resource manager for retrieving strings
    private static readonly ResourceManager s_resourcesJScript = GetResourceManager(".JavaScript.JScript");
    private static readonly ResourceManager s_resourcesApplication = GetResourceManager(".AjaxMin");
    private static readonly Dictionary<string, string> s_cache = new Dictionary<string, string>();

    public static string GetString(string ident)
    {
      return GetString(ident, null);
    }

    public static string GetString(string ident, params object[] args)
    {
      if (ident == null)
      {
        throw new ArgumentNullException("ident");
      }
      string localizedString;
      try
      {
        // if it's already in the cache...
        if (s_cache.ContainsKey(ident))
        {
          // pull from the cache
          localizedString = s_cache[ident];
        }
        else
        {
          // get the string from resources using the current ui culture
          localizedString = s_resourcesJScript.GetString(ident, CultureInfo.CurrentUICulture);
          if (localizedString == null)
          {
            localizedString = s_resourcesApplication.GetString(ident, CultureInfo.CurrentUICulture);
          }

          // just use the identifier as a last-ditch default
          if (localizedString == null)
          {
            localizedString = ident;
          }

          // and add it to the cache for future use
          s_cache[ident] = localizedString;
        }
      }
      catch (MissingManifestResourceException)
      {
        // just use the identifier as a last-ditch default
        localizedString = ident;
        s_cache[ident] = ident;
      }

      // locate context/no-context string delimiter
      int splitAt = localizedString.IndexOf(c_contextStringDelimiter, StringComparison.Ordinal);
      if (splitAt >= 0)
      {
        if (args == null || args.Length == 0)
        {
          // use the no-context part before the split point
          localizedString = localizedString.Substring(0, splitAt);
        }
        else
        {
          // splitAt is two characters before the beginning of the context string
          try
          {
            localizedString = String.Format(
              CultureInfo.InvariantCulture,
              localizedString.Substring(splitAt + c_contextStringDelimiter.Length),
              args
              );
          }
          catch (FormatException)
          {
            // use the no-context part before the split point
            localizedString = localizedString.Substring(0, splitAt);
          }
        }
      }
      return localizedString;
    }

    // get the resource manager for our strings
    private static ResourceManager GetResourceManager(string resourceName)
    {
      string ourNamespace = MethodInfo.GetCurrentMethod().DeclaringType.Namespace;
      // create our resource manager
      return new ResourceManager(
        ourNamespace + resourceName,
        Assembly.GetExecutingAssembly()
        );
    }
  }
}
