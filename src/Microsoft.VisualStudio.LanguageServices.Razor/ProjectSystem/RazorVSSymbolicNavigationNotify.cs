// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
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
            pfWouldNavigate = 0;

            var hr = pHierCodeFile.GetCanonicalName(itemidCodeFile, out var filePath);
            if (ErrorHandler.Failed(hr))
            {
                return hr;
            }

            // FilePath we want to see looks like: 'c:\<...>\designtimebuild\obj\debug\netcoreapp2.1\razor\views\home\index.g.cshtml.cs'
            if (!filePath.EndsWith(".g.cshtml.cs"))
            {
                return VSConstants.S_OK;
            }
            
            var project = (IVsProject)pHierCodeFile.GetActiveProjectContext();
            hr = project.GetMkDocument((uint)VSConstants.VSITEMID.Root, out var projectFilePath);
            if (ErrorHandler.Failed(hr))
            {
                return hr;
            }

            var projectDirectory = Path.GetDirectoryName(projectFilePath);
            var generatedCodeDirectory = Path.Combine(projectDirectory, "obj\\debug\\netcoreapp2.1\\razor\\");

            var relativePath = filePath.Substring(generatedCodeDirectory.Length);
            var sourcePath = Path.Combine(projectDirectory, relativePath.Replace(".g.cshtml.cs", ".cshtml"));

            hr = pHierCodeFile.ParseCanonicalName(sourcePath, out var sourceItemId);
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
                ppHierToNavigate = pHierCodeFile;
                pitemidToNavigate = sourceItemId;
                pSpanToNavigate[0] = new TextSpan();
                pfWouldNavigate = 1;
            }

            return VSConstants.S_OK;
        }

        private async Task<NavigationItem> FindNavigibleLocationAsync(Solution solution, DocumentId documentId, string rqName)
        {
            var project = solution.GetProject(documentId.ProjectId);
            var document = project.GetDocument(documentId);

            var syntaxTree = await document.GetSyntaxTreeAsync().ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);

            RQNames.RQNameNode parsed;

            try
            {
                parsed = RQNames.RQNameParser.Parse(rqName);
            }
            catch
            {
                Debug.Fail("Unable to parse RQName: " + rqName);
                throw;
            }

            string typeName = null;
            string propertyName = null;
            if (parsed is RQNames.AggregateNode typeNode)
            {
                typeName = typeNode.CombinedName;
                propertyName = null;
            }
            else if (parsed is RQNames.PropertyNode propertyNode)
            {
                typeName = propertyNode.Aggregate.CombinedName;
                propertyName = propertyNode.SymbolName.Name;
            }

            if (typeName == null)
            {
                return null;
            }

            var symbol = semanticModel.Compilation.GetTypeByMetadataName(typeName);
            if (symbol != null)
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
