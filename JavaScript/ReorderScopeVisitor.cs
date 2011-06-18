// ReorderScopeVisitor.cs
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

using System.Collections.Generic;

namespace Microsoft.Ajax.Utilities
{
    internal class ReorderScopeVisitor : TreeVisitor
    {
        // list of all function declarations found in this scope
        private List<FunctionObject> m_functionDeclarations;

        // list of all other functions found in this scope
        private List<FunctionObject> m_functionExpressions;

        // list of all var statements found in this scope
        private List<Var> m_varStatements;

        // whether we want to move var statements
        private bool m_moveVarStatements;

        // whether we want to move function declarations
        private bool m_moveFunctionDecls;

        // whether we want to combine adjacent var statements
        private bool m_combineAdjacentVars;

        private ReorderScopeVisitor(JSParser parser)
        {
            // save the mods we care about
            m_moveVarStatements = parser.Settings.ReorderScopeDeclarations && parser.Settings.IsModificationAllowed(TreeModifications.CombineVarStatementsToTopOfScope);
            m_moveFunctionDecls = parser.Settings.ReorderScopeDeclarations && parser.Settings.IsModificationAllowed(TreeModifications.MoveFunctionToTopOfScope);
            m_combineAdjacentVars = parser.Settings.IsModificationAllowed(TreeModifications.CombineVarStatements);
        }

        public static void Apply(Block block, JSParser parser)
        {
            // create a new instance of the visitor and apply it to the block
            var visitor = new ReorderScopeVisitor(parser);
            block.Accept(visitor);

            // get the first insertion point. Make sure that we skip over any comments and directive prologues.
            // we do NOT want to insert anything between the start of the scope and any directive prologues.
            int insertAt = 0;
            while (insertAt < block.Count 
                && (block[insertAt].IsDirectivePrologue || block[insertAt] is ImportantComment))
            {
                ++insertAt;
            }

            // first, we want to move all function declarations to the top of this block
            if (visitor.m_functionDeclarations != null)
            {
                foreach (var funcDecl in visitor.m_functionDeclarations)
                {
                    insertAt = RelocateFunction(block, insertAt, funcDecl);
                }
            }

            // special case: if there is only one var statement in the entire scope,
            // then just leave it alone because we will only add bytes by moving it around,
            // or be byte-neutral at best (no initializers and not in a for-statement).
            if (visitor.m_varStatements != null && visitor.m_varStatements.Count > 1)
            {
                // then we want to move all variable declarations after to the top (after the functions)
                foreach (var varStatement in visitor.m_varStatements)
                {
                    insertAt = RelocateVar(block, insertAt, varStatement);
                }
            }           

            // then we want to do the same thing for all child functions (declarations AND other)
            if (visitor.m_functionDeclarations != null)
            {
                foreach (var funcDecl in visitor.m_functionDeclarations)
                {
                    Apply(funcDecl.Body, parser);
                }
            }

            if (visitor.m_functionExpressions != null)
            {
                foreach (var funcExpr in visitor.m_functionExpressions)
                {
                    Apply(funcExpr.Body, parser);
                }
            }
        }

        private static bool InsideConditionalComment(AstNode node)
        {
            // walk up the parent chain until we either hit the root or it's a conditional comment
            while (node != null && !(node is ConditionalCompilationComment))
            {
                node = node.Parent;
            }
            return node != null;
        }

        private static int RelocateFunction(Block block, int insertAt, FunctionObject funcDecl)
        {
            if (block[insertAt] != funcDecl)
            {
                // technically function declarations can only be direct children of the program or a function block.
                // and since we are passing in such a block, the parent of the function declaration better be that
                // block. If it isn't, we don't want to move it because it's not in an allowed place, and different
                // browsers treat that situation differently. Some browsers would process such funcdecls as if
                // they were a direct child of the main block. Others will treat it like a function expression with
                // an external name, and only assign the function to the name if that line of code is actually
                // executed. So since there's a difference, just leave them as-is and only move valid funcdecls.
                if (funcDecl.Parent == block && !InsideConditionalComment(funcDecl))
                {
                    // remove the function from it's parent, which will take it away from where it is right now.
                    funcDecl.Parent.ReplaceChild(funcDecl, null);

                    // now insert it into the block at the new location, incrementing the location so the next function
                    // will be inserted after it. It is important that they be in the same order as the source, or the semantics
                    // will change when there are functions with the same name.
                    block.Insert(insertAt++, funcDecl);
                }
            }
            else
            {
                // we're already in the right place. Just increment the pointer to move to the next position
                // for next time
                ++insertAt;
            }

            // return the new position
            return insertAt;
        }

