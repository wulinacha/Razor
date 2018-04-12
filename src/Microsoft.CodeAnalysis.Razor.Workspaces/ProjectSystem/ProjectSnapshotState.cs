// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal class ProjectSnapshotState
    {
        public ProjectSnapshotState(
            HostWorkspaceServices services,
            HostProject hostProject,
            Project workspaceProject,
            VersionStamp? version = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (hostProject == null)
            {
                throw new ArgumentNullException(nameof(hostProject));
            }

            Services = services;
            HostProject = hostProject;
            WorkspaceProject = workspaceProject;
            Version = version ?? VersionStamp.Create();
        }

        public HostWorkspaceServices Services { get; }

        public HostProject HostProject { get; }

        public Project WorkspaceProject { get; }

        public VersionStamp Version { get; }

        public ProjectSnapshotState WithHostProject(HostProject hostProject)
        {
            if (hostProject == null)
            {
                throw new ArgumentNullException(nameof(hostProject));
            }

            return new ProjectSnapshotState(Services, hostProject, WorkspaceProject, Version.GetNewerVersion());
        }

        public ProjectSnapshotState WithWorkspaceProject(Project workspaceProject)
        {
            return new ProjectSnapshotState(Services, HostProject, workspaceProject, Version.GetNewerVersion());
        }

        public ProjectSnapshotStateDifference ComputeDifferenceFrom(ProjectSnapshotState older)
        {
            if (older == null)
            {
                throw new ArgumentNullException(nameof(older));
            }

            var difference = ProjectSnapshotStateDifference.Empty;
            if (older.HostProject.Configuration.Equals(HostProject.Configuration))
            {
                difference |= ProjectSnapshotStateDifference.ConfigurationChanged;
            }

            if (older.WorkspaceProject == null && WorkspaceProject != null)
            {
                difference |= ProjectSnapshotStateDifference.WorkspaceProjectAdded;
            }
            else if (older.WorkspaceProject != null && WorkspaceProject == null)
            {
                difference |= ProjectSnapshotStateDifference.WorkspaceProjectRemoved;
            }
            else if (older.WorkspaceProject?.Version != WorkspaceProject?.Version)
            {
                // For now this is very naive. We will want to consider changing
                // our logic here to be more robust.
                difference |= ProjectSnapshotStateDifference.WorkspaceProjectChanged;
            }

            return difference;
        }

        [Flags]
        public enum ProjectSnapshotStateDifference
        {
            Empty = 0,
            ConfigurationChanged,
            WorkspaceProjectAdded,
            WorkspaceProjectRemoved,
            WorkspaceProjectChanged,
        }
    }
}
