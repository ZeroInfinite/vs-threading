﻿/********************************************************
*                                                        *
*   © Copyright (C) Microsoft. All rights reserved.      *
*                                                        *
*********************************************************/

namespace Microsoft.VisualStudio.Threading.Analyzers
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    /// <summary>
    /// Report warnings when detect the code that is waiting on tasks or awaiters synchronously.
    /// </summary>
    /// <remarks>
    /// [Background] <see cref="Task.Wait()"/> or <see cref="Task{TResult}.Result"/> will often deadlock if
    /// they are called on main thread, because now it is synchronously blocking the main thread for the
    /// completion of a task that may need the main thread to complete. Even if they are called on a threadpool
    /// thread, it is occupying a threadpool thread to do nothing but block, which is not good either.
    ///
    /// i.e.
    ///   var task = Task.Run(DoSomethingOnBackground);
    ///   task.Wait();  /* This analyzer will report warning on this synchronous wait. */
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class VSTHRD002UseJtfRunAnalyzer : DiagnosticAnalyzer
    {
        public const string Id = "VSTHRD002";

        internal static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
            id: Id,
            title: Strings.VSTHRD002_Title,
            messageFormat: Strings.VSTHRD002_MessageFormat,
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(Descriptor);
            }
        }

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);

            context.RegisterCodeBlockStartAction<SyntaxKind>(ctxt =>
            {
                // We want to scan properties and methods that do not return Task or Task<T>.
                var methodSymbol = ctxt.OwningSymbol as IMethodSymbol;
                var propertySymbol = ctxt.OwningSymbol as IPropertySymbol;
                if (propertySymbol != null || (methodSymbol != null && !methodSymbol.HasAsyncCompatibleReturnType()))
                {
                    ctxt.RegisterSyntaxNodeAction(Utils.DebuggableWrapper(this.AnalyzeInvocation), SyntaxKind.InvocationExpression);
                    ctxt.RegisterSyntaxNodeAction(Utils.DebuggableWrapper(this.AnalyzeMemberAccess), SyntaxKind.SimpleMemberAccessExpression);
                }
            });
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocationExpressionSyntax = (InvocationExpressionSyntax)context.Node;
            CommonInterest.InspectMemberAccess(
                context,
                invocationExpressionSyntax.Expression as MemberAccessExpressionSyntax,
                Descriptor,
                CommonInterest.SyncBlockingMethods);

            //if (CommonInterest.ShouldIgnoreContext(context))
            //{
            //    return;
            //}

            //var node = (InvocationExpressionSyntax)context.Node;
            //var invokeMethod = context.SemanticModel.GetSymbolInfo(context.Node).Symbol as IMethodSymbol;
            //if (invokeMethod != null)
            //{
            //    var taskType = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(Task).FullName);
            //    if (string.Equals(invokeMethod.Name, nameof(Task.Wait), StringComparison.Ordinal)
            //        && Utils.IsEqualToOrDerivedFrom(invokeMethod.ContainingType, taskType))
            //    {
            //        context.ReportDiagnostic(Diagnostic.Create(Descriptor, context.Node.GetLocation()));
            //    }
            //    else if (string.Equals(invokeMethod.Name, "GetResult", StringComparison.Ordinal)
            //        && invokeMethod.ContainingType.Name.EndsWith("Awaiter", StringComparison.Ordinal))
            //    {
            //        context.ReportDiagnostic(Diagnostic.Create(Descriptor, context.Node.GetLocation()));
            //    }
            //}
        }

        private void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
        {
            var memberAccessSyntax = (MemberAccessExpressionSyntax)context.Node;
            CommonInterest.InspectMemberAccess(
                context,
                memberAccessSyntax,
                Descriptor,
                CommonInterest.SyncBlockingProperties);

            //if (CommonInterest.ShouldIgnoreContext(context))
            //{
            //    return;
            //}

            //if (Utils.IsWithinNameOf(context.Node as ExpressionSyntax))
            //{
            //    // We do not consider arguments to nameof( ) because they do not represent invocations of code.
            //    return;
            //}

            //var property = context.SemanticModel.GetSymbolInfo(context.Node).Symbol as IPropertySymbol;
            //if (property != null)
            //{
            //    var taskType = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(Task).FullName);
            //    if (string.Equals(property.Name, nameof(Task<object>.Result), StringComparison.Ordinal)
            //        && Utils.IsEqualToOrDerivedFrom(property.ContainingType, taskType))
            //    {
            //        context.ReportDiagnostic(Diagnostic.Create(Descriptor, context.Node.GetLocation()));
            //    }
            //}
        }
    }
}