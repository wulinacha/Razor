// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    public class DefaultProjectSnapshotTest
    {
        public DefaultProjectSnapshotTest()
        {
            Project project1 = null;
            Project project2 = null;
            Workspace = TestWorkspace.Create(workspace =>
            {
                project1 = workspace.AddProject("Project1", LanguageNames.CSharp);
                project2 = workspace.AddProject("Project2", LanguageNames.CSharp);
            });

            Project1 = project1;
            Project2 = project2;
        }

        public Project Project1 { get; }

        public Project Project2 { get; }

        public Workspace Workspace { get; }

        [Fact]
        public void WithWorkspaceProject_CreatesSnapshot_UpdatesUnderlyingProject()
        {
            // Arrange
            var hostProject = new HostProject("Test.cshtml", FallbackRazorConfiguration.MVC_2_0, Array.Empty<RazorDocument>());
            var state = new ProjectSnapshotState(Workspace.Services, hostProject, Project1);
            var original = new DefaultProjectSnapshot(state);

            var anotherProject = Project2;

            // Act
            var snapshot = original.WithWorkspaceProject(anotherProject);

            // Assert
            Assert.Same(anotherProject, snapshot.WorkspaceProject);
            Assert.Equal(original.ComputedVersion, snapshot.ComputedVersion);
            Assert.Equal(original.Configuration, snapshot.Configuration);
        }

        [Fact]
        public void WithProjectChange_WithProject_CreatesSnapshot_UpdatesValues()
        {
            // Arrange
            var hostProject = new HostProject("Test.cshtml", FallbackRazorConfiguration.MVC_2_0, Array.Empty<RazorDocument>());
            var state = new ProjectSnapshotState(Workspace.Services, hostProject, Project1);
            var original = new DefaultProjectSnapshot(state);

            var anotherProject = Project2;
            var update = new ProjectSnapshotUpdateContext(original.FilePath, hostProject, anotherProject, original.Version);

            // Act
            var snapshot = original.WithComputedUpdate(update);

            // Assert
            Assert.Same(original.WorkspaceProject, snapshot.WorkspaceProject);
        }
    }
}
