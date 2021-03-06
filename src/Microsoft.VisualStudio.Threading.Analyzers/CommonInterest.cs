﻿namespace Microsoft.VisualStudio.Threading.Analyzers
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    internal static class CommonInterest
    {
        internal static readonly IReadOnlyList<SyncBlockingMethod> JTFSyncBlockers = new[]
        {
            new SyncBlockingMethod(Namespaces.MicrosoftVisualStudioThreading, Types.JoinableTaskFactory.TypeName, Types.JoinableTaskFactory.Run, Types.JoinableTaskFactory.RunAsync),
            new SyncBlockingMethod(Namespaces.MicrosoftVisualStudioThreading, Types.JoinableTask.TypeName, Types.JoinableTask.Join, Types.JoinableTask.JoinAsync),
        };

        internal static readonly IReadOnlyList<SyncBlockingMethod> SyncBlockingMethods = new[]
        {
            new SyncBlockingMethod(Namespaces.MicrosoftVisualStudioThreading, Types.JoinableTaskFactory.TypeName, Types.JoinableTaskFactory.Run, Types.JoinableTaskFactory.RunAsync),
            new SyncBlockingMethod(Namespaces.MicrosoftVisualStudioThreading, Types.JoinableTask.TypeName, Types.JoinableTask.Join, Types.JoinableTask.JoinAsync),
            new SyncBlockingMethod(Namespaces.SystemThreadingTasks, nameof(Task), nameof(Task.Wait), null),
            new SyncBlockingMethod(Namespaces.SystemRuntimeCompilerServices, nameof(TaskAwaiter), nameof(TaskAwaiter.GetResult), null),
        };

        internal static readonly IReadOnlyList<LegacyThreadSwitchingMethod> LegacyThreadSwitchingMethods = new[]
        {
            new LegacyThreadSwitchingMethod(Namespaces.MicrosoftVisualStudioShell, Types.ThreadHelper.TypeName, Types.ThreadHelper.Invoke),
            new LegacyThreadSwitchingMethod(Namespaces.MicrosoftVisualStudioShell, Types.ThreadHelper.TypeName, Types.ThreadHelper.InvokeAsync),
            new LegacyThreadSwitchingMethod(Namespaces.MicrosoftVisualStudioShell, Types.ThreadHelper.TypeName, Types.ThreadHelper.BeginInvoke),
            new LegacyThreadSwitchingMethod(Namespaces.SystemWindowsThreading, Types.Dispatcher.TypeName, Types.Dispatcher.Invoke),
            new LegacyThreadSwitchingMethod(Namespaces.SystemWindowsThreading, Types.Dispatcher.TypeName, Types.Dispatcher.BeginInvoke),
            new LegacyThreadSwitchingMethod(Namespaces.SystemWindowsThreading, Types.Dispatcher.TypeName, Types.Dispatcher.InvokeAsync),
            new LegacyThreadSwitchingMethod(Namespaces.SystemThreading, Types.SynchronizationContext.TypeName, Types.SynchronizationContext.Send),
            new LegacyThreadSwitchingMethod(Namespaces.SystemThreading, Types.SynchronizationContext.TypeName, Types.SynchronizationContext.Post),
        };

        internal static readonly IReadOnlyList<SyncBlockingMethod> SyncBlockingProperties = new[]
        {
            new SyncBlockingMethod(Namespaces.SystemThreadingTasks, nameof(Task), nameof(Task<int>.Result), null),
        };

        /// <summary>
        /// This is an explicit rule to ignore the code that was generated by Xaml2CS.
        /// </summary>
        /// <remarks>
        /// The generated code has the comments like this:
        /// <![CDATA[
        ///   //------------------------------------------------------------------------------
        ///   // <auto-generated>
        /// ]]>
        /// This rule is based on the fact the keyword "&lt;auto-generated&gt;" should be found in the comments.
        /// </remarks>
        internal static bool ShouldIgnoreContext(SyntaxNodeAnalysisContext context)
        {
            var namespaceDeclaration = context.Node.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
            if (namespaceDeclaration != null)
            {
                foreach (var trivia in namespaceDeclaration.NamespaceKeyword.GetAllTrivia())
                {
                    const string autoGeneratedKeyword = @"<auto-generated>";
                    if (trivia.FullSpan.Length > autoGeneratedKeyword.Length
                        && trivia.ToString().Contains(autoGeneratedKeyword))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static void InspectMemberAccess(
            SyntaxNodeAnalysisContext context,
            MemberAccessExpressionSyntax memberAccessSyntax,
            DiagnosticDescriptor descriptor,
            IReadOnlyList<SyncBlockingMethod> problematicMethods,
            bool ignoreIfInsideAnonymousDelegate = false)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (memberAccessSyntax == null)
            {
                return;
            }

            if (ShouldIgnoreContext(context))
            {
                return;
            }

            if (ignoreIfInsideAnonymousDelegate && context.Node.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() != null)
            {
                // We do not analyze JTF.Run inside anonymous functions because
                // they are so often used as callbacks where the signature is constrained.
                return;
            }

            if (Utils.IsWithinNameOf(context.Node as ExpressionSyntax))
            {
                // We do not consider arguments to nameof( ) because they do not represent invocations of code.
                return;
            }

            var typeReceiver = context.SemanticModel.GetTypeInfo(memberAccessSyntax.Expression).Type;
            if (typeReceiver != null)
            {
                foreach (var item in problematicMethods)
                {
                    if (memberAccessSyntax.Name.Identifier.Text == item.MethodName &&
                        typeReceiver.Name == item.ContainingTypeName &&
                        typeReceiver.BelongsToNamespace(item.ContainingTypeNamespace))
                    {
                        var location = memberAccessSyntax.Name.GetLocation();
                        context.ReportDiagnostic(Diagnostic.Create(descriptor, location));
                    }
                }
            }
        }

        internal struct SyncBlockingMethod
        {
            public SyncBlockingMethod(IReadOnlyList<string> containingTypeNamespace, string containingTypeName, string methodName, string asyncAlternativeMethodName)
            {
                this.ContainingTypeNamespace = containingTypeNamespace;
                this.ContainingTypeName = containingTypeName;
                this.MethodName = methodName;
                this.AsyncAlternativeMethodName = asyncAlternativeMethodName;
            }

            public IReadOnlyList<string> ContainingTypeNamespace { get; private set; }

            public string ContainingTypeName { get; private set; }

            public string MethodName { get; private set; }

            public string AsyncAlternativeMethodName { get; private set; }
        }

        internal struct LegacyThreadSwitchingMethod
        {
            public LegacyThreadSwitchingMethod(IReadOnlyList<string> containingTypeNamespace, string containingTypeName, string methodName)
            {
                this.ContainingTypeNamespace = containingTypeNamespace;
                this.ContainingTypeName = containingTypeName;
                this.MethodName = methodName;
            }

            public IReadOnlyList<string> ContainingTypeNamespace { get; private set; }

            public string ContainingTypeName { get; private set; }

            public string MethodName { get; private set; }
        }
    }
}
