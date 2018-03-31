// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.VisualStudio.LanguageServices.Razor.RQNames
{
    internal class ParametersNode
    {
        public ParametersNode(List<ParameterNode> parameterList)
        {
            ParameterList = parameterList;
        }

        public List<ParameterNode> ParameterList { get; }
    }
}
