using System;
using Gtk;

namespace DriveMirror
{
    public partial class ComboDialog : Gtk.Dialog
    {
        public string SelectedOption { get; private set; }
        public ComboDialog(string[] Options)
        {
            Build();

            SelectedOption = null;
            foreach (var Option in Options)
                ComboList.AppendText(Option);
        }

        protected void ComboChanged(object sender, EventArgs e)
        {
            SelectedOption = ComboList.ActiveText;
        }
    }
}
