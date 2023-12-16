using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace ChessUI;

public partial class MessageBox : UserControl
{
    public event Action<Option> OptionSelected;
    public MessageBox()
    {
        InitializeComponent();
    }
    private void OK_Click(object sender, RoutedEventArgs e)
    {
        OptionSelected?.Invoke(Option.OK);
    }
    
}