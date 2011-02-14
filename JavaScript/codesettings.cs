// codesettings.cs
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
using System.Text;

namespace Microsoft.Ajax.Utilities
{
    public enum OutputMode
    {
        SingleLine,
        MultipleLines
    }

    public enum LocalRenaming
    {
        KeepAll,
        KeepLocalizationVars,
        CrunchAll
    }

    public enum EvalTreatment
    {
        Ignore = 0,
        MakeImmediateSafe,
        MakeAllSafe
    }

    public class CodeSettings
    {
        public CodeSettings()
        {
            this.CollapseToLiteral = true;
            this.CombineDuplicateLiterals = false;
            this.EvalTreatment = EvalTreatment.Ignore;
            this.IndentSize = 4;
            this.InlineSafeStrings = true;
            this.LocalRenaming = LocalRenaming.CrunchAll;
            this.MacSafariQuirks = true;
            this.MinifyCode = true;
            this.OutputMode = OutputMode.SingleLine;
            this.PreserveFunctionNames = false;
            this.RemoveFunctionExpressionNames = true;
            this.RemoveUnneededCode = true;
            this.StripDebugStatements = true;
            this.AllowEmbeddedAspNetBlocks = false;
            this.EvalLiteralExpressions = true;
            this.ManualRenamesProperties = true;
        }

        /// <summary>
        /// Whether to allow embedded asp.net blocks.
        /// </summary>
        public bool AllowEmbeddedAspNetBlocks
        {
            get;
            set;
        }

        /// <summary>
        /// deprecated setting
        /// </summary>
        [Obsolete("This property is obsolete and no longer used")]
        public bool CatchAsLocal
        {
            get; set;
        }

        /// <summary>
        /// collapse new Array() to [] and new Object() to {} [true]
        /// or leave ais [false]
        /// </summary>
        public bool CollapseToLiteral
        {
            get; set;
        }

        /// <summary>
        /// Combine duplicate literals within function scopes to local variables [true]
        /// or leave them as-is [false]
        /// </summary>
        public bool CombineDuplicateLiterals
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether EvalsAreSafe.
        /// Deprecated in favor of EvalTreatment, which is an enumeration
        /// allowing for more options than just true or false.
        /// True for this property is the equivalent of EvalTreament.Ignore;
        /// False is the equivalent to EvalTreament.MakeAllSafe
        /// </summary>
        [Obsolete("This property is deprecated; use EvalTreatment instead")]
        public bool EvalsAreSafe
        {
            get
            {
                return EvalTreatment == EvalTreatment.Ignore;
            }
            set
            {
                EvalTreatment = (value ? EvalTreatment.Ignore : EvalTreatment.MakeAllSafe);
            }
        }

        /// <summary>
        /// Evaluate expressions containing only literal bool, string, numeric, or null values [true]
        /// Leave literal expressions alone and do not evaluate them [false]
        /// </summary>
        public bool EvalLiteralExpressions
        {
            get;
            set;
        }

        /// <summary>
        /// Eval statements are safe and do not access local variables or functions [true]
        /// Code run by Eval statements may attempt to access local variables and functions [false]
        /// </summary>
        public EvalTreatment EvalTreatment
        {
            get; set;
        }

        /// <summary>
        /// Number of spaces per indent level when in MultipleLines output mode
        /// </summary>
        public int IndentSize
        {
            get; set;
        }

        /// <summary>
        /// Break up string literals containing &lt;/script&gt; so inline code won't break [true]
        /// Leave string literals as-is [false]
        /// </summary>
        public bool InlineSafeStrings
        {
            get; set;
        }

        /// <summary>
        /// How to rename local variables and functions:
        /// KeepAll - do not rename local variables and functions
        /// CrunchAll - rename all local variables and functions to shorter names
        /// KeepLocalizationVars - rename all local variables and functions that do NOT start with L_
        /// </summary>
        public LocalRenaming LocalRenaming
        {
            get; set;
        }

        /// <summary>
        /// Add characters to the output to make sure Mac Safari bugs are not generated [true]
        /// Disregard potential Mac Safari bugs [false]
        /// </summary>
        public bool MacSafariQuirks
        {
            get; set;
        }

        /// <summary>
        /// Modify the source code's syntax tree to provide the smallest equivalent output [true]
        /// Do not modify the syntax tree [false]
        /// </summary>
        public bool MinifyCode
        {
            get; set;
        }

        /// <summary>
        /// When using the manual-rename feature, properties with the "from" name will
        /// get renamed to the "to" name if this property is true (default), and left alone
        /// if this property is false.
        /// </summary>
        public bool ManualRenamesProperties
        {
            get; set;
        }

        /// <summary>
        /// Kill switch flags for each individual mod to the parsed code tree. Allows for
        /// callers to turn off specific modifications if desired.
        /// </summary>
        public TreeModifications KillSwitch
        {
            get; set;
        }

