// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal class HostProject
    {
        public HostProject(string projectFilePath, RazorConfiguration razorConfiguration, IEnumerable<RazorDocument> documents)
        {
            if (projectFilePath == null)
            {
                throw new ArgumentNullException(nameof(projectFilePath));
            }

            if (razorConfiguration == null)
            {
                throw new ArgumentNullException(nameof(razorConfiguration));
            }

            if (documents == null)
            {
                throw new ArgumentNullException(nameof(documents));
            }

            FilePath = projectFilePath;
            Configuration = razorConfiguration;
            Documents = documents.ToArray();
        }

        public RazorConfiguration Configuration { get; }

        public IReadOnlyList<RazorDocument> Documents { get; }

        public string FilePath { get; }
    }
}