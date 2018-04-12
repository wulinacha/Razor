// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal class ProjectSnapshotComputedState
    {
        public ProjectSnapshotComputedState(VersionStamp version)
        {
            Version = version;
        }

        public VersionStamp Version { get; }
    }
}
