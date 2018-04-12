// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal enum ProjectChangeKind
    {
        ProjectAdded,
        ProjectRemoved,

        // A 'project' change can also include more innocuous changes like document add/removes.
        // Consumers should assume that when project changed is fired, they should not cache any
        // state.
        ProjectChanged,
        DocumentsChanged,
        TagHelpersChanged,
    }
}
