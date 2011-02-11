// jserror.cs
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

    public enum JSError
    {
        //0 - 1000 legacy scripting errors, not JScript specific.
        NoError = 0,
        InternalError = 51, // "Internal error"

        //1000 - 2000 JScript errors that occur during compilation only. (regard Eval and Function as compilation). Typically used only in HandleError.
        SyntaxError = 1002, // "Syntax error"
        NoColon = 1003, // "Expected ':'"
        NoSemicolon = 1004, // "Expected ';'"
        NoLeftParenthesis = 1005, // "Expected '('"
        NoRightParenthesis = 1006, // "Expected ')'"
        NoRightBracket = 1007, // "Expected ']'"
        NoLeftCurly = 1008, // "Expected '{'"
        NoRightCurly = 1009, // "Expected '}'"
        NoIdentifier = 1010, // "Expected identifier"
        NoEqual = 1011, // "Expected '='"
        IllegalChar = 1014, // "Invalid character"
        UnterminatedString = 1015, // "Unterminated string constant"
        NoCommentEnd = 1016, // "Unterminated comment"
        BadReturn = 1018, // "'return' statement outside of function"
        BadBreak = 1019, // "Can't have 'break' outside of loop"
        BadContinue = 1020, // "Can't have 'continue' outside of loop"
        BadHexDigit = 1023, // "Expected hexadecimal digit"
        NoWhile = 1024, // "Expected 'while'"
        BadLabel = 1025, // "Label redefined"
        NoLabel = 1026, // "Label not found"
        DupDefault = 1027, // "'default' can only appear once in a 'switch' statement"
        NoMemberIdentifier = 1028, // "Expected identifier or string"
        NoCCEnd = 1029, // "Expected '@end'"
        CCOff = 1030, // "Conditional compilation is turned off"
        NotConst = 1031, // "Expected constant"
        NoAt = 1032, // "Expected '@'"
        NoCatch = 1033, // "Expected 'catch'"
        InvalidElse = 1034, // "Unmatched 'else'; no 'if' defined"
        NoComma = 1100, // "Expected ','"
        BadSwitch = 1103, // "Missing 'case' or 'default' statement"
        CCInvalidEnd = 1104, // "Unmatched '@end'; no '@if' defined"
        CCInvalidElse = 1105, // "Unmatched '@else'; no '@if' defined"
        CCInvalidElseIf = 1106, // "Unmatched '@elif'; no '@if' defined"
        ErrorEndOfFile = 1107, // "Expecting more source characters"
        DuplicateName = 1111, // "Identifier already in use"
        InvalidPositionDirective = 1114, // "Unknown position directive"
        MustBeEndOfLine = 1115, // "Directive may not be followed by other code on the same line"
        WrongDirective = 1118, // "Wrong debugger directive or wrong position for the directive"
        CannotNestPositionDirective = 1119, // "Position directive must be ended before a new one can be started"
        UndeclaredVariable = 1135, // "Variable has not been declared"
        VariableLeftUninitialized = 1136, // "Uninitialized variables are dangerous and slow to use. Did you intend to leave it uninitialized?"
        KeywordUsedAsIdentifier = 1137, // "'xxxx' is a new reserved word and should not be used as an identifier"
        UndeclaredFunction = 1138, // "Function has not been declared"
        NoCommaOrTypeDefinitionError = 1191, // "Expected ',' or illegal type declaration, write '<Identifier> : <Type>' not '<Type> <Identifier>'"
        NoRightParenthesisOrComma = 1193, // "Expected ',' or ')'"
        NoRightBracketOrComma = 1194, // "Expected ',' or ']'"
        ExpressionExpected = 1195, // "Expected expression"
        UnexpectedSemicolon = 1196, // "Unexpected ';'"
        TooManyTokensSkipped = 1197, // "Too many tokens have been skipped in the process of recovering from errors. The file may not be a JScript.NET file"
        DoesNotHaveAnAddress = 1203, //"Expression does not have an address"
        SuspectAssignment = 1206, //"Did you intend to write an assignment here?"
        SuspectSemicolon = 1207, //"Did you intend to have an empty statement for this branch of the if statement?"
        ParameterListNotLast = 1240, //"A variable argument list must be the last argument
        CCInvalidInDebugger = 1256, //"Conditional compilation directives and variables cannot be used in the debugger"
        StatementBlockExpected = 1267, //"A statement block is expected"
        VariableDefinedNotReferenced = 1268, //"A variabled was defined but not set or referenced"
        ArgumentNotReferenced = 1270, //"Argument was defined but not referenced"
        WithNotRecommended = 1271, //"With statement is not recommended"
        FunctionNotReferenced = 1272, //"A function was defined but not referenced"
        AmbiguousCatchVar = 1273, //"Catch identifiers should be unique"
        FunctionExpressionExpected = 1274, //"Function expression expected"
        ObjectConstructorTakesNoArguments = 1275, //"Object constructor takes no arguments"
        JSParserException = 1276, // "JSParser Exception"
        NumericOverflow = 1277, // "Numeric literal causes overflow or underflow exception"
        NumericMaximum = 1278, // "Consider replacing maximum numeric literal with Number.MAX_VALUE"
        NumericMinimum = 1279, // "Consider replacing minimum numeric literal with Number.MIN_VALUE"
        ResourceReferenceMustBeConstant = 1280, // "Resource reference must be single constant value"
        AmbiguousNamedFunctionExpression = 1281, // "Ambiguous named function expression"
        ConditionalCompilationTooComplex = 1282, // "Conditiona compilation structure too complex"
		UnterminatedAspNetBlock = 1283, // Unterminated asp.net block.
        MisplacedFunctionDeclaration = 1284, // function declaration other than where SourceElements are expected
        OctalLiteralsDeprecated = 1285, // octal literal encountered; possible cross-browser issues

        //5000 - 6000 JScript errors that can occur during execution. Typically (also) used in "throw new JScriptException".
        IllegalAssignment = 5008, // "Illegal assignment"
        RegExpSyntax = 5017, // "Syntax error in regular expression"
        UncaughtException = 5022, // "Exception thrown and not caught"
    }
}
