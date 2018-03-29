// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.LanguageServices.Razor.RQNames
{
    internal class MemberVariableNode : RQNameNode
    {
        public MemberVariableNode(AggregateNode aggregate, SymbolNameNode symbolName)
        {
            Aggregate = aggregate;
            SymbolName = symbolName;
        }

        public AggregateNode Aggregate { get; }

        public SymbolNameNode SymbolName { get; }
    }
}
