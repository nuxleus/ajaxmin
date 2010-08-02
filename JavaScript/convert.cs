// convert.cs
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
using System.Globalization;

namespace Microsoft.Ajax.Utilities
{
  internal static class JSConvert
  {
    internal static double ToNumber(string str, bool isInteger)
    {
      try
      {
        if (isInteger)
        {
          if (str[0] == '0')
          {
            if (str.Length > 1 && (str[1] == 'x' || str[1] == 'X'))
            {
              if (str.Length == 2)
              {
                // 0x???? must be a parse error. Just return zero
                return 0;
              }
              // parse the number as a hex integer, converted to a double
              return (double)System.Convert.ToInt64(str, 16);
            }
            else
            {
              // might be an octal value... try converting to octal
              // and if it fails, just convert to decimal
              try
              {
                return (double)System.Convert.ToInt64(str, 8);
              }
              catch (FormatException)
              {
                // ignore the format exception and fall through to parsing
                // the value as a base-10 decimal value
              }
            }
          }
          // just parse the integer as a decimal value
          return System.Convert.ToDouble(str, CultureInfo.InvariantCulture);

        }
        else
        {
          // use the system to convert the string to a double
          return System.Convert.ToDouble(str, CultureInfo.InvariantCulture);
        }
      }
      catch (OverflowException)
      {
        // overflow mean just return one of the infinity values
        return (str[0] == '-'
          ? Double.NegativeInfinity
          : Double.PositiveInfinity
          );
      }
    }

    internal static double ToNumber(Object value)
    {
      return System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }
  }
}
