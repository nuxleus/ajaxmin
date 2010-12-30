// jstoken.cs
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

    public enum JSToken : int
    {
        None = -1,
        EndOfFile,

        // main statement switch
        If,
        For,
        Do,
        While,
        Continue,
        Break,
        Return,
        Import,
        With,
        Switch,
        Throw,
        Try,
        Package,
        Internal,
        Abstract,
        Public,
        Static,
        Private,
        Protected,
        Final,
        Event,
        Var,
        Const,
        Class,

        // used by both statement and expression switches
        Function,
        LeftCurly,                      // {
        Semicolon,                      // ;

        // main expression switch
        Null,
        True,
        False,
        This,
        Identifier,
        StringLiteral,
        IntegerLiteral,
        NumericLiteral,

        LeftParenthesis,                // (
        LeftBracket,                    // [
        AccessField,                    // .

        // operators
        FirstOperator,
        // unary ops
        LogicalNot = FirstOperator,     // !
        BitwiseNot,                     // ~
        Delete,
        Void,
        TypeOf,
        Increment,                      // ++
        Decrement,                      // --
        FirstBinaryOperator,
        // binary ops
        Plus = FirstBinaryOperator,     // +
        Minus,                          // -
        LogicalOr,                      // ||
        LogicalAnd,                     // &&
        BitwiseOr,                      // |
        BitwiseXor,                     // ^
        BitwiseAnd,                     // &
        Equal,                          // ==
        NotEqual,                       // !=
        StrictEqual,                    // ===
        StrictNotEqual,                 // !==
        GreaterThan,                    // >
        LessThan,                       // <
        LessThanEqual,                  // <=
        GreaterThanEqual,               // >=
        LeftShift,                      // <<
        RightShift,                     // >>
        UnsignedRightShift,             // >>>
        Multiply,                       // *
        Divide,                         // /
        Modulo,                         // %
        LastPPOperator = Modulo,
        InstanceOf,
        In,
        Assign,                         // =
        PlusAssign,                     // +=
        MinusAssign,                    // -=
        MultiplyAssign,                 // *=
        DivideAssign,                   // /=
        BitwiseAndAssign,               // &=
        BitwiseOrAssign,                // |=
        BitwiseXorAssign,               // ^=
        ModuloAssign,                   // %=
        LeftShiftAssign,                // >>=
        RightShiftAssign,               // <<=
        UnsignedRightShiftAssign,       // <<<=
        LastAssign = UnsignedRightShiftAssign,
        LastBinaryOperator = UnsignedRightShiftAssign,
        ConditionalIf,                  // ? // MUST FOLLOW LastBinaryOp
        Colon,                          // :
        Comma,                          // ,
        LastOperator = Comma,

        // context specific keywords
        Case,
        Catch,
        Debugger,
        Default,
        Else,
        Export,
        Extends,
        Finally,
        Get,
        Implements,
        Interface,
        New,
        Set,
        Super,
        RightParenthesis,               // )
        RightCurly,                     // }
        RightBracket,                   // ]
        PreprocessorConstant,           // entity defined defined during preprocessing
        Comment,                        // for authoring
        UnterminatedComment,            // for authoring
        // js5, js8 and ECMA reserved words
        Assert,
        Boolean,
        Byte,
        Char,
        Decimal,
        Double,
        DoubleColon,                      // ::
        Enum,
        Ensure,
        Float,
        GoTo,
        Int,
        Invariant,
        Long,
        Namespace,
        Native,
        Require,
        SignedByte,
        Short,
        Synchronized,
        Transient,
        Throws,
        ParameterArray,                 // ...
        Volatile,
        UnsignedShort,
        UnsignedInt,
        UnsignedLong,
        Use,

        EndOfLine, // this is here only because of error recovery, in reality this token is never produced
        PreprocessDirective,
        ConditionalCommentStart,        // /*@ or //@
        ConditionalCommentEnd,          // @*/ or EOL
        ConditionalCompilationOn,       // @cc_on
        ConditionalCompilationSet,      // @set
        ConditionalCompilationIf,       // @if
        ConditionalCompilationElseIf,   // @elif
        ConditionalCompilationElse,     // @else
        ConditionalCompilationEnd,      // @end

		AspNetBlock,
    }
}
