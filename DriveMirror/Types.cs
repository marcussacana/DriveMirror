namespace DriveMirror
{
    [Gtk.TreeNode(ListOnly = true)]
    public class NameTreeNode : Gtk.TreeNode
    {
        public System.Action OnClicked { get; private set; }
        public NameTreeNode(string Name, System.Action OnClicked)
        {
            this.Name = Name;
            this.OnClicked = OnClicked;
        }

        [Gtk.TreeNodeValue(Column = 0)]
        public string Name;
    }

}
