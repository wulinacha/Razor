// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace Microsoft.VisualStudio.LanguageServices.Razor.RQNames
{
    internal class MethodNode : RQNameNode
    {
        public MethodNode(AggregateNode aggregate, SymbolNameNode symbolName, TypeVariableCountNode typeVariableCount, ParametersNode parameters)
        {
            Aggregate = aggregate;
            SymbolName = symbolName;
            TypeVariableCount = typeVariableCount;
            Parameters = parameters;
        }

        public AggregateNode Aggregate { get; }

        public SymbolNameNode SymbolName { get; }

        public TypeVariableCountNode TypeVariableCount { get; }

        public ParametersNode Parameters { get; }
    }
}
