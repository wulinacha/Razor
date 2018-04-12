// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        private readonly ProjectSnapshotState _state;

        private ProjectSnapshotComputedState _computedState;

        public DefaultProjectSnapshot(ProjectSnapshotState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _state = state;

            _projectEngine = new Lazy<RazorProjectEngine>(CreateProjectEngine);
        }

        public DefaultProjectSnapshot(ProjectSnapshotState state, DefaultProjectSnapshot other)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            _state = state;
            _projectEngine = other._projectEngine;

            var difference = _state.ComputeDifferenceFrom(other._state);
            if ((difference & ProjectSnapshotState.ProjectSnapshotStateDifference.ConfigurationChanged) != 0)
            {
                // Don't use the cached project engine if the configuration has changed.
                _projectEngine = new Lazy<RazorProjectEngine>(CreateProjectEngine);
            }
        }

        private DefaultProjectSnapshot(ProjectSnapshotUpdateContext update, DefaultProjectSnapshot other)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            _computedState = new ProjectSnapshotComputedState(update.Version);
            _state = other._state;

            _projectEngine = new Lazy<RazorProjectEngine>(CreateProjectEngine);
        }

        public override RazorConfiguration Configuration => HostProject.Configuration;

        public override IReadOnlyList<RazorDocument> Documents => Array.Empty<RazorDocument>();

        public override string FilePath => _state.HostProject.FilePath;

        public HostProject HostProject => _state.HostProject;

        public override bool IsInitialized => WorkspaceProject != null;

        public override VersionStamp Version => _state.Version;

        public override Project WorkspaceProject => _state.WorkspaceProject;
        
        public VersionStamp? ComputedVersion => _computedState?.Version;

        // We know the project is dirty if we don't have a computed result, or it was computed for a different version.
        // Since the PSM updates the snapshots synchronously, the snapshot can never be older than the computed state.
        public bool IsDirty =>  ComputedVersion != Version;

        public override RazorProjectEngine GetCurrentProjectEngine()
        {
            return _projectEngine.Value;
        }

        public ProjectSnapshotUpdateContext CreateUpdateContext()
        {
            return new ProjectSnapshotUpdateContext(FilePath, HostProject, WorkspaceProject, Version);
        }

        public DefaultProjectSnapshot WithHostProject(HostProject hostProject)
        {
            if (hostProject == null)
            {
                throw new ArgumentNullException(nameof(hostProject));
            }

            var state = _state.WithHostProject(hostProject);
            return new DefaultProjectSnapshot(state, this);
        }

        public DefaultProjectSnapshot WithWorkspaceProject(Project workspaceProject)
        {
            var state = _state.WithWorkspaceProject(workspaceProject);
            return new DefaultProjectSnapshot(state, this);
        }

        public DefaultProjectSnapshot WithComputedUpdate(ProjectSnapshotUpdateContext update)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            return new DefaultProjectSnapshot(update, this);
        }

        public bool HasConfigurationChanged(DefaultProjectSnapshot original)
        {
            if (original == null)
            {
                throw new ArgumentNullException(nameof(original));
            }

            return !object.Equals(Configuration, original.Configuration);
        }

        private RazorProjectEngine CreateProjectEngine()
        {
            var factory = _state.Services.GetRequiredService<IProjectEngineFactory>();
            return factory.Create(_state.HostProject.Configuration, RazorProjectFileSystem.Create(_state.HostProject.FilePath), null);
        }
    }
}