// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
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
    public class DefaultVisualStudioDocumentTrackerTest : ForegroundDispatcherTestBase
    {
        public DefaultVisualStudioDocumentTrackerTest()
        {
            EditorSettingsManager = new DefaultEditorSettingsManager(Mock.Of<ForegroundDispatcher>());
            FilePath = "C:/Some/Path/TestDocumentTracker.cshtml";
            ProjectPath = "C:/Some/Path/TestProject.csproj";
            RazorContentType = Mock.Of<IContentType>(c => c.IsOfType(RazorLanguage.ContentType) == true);
            TextBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == RazorContentType);

            TagHelperResolver = new TestTagHelperResolver();

            SomeTagHelpers = new List<TagHelperDescriptor>();
            SomeTagHelpers.Add(TagHelperDescriptorBuilder.Create("test", "test").Build());

            HostServices = TestServices.Create(
                new IWorkspaceService[] { },
                new ILanguageService[] { TagHelperResolver, });

            Workspace = TestWorkspace.Create(HostServices);
            ProjectManager = new TestProjectSnapshotManager(Workspace);

            HostProject = new HostProject(ProjectPath, FallbackRazorConfiguration.MVC_2_1);
            OtherHostProject = new HostProject(ProjectPath, FallbackRazorConfiguration.MVC_2_0);

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

        private HostServices HostServices { get; }

        private TestTagHelperResolver TagHelperResolver { get; }

        private Workspace Workspace { get; }

        private List<TagHelperDescriptor> SomeTagHelpers { get; }

        [ForegroundFact]
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

        [ForegroundFact]
        public void AddTextView_AddsToTextViewCollection()
        {
            // Arrange
            var textView = Mock.Of<ITextView>();

            // Act
            DocumentTracker.AddTextView(textView);

            // Assert
            Assert.Collection(DocumentTracker.TextViews, v => Assert.Same(v, textView));
        }

        [ForegroundFact]
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

        [ForegroundFact]
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

        [ForegroundFact]
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

        [ForegroundFact]
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

        [ForegroundFact]
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

        [ForegroundFact]
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

        [ForegroundFact]
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

        [ForegroundFact]
        public void Subscribed_InitializesEphemeralProjectSnapshot()
        {
            // Arrange
            var textView = Mock.Of<ITextView>();

            // Act
            DocumentTracker.AddTextView(textView);

            // Assert
            Assert.IsType<EphemeralProjectSnapshot>(DocumentTracker.ProjectSnapshot);
        }

        [ForegroundFact]
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

        [ForegroundFact]
        public async Task Subscribed_ListensToProjectChanges()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);

            var textView = Mock.Of<ITextView>();
            DocumentTracker.AddTextView(textView);

            await DocumentTracker.PendingTagHelperTask;

            // There can be multiple args here because the tag helpers will return
            // immediately and trigger another ContextChanged.
            List<ContextChangeEventArgs> args = new List<ContextChangeEventArgs>();
            DocumentTracker.ContextChanged += (sender, e) => { args.Add(e); };

            // Act
            ProjectManager.HostProjectChanged(OtherHostProject);
            await DocumentTracker.PendingTagHelperTask;

            // Assert
            var snapshot = Assert.IsType<DefaultProjectSnapshot>(DocumentTracker.ProjectSnapshot);

            Assert.Same(OtherHostProject, snapshot.HostProject);

            Assert.Collection(
                args,
                e => Assert.Equal(ContextChangeKind.ProjectChanged, e.Kind),
                e => Assert.Equal(ContextChangeKind.TagHelpersChanged, e.Kind));
        }

        [ForegroundFact]
        public async Task Subscribed_ListensToProjectRemoval()
        {
            // Arrange
            ProjectManager.HostProjectAdded(HostProject);

            var textView = Mock.Of<ITextView>();
            DocumentTracker.AddTextView(textView);

            await DocumentTracker.PendingTagHelperTask;

            List<ContextChangeEventArgs> args = new List<ContextChangeEventArgs>();
            DocumentTracker.ContextChanged += (sender, e) => { args.Add(e); };

            // Act
            ProjectManager.HostProjectRemoved(HostProject);
            await DocumentTracker.PendingTagHelperTask;

            // Assert
            Assert.IsType<EphemeralProjectSnapshot>(DocumentTracker.ProjectSnapshot);

            Assert.Collection(
                args,
                e => Assert.Equal(ContextChangeKind.ProjectChanged, e.Kind),
                e => Assert.Equal(ContextChangeKind.TagHelpersChanged, e.Kind));
        }

        [ForegroundFact]
        public async Task Subscribed_ListensToProjectChanges_ComputesTagHelpers()
        {
            // Arrange
            TagHelperResolver.CompletionSource = new TaskCompletionSource<TagHelperResolutionResult>();

            ProjectManager.HostProjectAdded(HostProject);

            var textView = Mock.Of<ITextView>();
            DocumentTracker.AddTextView(textView);

            // We haven't let the tag helpers complete yet
            Assert.False(DocumentTracker.PendingTagHelperTask.IsCompleted);
            Assert.Empty(DocumentTracker.TagHelpers);

            List<ContextChangeEventArgs> args = new List<ContextChangeEventArgs>();
            DocumentTracker.ContextChanged += (sender, e) => { args.Add(e); };

            // Act
            TagHelperResolver.CompletionSource.SetResult(new TagHelperResolutionResult(SomeTagHelpers, Array.Empty<RazorDiagnostic>()));
            await DocumentTracker.PendingTagHelperTask;

            // Assert
            Assert.Same(DocumentTracker.TagHelpers, SomeTagHelpers);

            Assert.Collection(
                args,
                e => Assert.Equal(ContextChangeKind.TagHelpersChanged, e.Kind));
        }

        private class TestProjectSnapshotManager : DefaultProjectSnapshotManager
        {
            public TestProjectSnapshotManager(Workspace workspace)
                : base(
                      Mock.Of<ForegroundDispatcher>(),
                      Mock.Of<ErrorReporter>(),
                      Enumerable.Empty<ProjectSnapshotChangeTrigger>(),
                      workspace)
            {
            }
        }
    }
}
