// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal class ProjectSnapshotUpdateContext
    {
        public ProjectSnapshotUpdateContext(ProjectSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Snapshot = snapshot;
        }

        public ProjectSnapshot Snapshot { get; }

        public IReadOnlyList<TagHelperDescriptor> TagHelpers { get; set; } = Array.Empty<TagHelperDescriptor>();
    }
}
