// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VSDesigner.Common;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.LanguageServices.Razor.ProjectSystem
{
    internal class RazorVSSymbolicNavigationNotify : IVsSymbolicNavigationNotify
    {
        private readonly VisualStudioWorkspace _workspace;

        public RazorVSSymbolicNavigationNotify(VisualStudioWorkspace workspace)
        {
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            _workspace = workspace;
        }

        public int OnBeforeNavigateToSymbol(
            IVsHierarchy pHierCodeFile,
            uint itemidCodeFile,
            string pszRQName,
            out int pfNavigationHandled)
        {
            pfNavigationHandled = 0;
            return VSConstants.S_OK;
        }

        public int QueryNavigateToSymbol(
            IVsHierarchy pHierCodeFile,
            uint itemidCodeFile,
            string pszRQName,
            out IVsHierarchy ppHierToNavigate,
            out uint pitemidToNavigate,
            TextSpan[] pSpanToNavigate,
            out int pfWouldNavigate)
        {
            ppHierToNavigate = null;
            pitemidToNavigate = 0u;
            pSpanToNavigate = null;
            pfWouldNavigate = 0;

            var hr = pHierCodeFile.GetCanonicalName(itemidCodeFile, out var filePath);
            if (ErrorHandler.Failed(hr))
            {
                return hr;
            }

            var solution = _workspace.CurrentSolution;
            var ids = solution.GetDocumentIdsWithFilePath(filePath);
            if (ids.Length != 1)
            {
                return VSConstants.E_FAIL;
            }

            var item = ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                return await FindNavigibleLocationAsync(solution, ids[0], pszRQName).ConfigureAwait(false);
            });

            if (item != null)
            {

            }

            return VSConstants.S_OK;
        }

        private async Task<NavigationItem> FindNavigibleLocationAsync(Solution solution, DocumentId documentId, string rqName)
        {
            var project = solution.GetProject(documentId.ProjectId);
            var document = project.GetDocument(documentId);

            var syntaxTree = await document.GetSyntaxTreeAsync().ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);

            var parsed = RQNames.RQNameParser.Parse(rqName);

            var symbolName = "billy";
            var symbols = semanticModel.Compilation.GetSymbolsWithName(s => s == symbolName, SymbolFilter.All);
            foreach (var symbol in symbols)
            {
                var declarations = symbol.DeclaringSyntaxReferences;
                foreach (var declaration in declarations)
                {
                    if (object.ReferenceEquals(syntaxTree, declaration.SyntaxTree))
                    {
                        return new NavigationItem(null, 0, new TextSpan());
                    }
                }
            }

            return null;
        }

        private class NavigationItem
        {
            public NavigationItem(IVsHierarchy hierarchy, uint itemId, TextSpan span)
            {
                Hierarchy = hierarchy;
                ItemId = itemId;
                Span = span;
            }

            public IVsHierarchy Hierarchy { get; }

            public uint ItemId { get; }

            public TextSpan Span { get; }
        }
    }
}
