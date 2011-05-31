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
        Semicolon,                      // ;
        RightCurly,                     // }
        LeftCurly,                      // {
        Debugger,
        Var,
        If,
        For,
        Do,
        While,
        Continue,
        Break,
        Return,
        With,
        Switch,
        Throw,
        Try,
        Function,
        Else,
        ConditionalCommentStart,        // /*@ or //@
        ConditionalCompilationOn,       // @cc_on
        ConditionalCompilationSet,      // @set
        ConditionalCompilationIf,       // @if
        ConditionalCompilationElseIf,   // @elif
        ConditionalCompilationElse,     // @else
        ConditionalCompilationEnd,      // @end
		AspNetBlock,

        // used by both statement and expression switches

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
        Void = FirstOperator,
        TypeOf,
        Delete,
        Increment,                      // ++
        Decrement,                      // --
        LogicalNot,     // !
        BitwiseNot,                     // ~

        FirstBinaryOperator,
        // binary ops
        Plus = FirstBinaryOperator,     // +
        Minus,                          // -
        LessThan,                       // <
        Divide,                         // /
        Modulo,                         // %
        LeftShift,                      // <<
        RightShift,                     // >>
        UnsignedRightShift,             // >>>

        Equal,                          // ==
        NotEqual,                       // !=
        StrictEqual,                    // ===
        StrictNotEqual,                 // !==
        LessThanEqual,                  // <=
        GreaterThan,                    // >
        GreaterThanEqual,               // >=

        Multiply,                       // *
        BitwiseAnd,                     // &
        BitwiseOr,                      // |
        BitwiseXor,                     // ^
        LogicalAnd,                     // &&
        LogicalOr,                      // ||

        Assign,                         // =
        PlusAssign,                     // +=
        MinusAssign,                    // -=
        MultiplyAssign,                 // *=
        DivideAssign,                   // /=
        ModuloAssign,                   // %=
        BitwiseAndAssign,               // &=
        BitwiseOrAssign,                // |=
        BitwiseXorAssign,               // ^=
        LeftShiftAssign,                // >>=
        RightShiftAssign,               // <<=
        UnsignedRightShiftAssign,       // <<<=
        LastAssign = UnsignedRightShiftAssign,

        InstanceOf,
        In,

        ConditionalIf,                  // ? // MUST FOLLOW LastBinaryOp
        Colon,                          // :
        Comma,                          // ,
        LastOperator = Comma,

        // context specific keywords
        Case,
        Catch,
        Default,
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

        Package,
        Internal,
        Abstract,
        Public,
        Static,
        Private,
        Protected,
        Final,
        Event,
        Const,
        Class,
        Import,

        EndOfLine, // only returned if the RawTokens flag is set on the scanner, but also used in error-recovery
        WhiteSpace, // only returned if the RawTokens flag is set on the scanner
        Error, // only returned if the RawTokens flag is set on the scanner
        RegularExpression, // only returned if the RawTokens flag is set on the scanner

        PreprocessDirective,
        ConditionalCommentEnd,          // @*/ or EOL

    }
}
