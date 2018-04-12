// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal class EphemeralProjectSnapshot : ProjectSnapshot
    {
        private readonly HostWorkspaceServices _services;
        private readonly Lazy<RazorProjectEngine> _projectEngine;

        public EphemeralProjectSnapshot(HostWorkspaceServices services, string filePath)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            _services = services;
            FilePath = filePath;

            _projectEngine = new Lazy<RazorProjectEngine>(CreateProjectEngine);
        }

        public override RazorConfiguration Configuration => FallbackRazorConfiguration.MVC_2_1;

        public override IReadOnlyList<RazorDocument> Documents => Array.Empty<RazorDocument>();

        public override string FilePath { get; }

        public override bool IsInitialized => false;

        public override IReadOnlyList<TagHelperDescriptor> TagHelpers => Array.Empty<TagHelperDescriptor>();

        public override VersionStamp Version { get; } = VersionStamp.Default;

        public override Project WorkspaceProject => null;

        public override RazorProjectEngine GetCurrentProjectEngine()
        {
            return _projectEngine.Value;
        }

        private RazorProjectEngine CreateProjectEngine()
        {
            var factory = _services.GetRequiredService<ProjectSnapshotProjectEngineFactory>();
            return factory.Create(this);
        }
    }
}
