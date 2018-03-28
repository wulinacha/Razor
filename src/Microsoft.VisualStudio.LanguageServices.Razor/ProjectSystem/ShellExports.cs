// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.VS;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Razor.ProjectSystem
{
    public class ShellExports
    {
        private readonly Lazy<IVsHierarchyRefactorNotify> _refactorNotify;
        private readonly Lazy<IVsSymbolicNavigationNotify> _symbolicNavigationNotify;
        private readonly VisualStudioWorkspace _workspace;

        [ImportingConstructor]
        public ShellExports(VisualStudioWorkspace workspace)
        {
            _workspace = workspace;

            _refactorNotify = new Lazy<IVsHierarchyRefactorNotify>(CreateRefactorNotify);
            _symbolicNavigationNotify = new Lazy<IVsSymbolicNavigationNotify>(CreateSymbolicNavigationNotify);
        }


        [Export(ExportContractNames.VsTypes.ProjectNodeComExtension)]
        [AppliesTo(ProjectCapabilities.Cps)]
        [ComServiceIid(typeof(IVsHierarchyRefactorNotify))]
        public IVsHierarchyRefactorNotify RefactorNotify => _refactorNotify.Value;

        [Export(ExportContractNames.VsTypes.ProjectNodeComExtension)]
        [AppliesTo(ProjectCapabilities.Cps)]
        [ComServiceIid(typeof(IVsSymbolicNavigationNotify))]
        public IVsSymbolicNavigationNotify SymbolicNavigationNotify => _symbolicNavigationNotify.Value;

        private IVsHierarchyRefactorNotify CreateRefactorNotify()
        {
            return new RazorHierarchyRefactorNotify();
        }

        private IVsSymbolicNavigationNotify CreateSymbolicNavigationNotify()
        {
            return new RazorVSSymbolicNavigationNotify(_workspace);
        }
    }
}
