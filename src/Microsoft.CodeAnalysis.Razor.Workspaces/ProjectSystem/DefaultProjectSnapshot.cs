// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    // All of the public state of this is immutable - we create a new instance and notify subscribers
    // when it changes. 
    //
    // However we use the private state to track things like dirty/clean.
    //
    // See the private constructors... When we update the snapshot we either are processing a Workspace
    // change (Project) or updating the computed state (ProjectSnapshotUpdateContext). We don't do both
    // at once. 
    internal class DefaultProjectSnapshot : ProjectSnapshot
    {
        private readonly Lazy<RazorProjectEngine> _projectEngine;
        private ProjectSnapshotComputedState _computedState;

        public DefaultProjectSnapshot(ProjectSnapshotState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            State = state;
            _projectEngine = new Lazy<RazorProjectEngine>(CreateProjectEngine);
        }

        public DefaultProjectSnapshot(ProjectSnapshotState state, DefaultProjectSnapshot other, ProjectSnapshotStateDifference difference)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            State = state;
            
            if ((difference & ProjectSnapshotStateDifference.ConfigurationChanged) == 0)
            {
                // Use cached project engine if the configuration hasn't changed.
                _projectEngine = other._projectEngine;
            }
            else
            {
                _projectEngine = new Lazy<RazorProjectEngine>(CreateProjectEngine);
            }

            if (other._computedState != null)
            {
                if (difference == ProjectSnapshotStateDifference.None ||
                    difference == ProjectSnapshotStateDifference.DocumentsChanged)
                {
                    // Keep the computed state and mark up-to-date to date if possible.
                    _computedState = new ProjectSnapshotComputedState(other._computedState, Version);
                }
                else if (difference == ProjectSnapshotStateDifference.WorkspaceProjectChanged)
                {
                    // Keep the computed state, but still dirty
                    _computedState = other._computedState;
                }
            }
        }

        public DefaultProjectSnapshot(ProjectSnapshotUpdateContext update, DefaultProjectSnapshot other, ProjectSnapshotStateDifference difference)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            State = other.State;
            _projectEngine = other._projectEngine;

            if (difference == ProjectSnapshotStateDifference.None ||
                difference == ProjectSnapshotStateDifference.DocumentsChanged)
            {
                // Mark the computed as up to date if there are no project-level changes.
                _computedState = new ProjectSnapshotComputedState(update, Version);
            }
            else
            {
                // Use the new computed state, but stay dirty if the project has changed.
                _computedState = new ProjectSnapshotComputedState(update);
            }
        }

        public override RazorConfiguration Configuration => HostProject.Configuration;

        public override IReadOnlyList<RazorDocument> Documents => State.HostProject.Documents;

        public override string FilePath => State.HostProject.FilePath;

        public HostProject HostProject => State.HostProject;

        public override bool IsInitialized => WorkspaceProject != null;

        public override IReadOnlyList<TagHelperDescriptor> TagHelpers => _computedState?.TagHelpers ?? Array.Empty<TagHelperDescriptor>();

        public override VersionStamp Version => State.Version;

        public override Project WorkspaceProject => State.WorkspaceProject;
        
        public VersionStamp? ComputedVersion => _computedState?.Version;

        // We know the project is dirty if we don't have a computed result, or it was computed for a different version.
        // Since the PSM updates the snapshots synchronously, the snapshot can never be older than the computed state.
        public bool IsProjectDirty =>  ComputedVersion != Version;

        public ProjectSnapshotState State { get; }

        public override RazorProjectEngine GetCurrentProjectEngine()
        {
            var projectEngine = _projectEngine.Value;

            if (ComputedVersion.HasValue)
            {
                // Make sure the tag helpers are no older than the ones we know about.
                var feature = projectEngine.EngineFeatures.OfType<ComputedTagHelperFeature>().Single();
                lock (feature.Lock)
                {
                    if (feature.ComputedVersion.GetNewerVersion(ComputedVersion.Value) != feature.ComputedVersion)
                    {
                        // We have a newer version, update the feature.
                        feature.ComputedVersion = ComputedVersion.Value;
                        feature.TagHelpers = TagHelpers;
                    }
                }
            }

            return projectEngine;
        }

        private RazorProjectEngine CreateProjectEngine()
        {
            var factory = State.Services.GetRequiredService<ProjectSnapshotProjectEngineFactory>();
            return factory.Create(this, builder =>
            {
                // Allow the original snapshot to create the engine. Newer snapshots will update
                // the feature.
                builder.Features.Add(new ComputedTagHelperFeature()
                {
                    TagHelpers = TagHelpers,
                    ComputedVersion = ComputedVersion == null ? VersionStamp.Default : ComputedVersion.Value,
                });
            });
        }

        private class ComputedTagHelperFeature : ITagHelperFeature
        {
            public object Lock = new object();

            public RazorEngine Engine { get; set; }

            public IReadOnlyList<TagHelperDescriptor> TagHelpers { get; set; }

            public VersionStamp ComputedVersion { get; set; }

            public IReadOnlyList<TagHelperDescriptor> GetDescriptors()
            {
                lock (Lock)
                {
                    return TagHelpers;
                }
            }
        }
    }
}