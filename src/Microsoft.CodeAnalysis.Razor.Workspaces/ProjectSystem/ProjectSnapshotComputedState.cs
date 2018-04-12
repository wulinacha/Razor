// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal class ProjectSnapshotComputedState
    {
        public ProjectSnapshotComputedState(ProjectSnapshotUpdateContext update)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            TagHelpers = update.TagHelpers ?? Array.Empty<TagHelperDescriptor>();
            Version = update.Snapshot.Version;
        }

        public ProjectSnapshotComputedState(ProjectSnapshotUpdateContext update, VersionStamp version)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            TagHelpers = update.TagHelpers ?? Array.Empty<TagHelperDescriptor>();
            Version = version;
        }

        public ProjectSnapshotComputedState(ProjectSnapshotComputedState other, VersionStamp version)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            TagHelpers = other.TagHelpers;
            Version = version;
        }

        public IReadOnlyList<TagHelperDescriptor> TagHelpers { get; }

        public VersionStamp Version { get; }
    }
}