        private static int RelocateVar(Block block, int insertAt, Var varStatement)
        {
            // if the var statement is at the next position to insert, then we don't need
            // to do anything.
            if (block[insertAt] != varStatement && !InsideConditionalComment(varStatement))
            {
                // check to see if the current position is a var and we are the NEXT statement.
                // if that's the case, we don't need to break out the initializer, just append all the
                // vardecls as-is to the current position.
                var existingVar = block[insertAt] as Var;
                if (existingVar != null && block[insertAt + 1] == varStatement)
                {
                    // just append our vardecls to the insertion point, then delete our statement
                    existingVar.Append(varStatement);
                    block.RemoveAt(insertAt + 1);
                }
                else
                {
                    // iterate through the decls and count how many have initializers
                    var initializerCount = 0;
                    for (var ndx = 0; ndx < varStatement.Count; ++ndx)
                    {
                        if (varStatement[ndx].Initializer != null)
                        {
                            ++initializerCount;
                        }
                    }

                    // if there are more than two decls with initializers, then we won't actually
                    // be gaining anything by moving the var to the top. We'll get rid of the four
                    // bytes for the "var ", but we'll be adding two bytes for the name and comma
                    // because name=init will still need to remain behind.
                    if (initializerCount <= 2)
                    {
                        // first iterate through all the declarations in the var statement,
                        // constructing an expression statement that is made up of assignment
                        // operators for each of the declarations that have initializers (if any)
                        // and removing all the initializers
                        var assignments = new List<AstNode>();
                        for (var ndx = 0; ndx < varStatement.Count; ++ndx)
                        {
                            var varDecl = varStatement[ndx];
                            if (varDecl.Initializer != null)
                            {
                                if (varDecl.IsCCSpecialCase)
                                {
                                    // create a vardecl with the same name and no initializer
                                    var copyDecl = new VariableDeclaration(
                                        varDecl.Context,
                                        varDecl.Parser,
                                        varDecl.Identifier,
                                        varDecl.Field.OriginalContext,
                                        null,
                                        0,
                                        true);

                                    // replace the special vardecl with the copy
                                    varStatement.ReplaceChild(varDecl, copyDecl);

                                    // add the original vardecl to the list of "assignments"
                                    assignments.Add(varDecl);
                                }
                                else
                                {
                                    // hold on to the object so we don't lose it to the GC
                                    var initializer = varDecl.Initializer;

                                    // remove it from the vardecl
                                    varDecl.ReplaceChild(initializer, null);

                                    // create an assignment operator for a lookup to the name
                                    // as the left, and the initializer as the right, and add it to the list
                                    assignments.Add(new BinaryOperator(
                                        varDecl.Context,
                                        varDecl.Parser,
                                        new Lookup(varDecl.Identifier, varDecl.Field.OriginalContext, varDecl.Parser),
                                        initializer,
                                        JSToken.Assign));
                                }
                            }
                        }

                        // now if there were any initializers...
                        if (assignments.Count > 0)
                        {
                            // we want to create one big expression from all the assignments and replace the
                            // var statement with the assignment(s) expression. Start at position n=1 and create
                            // a binary operator of n-1 as the left, n as the right, and using a comma operator.
                            var expression = assignments[0];
                            for (var ndx = 1; ndx < assignments.Count; ++ndx)
                            {
                                expression = new BinaryOperator(
                                    null,
                                    expression.Parser,
                                    expression,
                                    assignments[ndx],
                                    JSToken.Comma);
                            }

                            // replace the var with the expression.
                            // we still have a pointer to the var, so we can insert it back into the proper
                            // place next.
                            varStatement.Parent.ReplaceChild(varStatement, expression);
                        }
                        else
                        {
                            // no initializers.
                            // if the parent is a for-in statement...
                            var forInParent = varStatement.Parent as ForIn;
                            if (forInParent != null)
                            {
                                // we want to replace the var statement with a lookup for the var
                                // there should be only one vardecl
                                var varDecl = varStatement[0];
                                varStatement.Parent.ReplaceChild(
                                    varStatement,
                                    new Lookup(varDecl.Identifier, varDecl.Field.OriginalContext, varStatement.Parser));
                            }
                            else
                            {
                                // just remove the var statement altogether
                                varStatement.Parent.ReplaceChild(varStatement, null);
                            }
                        }

                        // if the statement at the insertion point is a var-statement already,
                        // then we just need to append our vardecls to it. Otherwise we'll insert our
                        // var statement at the right point
                        if (existingVar != null)
                        {
                            // append the varstatement we want to move to the existing var, which will
                            // transfer all the vardecls to it.
                            existingVar.Append(varStatement);
                        }
                        else
                        {
                            // move the var to the insert point, incrementing the position or next time
                            block.Insert(insertAt, varStatement);
                        }
                    }
                }
            }

            return insertAt;
        }

