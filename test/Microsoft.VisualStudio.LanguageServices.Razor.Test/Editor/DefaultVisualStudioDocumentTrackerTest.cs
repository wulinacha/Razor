// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Editor
{
    public class DefaultVisualStudioDocumentTrackerTest
    {
        public DefaultVisualStudioDocumentTrackerTest()
        {
            EditorSettingsManager = new DefaultEditorSettingsManager(Mock.Of<ForegroundDispatcher>());
            FilePath = "C:/Some/Path/TestDocumentTracker.cshtml";
            ProjectPath = "C:/Some/Path/TestProject.csproj";
            RazorContentType = Mock.Of<IContentType>(c => c.IsOfType(RazorLanguage.ContentType) == true);
            TextBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == RazorContentType);

            Workspace = TestWorkspace.Create();
            ProjectManager = new TestProjectSnapshotManager(Workspace);

            HostProject = new HostProject(ProjectPath, FallbackRazorConfiguration.MVC_2_1, Array.Empty<RazorDocument>());
            OtherHostProject = new HostProject(ProjectPath, FallbackRazorConfiguration.MVC_2_0, Array.Empty<RazorDocument>());

            ProjectService = Mock.Of<TextBufferProjectService>(s =>
                s.GetHostProject(It.IsAny<ITextBuffer>()) == Mock.Of<IVsHierarchy>() &&
                s.IsSupportedProject(It.IsAny<IVsHierarchy>()) == true &&
                s.GetProjectPath(It.IsAny<IVsHierarchy>()) == ProjectPath);

            DocumentTracker = new DefaultVisualStudioDocumentTracker(FilePath, ProjectManager, ProjectService, EditorSettingsManager, Workspace, TextBuffer);
        }

        private DefaultVisualStudioDocumentTracker DocumentTracker { get; }

        private IContentType RazorContentType { get; }

        private ITextBuffer TextBuffer { get; }

        private string FilePath { get; }

        private HostProject HostProject { get; }

        private HostProject OtherHostProject { get; }

        private TestProjectSnapshotManager ProjectManager { get; }

        private string ProjectPath { get; }

        private TextBufferProjectService ProjectService { get; }

        private EditorSettingsManager EditorSettingsManager { get; }

        private Workspace Workspace { get; }

        [Fact]
        public void EditorSettingsManager_Changed_TriggersContextChanged()
        {
            // Arrange
            
            var called = false;
            DocumentTracker.ContextChanged += (sender, args) =>
            {
                called = true;
            };

            // Act
            DocumentTracker.EditorSettingsManager_Changed(null, null);

            // Assert
            Assert.True(called);
        }

        [Fact]
        public void AddTextView_AddsToTextViewCollection()
        {
            // Arrange
            var textView = Mock.Of<ITextView>();

            // Act
            DocumentTracker.AddTextView(textView);

            // Assert
            Assert.Collection(DocumentTracker.TextViews, v => Assert.Same(v, textView));
        }

        [Fact]
        public void AddTextView_SubscribesAfterFirstTextViewAdded()
        {
            // Arrange
            var textView = Mock.Of<ITextView>();

            // Assert - 1
            Assert.False(DocumentTracker.IsSupportedProject);

            // Act
            DocumentTracker.AddTextView(textView);

            // Assert - 2
            Assert.True(DocumentTracker.IsSupportedProject);
        }

        [Fact]
        public void AddTextView_DoesNotAddDuplicateTextViews()
        {
            // Arrange
            var textView = Mock.Of<ITextView>();

            // Act
            DocumentTracker.AddTextView(textView);
            DocumentTracker.AddTextView(textView);

            // Assert
            Assert.Collection(DocumentTracker.TextViews, v => Assert.Same(v, textView));
        }

        [Fact]
        public void AddTextView_AddsMultipleTextViewsToCollection()
        {
            // Arrange
            var textView1 = Mock.Of<ITextView>();
            var textView2 = Mock.Of<ITextView>();

            // Act
            DocumentTracker.AddTextView(textView1);
            DocumentTracker.AddTextView(textView2);

            // Assert
            Assert.Collection(
                DocumentTracker.TextViews,
                v => Assert.Same(v, textView1),
                v => Assert.Same(v, textView2));
        }

        [Fact]
        public void RemoveTextView_RemovesTextViewFromCollection_SingleItem()
        {
            // Arrange
            var textView = Mock.Of<ITextView>();
            DocumentTracker.AddTextView(textView);

            // Act
            DocumentTracker.RemoveTextView(textView);

            // Assert
            Assert.Empty(DocumentTracker.TextViews);
        }

        [Fact]
        public void RemoveTextView_RemovesTextViewFromCollection_MultipleItems()
        {
            // Arrange
            var textView1 = Mock.Of<ITextView>();
            var textView2 = Mock.Of<ITextView>();
            var textView3 = Mock.Of<ITextView>();
            DocumentTracker.AddTextView(textView1);
            DocumentTracker.AddTextView(textView2);
            DocumentTracker.AddTextView(textView3);

            // Act
            DocumentTracker.RemoveTextView(textView2);

            // Assert
            Assert.Collection(
                DocumentTracker.TextViews,
                v => Assert.Same(v, textView1),
                v => Assert.Same(v, textView3));
        }

        [Fact]
        public void RemoveTextView_NoopsWhenRemovingTextViewNotInCollection()
        {
            // Arrange
            var textView1 = Mock.Of<ITextView>();
            DocumentTracker.AddTextView(textView1);
            var textView2 = Mock.Of<ITextView>();

            // Act
            DocumentTracker.RemoveTextView(textView2);

            // Assert
            Assert.Collection(DocumentTracker.TextViews, v => Assert.Same(v, textView1));
        }

        [Fact]
        public void RemoveTextView_UnsubscribesAfterLastTextViewRemoved()
        {
            // Arrange
            var textView1 = Mock.Of<ITextView>();
            var textView2 = Mock.Of<ITextView>();
            DocumentTracker.AddTextView(textView1);
            DocumentTracker.AddTextView(textView2);

            // Act - 1
            DocumentTracker.RemoveTextView(textView1);

            // Assert - 1
            Assert.True(DocumentTracker.IsSupportedProject);

            // Act - 2
            DocumentTracker.RemoveTextView(textView2);

            // Assert - 2
            Assert.False(DocumentTracker.IsSupportedProject);
        }

        [Fact]
        public void Subscribed_InitializesEphemeralProjectSnapshot()
        {
            // Arrange
            var textView = Mock.Of<ITextView>();

            // Act
            DocumentTracker.AddTextView(textView);

            // Assert
            Assert.IsType<EphemeralProjectSnapshot>(DocumentTracker.ProjectSnapshot);
        }

        [Fact]
        public void Subscribed_InitializesRealProjectSnapshot()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);

            var textView = Mock.Of<ITextView>();

            // Act
            DocumentTracker.AddTextView(textView);

            // Assert
            Assert.IsType<DefaultProjectSnapshot>(DocumentTracker.ProjectSnapshot);
        }

        [Fact]
        public void Subscribed_ListensToProjectChanges()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);

            var textView = Mock.Of<ITextView>();
            DocumentTracker.AddTextView(textView);

            ContextChangeEventArgs args = null;
            DocumentTracker.ContextChanged += (sender, e) => { args = e; };

            // Act
            ProjectManager.HostProjectChanged(OtherHostProject);

            // Assert
            var snapshot = Assert.IsType<DefaultProjectSnapshot>(DocumentTracker.ProjectSnapshot);
            Assert.Same(OtherHostProject, snapshot.HostProject);

            Assert.Equal(ContextChangeKind.ProjectChanged, args.Kind);
        }

        [Fact]
        public void Subscribed_ListensToProjectRemoval()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);

            var textView = Mock.Of<ITextView>();
            DocumentTracker.AddTextView(textView);

            ContextChangeEventArgs args = null;
            DocumentTracker.ContextChanged += (sender, e) => { args = e; };

            // Act
            ProjectManager.HostProjectRemoved(HostProject);

            // Assert
            Assert.IsType<EphemeralProjectSnapshot>(DocumentTracker.ProjectSnapshot);

            Assert.Equal(ContextChangeKind.ProjectChanged, args.Kind);
        }

        private class TestProjectSnapshotManager : DefaultProjectSnapshotManager
        {
            public TestProjectSnapshotManager(Workspace workspace)
                : base(
                      Mock.Of<ForegroundDispatcher>(),
                      Mock.Of<ErrorReporter>(),
                      Mock.Of<ProjectSnapshotWorker>(),
                      Enumerable.Empty<ProjectSnapshotChangeTrigger>(),
                      workspace)
            {
            }
        }
    }
}
