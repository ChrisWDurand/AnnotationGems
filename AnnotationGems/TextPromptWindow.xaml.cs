using System.Windows;

namespace AnnotationGems;

public partial class TextPromptWindow : Window
{
    public string? ResultText { get; private set; }

    public TextPromptWindow()
    {
        InitializeComponent();
    }

    public static string? Show(Window owner, string prompt, string title)
    {
        var w = new TextPromptWindow
        {
            Owner = owner,
            Title = title
        };
        w.PromptText.Text = prompt;
        w.InputBox.Focus();

        var ok = w.ShowDialog();
        return ok == true ? w.ResultText : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResultText = InputBox.Text;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