        /// <summary>
        /// Output mode:
        /// SingleLine - output all code on a single line
        /// MultipleLines - break the output into multiple lines to be more human-readable
        /// </summary>
        public OutputMode OutputMode
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether all function names must be preserved
        /// and remain as-named.
        /// </summary>
        public bool PreserveFunctionNames
        {
            get; set;
        }

        public bool RemoveFunctionExpressionNames
        {
            get; set;
        }

        /// <summary>
        /// Remove unneeded code, like uncalled local functions [true]
        /// Keep all code [false]
        /// </summary>
        public bool RemoveUnneededCode
        {
            get; set;
        }

        /// <summary>
        /// Strip debug statements [true]
        /// Leave debug statements [false]
        /// </summary>
        public bool StripDebugStatements
        {
            get; set;
        }

        [Obsolete("This property is obsolete and no longer used")]
        public bool W3Strict
        {
            get; set;
        }

        /// <summary>
        /// Determine whether a particular AST tree modification is allowed, or has
        /// been squelched (regardless of any other settings)
        /// </summary>
        /// <param name="modification">one or more tree modification settings</param>
        /// <returns>true only if NONE of the passed modifications have their kill bits set</returns>
        public bool IsModificationAllowed(TreeModifications modification)
        {
            return (this.KillSwitch & modification) == TreeModifications.None;
        }

        #region Indent methods

        // this is the indent level and size for the pretty-print
        private int m_indentLevel;// = 0;

        internal void Indent()
        {
            ++m_indentLevel;
        }

        internal void Unindent()
        {
            --m_indentLevel;
        }

        internal string IndentSpaces
        {
            get
            {
                int numSpaces = m_indentLevel * IndentSize;
                return (numSpaces > 0 ? new string(' ', numSpaces) : string.Empty);
            }
        }

        // put indent level and size together for a new-line
        internal bool NewLine(StringBuilder sb)
        {
            bool addNewLine = (OutputMode == OutputMode.MultipleLines);
            if (addNewLine)
            {
                sb.AppendLine();
                sb.Append(IndentSpaces);
            }

            return addNewLine;
        }

        #endregion
    }

    [Flags]
    public enum TreeModifications : long
    {
        /// <summary>
        /// Default. No specific tree modification
        /// </summary>
        None                                        = 0x0000000000000000,

        /// <summary>
        /// Preserve "important" comments in output: /*! ... */
        /// </summary>
        PreserveImportantComments                   = 0x0000000000000001,

        /// <summary>
        /// Replace a member-bracket call with a member-dot construct if the member
        /// name is a string literal that can be an identifier.
        /// A["B"] ==&gt; A.B
        /// </summary>
        BracketMemberToDotMember                    = 0x0000000000000002,

        /// <summary>
        /// Replace a new Object constructor call with an object literal
        /// new Object() ==&gt; {}
        /// </summary>
        NewObjectToObjectLiteral                    = 0x0000000000000004,

        /// <summary>
        /// Change Array constructor calls with array literals.
        /// Does not replace constructors called with a single numeric parameter
        /// (could be a capacity contructor call).
        /// new Array() ==&gt; []
        /// new Array(A,B,C) ==&gt; [A,B,C]
        /// </summary>
        NewArrayToArrayLiteral                      = 0x0000000000000008,

        /// <summary>
        /// Remove the default case in a switch statement if the block contains
        /// only a break statement.
        /// remove default:break;
        /// </summary>
        RemoveEmptyDefaultCase                      = 0x0000000000000010,

        /// <summary>
        /// If there is no default case, remove any case statements that contain
        /// only a single break statement.
        /// remove case A:break;
        /// </summary>
        RemoveEmptyCaseWhenNoDefault                = 0x0000000000000020,

        /// <summary>
        /// Remove the break statement from the last case block of a switch statement.
        /// switch(A){case B: C;break;} ==&gt; switch(A){case B:C;}
        /// </summary>
        RemoveBreakFromLastCaseBlock                = 0x0000000000000040,

        /// <summary>
        /// Remove an empty finally statement if there is a non-empty catch block.
        /// try{...}catch(E){...}finally{} ==&gt; try{...}catch(E){...}
        /// </summary>
        RemoveEmptyFinally                          = 0x0000000000000080,

        /// <summary>
        /// Remove duplicate var declarations in a var statement that have no initializers.
        /// var A,A=B  ==&gt;  var A=B
        /// var A=B,A  ==&gt;  var A=B
        /// </summary>
        RemoveDuplicateVar                          = 0x0000000000000100,

        /// <summary>
        /// Combine adjacent var statements.
        /// var A;var B  ==&gt;  var A,B
        /// </summary>
        CombineVarStatements                        = 0x0000000000000200,