        // unnest any child blocks
        private void UnnestBlocks(Block node)
        {
            // before we unnest the blocks or do anything else, let's identify any directive prologues.
            // if the parent is null (we are the program) or a function object...
            if (node.Parent == null || node.Parent is FunctionObject)
            {
                // directive prologues are the first n statements of a program or function body that
                // are expression statements consisting entirely of a string literal. So let's walk from
                // the beginning until we find the first statement that is not a ConstantWrapper of primitive
                // type string. Mark the ones we find as directive prologues
                for (var ndx = 0; ndx < node.Count; ++ndx)
                {
                    var constWrapper = node[ndx] as ConstantWrapper;
                    if (constWrapper == null || constWrapper.PrimitiveType != PrimitiveType.String)
                    {
                        // not a constant, not a string -- we are done searching!
                        break;
                    }

                    // otherwise it is a directive prologue. Mark it as such
                    constWrapper.IsDirectivePrologue = true;

                    // if it's a "use strict" prologue, let's mark the appropriate scope as strict
                    if (string.CompareOrdinal(constWrapper.ToString(), "use strict") == 0)
                    {
                        // the actual code cannot contain any escape sequences or line-contunations,
                        // so check the context code to make sure it ALSO is the right text. Be sure
                        // not to compare with the quote delimiters.
                        if (constWrapper.Context == null
                            || string.CompareOrdinal(constWrapper.Context.Code, 1, "use strict", 0, 10) == 0)
                        {
                            var funcObject = node.Parent as FunctionObject;
                            if (funcObject != null)
                            {
                                // function scope
                                funcObject.FunctionScope.UseStrict = true;
                            }
                            else
                            {
                                // global scope
                                node.Parser.GlobalScope.UseStrict = true;
                            }
                        }
                    }
                }
            }

            // walk the list of items backwards -- if we come
            // to any blocks, unnest the block recursively. We walk backwards because
            // we could be adding any number of statements and we don't want to have
            // to modify the counter
            for (int ndx = node.Count - 1; ndx >= 0; --ndx)
            {
                var nestedBlock = node[ndx] as Block;
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
                else if (ndx > 0)
                {
                    // see if the previous node is a conditional-compilation comment, because
                    // we will also combine adjacent those
                    var previousComment = node[ndx - 1] as ConditionalCompilationComment;
                    if (previousComment != null)
                    {
                        ConditionalCompilationComment thisComment = node[ndx] as ConditionalCompilationComment;
                        if (thisComment != null)
                        {
                            // two adjacent conditional comments -- combine them into the first.
                            previousComment.Statements.Append(thisComment.Statements);

                            // and remove the second one (which is now a duplicate)
                            node.RemoveAt(ndx);
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

                if (m_combineAdjacentVars)
                {
                    // look at the statements in the block. 
                    // if there are multiple var statements adjacent to each other, combine them.
                    // walk BACKWARDS down the list because we'll be removing items when we encounter
                    // multiple vars, etc.
                    // we also don't need to check the first one, since there is nothing before it.
                    for (int ndx = node.Count - 1; ndx > 0; --ndx)
                    {
                        // if the previous node is not a Var, then we don't need to try and combine
                        // it with the current node
                        var previousVar = node[ndx - 1] as Var;
                        if (previousVar != null && node[ndx] is Var)
                        {
                            // add the items in this VAR to the end of the previous
                            previousVar.Append(node[ndx]);

                            // delete this item from the block
                            node.RemoveAt(ndx);
                        }
                    }
                }

                // recurse down the tree after we've combined the adjacent var statements
                base.Visit(node);
            }
        }

        public override void Visit(FunctionObject node)
        {
            if (node != null)
            {
                // if we are reordering ANYTHING, then we need to do the reordering on a scope level.
                // so if that's the case, we need to create a list of all the child functions and NOT
                // recurse at this point. Then we'll reorder, then we'll use the lists to recurse.
                // BUT if we are not reordering anything, no sense making the lists and recursing later.
                // if that's the case, we can just recurse now and not have to worry about anything later.
                if (m_moveVarStatements || m_moveFunctionDecls)
                {
                    // add the node to the appropriate list: either function expression or function declaration.
                    // assume if it's not a function declaration, it must be an expression since the other types
                    // are not declaration (getter, setter) and we want to treat declarations special.
                    if (node.FunctionType == FunctionType.Declaration)
                    {
                        if (m_functionDeclarations == null)
                        {
                            m_functionDeclarations = new List<FunctionObject>();
                        }

                        m_functionDeclarations.Add(node);
                    }
                    else
                    {
                        if (m_functionExpressions == null)
                        {
                            m_functionExpressions = new List<FunctionObject>();
                        }

                        m_functionExpressions.Add(node);
                    }

                    // BUT DO NOT RECURSE!!!!
                    // we only want the functions and variables in THIS scope, not child function scopes.
                    //base.Visit(node);
                }
                else
                {
                    // we're not reordering, so just recurse now to save the hassle
                    base.Visit(node);
                }
            }
        }

        public override void Visit(Var node)
        {
            if (node != null)
            {
                // don't bother creating a list of var-statements if we're not going to move them
                if (m_moveVarStatements)
                {
                    if (m_varStatements == null)
                    {
                        m_varStatements = new List<Var>();
                    }

                    // add the node to the list of variable declarations
                    m_varStatements.Add(node);
                }

                // and recurse
                base.Visit(node);
            }
        }
    }
}
