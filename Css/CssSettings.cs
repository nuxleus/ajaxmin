// CssSettings.cs
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

namespace Microsoft.Ajax.Utilities
{
    /// <summary>
    /// Settings Object for CSS Minifier
    /// </summary>
    public class CssSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CssSettings"/> class with default settings.
        /// </summary>
        public CssSettings()
        {
            ColorNames = CssColor.Strict;
            CommentMode = CssComment.Important;
            ExpandOutput = false;
            IndentSpaces = 4;
            Severity = 1;
            TermSemicolons = false;
            MinifyExpressions = true;
        }

        /// <summary>
        /// Gets or sets ColorNames setting.
        /// </summary>
        public CssColor ColorNames
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets CommentMode setting.
        /// </summary>
        public CssComment CommentMode
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether output should be single line (default false) or multi-line (true).
        /// </summary>
        public bool ExpandOutput
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the number of spaces to use per indent when ExpandOutput is true.
        /// </summary>
        public int IndentSpaces
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets minimum Severity to report in error output.
        /// </summary>
        public int Severity
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to ensure all declarations are terminated with semicolons, even if not necessary.
        /// </summary>
        public bool TermSemicolons
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to minify the javascript within expression functions
        /// </summary>
        public bool MinifyExpressions
        {
            get; set;
        }
    }
}
