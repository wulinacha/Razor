// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.LanguageServices.Razor.RQNames
{
    internal class AggregateNameNode : SymbolNameNode
    {
        public AggregateNameNode(SimpleNameNode simpleName, TypeVariableCountNode typeVariableCount)
        {
            SimpleName = simpleName;
            TypeVariableCount = typeVariableCount;
        }

        public SimpleNameNode SimpleName { get; }

        public TypeVariableCountNode TypeVariableCount { get; }
    }
}
