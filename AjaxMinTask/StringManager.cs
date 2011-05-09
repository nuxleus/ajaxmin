// StringManager.cs
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

namespace Microsoft.Ajax.Minifier.Tasks
{
    using System.Globalization;
    using System.Resources;
    using System.Reflection;

    internal enum StringEnum
    {
        NoError = 0,
        RequiredParameterIsEmpty,
        NoWritePermission,
        DidNotMinify,
        InvalidInputParameter,
        DestinationIsReadOnly,
    };

    internal static class StringManager
    {
        // resource manager for retrieving strings
        private static readonly ResourceManager s_resources = GetResourceManager();

        public static string GetString(StringEnum stringEnum, params object[] args)
        {
            // get the identifier
            string ident = stringEnum.ToString();

            // get the string from resources using the current ui culture
            string format = s_resources.GetString(ident, CultureInfo.CurrentUICulture);
            if (format != null)
            {
                // if we have an array of args
                if (args != null && args.Length > 0)
                {
                    // format the string with the args
                    format = string.Format(CultureInfo.CurrentCulture, format, args);
                }
            }
            else
            {
                // just use the identifier as a last-ditch default
                format = ident;
            }
            return format;
        }

        // get the resource manager for our strings
        private static ResourceManager GetResourceManager()
        {
            // create our resource manager
            return new ResourceManager(
              MethodInfo.GetCurrentMethod().DeclaringType.Namespace + ".Strings",
              Assembly.GetExecutingAssembly()
              );
        }
    }
}
