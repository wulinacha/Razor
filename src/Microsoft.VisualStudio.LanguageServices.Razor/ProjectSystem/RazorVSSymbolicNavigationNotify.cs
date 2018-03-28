// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            pSpanToNavigate = null;
            pfWouldNavigate = 0;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await FindNavigibleLocationAsync().ConfigureAwait(false);
            });

            return VSConstants.S_OK;
        }

        private async Task FindNavigibleLocationAsync()
        {
            await Task.Delay(0);
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
