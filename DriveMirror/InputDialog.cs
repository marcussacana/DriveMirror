using System;
namespace DriveMirror
{
    public partial class InputDialog : Gtk.Dialog
    {
        public string Input { get => TBInput.Text; set => TBInput.Text = value; }

        public InputDialog(string Title)
        {
            this.Build();
            this.Title = Title;
        }
    }
}
