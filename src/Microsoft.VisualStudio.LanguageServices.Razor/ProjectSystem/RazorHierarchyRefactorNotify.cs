// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Razor.ProjectSystem
{
    internal class RazorHierarchyRefactorNotify : IVsHierarchyRefactorNotify
    {
        public int OnBeforeGlobalSymbolRenamed(uint cItemsAffected, uint[] rgItemsAffected, uint cRQNames, string[] rglpszRQName, string lpszNewName, int promptContinueOnFail)
        {
            throw new NotImplementedException();
        }

        public int OnGlobalSymbolRenamed(uint cItemsAffected, uint[] rgItemsAffected, uint cRQNames, string[] rglpszRQName, string lpszNewName)
        {
            throw new NotImplementedException();
        }

        public int OnBeforeReorderParams(uint itemid, string lpszRQName, uint cParamIndexes, uint[] rgParamIndexes, int promptContinueOnFail)
        {
            throw new NotImplementedException();
        }

        public int OnReorderParams(uint itemid, string lpszRQName, uint cParamIndexes, uint[] rgParamIndexes)
        {
            throw new NotImplementedException();
        }

        public int OnBeforeRemoveParams(uint itemid, string lpszRQName, uint cParamIndexes, uint[] rgParamIndexes, int promptContinueOnFail)
        {
            throw new NotImplementedException();
        }

        public int OnRemoveParams(uint itemid, string lpszRQName, uint cParamIndexes, uint[] rgParamIndexes)
        {
            throw new NotImplementedException();
        }

        public int OnBeforeAddParams(uint itemid, string lpszRQName, uint cParams, uint[] rgszParamIndexes, string[] rgszRQTypeNames, string[] rgszParamNames, int promptContinueOnFail)
        {
            throw new NotImplementedException();
        }

        public int OnAddParams(uint itemid, string lpszRQName, uint cParams, uint[] rgszParamIndexes, string[] rgszRQTypeNames, string[] rgszParamNames)
        {
            throw new NotImplementedException();
        }
    }
}
