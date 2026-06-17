using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using SharpTimer.App.Services;
using SharpTimer.App.ViewModels;
using SharpTimer.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SharpTimer.App.Views.Solving;

public sealed partial class SolvesView : UserControl
{
    private bool _isRendering;

    public SolvesView()
    {
        InitializeComponent();
    }

    public event EventHandler<SessionListItem>? SessionChanged;
    public event EventHandler? NewSessionRequested;
    public event EventHandler? RenameSessionRequested;
    public event EventHandler? ArchiveSessionRequested;
    public event EventHandler<SolveListItem>? SolveClicked;

    public object? SolvesItemsSource
    {
        get => SolvesList.ItemsSource;
        set => SolvesList.ItemsSource = value;
    }

    public object? SessionsItemsSource
    {
        get => SessionComboBox.ItemsSource;
        set => SessionComboBox.ItemsSource = value;
    }

    public SessionListItem? SelectedSession
    {
        get => SessionComboBox.SelectedItem as SessionListItem;
        set => SessionComboBox.SelectedItem = value;
    }

    public SolveListItem? SelectedSolve
    {
        get => SolvesList.SelectedItem as SolveListItem;
        set => SolvesList.SelectedItem = value;
    }

    public Guid? SelectedSolveId => SelectedSolve?.Id;

    public void BeginRender()
    {
        _isRendering = true;
    }

    public void EndRender()
    {
        _isRendering = false;
    }

    public void UpdateCount(string count)
    {
        CountText.Text = count;
    }

    public void UpdateAnalysis(
        string best,
        string worst,
        string mean,
        string completed,
        IEnumerable<Solve> solves,
        int decimalPlaces)
    {
        AnalysisBestText.Text = best;
        AnalysisWorstText.Text = worst;
        AnalysisMeanText.Text = mean;
        AnalysisCompletedText.Text = completed;
        SolveAnalysisChart.SetSolves(solves, decimalPlaces);
    }

    public void SetEmptyStateVisible(bool isVisible)
    {
        EmptySolvesPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ApplyLanguage(LocalizedStrings strings)
    {
        ToolTipService.SetToolTip(SessionActionsButton, strings.SessionActions);
        AutomationProperties.SetName(SessionActionsButton, strings.SessionActions);
        RenameSessionMenuItem.Text = strings.RenameSession;
        NewSessionMenuItem.Text = strings.NewSession;
        ArchiveSessionMenuItem.Text = strings.Delete;
        TimeColumnText.Text = strings.TimeColumn;
        AnalysisBestLabelText.Text = strings.BestLabel;
        AnalysisWorstLabelText.Text = strings.WorstLabel;
        AnalysisMeanLabelText.Text = strings.MeanLabel;
        AnalysisCompletedLabelText.Text = strings.CompletedCountLabel;
        SolveAnalysisChart.SetText(strings.SolveTrendTitle, strings.SolveDistributionTitle, strings.SolveChartEmptyText);
        EmptySolvesTitleText.Text = strings.EmptySolvesTitle;
        EmptySolvesDescriptionText.Text = strings.EmptySolvesDescription;
    }

    private void SessionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRendering || SessionComboBox.SelectedItem is not SessionListItem item)
        {
            return;
        }

        SessionChanged?.Invoke(this, item);
    }

    private void NewSessionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        NewSessionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RenameSessionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RenameSessionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ArchiveSessionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ArchiveSessionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SolvesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SolveListItem item)
        {
            SolvesList.SelectedItem = item;
            SolveClicked?.Invoke(this, item);
        }
    }
}
