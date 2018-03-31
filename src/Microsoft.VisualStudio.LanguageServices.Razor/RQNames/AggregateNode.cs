// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.VisualStudio.LanguageServices.Razor.RQNames
{
    internal class AggregateNode : RQNameNode
    {
        public AggregateNode(List<SymbolNameNode> names)
        {
            Names = names;
        }

        public IReadOnlyList<SymbolNameNode> Names { get; }


        public string CombinedName => string.Join(".", Names.Select(n => n.Name));
    }
}
