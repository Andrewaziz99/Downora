using System.Windows;
using System.Windows.Controls;
using Downora.Core.Enums;
using Wpf.Ui.Controls;

namespace Downora.App.Controls;

/// <summary>Rounded status badge; colors come from the Fluent Velocity status palette (see Styles.xaml).</summary>
public sealed class StatusPill : Control
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(DownloadStatus), typeof(StatusPill), new PropertyMetadata(DownloadStatus.Waiting));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(StatusPill), new PropertyMetadata(string.Empty));

    static StatusPill() =>
        DefaultStyleKeyProperty.OverrideMetadata(typeof(StatusPill), new FrameworkPropertyMetadata(typeof(StatusPill)));

    public DownloadStatus Status
    {
        get => (DownloadStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}

/// <summary>Icon, title and explanation for a screen with nothing to show; <see cref="ContentControl.Content"/> holds optional actions.</summary>
public sealed class EmptyState : ContentControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(SymbolRegular), typeof(EmptyState), new PropertyMetadata(SymbolRegular.Empty));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

    static EmptyState() =>
        DefaultStyleKeyProperty.OverrideMetadata(typeof(EmptyState), new FrameworkPropertyMetadata(typeof(EmptyState)));

    public SymbolRegular Icon
    {
        get => (SymbolRegular)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}

/// <summary>A dashboard number with label, hint and icon.</summary>
public sealed class StatCard : Control
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
        nameof(Hint), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(SymbolRegular), typeof(StatCard), new PropertyMetadata(SymbolRegular.Empty));

    static StatCard() =>
        DefaultStyleKeyProperty.OverrideMetadata(typeof(StatCard), new FrameworkPropertyMetadata(typeof(StatCard)));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public SymbolRegular Icon
    {
        get => (SymbolRegular)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
