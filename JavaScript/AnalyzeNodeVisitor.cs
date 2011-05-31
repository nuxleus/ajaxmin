// AnalyzeNodeVisitor.cs
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
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Ajax.Utilities
{
    internal class AnalyzeNodeVisitor : TreeVisitor
    {
        private JSParser m_parser;
        private uint m_uniqueNumber;// = 0;
        bool m_encounteredCCOn;// = false;

        private Stack<ActivationObject> ScopeStack { get { return m_parser.ScopeStack; } }

        private uint UniqueNumber
        {
            get
            {
                lock (this)
                {
                    // we'll want to roll over if for some reason we ever hit the max
                    if (m_uniqueNumber == int.MaxValue)
                    {
                        m_uniqueNumber = 0;
                    }
                    return m_uniqueNumber++;
                }
            }
        }

        public AnalyzeNodeVisitor(JSParser parser)
        {
            m_parser = parser;
        }

        public override void Visit(BinaryOperator node)
        {
            if (node != null)
            {
                base.Visit(node);

                // see if this operation is subtracting zero from a lookup -- that is typically done to
                // coerce a value to numeric. There's a simpler way: unary plus operator.
                if (node.OperatorToken == JSToken.Minus
                    && m_parser.Settings.IsModificationAllowed(TreeModifications.SimplifyStringToNumericConversion))
                {
                    Lookup lookup = node.Operand1 as Lookup;
                    if (lookup != null)
                    {
                        ConstantWrapper right = node.Operand2 as ConstantWrapper;
                        if (right != null && right.IsIntegerLiteral && right.ToNumber() == 0)
                        {
                            // okay, so we have "lookup - 0"
                            // this is done frequently to force a value to be numeric. 
                            // There is an easier way: apply the unary + operator to it. 
                            NumericUnary unary = new NumericUnary(node.Context, m_parser, lookup, JSToken.Plus);
                            node.Parent.ReplaceChild(node, unary);

                            // because we recursed at the top of this function, we don't need to Analyze
                            // the new Unary node. This visitor's method for NumericUnary only does something
                            // if the operand is a constant -- and this one is a Lookup. And we already analyzed
                            // the lookup.
                        }
                    }
                }
            }
        }

        public override void Visit(Block node)
        {
            if (node != null)
            {
                // javascript doesn't have block scope, so there really is no point
                // in nesting blocks. Unnest any now, before we start combining var statements
                UnnestBlocks(node);

                // if we want to remove debug statements...
                if (m_parser.Settings.StripDebugStatements && m_parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements))
                {
                    // do it now before we try doing other things
                    StripDebugStatements(node);
                }

                // these variables are used to check for combining a particular type of
                // for-statement with preceding var-statements.
                ForNode targetForNode = null;
                string targetName = null;

                // check to see if we want to combine adjacent var statements
                bool combineVarStatements = m_parser.Settings.IsModificationAllowed(TreeModifications.CombineVarStatements);

                // check to see if we want to combine a preceding var with a for-statement
                bool moveVarIntoFor = m_parser.Settings.IsModificationAllowed(TreeModifications.MoveVarIntoFor);

                // look at the statements in the block. 
                // if there are multiple var statements adjacent to each other, combine them.
                // walk BACKWARDS down the list because we'll be removing items when we encounter
                // multiple vars.
                // we also don't need to check the first one, since there is nothing before it.
                for (int ndx = node.Count - 1; ndx > 0; --ndx)
                {
                    // if the previous node is not a Var, then we don't need to try and combine
                    // it with the current node
                    Var previousVar = node[ndx - 1] as Var;
                    if (previousVar != null)
                    {
                        // see if THIS item is also a Var...
                        if (node[ndx] is Var && combineVarStatements)
                        {
                            // add the items in this VAR to the end of the previous
                            previousVar.Append(node[ndx]);

                            // delete this item from the block
                            node.RemoveAt(ndx);

                            // if we have a target for-node waiting for another comparison....
                            if (targetForNode != null)
                            {
                                // check to see if the variable we are looking for is in the new list
                                if (previousVar.Contains(targetName))
                                {
                                    // IT DOES! we can combine the var statement with the initializer in the for-statement
                                    // we already know it's a binaryop, or it wouldn't be a target for-statement
                                    BinaryOperator binaryOp = targetForNode.Initializer as BinaryOperator;

                                    // create a vardecl that matches our assignment initializer
                                    // ignore duplicates because this scope will already have the variable defined.
                                    VariableDeclaration varDecl = new VariableDeclaration(
                                        binaryOp.Context.Clone(),
                                        m_parser,
                                        targetName,
                                        binaryOp.Operand1.Context.Clone(),
                                        binaryOp.Operand2,
                                        0,
                                        true
                                        );
                                    // append it to the preceding var-statement
                                    previousVar.Append(varDecl);

                                    // move the previous vardecl to our initializer
                                    targetForNode.ReplaceChild(targetForNode.Initializer, previousVar);

                                    // and remove the previous var from the list.
                                    node.RemoveAt(ndx - 1);
                                    // this will bump the for node up one position in the list, so the next iteration
                                    // will be right back on this node, but the initializer will not be null

                                    // but now we no longer need the target mechanism -- the for-statement is
                                    // not the current node again
                                    targetForNode = null;
                                }
                            }
                        }
                        else if (moveVarIntoFor)
                        {
                            // see if this item is a ForNode
                            ForNode forNode = node[ndx] as ForNode;
                            if (forNode != null)
                            {
                                // and see if the forNode's initializer is empty
                                if (forNode.Initializer != null)
                                {
                                    // not empty -- see if it is a Var node
                                    Var varInitializer = forNode.Initializer as Var;
                                    if (varInitializer != null)
                                    {
                                        // we want to PREPEND the initializers in the previous var statement
                                        // to our for-statement's initializer list
                                        varInitializer.InsertAt(0, previousVar);

                                        // then remove the previous var statement
                                        node.RemoveAt(ndx - 1);
                                        // this will bump the for node up one position in the list, so the next iteration
                                        // will be right back on this node in case there are other var statements we need
                                        // to combine
                                    }
                                    else
                                    {
                                        // see if the initializer is a simple assignment
                                        BinaryOperator binaryOp = forNode.Initializer as BinaryOperator;
                                        if (binaryOp != null && binaryOp.OperatorToken == JSToken.Assign)
                                        {
                                            // it is. See if it's a simple lookup
                                            Lookup lookup = binaryOp.Operand1 as Lookup;
                                            if (lookup != null)
                                            {
                                                // it is. see if that variable is in the previous var statement
                                                if (previousVar.Contains(lookup.Name))
                                                {
                                                    // create a vardecl that matches our assignment initializer
                                                    // ignore duplicates because this scope will already have the variable defined.
                                                    VariableDeclaration varDecl = new VariableDeclaration(
                                                        binaryOp.Context.Clone(),
                                                        m_parser,
                                                        lookup.Name,
                                                        lookup.Context.Clone(),
                                                        binaryOp.Operand2,
                                                        0,
                                                        true
                                                        );
                                                    // append it to the var statement before us
                                                    previousVar.Append(varDecl);

                                                    // move the previous vardecl to our initializer
                                                    forNode.ReplaceChild(forNode.Initializer, previousVar);

                                                    // and remove the previous var from the list.
                                                    node.RemoveAt(ndx - 1);
                                                    // this will bump the for node up one position in the list, so the next iteration
                                                    // will be right back on this node, but the initializer will not be null
                                                }
                                                else
                                                {
                                                    // it's not in the immediately preceding var-statement, but that doesn't mean it won't be in 
                                                    // a var-statement immediately preceding that one -- in which case they'll get combined and 
                                                    // then it WILL be in the immediately preceding var-statement. So hold on to this
                                                    // for statement and we'll check after we do a combine.
                                                    targetForNode = forNode;
                                                    targetName = lookup.Name;
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // if it's empty, then we're free to add the previous var statement
                                    // to this for statement's initializer. remove it from it's current
                                    // position and add it as the initializer
                                    node.RemoveAt(ndx - 1);
                                    forNode.ReplaceChild(forNode.Initializer, previousVar);
                                    // this will bump the for node up one position in the list, so the next iteration
                                    // will be right back on this node, but the initializer will not be null
                                }
                            }
                        }
                    }
                    else
                    {
                        // not a var statement. make sure the target for-node is cleared.
                        targetForNode = null;

                        ConditionalCompilationComment previousComment = node[ndx - 1] as ConditionalCompilationComment;
                        if (previousComment != null)
                        {
                            ConditionalCompilationComment thisComment = node[ndx] as ConditionalCompilationComment;
                            if (thisComment != null)
                            {
                                // two adjacent conditional comments -- combine them into the first.
                                // this will actually make the second block a nested block within the first block,
                                // but they'll be flattened when the comment's block gets recursed.
                                previousComment.Statements.Append(thisComment.Statements);

                                // and remove the second one (which is now a duplicate)
                                node.RemoveAt(ndx);
                            }
                        }
                    }
                }

                if (node.BlockScope != null)
                {
                    ScopeStack.Push(node.BlockScope);
                }
                try
                {
                    // call the base class to recurse
                    base.Visit(node);
                }
                finally
                {
                    if (node.BlockScope != null)
                    {
                        ScopeStack.Pop();
                    }
                }

                // NOW that we've recursively analyzed all the child nodes in this block, let's see
                // if we can further reduce the statements by checking for a couple good opportunities
                if (m_parser.Settings.RemoveUnneededCode)
                {
                    // Transform: {var foo=expression;return foo;} to: {return expression;}
                    if (node.Count == 2 && m_parser.Settings.IsModificationAllowed(TreeModifications.VarInitializeReturnToReturnInitializer))
                    {
                        Var varStatement = node[0] as Var;
                        ReturnNode returnStatement = node[1] as ReturnNode;

                        // see if we have two statements in our block: a var with a single declaration, and a return
                        if (returnStatement != null && varStatement != null
                            && varStatement.Count == 1 && varStatement[0].Initializer != null)
                        {
                            // now see if the return is returning a lookup for the same var we are declaring in the
                            // previous statement
                            Lookup lookup = returnStatement.Operand as Lookup;
                            if (lookup != null
                                && string.Compare(lookup.Name, varStatement[0].Identifier, StringComparison.Ordinal) == 0)
                            {
                                // it's a match!
                                // create a combined context starting with the var and adding in the return
                                Context context = varStatement.Context.Clone();
                                context.UpdateWith(returnStatement.Context);

                                // create a new return statement
                                ReturnNode newReturn = new ReturnNode(context, m_parser, varStatement[0].Initializer);

                                // clear out the existing statements
                                node.Clear();

                                // and add our new one
                                node.Append(newReturn);
                            }
                        }
                    }

                    // we do things differently if these statements are the last in a function
                    // because we can assume the implicit return
                    bool isFunctionLevel = (node.Parent is FunctionObject);

                    // see if we want to change if-statement that forces a return to a return conditional
                    if (m_parser.Settings.IsModificationAllowed(TreeModifications.IfElseReturnToReturnConditional))
                    {
                        // transform: {...; if(cond1)return;} to {...;cond;}
                        // transform: {...; if(cond1)return exp1;else return exp2;} to {...;return cond1?exp1:exp2;}
                        if (node.Count >= 1)
                        {
                            // see if the last statement is an if-statement with a true-block containing only one statement
                            IfNode ifStatement = node[node.Count - 1] as IfNode;
                            if (ifStatement != null
                                && ifStatement.TrueBlock != null)
                            {
                                // see if this if-statement is structured such that we can convert it to a
                                // Conditional node that is the operand of a return statement
                                Conditional returnOperand = ifStatement.CanBeReturnOperand(null, isFunctionLevel);
                                if (returnOperand != null)
                                {
                                    // it can! change it.
                                    ReturnNode returnNode = new ReturnNode(
                                        (node.Context == null ? null : node.Context.Clone()),
                                        m_parser,
                                        returnOperand);

                                    // replace the if-statement with the return statement
                                    node.ReplaceChild(ifStatement, returnNode);
                                }
                            }
                            // else last statement is not an if-statement, or true block is not a single statement
                        }

                        // transform: {...; if(cond1)return exp1;return exp2;} to {...; return cond1?exp1:exp2;}
                        // my cascade! changing the two statements to a return may cause us to run this again if the
                        // third statement up becomes the penultimate and is an if-statement
                        while (node.Count > 1)
                        {
                            int lastIndex = node.Count - 1;
                            // end in a return statement?
                            ReturnNode finalReturn = node[lastIndex] as ReturnNode;
                            if (finalReturn != null)
                            {
                                // it does -- see if the penultimate statement is an if-block
                                IfNode ifNode = node[lastIndex - 1] as IfNode;
                                if (ifNode != null)
                                {
                                    // if followed by return. See if the if statement can be changed to a
                                    // return of a conditional, using the operand of the following return
                                    // as the ultimate expression
                                    Conditional returnConditional = ifNode.CanBeReturnOperand(finalReturn.Operand, isFunctionLevel);
                                    if (returnConditional != null)
                                    {
                                        // it can! so create the new return statement.
                                        // the context of this new return statement should start with a clone of
                                        // the if-statement and updated with the return statement
                                        Context context = ifNode.Context.Clone();
                                        context.UpdateWith(finalReturn.Context);

                                        // create the new return node
                                        ReturnNode newReturn = new ReturnNode(
                                            context,
                                            m_parser,
                                            returnConditional);

                                        // remove the last node (the old return)
                                        node.RemoveAt(lastIndex--);

                                        // and replace the if-statement with the new return
                                        node[lastIndex] = newReturn;

                                        // we collapsed the last two statements, and we KNOW the last one is a
                                        // return -- go back up to the top of the loop to see if we can keep going.
                                        continue;
                                    }
                                }
                            }

                            // if we get here, then something went wrong, we didn't collapse the last
                            // two statements, so break out of the loop
                            break;
                        }

                        // now we may have converted the last functional statement 
                        // from if(cond)return expr to return cond?expr:void 0, which is four
                        // extra bytes. So let's check to see if the last statement in the function
                        // now fits this pattern, and if so, change it back.
                        // We didn't just NOT change it in the first place because changing it could've
                        // enabled even more changes that would save a lot more space. But apparently
                        // those subsequent changes didn't pan out.
                        if (node.Count >= 1)
                        {
                            int lastIndex = node.Count - 1;
                            ReturnNode returnNode = node[lastIndex] as ReturnNode;
                            if (returnNode != null)
                            {
                                Conditional conditional = returnNode.Operand as Conditional;
                                if (conditional != null)
                                {
                                    VoidNode falseVoid = conditional.FalseExpression as VoidNode;
                                    if (falseVoid != null && falseVoid.Operand is ConstantWrapper)
                                    {
                                        // we have the required pattern: "return cond?expr:void 0"
                                        // (well, the object of the void is a constant, at least).
                                        // undo it back to "if(cond)return expr" because that takes fewer bytes.

                                        // by default, the operand of the return operator will be the 
                                        // true branch of the conditional
                                        AstNode returnOperand = conditional.TrueExpression;

                                        VoidNode trueVoid = conditional.TrueExpression as VoidNode;
                                        if (trueVoid != null && trueVoid.Operand is ConstantWrapper)
                                        {
                                            // the true branch of the conditional is a void operator acting
                                            // on a constant! So really, there is no operand to the return statement
                                            returnOperand = null;

                                            if (m_parser.Settings.IsModificationAllowed(TreeModifications.IfConditionReturnToCondition))
                                            {
                                                // actually, we have return cond?void 0:void 0,
                                                // which would get changed back to function{...;if(cond)return}
                                                // BUT we can just shorten it to function{...;cond}
                                                node[lastIndex] = conditional.Condition;
                                                return;
                                            }
                                        }

                                        IfNode ifNode = new IfNode(
                                            returnNode.Context.Clone(),
                                            m_parser,
                                            conditional.Condition,
                                            new ReturnNode(returnNode.Context.Clone(), m_parser, returnOperand),
                                            null);
                                        node[lastIndex] = ifNode;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public override void Visit(Break node)
        {
            if (node != null)
            {
                if (node.Label != null)
                {
                    // if the nest level is zero, then we might be able to remove the label altogether
                    // IF local renaming is not KeepAll AND the kill switch for removing them isn't set.
                    // the nest level will be zero if the label is undefined.
                    if (node.NestLevel == 0
                        && m_parser.Settings.LocalRenaming != LocalRenaming.KeepAll
                        && m_parser.Settings.IsModificationAllowed(TreeModifications.RemoveUnnecessaryLabels))
                    {
                        node.Label = null;
                    }
                }

                // don't need to call the base; this statement has no children to recurse
                //base.Visit(node);
            }
        }

        public override void Visit(CallNode node)
        {
            if (node != null)
            {
                // see if this is a member (we'll need it for a couple checks)
                Member member = node.Function as Member;

                if (m_parser.Settings.StripDebugStatements
                    && m_parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements))
                {
                    // if this is a member, and it's a debugger object, and it's a constructor....
                    if (member != null && member.IsDebuggerStatement && node.IsConstructor)
                    {
                        // we need to replace our debugger object with a generic Object
                        node.ReplaceChild(node.Function, new Lookup("Object", node.Function.Context, m_parser));

                        // and make sure the node list is empty
                        if (node.Arguments != null && node.Arguments.Count > 0)
                        {
                            node.ReplaceChild(node.Arguments, new AstNodeList(node.Arguments.Context, m_parser));
                        }
                    }
                }

                // if this is a constructor and we want to collapse
                // some of them to literals...
                if (node.IsConstructor && m_parser.Settings.CollapseToLiteral)
                {
                    // see if this is a lookup, and if so, if it's pointing to one
                    // of the two constructors we want to collapse
                    Lookup lookup = node.Function as Lookup;
                    if (lookup != null)
                    {
                        if (lookup.Name == "Object"
                            && m_parser.Settings.IsModificationAllowed(TreeModifications.NewObjectToObjectLiteral))
                        {
                            // no arguments -- the Object constructor with no arguments is the exact same as an empty
                            // object literal
                            if (node.Arguments == null || node.Arguments.Count == 0)
                            {
                                // replace our node with an object literal
                                ObjectLiteral objLiteral = new ObjectLiteral(node.Context, m_parser, null, null);
                                if (node.Parent.ReplaceChild(node, objLiteral))
                                {
                                    // and bail now. No need to recurse -- it's an empty literal
                                    return;
                                }
                            }
                            else if (node.Arguments.Count == 1)
                            {
                                // one argument
                                // check to see if it's an object literal.
                                ObjectLiteral objectLiteral = node.Arguments[0] as ObjectLiteral;
                                if (objectLiteral != null)
                                {
                                    // the Object constructor with an argument that is a JavaScript object merely returns the
                                    // argument. Since the argument is an object literal, it is by definition a JavaScript object
                                    // and therefore we can replace the constructor call with the object literal
                                    node.Parent.ReplaceChild(node, objectLiteral);

                                    // don't forget to recurse the object now
                                    objectLiteral.Accept(this);

                                    // and then bail -- we don't want to process this call
                                    // operation any more; we've gotten rid of it
                                    return;
                                }
                            }
                        }
                        else if (lookup.Name == "Array"
                            && m_parser.Settings.IsModificationAllowed(TreeModifications.NewArrayToArrayLiteral))
                        {
                            // Array is trickier. 
                            // If there are no arguments, then just use [].
                            // if there are multiple arguments, then use [arg0,arg1...argN].
                            // but if there is one argument and it's numeric, we can't crunch it.
                            // also can't crunch if it's a function call or a member or something, since we won't
                            // KNOW whether or not it's numeric.
                            //
                            // so first see if it even is a single-argument constant wrapper. 
                            ConstantWrapper constWrapper = (node.Arguments != null && node.Arguments.Count == 1
                                ? node.Arguments[0] as ConstantWrapper
                                : null);

                            // if the argument count is not one, then we crunch.
                            // if the argument count IS one, we only crunch if we have a constant wrapper, 
                            // AND it's not numeric.
                            if (node.Arguments == null
                              || node.Arguments.Count != 1
                              || (constWrapper != null && !constWrapper.IsNumericLiteral))
                            {
                                // create the new array literal object
                                ArrayLiteral arrayLiteral = new ArrayLiteral(node.Context, m_parser, node.Arguments);

                                // replace ourself within our parent
                                if (node.Parent.ReplaceChild(node, arrayLiteral))
                                {
                                    // recurse
                                    arrayLiteral.Accept(this);
                                    // and bail -- we don't want to recurse this node any more
                                    return;
                                }
                            }
                        }
                    }
                }

                // if we are replacing resource references with strings generated from resource files
                // and this is a brackets call: lookup[args]
                ResourceStrings resourceStrings = m_parser.ResourceStrings;
                if (node.InBrackets && resourceStrings != null && resourceStrings.Count > 0)
                {
                    // see if the root object is a lookup that corresponds to the 
                    // global value (not a local field) for our resource object
                    // (same name)
                    Lookup rootLookup = node.Function as Lookup;
                    if (rootLookup != null
                        && rootLookup.LocalField == null
                        && string.CompareOrdinal(rootLookup.Name, resourceStrings.Name) == 0)
                    {
                        // we're going to replace this node with a string constant wrapper
                        // but first we need to make sure that this is a valid lookup.
                        // if the parameter contains anything that would vary at run-time, 
                        // then we need to throw an error.
                        // the parser will always have either one or zero nodes in the arguments
                        // arg list. We're not interested in zero args, so just make sure there is one
                        if (node.Arguments.Count == 1)
                        {
                            // must be a constant wrapper
                            ConstantWrapper argConstant = node.Arguments[0] as ConstantWrapper;
                            if (argConstant != null)
                            {
                                string resourceName = argConstant.Value.ToString();

                                // get the localized string from the resources object
                                ConstantWrapper resourceLiteral = new ConstantWrapper(
                                    resourceStrings[resourceName],
                                    PrimitiveType.String,
                                    node.Context,
                                    m_parser);

                                // replace this node with localized string, analyze it, and bail
                                // so we don't anaylze the tree we just replaced
                                node.Parent.ReplaceChild(node, resourceLiteral);
                                resourceLiteral.Accept(this);
                                return;
                            }
                            else
                            {
                                // error! must be a constant
                                node.Context.HandleError(
                                    JSError.ResourceReferenceMustBeConstant,
                                    true);
                            }
                        }
                        else
                        {
                            // error! can only be a single constant argument to the string resource object.
                            // the parser will only have zero or one arguments, so this must be zero
                            // (since the parser won't pass multiple args to a [] operator)
                            node.Context.HandleError(
                                JSError.ResourceReferenceMustBeConstant,
                                true);
                        }
                    }
                }

                // and finally, if this is a backets call and the argument is a constantwrapper that can
                // be an identifier, just change us to a member node:  obj["prop"] to obj.prop.
                // but ONLY if the string value is "safe" to be an identifier. Even though the ECMA-262
                // spec says certain Unicode categories are okay, in practice the various major browsers
                // all seem to have problems with certain characters in identifiers. Rather than risking
                // some browsers breaking when we change this syntax, don't do it for those "danger" categories.
                if (node.InBrackets && node.Arguments != null)
                {
                    // see if there is a single, constant argument
                    string argText = node.Arguments.SingleConstantArgument;
                    if (argText != null)
                    {
                        // see if we want to replace the name
                        string newName;
                        if (m_parser.Settings.HasRenamePairs && m_parser.Settings.ManualRenamesProperties
                            && m_parser.Settings.IsModificationAllowed(TreeModifications.PropertyRenaming)
                            && !string.IsNullOrEmpty(newName = m_parser.Settings.GetNewName(argText)))
                        {
                            // yes -- we are going to replace the name, either as a string literal, or by converting
                            // to a member-dot operation.
                            // See if we can't turn it into a dot-operator. If we can't, then we just want to replace the operator with
                            // a new constant wrapper. Otherwise we'll just replace the operator with a new constant wrapper.
                            if (m_parser.Settings.IsModificationAllowed(TreeModifications.BracketMemberToDotMember)
                                && JSScanner.IsSafeIdentifier(newName)
                                && !JSScanner.IsKeyword(newName))
                            {
                                // the new name is safe to convert to a member-dot operator.
                                // but we don't want to convert the node to the NEW name, because we still need to Analyze the
                                // new member node -- and it might convert the new name to something else. So instead we're
                                // just going to convert this existing string to a member node WITH THE OLD STRING, 
                                // and THEN analyze it (which will convert the old string to newName)
                                Member replacementMember = new Member(node.Context, m_parser, node.Function, argText);
                                node.Parent.ReplaceChild(node, replacementMember);

                                // this analyze call will convert the old-name member to the newName value
                                replacementMember.Accept(this);
                                return;
                            }
                            else
                            {
                                // nope; can't convert to a dot-operator. 
                                // we're just going to replace the first argument with a new string literal
                                // and continue along our merry way.
                                node.Arguments.ReplaceChild(node.Arguments[0], new ConstantWrapper(newName, PrimitiveType.String, node.Arguments[0].Context, m_parser));
                            }
                        }
                        else if (m_parser.Settings.IsModificationAllowed(TreeModifications.BracketMemberToDotMember)
                            && JSScanner.IsSafeIdentifier(argText)
                            && !JSScanner.IsKeyword(argText))
                        {
                            // not a replacement, but the string literal is a safe identifier. So we will
                            // replace this call node with a Member-dot operation
                            Member replacementMember = new Member(node.Context, m_parser, node.Function, argText);
                            node.Parent.ReplaceChild(node, replacementMember);
                            replacementMember.Accept(this);
                            return;
                        }
                    }
                }

                // call the base class to recurse
                base.Visit(node);

                // call this AFTER recursing to give the fields a chance to resolve, because we only
                // want to make this replacement if we are working on the global Date object.
                if (!node.InBrackets && !node.IsConstructor
                    && (node.Arguments == null || node.Arguments.Count == 0)
                    && member != null && string.CompareOrdinal(member.Name, "getTime") == 0
                    && m_parser.Settings.IsModificationAllowed(TreeModifications.DateGetTimeToUnaryPlus))
                {
                    // this is not a constructor and it's not a brackets call, and there are no arguments.
                    // if the function is a member operation to "getTime" and the object of the member is a 
                    // constructor call to the global "Date" object (not a local), then we want to replace the call
                    // with a unary plus on the Date constructor. Converting to numeric type is the same as
                    // calling getTime, so it's the equivalent with much fewer bytes.
                    CallNode dateConstructor = member.Root as CallNode;
                    if (dateConstructor != null
                        && dateConstructor.IsConstructor)
                    {
                        // lookup for the predifined (not local) "Date" field
                        Lookup lookup = dateConstructor.Function as Lookup;
                        if (lookup != null && string.CompareOrdinal(lookup.Name, "Date") == 0
                            && lookup.LocalField == null)
                        {
                            // this is in the pattern: (new Date()).getTime()
                            // we want to replace it with +new Date
                            // use the same date constructor node as the operand
                            NumericUnary unary = new NumericUnary(node.Context, m_parser, dateConstructor, JSToken.Plus);

                            // replace us (the call to the getTime method) with this unary operator
                            node.Parent.ReplaceChild(node, unary);

                            // don't need to recurse on the unary operator. The operand has already
                            // been analyzed when we recursed, and the unary operator wouldn't do anything
                            // special anyway (since the operand is not a numeric constant)
                        }
                    }
                }
                else if (m_parser.Settings.EvalTreatment != EvalTreatment.Ignore)
                {
                    // if this is a window.eval call, then we need to mark this scope as unknown just as
                    // we would if this was a regular eval call.
                    // (unless, of course, the parser settings say evals are safe)
                    // call AFTER recursing so we know the left-hand side properties have had a chance to
                    // lookup their fields to see if they are local or global
                    if (member != null && string.CompareOrdinal(member.Name, "eval") == 0)
                    {
                        if (member.LeftHandSide.IsWindowLookup)
                        {
                            // this is a call to window.eval()
                            // mark this scope as unknown so we don't crunch out locals 
                            // we might reference in the eval at runtime
                            ScopeStack.Peek().IsKnownAtCompileTime = false;
                        }
                    }
                    else
                    {
                        CallNode callNode = node.Function as CallNode;
                        if (callNode != null
                            && callNode.InBrackets
                            && callNode.LeftHandSide.IsWindowLookup
                            && callNode.Arguments.IsSingleConstantArgument("eval"))
                        {
                            // this is a call to window["eval"]
                            // mark this scope as unknown so we don't crunch out locals 
                            // we might reference in the eval at runtime
                            ScopeStack.Peek().IsKnownAtCompileTime = false;
                        }
                    }
                }
            }
        }

        public override void Visit(ConditionalCompilationOn node)
        {
            // well, we've encountered a cc_on statement now
            m_encounteredCCOn = true;
        }

        public override void Visit(ConstantWrapper node)
        {
            if (node != null)
            {
                // check to see if this node is an argument to a RegExp constructor.
                // if it is, we'll want to not use certain string escapes
                AstNode previousNode = null;
                AstNode parentNode = node.Parent;
                while (parentNode != null)
                {
                    // is this a call node and the previous node was one of the parameters?
                    CallNode callNode = parentNode as CallNode;
                    if (callNode != null && previousNode == callNode.Arguments)
                    {
                        // are we calling a simple lookup for "RegExp"?
                        Lookup lookup = callNode.Function as Lookup;
                        if (lookup != null && lookup.Name == "RegExp")
                        {
                            // we are -- so all string literals passed within this constructor should not use
                            // standard string escape sequences
                            node.IsParameterToRegExp = true;
                            // we can stop looking
                            break;
                        }
                    }

                    // next up the chain, keeping track of this current node as next iteration's "previous" node
                    previousNode = parentNode;
                    parentNode = parentNode.Parent;
                }

                // we only need to process the literals IF we are actually going to do
                // anything with them (combine duplicates). So if we aren't, don't call
                // AddLiteral because it hugely inflates the processing time of the application.
                if (m_parser.Settings.CombineDuplicateLiterals)
                {
                    // add this literal to the scope's literal collection. 
                    // HOWEVER, we do NOT want to add it for consideration of literal combination
                    // if any scope above us is a with-scope -- otherwise the 
                    // variable we use to combine the literals might be confused with a
                    // property on the with-object. 
                    // AND we don't want to do it if the scope is unknown, for the same reason.
                    // we won't really know if the variable we create will interfere with the 
                    // scope resolution of any variables that me in the eval string.
                    ActivationObject thisScope = ScopeStack.Peek();
                    if (thisScope.IsKnownAtCompileTime && !thisScope.IsInWithScope)
                    {
                        thisScope.AddLiteral(node, thisScope);
                    }
                }

                // this node has no children, so don't bother calling the base
                //base.Visit(node);
            }
        }

        public override void Visit(ContinueNode node)
        {
            if (node != null)
            {
                if (node.Label != null)
                {
                    // if the nest level is zero, then we might be able to remove the label altogether
                    // IF local renaming is not KeepAll AND the kill switch for removing them isn't set.
                    // the nest level will be zero if the label is undefined.
                    if (node.NestLevel == 0
                        && m_parser.Settings.LocalRenaming != LocalRenaming.KeepAll
                        && m_parser.Settings.IsModificationAllowed(TreeModifications.RemoveUnnecessaryLabels))
                    {
                        node.Label = null;
                    }
                }

                // don't need to call the base; this statement has no children to recurse
                //base.Visit(node);
            }
        }

        public override void Visit(DoWhile node)
        {
            if (node != null)
            {
                // if we are stripping debugger statements and the body is
                // just a debugger statement, replace it with a null
                if (m_parser.Settings.StripDebugStatements
                     && m_parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements)
                     && node.Body != null
                     && node.Body.IsDebuggerStatement)
                {
                    node.ReplaceChild(node.Body, null);
                }

                // recurse
                base.Visit(node);

                // if the body is now empty, make it null
                if (node.Body != null && node.Body.Count == 0)
                {
                    node.ReplaceChild(node.Body, null);
                }
            }
        }

        public override void Visit(EvaluateNode node)
        {
            // if the developer hasn't explicitly flagged eval statements as safe...
            if (m_parser.Settings.EvalTreatment != EvalTreatment.Ignore)
            {
                // mark this scope as unknown so we don't
                // crunch out locals we might reference in the eval at runtime
                ActivationObject enclosingScope = ScopeStack.Peek();
                if (enclosingScope != null)
                {
                    enclosingScope.IsKnownAtCompileTime = false;
                }
            }

            // then just do the default analysis
            base.Visit(node);
        }

        public override void Visit(ForNode node)
        {
            if (node != null)
            {
                // if we are stripping debugger statements and the body is
                // just a debugger statement, replace it with a null
                if (m_parser.Settings.StripDebugStatements
                     && m_parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements)
                     && node.Body != null
                     && node.Body.IsDebuggerStatement)
                {
                    node.ReplaceChild(node.Body, null);
                }

                // recurse
                base.Visit(node);

                // if the body is now empty, make it null
                if (node.Body != null && node.Body.Count == 0)
                {
                    node.ReplaceChild(node.Body, null);
                }
            }
        }

        public override void Visit(ForIn node)
        {
            if (node != null)
            {
                // if we are stripping debugger statements and the body is
                // just a debugger statement, replace it with a null
                if (m_parser.Settings.StripDebugStatements
                     && m_parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements)
                     && node.Body != null
                     && node.Body.IsDebuggerStatement)
                {
                    node.ReplaceChild(node.Body, null);
                }

                // recurse
                base.Visit(node);

                // if the body is now empty, make it null
                if (node.Body != null && node.Body.Count == 0)
                {
                    node.ReplaceChild(node.Body, null);
                }
            }
        }

        public override void Visit(FunctionObject node)
        {
            if (node != null)
            {
                // get the name of this function, calculate something if it's anonymous
                if (node.Identifier == null)
                {
                    node.Name = GuessAtName(node);
                }

                // don't analyze the identifier or we'll add an extra reference to it.
                // and we don't need to analyze the parameters because they were fielded-up
                // back when the function object was created, too

                // push the stack and analyze the body
                ScopeStack.Push(node.FunctionScope);
                try
                {
                    // recurse
                    base.Visit(node);
                }
                finally
                {
                    ScopeStack.Pop();
                }
            }
        }

        public override void Visit(IfNode node)
        {
            if (node != null)
            {
                if (m_parser.Settings.StripDebugStatements
                     && m_parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements))
                {
                    if (node.TrueBlock != null && node.TrueBlock.IsDebuggerStatement)
                    {
                        node.ReplaceChild(node.TrueBlock, null);
                    }

                    if (node.FalseBlock != null && node.FalseBlock.IsDebuggerStatement)
                    {
                        node.ReplaceChild(node.FalseBlock, null);
                    }
                }

                // recurse....
                base.Visit(node);

                // now check to see if the two branches are now empty.
                // if they are, null them out.
                if (node.TrueBlock != null && node.TrueBlock.Count == 0)
                {
                    node.ReplaceChild(node.TrueBlock, null);
                }
                if (node.FalseBlock != null && node.FalseBlock.Count == 0)
                {
                    node.ReplaceChild(node.FalseBlock, null);
                }

                // if there is no true branch but a false branch, then
                // put a not on the condition and move the false branch to the true branch.
                if (node.TrueBlock == null && node.FalseBlock != null
                    && m_parser.Settings.IsModificationAllowed(TreeModifications.IfConditionFalseToIfNotConditionTrue))
                {
                    // check to see if not-ing the condition produces a quick and easy
                    // version first
                    AstNode nottedCondition = node.Condition.LogicalNot();
                    if (nottedCondition != null)
                    {
                        // it does -- use it
                        node.ReplaceChild(node.Condition, nottedCondition);
                    }
                    else
                    {
                        // it doesn't. Just wrap it.
                        node.ReplaceChild(node.Condition, new NumericUnary(
                          null,
                          m_parser,
                          node.Condition,
                          JSToken.LogicalNot));
                    }

                    // and swap the branches
                    node.SwapBranches();
                }
                else if (node.TrueBlock == null && node.FalseBlock == null
                    && m_parser.Settings.IsModificationAllowed(TreeModifications.IfEmptyToExpression))
                {
                    // NEITHER branches have anything now!

                    // something we can do in the future: as long as the condition doesn't
                    // contain calls or assignments, we should be able to completely delete
                    // the statement altogether rather than changing it to an expression
                    // statement on the condition.

                    // I'm just not doing it yet because I don't
                    // know what the effect will be on the iteration of block statements.
                    // if we're on item, 5, for instance, and we delete it, will the next
                    // item be item 6, or will it return the NEW item 5 (since the old item
                    // 5 was deleted and everything shifted up)?

                    // We don't know what it is and what the side-effects may be, so
                    // just change this statement into an expression statement by replacing us with 
                    // the expression
                    node.Parent.ReplaceChild(node, node.Condition);
                    // no need to analyze -- we already recursed
                }

                // if this statement is now of the pattern "if (condtion) callNode" then
                // we can further reduce it by changing it to "condition && callNode".
                if (node.TrueBlock != null && node.FalseBlock == null
                    && node.TrueBlock.Count == 1
                    && m_parser.Settings.IsModificationAllowed(TreeModifications.IfConditionCallToConditionAndCall))
                {
                    // BUT we don't want to replace the statement if the true branch is a
                    // call to an onXXXX method of an object. This is because of an IE bug
                    // that throws an error if you pass any parameter to onclick or onfocus or
                    // any of those event handlers directly from within an and-expression -- 
                    // although expression-statements seem to work just fine.
                    CallNode callNode = node.TrueBlock[0] as CallNode;
                    if (callNode != null)
                    {
                        Member callMember = callNode.Function as Member;
                        if (callMember == null
                            || !callMember.Name.StartsWith("on", StringComparison.Ordinal)
                            || callNode.Arguments.Count == 0)
                        {
                            // we're good -- go ahead and replace it
                            BinaryOperator binaryOp = new BinaryOperator(
                                node.Context,
                                m_parser,
                                node.Condition,
                                node.TrueBlock,
                                JSToken.LogicalAnd
                                );

                            // we don't need to analyse this new node because we've already analyzed
                            // the pieces parts as part of the if. And this visitor's method for the BinaryOperator
                            // doesn't really do anything else. Just replace our current node with this
                            // new node
                            node.Parent.ReplaceChild(node, binaryOp);
                        }
                    }
                }
            }
        }

        public override void Visit(Lookup node)
        {
            if (node != null)
            {
                // figure out if our reference type is a function or a constructor
                if (node.Parent is CallNode)
                {
                    node.RefType = (
                      ((CallNode)(node.Parent)).IsConstructor
                      ? ReferenceType.Constructor
                      : ReferenceType.Function
                      );
                }

                ActivationObject scope = ScopeStack.Peek();
                node.VariableField = scope.FindReference(node.Name);
                if (node.VariableField == null)
                {
                    // this must be a global. if it isn't in the global space, throw an error
                    // this name is not in the global space.
                    // if it isn't generated, then we want to throw an error
                    // we also don't want to report an undefined variable if it is the object
                    // of a typeof operator
                    if (!node.IsGenerated && !(node.Parent is TypeOfNode))
                    {
                        // report this undefined reference
                        node.Context.ReportUndefined(node);

                        // possibly undefined global (but definitely not local)
                        node.Context.HandleError(
                          (node.Parent is CallNode && ((CallNode)(node.Parent)).Function == node ? JSError.UndeclaredFunction : JSError.UndeclaredVariable),
                          null,
                          false
                          );
                    }

                    if (!(scope is GlobalScope))
                    {
                        // add it to the scope so we know this scope references the global
                        scope.AddField(new JSGlobalField(
                          node.Name,
                          Missing.Value,
                          0
                          ));
                    }
                }
                else
                {
                    // BUT if this field is a place-holder in the containing scope of a named
                    // function expression, then we need to throw an ambiguous named function expression
                    // error because this could cause problems.
                    // OR if the field is already marked as ambiguous, throw the error
                    if (node.VariableField.NamedFunctionExpression != null
                        || node.VariableField.IsAmbiguous)
                    {
                        // mark it as a field that's referenced ambiguously
                        node.VariableField.IsAmbiguous = true;
                        // throw as an error
                        node.Context.HandleError(JSError.AmbiguousNamedFunctionExpression, true);

                        // if we are preserving function names, then we need to mark this field
                        // as not crunchable
                        if (m_parser.Settings.PreserveFunctionNames)
                        {
                            node.VariableField.CanCrunch = false;
                        }
                    }

                    // see if this scope already points to this name
                    if (scope[node.Name] == null)
                    {
                        // create an inner reference so we don't keep walking up the scope chain for this name
                        node.VariableField = scope.CreateInnerField(node.VariableField);
                    }

                    // add the reference
                    node.VariableField.AddReference(scope);

                    if (node.VariableField is JSPredefinedField)
                    {
                        // this is a predefined field. If it's Nan or Infinity, we should
                        // replace it with the numeric value in case we need to later combine
                        // some literal expressions.
                        if (string.CompareOrdinal(node.Name, "NaN") == 0)
                        {
                            // don't analyze the new ConstantWrapper -- we don't want it to take part in the
                            // duplicate constant combination logic should it be turned on.
                            node.Parent.ReplaceChild(node, new ConstantWrapper(double.NaN, PrimitiveType.Number, node.Context, m_parser));
                        }
                        else if (string.CompareOrdinal(node.Name, "Infinity") == 0)
                        {
                            // don't analyze the new ConstantWrapper -- we don't want it to take part in the
                            // duplicate constant combination logic should it be turned on.
                            node.Parent.ReplaceChild(node, new ConstantWrapper(double.PositiveInfinity, PrimitiveType.Number, node.Context, m_parser));
                        }
                    }
                }
            }
        }

        public override void Visit(Member node)
        {
            if (node != null)
            {
                // if we don't even have any resource strings, then there's nothing
                // we need to do and we can just perform the base operation
                ResourceStrings resourceStrings = m_parser.ResourceStrings;
                if (resourceStrings != null && resourceStrings.Count > 0)
                {
                    // see if the root object is a lookup that corresponds to the 
                    // global value (not a local field) for our resource object
                    // (same name)
                    Lookup rootLookup = node.Root as Lookup;
                    if (rootLookup != null
                        && rootLookup.LocalField == null
                        && string.CompareOrdinal(rootLookup.Name, resourceStrings.Name) == 0)
                    {
                        // it is -- we're going to replace this with a string value.
                        // if this member name is a string on the object, we'll replacve it with
                        // the literal. Otherwise we'll replace it with an empty string.
                        // see if the string resource contains this value
                        ConstantWrapper stringLiteral = new ConstantWrapper(
                            resourceStrings[node.Name],
                            PrimitiveType.String,
                            node.Context,
                            m_parser
                            );

                        node.Parent.ReplaceChild(node, stringLiteral);

                        // analyze the literal
                        stringLiteral.Accept(this);
                        return;
                    }
                }

                // if we are replacing property names and we have something to replace
                if (m_parser.Settings.HasRenamePairs && m_parser.Settings.ManualRenamesProperties
                    && m_parser.Settings.IsModificationAllowed(TreeModifications.PropertyRenaming))
                {
                    // see if this name is a target for replacement
                    string newName = m_parser.Settings.GetNewName(node.Name);
                    if (!string.IsNullOrEmpty(newName))
                    {
                        // it is -- set the name to the new name
                        node.Name = newName;
                    }
                }

                // recurse
                base.Visit(node);
            }
        }

        public override void Visit(NumericUnary node)
        {
            if (node != null)
            {
                // recurse first, then check to see if the unary is still needed
                base.Visit(node);

                // if the operand is a numeric literal
                ConstantWrapper constantWrapper = node.Operand as ConstantWrapper;
                if (constantWrapper != null
                    && constantWrapper.IsNumericLiteral)
                {
                    // get the value of the constant. We've already screened it for numeric, so
                    // we don't have to worry about catching any errors
                    double doubleValue = constantWrapper.ToNumber();

                    // if this is a unary minus...
                    if (node.OperatorToken == JSToken.Minus
                        && m_parser.Settings.IsModificationAllowed(TreeModifications.ApplyUnaryMinusToNumericLiteral))
                    {
                        // negate the value
                        constantWrapper.Value = -doubleValue;

                        // replace us with the negated constant
                        if (node.Parent.ReplaceChild(node, constantWrapper))
                        {
                            // the context for the minus will include the number (its operand),
                            // but the constant will just be the number. Update the context on
                            // the constant to be a copy of the context on the operator
                            constantWrapper.Context = node.Context.Clone();
                        }
                    }
                    else if (node.OperatorToken == JSToken.Plus
                        && m_parser.Settings.IsModificationAllowed(TreeModifications.RemoveUnaryPlusOnNumericLiteral))
                    {
                        // +NEG is still negative, +POS is still positive, and +0 is still 0.
                        // so just get rid of the unary operator altogether
                        if (node.Parent.ReplaceChild(node, constantWrapper))
                        {
                            // the context for the unary will include the number (its operand),
                            // but the constant will just be the number. Update the context on
                            // the constant to be a copy of the context on the operator
                            constantWrapper.Context = node.Context.Clone();
                        }
                    }
                }
            }
        }

        public override void Visit(ObjectLiteralField node)
        {
            if (node != null)
            {
                if (node.PrimitiveType == PrimitiveType.String
                    && m_parser.Settings.HasRenamePairs && m_parser.Settings.ManualRenamesProperties
                    && m_parser.Settings.IsModificationAllowed(TreeModifications.PropertyRenaming))
                {
                    string newName = m_parser.Settings.GetNewName(node.Value.ToString());
                    if (!string.IsNullOrEmpty(newName))
                    {
                        node.Value = newName;
                    }
                }

                // don't call the base -- we don't want to add the literal to
                // the combination logic, which is what the ConstantWrapper (base class) does
                //base.Visit(node);
            }
        }

        public override void Visit(RegExpLiteral node)
        {
            if (node != null)
            {
                // verify the syntax
                try
                {
                    // just try instantiating a Regex object with this string.
                    // if it's invalid, it will throw an exception.
                    // we don't need to pass the flags -- we're just interested in the pattern
                    Regex re = new Regex(node.Pattern, RegexOptions.ECMAScript);

                    // basically we have this test here so the re variable is referenced
                    // and FxCop won't throw an error. There really aren't any cases where
                    // the constructor will return null (other than out-of-memory)
                    if (re == null)
                    {
                        node.Context.HandleError(JSError.RegExpSyntax, true);
                    }
                }
                catch (System.ArgumentException e)
                {
                    System.Diagnostics.Debug.WriteLine(e.ToString());
                    node.Context.HandleError(JSError.RegExpSyntax, true);
                }
                // don't bother calling the base -- there are no children
            }
        }

        public override void Visit(ReturnNode node)
        {
            if (node != null)
            {
                // first we want to make sure that we are indeed within a function scope.
                // it makes no sense to have a return outside of a function
                ActivationObject scope = ScopeStack.Peek();
                while (scope != null && !(scope is FunctionScope))
                {
                    scope = scope.Parent;
                }

                if (scope == null)
                {
                    node.Context.HandleError(JSError.BadReturn);
                }

                // now just do the default analyze
                base.Visit(node);
            }
        }

        public override void Visit(Switch node)
        {
            if (node != null)
            {
                base.Visit(node);

                // we only want to remove stuff if we are hypercrunching
                if (m_parser.Settings.RemoveUnneededCode)
                {
                    // because we are looking at breaks, we need to know if this
                    // switch statement is labeled
                    string thisLabel = string.Empty;
                    LabeledStatement label = node.Parent as LabeledStatement;
                    if (label != null)
                    {
                        thisLabel = label.Label;
                    }

                    // loop through all the cases, looking for the default.
                    // then, if it's empty (or just doesn't do anything), we can
                    // get rid of it altogether
                    int defaultCase = -1;
                    bool eliminateDefault = false;
                    for (int ndx = 0; ndx < node.Cases.Count; ++ndx)
                    {
                        // it should always be a switch case, but just in case...
                        SwitchCase switchCase = node.Cases[ndx] as SwitchCase;
                        if (switchCase != null)
                        {
                            if (switchCase.IsDefault)
                            {
                                // save the index for later
                                defaultCase = ndx;

                                // set the flag to true unless we can prove that we need it.
                                // we'll prove we need it by finding the statement block executed by
                                // this case and showing that it's neither empty nor containing
                                // just a single break statement.
                                eliminateDefault = true;
                            }

                            // if the default case is empty, then we need to keep going
                            // until we find the very next non-empty case
                            if (eliminateDefault && switchCase.Statements.Count > 0)
                            {
                                // this is the set of statements executed during default processing.
                                // if it does nothing -- one break statement -- then we can get rid
                                // of the default case. Otherwise we need to leave it in.
                                if (switchCase.Statements.Count == 1)
                                {
                                    // see if it's a break
                                    Break lastBreak = switchCase.Statements[0] as Break;

                                    // if the last statement is not a break,
                                    // OR it has a label and it's not this switch statement...
                                    if (lastBreak == null
                                      || (lastBreak.Label != null && lastBreak.Label != thisLabel))
                                    {
                                        // set the flag back to false to indicate that we need to keep it.
                                        eliminateDefault = false;
                                    }
                                }
                                else
                                {
                                    // set the flag back to false to indicate that we need to keep it.
                                    eliminateDefault = false;
                                }

                                // break out of the loop
                                break;
                            }
                        }
                    }

                    // if we get here and the flag is still true, then either the default case is
                    // empty, or it contains only a single break statement. Either way, we can get 
                    // rid of it.
                    if (eliminateDefault && defaultCase >= 0
                        && m_parser.Settings.IsModificationAllowed(TreeModifications.RemoveEmptyDefaultCase))
                    {
                        // remove it and reset the position index
                        node.Cases.RemoveAt(defaultCase);
                        defaultCase = -1;
                    }

                    // if we have no default handling, then we know we can get rid
                    // of any cases that don't do anything either.
                    if (defaultCase == -1
                        && m_parser.Settings.IsModificationAllowed(TreeModifications.RemoveEmptyCaseWhenNoDefault))
                    {
                        // when we delete a case statement, we set this flag to true.
                        // when we hit a non-empty case statement, we set the flag to false.
                        // if we hit an empty case statement when this flag is true, we can delete this case, too.
                        bool emptyStatements = true;
                        Break deletedBreak = null;

                        // walk the tree backwards because we don't know how many we will
                        // be deleting, and if we go backwards, we won't have to adjust the 
                        // index as we go.
                        for (int ndx = node.Cases.Count - 1; ndx >= 0; --ndx)
                        {
                            // should always be a switch case
                            SwitchCase switchCase = node.Cases[ndx] as SwitchCase;
                            if (switchCase != null)
                            {
                                // if the block is empty and the last block was empty, we can delete this case.
                                // OR if there is only one statement and it's a break, we can delete it, too.
                                if (switchCase.Statements.Count == 0 && emptyStatements)
                                {
                                    // remove this case statement because it falls through to a deleted case
                                    node.Cases.RemoveAt(ndx);
                                }
                                else
                                {
                                    // onlyBreak will be set to null if this block is not a single-statement break block
                                    Break onlyBreak = (switchCase.Statements.Count == 1 ? switchCase.Statements[0] as Break : null);
                                    if (onlyBreak != null)
                                    {
                                        // we'll only delete this case if the break either doesn't have a label
                                        // OR the label matches the switch statement
                                        if (onlyBreak.Label == null || onlyBreak.Label == thisLabel)
                                        {
                                            // if this is a block with only a break, then we need to keep a hold of the break
                                            // statement in case we need it later
                                            deletedBreak = onlyBreak;

                                            // remove this case statement
                                            node.Cases.RemoveAt(ndx);
                                            // make sure the flag is set so we delete any other empty
                                            // cases that fell through to this empty case block
                                            emptyStatements = true;
                                        }
                                        else
                                        {
                                            // the break statement has a label and it's not the switch statement.
                                            // we're going to keep this block
                                            emptyStatements = false;
                                            deletedBreak = null;
                                        }
                                    }
                                    else
                                    {
                                        // either this is a non-empty block, or it's an empty case that falls through
                                        // to a non-empty block. if we have been deleting case statements and this
                                        // is not an empty block....
                                        if (emptyStatements && switchCase.Statements.Count > 0 && deletedBreak != null)
                                        {
                                            // we'll need to append the deleted break statement if it doesn't already have
                                            // a flow-changing statement: break, continue, return, or throw
                                            AstNode lastStatement = switchCase.Statements[switchCase.Statements.Count - 1];
                                            if (!(lastStatement is Break) && !(lastStatement is ContinueNode)
                                              && !(lastStatement is ReturnNode) && !(lastStatement is ThrowNode))
                                            {
                                                switchCase.Statements.Append(deletedBreak);
                                            }
                                        }

                                        // make sure the deletedBreak flag is reset
                                        deletedBreak = null;

                                        // reset the flag
                                        emptyStatements = false;
                                    }
                                }
                            }
                        }
                    }

                    // if the last case's statement list ends in a break, 
                    // we can get rid of the break statement
                    if (node.Cases.Count > 0
                        && m_parser.Settings.IsModificationAllowed(TreeModifications.RemoveBreakFromLastCaseBlock))
                    {
                        SwitchCase lastCase = node.Cases[node.Cases.Count - 1] as SwitchCase;
                        if (lastCase != null)
                        {
                            // get the block of statements making up the last case block
                            Block lastBlock = lastCase.Statements;
                            // if the last statement is not a break, then lastBreak will be null
                            Break lastBreak = (lastBlock.Count > 0 ? lastBlock[lastBlock.Count - 1] as Break : null);
                            // if lastBreak is not null and it either has no label, or the label matches this switch statement...
                            if (lastBreak != null
                              && (lastBreak.Label == null || lastBreak.Label == thisLabel))
                            {
                                // remove the break statement
                                lastBlock.RemoveLast();
                            }
                        }
                    }
                }
            }
        }

        public override void Visit(ThisLiteral node)
        {
            // we're going to look for the first FunctionScope on the stack
            FunctionScope functionScope = null;

            // get the current scope
            ActivationObject activationObject = ScopeStack.Peek();
            do
            {
                functionScope = activationObject as FunctionScope;
                if (functionScope != null)
                {
                    // found it -- break out of the loop
                    break;
                }
                // otherwise go up the chain
                activationObject = activationObject.Parent;
            } while (activationObject != null);

            // if we found one....
            if (functionScope != null)
            {
                // add this object to the list of thisliterals
                functionScope.AddThisLiteral(node);
            }
        }

        public override void Visit(TryNode node)
        {
            if (node != null)
            {
                // get the field -- it should have been generated when the scope was analyzed
                if (node.CatchBlock != null && !string.IsNullOrEmpty(node.CatchVarName))
                {
                    node.SetCatchVariable(node.CatchBlock.BlockScope[node.CatchVarName]);
                }

                // anaylze the blocks
                base.Visit(node);

                // if the try block is empty, then set it to null
                if (node.TryBlock != null && node.TryBlock.Count == 0)
                {
                    node.ReplaceChild(node.TryBlock, null);
                }

                // eliminate an empty finally block UNLESS there is no catch block.
                if (node.FinallyBlock != null && node.FinallyBlock.Count == 0 && node.CatchBlock != null
                    && m_parser.Settings.IsModificationAllowed(TreeModifications.RemoveEmptyFinally))
                {
                    node.ReplaceChild(node.FinallyBlock, null);
                }
            }
        }

        public override void Visit(Var node)
        {
            if (node != null)
            {
                // first we want to weed out duplicates that don't have initializers
                // var a=1, a=2 is okay, but var a, a=2 and var a=2, a should both be just var a=2, 
                // and var a, a should just be var a
                if (m_parser.Settings.IsModificationAllowed(TreeModifications.RemoveDuplicateVar))
                {
                    // first we want to weed out duplicates that don't have initializers
                    // var a=1, a=2 is okay, but var a, a=2 and var a=2, a should both be just var a=2, 
                    // and var a, a should just be var a
                    int ndx = 0;
                    while (ndx < node.Count)
                    {
                        string thisName = node[ndx].Identifier;

                        // handle differently if we have an initializer or not
                        if (node[ndx].Initializer != null)
                        {
                            // the current vardecl has an initializer, so we want to delete any other
                            // vardecls of the same name in the rest of the list with no initializer
                            // and move on to the next item afterwards
                            DeleteNoInits(node, ++ndx, thisName);
                        }
                        else
                        {
                            // this vardecl has no initializer, so we can delete it if there is ANY
                            // other vardecl with the same name (whether or not it has an initializer)
                            if (VarDeclExists(node, ndx + 1, thisName))
                            {
                                node.RemoveAt(ndx);

                                // don't increment the index; we just deleted the current item,
                                // so the next item just slid into this position
                            }
                            else
                            {
                                // nope -- it's the only one. Move on to the next
                                ++ndx;
                            }
                        }
                    }
                }

                // recurse the analyze
                base.Visit(node);
            }
        }

        public override void Visit(VariableDeclaration node)
        {
            if (node != null)
            {
                base.Visit(node);

                if (node.IsCCSpecialCase && m_parser.Settings.IsModificationAllowed(TreeModifications.RemoveUnnecessaryCCOnStatements))
                {
                    node.UseCCOn = !m_encounteredCCOn;
                    m_encounteredCCOn = true;
                }
            }
        }

        public override void Visit(WhileNode node)
        {
            if (node != null)
            {
                if (m_parser.Settings.StripDebugStatements
                     && m_parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements)
                     && node.Body != null
                     && node.Body.IsDebuggerStatement)
                {
                    node.ReplaceChild(node.Body, null);
                }

                // recurse
                base.Visit(node);

                // if the body is now empty, make it null
                if (node.Body != null && node.Body.Count == 0)
                {
                    node.ReplaceChild(node.Body, null);
                }
            }
        }

        public override void Visit(WithNode node)
        {
            if (node != null)
            {
                // throw a warning discouraging the use of this statement
                node.Context.HandleError(JSError.WithNotRecommended, false);

                // hold onto the with-scope in case we need to do something with it
                BlockScope withScope = (node.Body == null ? null : node.Body.BlockScope);

                if (m_parser.Settings.StripDebugStatements
                     && m_parser.Settings.IsModificationAllowed(TreeModifications.StripDebugStatements)
                     && node.Body != null
                     && node.Body.IsDebuggerStatement)
                {
                    node.ReplaceChild(node.Body, null);
                }

                // recurse
                base.Visit(node);

                // we'd have to know what the object (obj) evaluates to before we
                // can figure out what to add to the scope -- not possible without actually
                // running the code. This could throw a whole bunch of 'undefined' errors.
                if (node.Body != null && node.Body.Count == 0)
                {
                    node.ReplaceChild(node.Body, null);
                }

                // we got rid of the block -- tidy up the no-longer-needed scope
                if (node.Body == null && withScope != null)
                {
                    // because the scope is empty, we now know it (it does nothing)
                    withScope.IsKnownAtCompileTime = true;
                }
            }
        }

        private string GuessAtName(AstNode node)
        {
            var parent = node.Parent;
            if (parent != null)
            {
                if (parent is AstNodeList)
                {
                    // if the parent is an ASTList, then we're really interested
                    // in our parent's parent (probably a call)
                    parent = parent.Parent;
                }
                CallNode call = parent as CallNode;
                if (call != null && call.IsConstructor)
                {
                    // if this function expression is the object of a new, then we want the parent
                    parent = parent.Parent;
                }

                string guess = parent.GetFunctionGuess(node);
                if (guess != null && guess.Length > 0)
                {
                    if (guess.StartsWith("\"", StringComparison.Ordinal)
                      && guess.EndsWith("\"", StringComparison.Ordinal))
                    {
                        // don't need to wrap it in quotes -- it already is
                        return guess;
                    }
                    // wrap the guessed name in quotes
                    return string.Format(CultureInfo.InvariantCulture, "\"{0}\"", guess);
                }
                else
                {
                    return string.Format(CultureInfo.InvariantCulture, "anonymous_{0}", UniqueNumber);
                }
            }
            return string.Empty;
        }

        // unnest any child blocks
        private void UnnestBlocks(Block node)
        {
            // walk the list of items backwards -- if we come
            // to any blocks, unnest the block recursively. We walk backwards because
            // we could be adding any number of statements and we don't want to have
            // to modify the counter
            for (int ndx = node.Count - 1; ndx >= 0; --ndx)
            {
                Block nestedBlock = node[ndx] as Block;
                if (nestedBlock != null)
                {
                    // unnest recursively
                    UnnestBlocks(nestedBlock);

                    // remove the nested block
                    node.RemoveAt(ndx);

                    // then start adding the statements in the nested block to our own.
                    // go backwards so we can just keep using the same index
                    node.InsertRange(ndx, nestedBlock.Children);
                }
            }
        }

        private static void StripDebugStatements(Block node)
        {
            // walk the list backwards
            for (int ndx = node.Count - 1; ndx >= 0; --ndx)
            {
                // if this item pops positive...
                if (node[ndx].IsDebuggerStatement)
                {
                    // just remove it
                    node.RemoveAt(ndx);
                }
            }
        }

        private static bool VarDeclExists(Var node, int ndx, string name)
        {
            // only need to look forward from the index passed
            for (; ndx < node.Count; ++ndx)
            {
                // string must be exact match
                if (string.CompareOrdinal(node[ndx].Identifier, name) == 0)
                {
                    // there is at least one -- we can bail
                    return true;
                }
            }
            // if we got here, we didn't find any matches
            return false;
        }

        private static void DeleteNoInits(Var node, int min, string name)
        {
            // walk backwards from the end of the list down to (and including) the minimum index
            for (int ndx = node.Count - 1; ndx >= min; --ndx)
            {
                // if the name matches and there is no initializer...
                if (string.CompareOrdinal(node[ndx].Identifier, name) == 0
                    && node[ndx].Initializer == null)
                {
                    // ...remove it from the list
                    node.RemoveAt(ndx);
                }
            }
        }
    }
}
