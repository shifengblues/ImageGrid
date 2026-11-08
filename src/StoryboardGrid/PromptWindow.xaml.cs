using System.Windows;
using System.Windows.Input;

namespace StoryboardGrid;
public partial class PromptWindow : Window
{
    public bool Accepted {get;private set;}
    public PromptWindow(string title,string message,string confirmText="知道了",bool confirmation=false)
    {
        InitializeComponent();ThemeManager.Register(this);Title=title;Heading.Text=title;Message.Text=message;ConfirmButton.Content=confirmText;
        CancelButton.Visibility=confirmation?Visibility.Visible:Visibility.Collapsed;
        CancelButton.IsDefault=confirmation;ConfirmButton.IsDefault=!confirmation;
    }
    internal void Accept(object sender,RoutedEventArgs e){Accepted=true;Close();}
    internal void Cancel(object sender,RoutedEventArgs e){Accepted=false;Close();}
    void DialogKeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Escape){Cancel(sender,e);e.Handled=true;}}
    void DragHeader(object sender,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Left&&e.ButtonState==MouseButtonState.Pressed)DragMove();}
    internal static bool Show(Window owner,string title,string message,string confirmText="知道了",bool confirmation=false)
    {
        var dialog=new PromptWindow(title,message,confirmText,confirmation){Owner=owner};dialog.ShowDialog();return dialog.Accepted;
    }
}
