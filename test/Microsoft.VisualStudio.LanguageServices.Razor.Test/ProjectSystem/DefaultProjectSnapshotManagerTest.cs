// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;
using Moq;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    public class DefaultProjectSnapshotManagerTest : ForegroundDispatcherTestBase
    {
        public DefaultProjectSnapshotManagerTest()
        {
            var projectEngineFactory = new Mock<ProjectSnapshotProjectEngineFactory>();
            projectEngineFactory
                .Setup(f => f.Create(It.IsAny<ProjectSnapshot>(), It.IsAny<RazorProjectFileSystem>(), It.IsAny<Action<RazorProjectEngineBuilder>>()))
                .Returns<ProjectSnapshot, RazorProjectFileSystem, Action<RazorProjectEngineBuilder>>((ps, fs, c) =>
                {
                    return RazorProjectEngine.Create(ps.Configuration, fs, c);
                });
            ProjectEngineFactory = projectEngineFactory.Object;

            HostServices = TestServices.Create(
                new IWorkspaceService[]
                {
                    ProjectEngineFactory,
                },
                new ILanguageService[]
                {
                });

            HostProject = new HostProject(
                "c:\\MyProject\\Test.csproj",
                FallbackRazorConfiguration.MVC_2_0,
                new RazorDocument[]
                {
                    new ProjectSystemRazorDocument("c:\\MyProject\\File.cshtml", "File.cshtml"),
                });

            HostProjectWithConfigurationChange = new HostProject(
                "c:\\MyProject\\Test.csproj",
                FallbackRazorConfiguration.MVC_1_0,
                new RazorDocument[]
                {
                    new ProjectSystemRazorDocument("c:\\MyProject\\File.cshtml", "File.cshtml"),
                });

            HostProjectWithDocumentAdded = new HostProject(
                "c:\\MyProject\\Test.csproj",
                FallbackRazorConfiguration.MVC_2_0,
                new RazorDocument[]
                {
                    new ProjectSystemRazorDocument("c:\\MyProject\\File.cshtml", "File.cshtml"),
                    new ProjectSystemRazorDocument("c:\\MyProject\\AddedFile.cshtml", "AddedFile.cshtml"),
                });

            Workspace = TestWorkspace.Create(HostServices);
            ProjectManager = new TestProjectSnapshotManager(Dispatcher, Enumerable.Empty<ProjectSnapshotChangeTrigger>(), Workspace);

            var projectId = ProjectId.CreateNewId("Test");
            var solution = Workspace.CurrentSolution.AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Default,
                "Test",
                "Test",
                LanguageNames.CSharp,
                "c:\\MyProject\\Test.csproj"));
            WorkspaceProject = solution.GetProject(projectId);

            var vbProjectId = ProjectId.CreateNewId("VB");
            solution = solution.AddProject(ProjectInfo.Create(
                vbProjectId,
                VersionStamp.Default,
                "VB",
                "VB",
                LanguageNames.VisualBasic,
                "VB.vbproj"));
            VBWorkspaceProject = solution.GetProject(vbProjectId);

            var projectWithoutFilePathId = ProjectId.CreateNewId("NoFile");
            solution = solution.AddProject(ProjectInfo.Create(
                projectWithoutFilePathId,
                VersionStamp.Default,
                "NoFile",
                "NoFile",
                LanguageNames.CSharp));
            WorkspaceProjectWithoutFilePath = solution.GetProject(projectWithoutFilePathId);

            // Approximates a project with multi-targeting
            var projectIdWithDifferentTfm = ProjectId.CreateNewId("TestWithDifferentTfm");
            solution = Workspace.CurrentSolution.AddProject(ProjectInfo.Create(
                projectIdWithDifferentTfm,
                VersionStamp.Default,
                "Test (Different TFM)",
                "Test",
                LanguageNames.CSharp,
                "c:\\MyProject\\Test.csproj"));
            WorkspaceProjectWithDifferentTfm = solution.GetProject(projectIdWithDifferentTfm);

            SomeTagHelpers = new List<TagHelperDescriptor>();
            SomeTagHelpers.Add(TagHelperDescriptorBuilder.Create("Test1", "TestAssembly").Build());

            OtherTagHelpers = new List<TagHelperDescriptor>();
            OtherTagHelpers.Add(TagHelperDescriptorBuilder.Create("Test2", "TestAssembly").Build());
        }

        private HostProject HostProject { get; }

        private HostProject HostProjectWithConfigurationChange { get; }

        private HostProject HostProjectWithDocumentAdded { get; }

        private Project WorkspaceProject { get; }

        private Project WorkspaceProjectWithDifferentTfm { get; }

        private Project WorkspaceProjectWithoutFilePath { get; }

        private Project VBWorkspaceProject { get; }

        private ProjectSnapshotProjectEngineFactory ProjectEngineFactory { get; }

        private TestProjectSnapshotManager ProjectManager { get; }

        private HostServices HostServices { get; }

        private Workspace Workspace { get; }

        private List<TagHelperDescriptor> SomeTagHelpers { get; }

        private List<TagHelperDescriptor> OtherTagHelpers { get; }

        [ForegroundFact]
        public void HostProjectAdded_WithoutWorkspaceProject_NotifiesListeners()
        {
            // Arrange

            // Act
            ProjectManager.HostProjectAdded(HostProject);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.True(snapshot.IsProjectDirty);
            Assert.False(snapshot.IsInitialized);

            Assert.Equal(ProjectChangeKind.ProjectAdded, ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void HostProjectAdded_FindsWorkspaceProject_NotifiesListeners_AndStartsBackgroundWorker()
        {
            // Arrange
            Assert.True(Workspace.TryApplyChanges(WorkspaceProject.Solution));

            // Act
            ProjectManager.HostProjectAdded(HostProject);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.True(snapshot.IsProjectDirty);
            Assert.True(snapshot.IsInitialized);

            Assert.Equal(ProjectChangeKind.ProjectAdded, ProjectManager.ListenersNotifiedOf);
            Assert.True(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void HostProjectChanged_ConfigurationChange_WithoutWorkspaceProject_NotifiesListeners_AndDoesNotStartBackgroundWorker()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.HostProjectChanged(HostProjectWithConfigurationChange);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProjectWithConfigurationChange);
            Assert.True(snapshot.IsProjectDirty);
            Assert.False(snapshot.IsInitialized);

            Assert.Equal(ProjectChangeKind.ProjectChanged, ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void HostProjectChanged_ConfigurationChange_WithWorkspaceProject_NotifiesListeners_AndStartsBackgroundWorker()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.HostProjectChanged(HostProjectWithConfigurationChange);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProjectWithConfigurationChange);
            Assert.True(snapshot.IsProjectDirty);
            Assert.True(snapshot.IsInitialized);

            Assert.Equal(ProjectChangeKind.ProjectChanged, ProjectManager.ListenersNotifiedOf);
            Assert.True(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void HostProjectChanged_ConfigurationChange_DoesNotCacheProjectEngine()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var projectEngine = snapshot.GetCurrentProjectEngine();

            // Act
            ProjectManager.HostProjectChanged(HostProjectWithConfigurationChange);

            // Assert
            snapshot = ProjectManager.GetSnapshot(HostProjectWithConfigurationChange);
            Assert.NotSame(projectEngine, snapshot.GetCurrentProjectEngine());
        }

        [ForegroundFact]
        public void HostProjectChanged_ConfigurationChange_DoesNotCacheComputedState()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Adding some computed state
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var updateContext = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = SomeTagHelpers,
            };
            ProjectManager.ProjectUpdated(updateContext);
            ProjectManager.Reset();

            // Act
            ProjectManager.HostProjectChanged(HostProjectWithConfigurationChange);

            // Assert
            snapshot = ProjectManager.GetSnapshot(HostProjectWithConfigurationChange);
            Assert.Empty(snapshot.TagHelpers);
        }

        [ForegroundFact]
        public void HostProjectChanged_DocumentAdded_NotifiesListeners_AndDoesNotStartBackgroundWorker()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var projectEngine = snapshot.GetCurrentProjectEngine();

            // Act
            ProjectManager.HostProjectChanged(HostProjectWithDocumentAdded);

            // Assert
            snapshot = ProjectManager.GetSnapshot(HostProjectWithDocumentAdded);
            Assert.True(snapshot.IsProjectDirty);
            Assert.True(snapshot.IsInitialized);

            Assert.Equal(ProjectChangeKind.DocumentsChanged, ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void HostProjectChanged_DocumentAdded_KeepsComputedState()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var update = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = SomeTagHelpers,
            };
            ProjectManager.ProjectUpdated(update);
            ProjectManager.Reset();

            // Act
            ProjectManager.HostProjectChanged(HostProjectWithDocumentAdded);

            // Assert
            snapshot = ProjectManager.GetSnapshot(HostProjectWithDocumentAdded);
            Assert.Same(SomeTagHelpers, snapshot.TagHelpers);
            Assert.False(snapshot.IsProjectDirty);
        }

        [ForegroundFact]
        public void HostProjectChanged_DocumentAdded_CachesProjectEngine()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.Reset();

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var projectEngine = snapshot.GetCurrentProjectEngine();

            // Act
            ProjectManager.HostProjectChanged(HostProjectWithDocumentAdded);

            // Assert
            snapshot = ProjectManager.GetSnapshot(HostProjectWithDocumentAdded);
            Assert.Same(projectEngine, snapshot.GetCurrentProjectEngine());
        }

        [ForegroundFact]
        public void HostProjectChanged_IgnoresUnknownProject()
        {
            // Arrange

            // Act
            ProjectManager.HostProjectChanged(HostProject);

            // Assert
            Assert.Empty(ProjectManager.Projects);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void HostProjectRemoved_RemovesProject_NotifiesListeners()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.HostProjectRemoved(HostProject);

            // Assert
            Assert.Empty(ProjectManager.Projects);

            Assert.Equal(ProjectChangeKind.ProjectRemoved, ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void ProjectUpdated_IgnoresUnknownProject()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            ProjectManager.HostProjectRemoved(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectUpdated(new ProjectSnapshotUpdateContext(snapshot));

            // Assert
            Assert.Empty(ProjectManager.Projects);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void ProjectUpdated_WhenNoChanges_NotifiesListeners_AndDoesNotStartBackgroundWorker()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Generate the update
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var updateContext = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = SomeTagHelpers,
            };

            // Act
            ProjectManager.ProjectUpdated(updateContext);

            // Assert
            snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.False(snapshot.IsProjectDirty);
            Assert.Same(SomeTagHelpers, snapshot.TagHelpers);

            Assert.Equal(ProjectChangeKind.TagHelpersChanged, ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void ProjectUpdated_WhenNoChanges_TagHelpersSame_DoesNotNotifyListeners_AndDoesNotStartBackgroundWorker()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Generate the update
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var updateContext = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = SomeTagHelpers,
            };
            ProjectManager.ProjectUpdated(updateContext);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectUpdated(updateContext);

            // Assert
            snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.False(snapshot.IsProjectDirty);
            Assert.Same(SomeTagHelpers, snapshot.TagHelpers);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void ProjectUpdated_WhenNoopChange_NotifiesListeners_AndDoesNotStartBackgroundWorker()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Generate the update
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var updateContext = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = SomeTagHelpers,
            };

            ProjectManager.HostProjectChanged(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectUpdated(updateContext);

            // Assert
            snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.False(snapshot.IsProjectDirty);
            Assert.Same(SomeTagHelpers, snapshot.TagHelpers);

            Assert.Equal(ProjectChangeKind.TagHelpersChanged, ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void ProjectUpdated_WhenDocumentsChanged_NotifiesListeners_AndDoesNotStartBackgroundWorker()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Generate the update
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var updateContext = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = SomeTagHelpers,
            };

            ProjectManager.HostProjectChanged(HostProjectWithDocumentAdded);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectUpdated(updateContext);

            // Assert
            snapshot = ProjectManager.GetSnapshot(HostProjectWithDocumentAdded);
            Assert.False(snapshot.IsProjectDirty);
            Assert.Same(SomeTagHelpers, snapshot.TagHelpers);

            Assert.Equal(ProjectChangeKind.TagHelpersChanged, ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void ProjectUpdated_WhenConfigurationChanged_RejectsUpdate()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Generate the update
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var updateContext = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = SomeTagHelpers,
            };

            ProjectManager.HostProjectChanged(HostProjectWithConfigurationChange);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectUpdated(updateContext);

            // Assert
            snapshot = ProjectManager.GetSnapshot(HostProjectWithConfigurationChange);
            Assert.True(snapshot.IsProjectDirty);
            Assert.Empty(snapshot.TagHelpers);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.True(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void ProjectUpdated_WorkspaceProjectChanged_NotifiesListeners_AndStartsBackgroundWorker()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Generate the update
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            var updateContext = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = SomeTagHelpers,
            };

            var project = WorkspaceProject.WithAssemblyName("Test1"); // Simulate a project change
            ProjectManager.WorkspaceProjectChanged(project);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectUpdated(updateContext);

            // Assert
            snapshot = ProjectManager.GetSnapshot(project);
            Assert.True(snapshot.IsProjectDirty);
            Assert.Same(snapshot.TagHelpers, SomeTagHelpers);

            Assert.Equal(ProjectChangeKind.TagHelpersChanged, ProjectManager.ListenersNotifiedOf);
            Assert.True(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void ProjectUpdated_WhenHostProjectRemoved_DiscardsUpdate()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Generate the update
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var updateContext = new ProjectSnapshotUpdateContext(snapshot);

            ProjectManager.HostProjectRemoved(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectUpdated(updateContext);

            // Assert
            snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.Null(snapshot);
        }

        [ForegroundFact]
        public void ProjectUpdated_WhenWorkspaceProjectRemoved_DiscardsUpdate()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Generate the update
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            var updateContext = new ProjectSnapshotUpdateContext(snapshot);

            ProjectManager.WorkspaceProjectRemoved(WorkspaceProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectUpdated(updateContext);

            // Assert
            snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.True(snapshot.IsProjectDirty);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectAdded_WithoutHostProject_IgnoresWorkspaceProject()
        {
            // Arrange

            // Act
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);

            // Assert
            Assert.Empty(ProjectManager.Projects);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectAdded_IgnoresNonCSharpProject()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.WorkspaceProjectAdded(VBWorkspaceProject);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.False(snapshot.IsInitialized);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectAdded_IgnoresSecondProjectWithSameFilePath()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.WorkspaceProjectAdded(WorkspaceProjectWithDifferentTfm);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.Same(WorkspaceProject, snapshot.WorkspaceProject);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectAdded_IgnoresProjectWithoutFilePath()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.WorkspaceProjectAdded(WorkspaceProjectWithoutFilePath);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.False(snapshot.IsInitialized);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectAdded_WithHostProject_NotifiesListenters_AndStartsBackgroundWorker()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.True(snapshot.IsProjectDirty);
            Assert.True(snapshot.IsInitialized);

            Assert.Equal(ProjectChangeKind.ProjectChanged, ProjectManager.ListenersNotifiedOf);
            Assert.True(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectChanged_WithoutHostProject_IgnoresWorkspaceProject()
        {
            // Arrange
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            var project = WorkspaceProject.WithAssemblyName("Test1"); // Simulate a project change

            // Act
            ProjectManager.WorkspaceProjectChanged(project);

            // Assert
            Assert.Empty(ProjectManager.Projects);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectChanged_IgnoresNonCSharpProject()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(VBWorkspaceProject);
            ProjectManager.Reset();

            var project = VBWorkspaceProject.WithAssemblyName("Test1"); // Simulate a project change

            // Act
            ProjectManager.WorkspaceProjectChanged(project);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.False(snapshot.IsInitialized);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectChanged_IgnoresProjectWithoutFilePath()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProjectWithoutFilePath);
            ProjectManager.Reset();

            var project = WorkspaceProjectWithoutFilePath.WithAssemblyName("Test1"); // Simulate a project change

            // Act
            ProjectManager.WorkspaceProjectChanged(project);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.False(snapshot.IsInitialized);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectChanged_IgnoresSecondProjectWithSameFilePath()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.WorkspaceProjectChanged(WorkspaceProjectWithDifferentTfm);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.Same(WorkspaceProject, snapshot.WorkspaceProject);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectChanged_SimulateProjectChange_AndTagHelperUpdate()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Generate the update
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var updateContext = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = SomeTagHelpers,
            };
            ProjectManager.ProjectUpdated(updateContext);
            ProjectManager.Reset();

            snapshot = ProjectManager.GetSnapshot(HostProject);
            var tagHelpers = snapshot.GetCurrentProjectEngine()
                .EngineFeatures
                .OfType<ITagHelperFeature>()
                .Single()
                .GetDescriptors();

            var project = WorkspaceProject.WithAssemblyName("Test1"); // Simulate a project change

            // Act - 1
            ProjectManager.WorkspaceProjectChanged(project);

            // Assert - 1
            snapshot = ProjectManager.GetSnapshot(project);
            Assert.True(snapshot.IsProjectDirty);
            Assert.Same(SomeTagHelpers, snapshot.TagHelpers);
            Assert.Same(SomeTagHelpers, tagHelpers);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.True(ProjectManager.WorkerStarted);

            // Act -2 
            updateContext = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = OtherTagHelpers,
            };
            ProjectManager.Reset();
            ProjectManager.ProjectUpdated(updateContext);

            snapshot = ProjectManager.GetSnapshot(HostProject);

            // Should trigger the update of the project engine
            tagHelpers = snapshot.GetCurrentProjectEngine()
                .EngineFeatures
                .OfType<ITagHelperFeature>()
                .Single()
                .GetDescriptors();

            // Assert - 2
            snapshot = ProjectManager.GetSnapshot(project);
            Assert.False(snapshot.IsProjectDirty);
            Assert.Same(OtherTagHelpers, snapshot.TagHelpers);
            Assert.Same(OtherTagHelpers, tagHelpers);

            Assert.Equal(ProjectChangeKind.TagHelpersChanged, ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectRemoved_WithHostProject_DoesNotRemoveProject_RemovesTagHelpers()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var update = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = SomeTagHelpers,
            };
            ProjectManager.ProjectUpdated(update);
            ProjectManager.Reset();

            // Act
            ProjectManager.WorkspaceProjectRemoved(WorkspaceProject);

            // Assert
            snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.True(snapshot.IsProjectDirty);
            Assert.False(snapshot.IsInitialized);
            Assert.Empty(snapshot.TagHelpers);

            Assert.Equal(ProjectChangeKind.ProjectChanged, ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectRemoved_WithHostProject_FallsBackToSecondProject()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var update = new ProjectSnapshotUpdateContext(snapshot)
            {
                TagHelpers = SomeTagHelpers,
            };
            ProjectManager.ProjectUpdated(update);
            ProjectManager.Reset();

            // Sets up a solution where the which has WorkspaceProjectWithDifferentTfm but not WorkspaceProject
            // This will enable us to fall back and find the WorkspaceProjectWithDifferentTfm 
            Assert.True(Workspace.TryApplyChanges(WorkspaceProjectWithDifferentTfm.Solution));

            // Act
            ProjectManager.WorkspaceProjectRemoved(WorkspaceProject);

            // Assert
            snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.True(snapshot.IsProjectDirty);
            Assert.True(snapshot.IsInitialized);
            Assert.Equal(WorkspaceProjectWithDifferentTfm.Id, snapshot.WorkspaceProject.Id);
            Assert.Same(SomeTagHelpers, snapshot.TagHelpers);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.True(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectRemoved_IgnoresSecondProjectWithSameFilePath()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.WorkspaceProjectRemoved(WorkspaceProjectWithDifferentTfm);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.Same(WorkspaceProject, snapshot.WorkspaceProject);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectRemoved_IgnoresNonCSharpProject()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(VBWorkspaceProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.WorkspaceProjectRemoved(VBWorkspaceProject);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.False(snapshot.IsInitialized);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectRemoved_IgnoresProjectWithoutFilePath()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);
            ProjectManager.WorkspaceProjectAdded(WorkspaceProjectWithoutFilePath);
            ProjectManager.Reset();

            // Act
            ProjectManager.WorkspaceProjectRemoved(WorkspaceProjectWithoutFilePath);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(WorkspaceProject);
            Assert.False(snapshot.IsInitialized);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        [ForegroundFact]
        public void WorkspaceProjectRemoved_IgnoresUnknownProject()
        {
            // Arrange

            // Act
            ProjectManager.WorkspaceProjectRemoved(WorkspaceProject);

            // Assert
            Assert.Empty(ProjectManager.Projects);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
            Assert.False(ProjectManager.WorkerStarted);
        }

        private class TestProjectSnapshotManager : DefaultProjectSnapshotManager
        {
            public TestProjectSnapshotManager(ForegroundDispatcher dispatcher, IEnumerable<ProjectSnapshotChangeTrigger> triggers, Workspace workspace)
                : base(dispatcher, Mock.Of<ErrorReporter>(), Mock.Of<ProjectSnapshotWorker>(), triggers, workspace)
            {
            }

            public ProjectChangeKind? ListenersNotifiedOf { get; private set; }

            public bool WorkerStarted { get; private set; }

            public DefaultProjectSnapshot GetSnapshot(HostProject hostProject)
            {
                return Projects.Cast<DefaultProjectSnapshot>().FirstOrDefault(s => s.FilePath == hostProject.FilePath);
            }

            public DefaultProjectSnapshot GetSnapshot(Project workspaceProject)
            {
                return Projects.Cast<DefaultProjectSnapshot>().FirstOrDefault(s => s.FilePath == workspaceProject.FilePath);
            }

            public void Reset()
            {
                ListenersNotifiedOf = null;
                WorkerStarted = false;
            }

            protected override void NotifyListeners(ProjectChangeEventArgs e)
            {
                ListenersNotifiedOf = e.Kind;
            }

            protected override void NotifyBackgroundWorker(ProjectSnapshotUpdateContext context)
            {
                Assert.NotNull(context.Snapshot);
                Assert.NotNull(context.Snapshot.WorkspaceProject);

                WorkerStarted = true;
            }
        }
    }
}