        /// <summary>
        /// Move preceeding var statement into the initializer of the for statement.
        /// var A;for(var B;;);  ==&gt;  for(var A,B;;);
        /// var A;for(;;)  ==&gt; for(var A;;)
        /// </summary>
        MoveVarIntoFor                              = 0x0000000000000400,

        /// <summary>
        /// Combine adjacent var statement and return statement to a single return statement
        /// var A=B;return A  ==&gt; return B
        /// </summary>
        VarInitializeReturnToReturnInitializer      = 0x0000000000000800,

        /// <summary>
        /// Replace an if-statement that has empty true and false branches with just the 
        /// condition expression.
        /// if(A);else;  ==&gt; A;
        /// </summary>
        IfEmptyToExpression                         = 0x0000000000001000,

        /// <summary>
        /// replace if-statement that only has a single call statement in the true branch
        /// with a logical-and statement
        /// if(A)B() ==&gt; A&amp;&amp;B()
        /// </summary>
        IfConditionCallToConditionAndCall           = 0x0000000000002000,

        /// <summary>
        /// Replace an if-else-statement where both branches are only a single return
        /// statement with a single return statement and a conditional operator.
        /// if(A)return B;else return C  ==&gt;  return A?B:C 
        /// </summary>
        IfElseReturnToReturnConditional             = 0x0000000000004000,

        /// <summary>
        /// If a function ends in an if-statement that only has a true-branch containing
        /// a single return statement with no operand, replace the if-statement with just
        /// the condition expression.
        /// function A(...){...;if(B)return}  ==&gt; function A(...){...;B}
        /// </summary>
        IfConditionReturnToCondition                = 0x0000000000008000,

        /// <summary>
        /// If the true-block of an if-statment is empty and the else-block is not,
        /// negate the condition and move the else-block to the true-block.
        /// if(A);else B  ==&gt;  if(!A)B
        /// </summary>
        IfConditionFalseToIfNotConditionTrue        = 0x0000000000010000,

        /// <summary>
        /// Combine adjacent string literals.
        /// "A"+"B"  ==&gt; "AB"
        /// </summary>
        CombineAdjacentStringLiterals               = 0x0000000000020000,

        /// <summary>
        /// Remove unary-plus operators when the operand is a numeric literal
        /// +123  ==&gt;  123
        /// </summary>
        RemoveUnaryPlusOnNumericLiteral             = 0x0000000000040000,

        /// <summary>
        /// Apply (and cascade) unary-minus operators to the value of a numeric literal
        /// -(4)  ==&gt;  -4   (unary minus applied to a numeric 4 ==&gt; numeric -4)
        /// -(-4)  ==&gt;  4   (same as above, but cascading)
        /// </summary>
        ApplyUnaryMinusToNumericLiteral             = 0x0000000000080000,

        /// <summary>
        /// Apply minification technics to string literals
        /// </summary>
        MinifyStringLiterals                        = 0x0000000000100000,

        /// <summary>
        /// Apply minification techniques to numeric literals
        /// </summary>
        MinifyNumericLiterals                       = 0x0000000000200000,

        /// <summary>
        /// Remove unused function parameters
        /// </summary>
        RemoveUnusedParameters                      = 0x0000000000400000,

        /// <summary>
        /// remove "debug" statements
        /// </summary>
        StripDebugStatements                        = 0x0000000000800000,

        /// <summary>
        /// Rename local variables and functions
        /// </summary>
        LocalRenaming                               = 0x0000000001000000,

        /// <summary>
        /// Remove unused function expression names
        /// </summary>
        RemoveFunctionExpressionNames               = 0x0000000002000000,

        /// <summary>
        /// Remove unnecessary labels from break or continue statements
        /// </summary>
        RemoveUnnecessaryLabels                     = 0x0000000004000000,

        /// <summary>
        /// Remove unnecessary @cc_on statements
        /// </summary>
        RemoveUnnecessaryCCOnStatements             = 0x0000000008000000,

        /// <summary>
        /// Convert (new Date()).getTime() to +new Date
        /// </summary>
        DateGetTimeToUnaryPlus                      = 0x0000000010000000,

        /// <summary>
        /// Evaluate numeric literal expressions.
        /// 1 + 2  ==&gt; 3
        /// </summary>
        EvaluateNumericExpressions                  = 0x0000000020000000,

        /// <summary>
        /// Simplify a common method on converting string to numeric: 
        /// lookup - 0  ==&gt; +lookup
        /// (Subtracting zero converts lookup to number, then doesn't modify
        /// it; unary plus also converts operand to numeric)
        /// </summary>
        SimplifyStringToNumericConversion           = 0x0000000040000000,

        /// <summary>
        /// Rename properties in object literals, member-dot, and member-bracket operations
        /// </summary>
        PropertyRenaming                            = 0x0000000080000000,
    }
}
