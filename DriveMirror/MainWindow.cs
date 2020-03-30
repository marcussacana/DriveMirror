using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dasync.Collections;
using DriveMirror;
using Gtk;

public partial class MainWindow : Gtk.Window
{
    const string RED = "FF0000";
    const string GREEN = "00FF00";
    const string YELLOW = "E88700";

    public MainWindow() : base(Gtk.WindowType.Toplevel)
    {
        Build();

        bntDisconnect.Visible = false;
        bntCreateMirror.Visible = false;
        bntDebug.Visible = System.Diagnostics.Debugger.IsAttached;

        LeftNodeList.AppendColumn("Name", new CellRendererText(), "text", 0);
        RightNodeList.AppendColumn("Name", new CellRendererText(), "text", 0);

    }

    bool AConnected = false;
    bool BConnected = false;

    protected void OnConnectLeftClicked(object sender, EventArgs e)
    {
        SetStatus("Connecting...", YELLOW);
        if (!Server.ConnectA().GetAwaiter().GetResult()) {
            SetStatus("Failed", RED);
            return;
        }
        AConnected = true;
        bntChangeCredentials.Visible = false;
        bntConnectLeft.Visible = false;
        LeftSpliter.Sensitive = true;
        if (BConnected)
        {
            bntCreateMirror.Visible = true;
            bntDisconnect.Visible = true;
        }
        SetStatus("Connected", GREEN);
    }

    protected void OnConnectRigthClicked(object sender, EventArgs e)
    {
        SetStatus("Connecting...", YELLOW);
        if (!Server.ConnectB().GetAwaiter().GetResult()) {
            SetStatus("Failed", RED);
            return;
        }
        BConnected = true;
        bntChangeCredentials.Visible = false;
        bntConnectRigth.Visible = false;
        RigthSpliter.Sensitive = true;
        if (AConnected)
        {
            bntCreateMirror.Visible = true;
            bntDisconnect.Visible = true;
        }
        SetStatus("Connected", GREEN);
    }

    protected void OnDisconnectClicked(object sender, EventArgs e)
    {
        SetStatus("Disconnecting...", YELLOW);
        Server.Disconnect().Wait();
        bntDisconnect.Visible = false;
        bntConnectLeft.Visible = true;
        bntConnectRigth.Visible = true;
        AConnected = false;
        BConnected = false;
        bntChangeCredentials.Visible = true;
        bntCreateMirror.Visible = false;
        RigthSpliter.Sensitive = false;
        LeftSpliter.Sensitive = false;
        SetStatus("Disconnected", RED);
    }

    private void SetStatus(string Text, string Color)
    {
        Application.Invoke((a, s) => lblStatus.Markup = $"<span foreground=\"#{Color}\">{Text}</span>");
        DoEvents();
    }

    private ResponseType Message(string text, string title, MessageType MType, ButtonsType Buttons, bool Markup = false)
    {
        var Dialog = new MessageDialog(this, DialogFlags.DestroyWithParent, MType, Buttons, Markup, text);
        Dialog.Title = title;
        var Response = (ResponseType)Dialog.Run();
        Dialog.Destroy();
        return Response;
    }

    NodeStore LeftStore;
    protected void OnLeftOpenClicked(object sender, EventArgs e)
    {
        var DriveList = Server.EnumDrivesA().Result;
        var SelDrive = SelectDrive(DriveList);
        if (SelDrive == null)
            return;
        Server.SelectDriveA(SelDrive?.ID).Wait();

        LeftOpenFolder(LeftPathBox.Text).Wait();
    }

    private async Task LeftOpenFolder(string Path)
    {
        SetStatus("Openning Directory...", YELLOW);
        var BaseDir = await Server.ParsePathA(Path);
        var Folders = await Server.EnumFoldersA(BaseDir?.ID, false);
        SetStatus("Connected", GREEN);

        LeftStore = new NodeStore(typeof(NameTreeNode));
        if (Path.Trim(' ', '/', '\\', '\r', '\n') != string.Empty)
        {
            LeftStore.AddNode(new NameTreeNode("...", async () =>
            {
                int IndexOfLast = LeftPathBox.Text.TrimEnd('\\', '/').LastIndexOfAny(new char[] { '\\', '/' });
                if (IndexOfLast < 0)
                    return;
                LeftPathBox.Text = LeftPathBox.Text.Substring(0, IndexOfLast + 1);
                await LeftOpenFolder(LeftPathBox.Text);
            }));
        }
        foreach (var Folder in Folders)
        {
            LeftStore.AddNode(new NameTreeNode(Folder?.Name, async () =>
            {
                LeftPathBox.Text = Path + Folder?.Name + "/";
                await LeftOpenFolder(LeftPathBox.Text);
            }));
        }
        LeftNodeList.NodeStore = LeftStore;
    }

    NodeStore RigthStore;
    protected void OnRigthOpenClicked(object sender, EventArgs e)
    {
        var DriveList = Server.EnumDrivesB().Result;
        var SelDrive = SelectDrive(DriveList);
        if (SelDrive == null)
            return;
        Server.SelectDriveB(SelDrive?.ID).Wait();

        RigthOpenFolder(RigthPathBox.Text).Wait();
    }

    private async Task RigthOpenFolder(string Path)
    {
        SetStatus("Openning Directory...", YELLOW);
        var BaseDir = await Server.ParsePathB(Path);
        var Folders = await Server.EnumFoldersB(BaseDir?.ID, false);
        SetStatus("Connected", GREEN);

        RigthStore = new NodeStore(typeof(NameTreeNode));
        if (Path.Trim(' ', '/', '\\', '\r', '\n') != string.Empty)
        {
            RigthStore.AddNode(new NameTreeNode("...", async () =>
            {
                int IndexOfLast = RigthPathBox.Text.TrimEnd('\\', '/').LastIndexOfAny(new char[] { '\\', '/' });
                if (IndexOfLast < 0)
                    return;
                RigthPathBox.Text = RigthPathBox.Text.Substring(0, IndexOfLast + 1);
                await RigthOpenFolder(RigthPathBox.Text);
            }));
        }
        foreach (var Folder in Folders)
        {
            RigthStore.AddNode(new NameTreeNode(Folder?.Name, async () =>
            {
                RigthPathBox.Text = Path + Folder?.Name + "/";
                await RigthOpenFolder(RigthPathBox.Text);
            }));
        }
        RightNodeList.NodeStore = RigthStore;
    }

    private DriveInfo? SelectDrive(DriveInfo[] Drives)
    {
        if (Drives.Length > 1)
        {
            var Dlg = new ComboDialog((from x in Drives select x.Name).ToArray());
            var Rep = (ResponseType)Dlg.Run();
            Dlg.Destroy();
            if (Rep == ResponseType.Ok && Dlg.SelectedOption != null)
            {
                return (from x in Drives where x.Name == Dlg.SelectedOption select x).First();
            }
            return null;
        }

        return Drives.First();
    }

    protected void OnLeftRowActivated(object o, RowActivatedArgs args)
    {
        var SelectedNode = LeftStore.GetNode(args.Path);
        ((NameTreeNode)SelectedNode).OnClicked();
    }

    protected void OnRigthRowActivated(object o, RowActivatedArgs args)
    {
        var SelectedNode = RigthStore.GetNode(args.Path);
        ((NameTreeNode)SelectedNode).OnClicked();
    }

    protected override bool OnDeleteEvent(Gdk.Event ev)
    {
        if (!LeftSpliter.Sensitive && !RigthSpliter.Sensitive && !bntCreateMirror.Visible)
        {
            var Msg = Message("Save the progress?", "DriveMirror", MessageType.Question, ButtonsType.YesNo);
            if (Msg == ResponseType.Yes)
                MirrorWorker.SaveProgress();

        }

        Application.Quit();
        return true;
    }

    protected void OnLeftClickRelease(object o, ButtonReleaseEventArgs args)
    {
        if (args.Event.Button == 3)
        {
            Menu m = new Menu();
            MenuItem BntOption = new MenuItem("Create Folder");
            BntOption.ButtonPressEvent += (a, e) =>
            {
                var Dlg = new DriveMirror.InputDialog("Type the folder name");
                Dlg.Input = "New Folder";
                var Rst = (ResponseType)Dlg.Run();
                if (Rst == ResponseType.Ok)
                {
                    Server.CreateDirectoryA(LeftPathBox.Text.TrimEnd('\\', '/') + '/' + Dlg.Input).Wait();
                    LeftOpenFolder(LeftPathBox.Text).Wait();
                }
                Dlg.Destroy();
            };
            MenuItem BntListFiles = new MenuItem("List Files");
            BntListFiles.ButtonPressEvent += (a, e) =>
            {
                ListFilesA(LeftPathBox.Text).Wait();
            };
            var Clicked = GetTreeNodeByLocation(LeftNodeList, LeftStore, args.Event.X, args.Event.Y);
            if (Clicked != null && Clicked.Name != "...")
            {
                MenuItem BntExclude = new MenuItem("Exclude Folder");
                BntExclude.ButtonPressEvent += (a, e) =>
                {
                    var Rst = Message("You really want exclude this folder from of the sync?", "Are you Sure?", MessageType.Question, ButtonsType.YesNo, true);
                    if (Rst != ResponseType.Yes)
                        return;

                    MirrorWorker.ExcludeA.Add(LeftPathBox.Text.TrimEnd('\\', '/') + '/' + Clicked.Name);
                };
                m.Add(BntExclude);
            }
            m.Add(BntOption);
            m.Add(BntListFiles);
            m.ShowAll();
            m.Popup();
        }
    }

    protected void OnRigthClickRelease(object o, ButtonReleaseEventArgs args)
    {
        if (args.Event.Button == 3)
        {
            Menu m = new Menu();
            MenuItem BntCreate = new MenuItem("Create Folder");
            BntCreate.ButtonPressEvent += (a, e) =>
            {
                var Dlg = new DriveMirror.InputDialog("Type the folder name");
                Dlg.Input = "New Folder";
                var Rst = (ResponseType)Dlg.Run();
                if (Rst == ResponseType.Ok)
                {
                    Server.CreateDirectoryB(RigthPathBox.Text.TrimEnd('\\', '/') + '/' + Dlg.Input).Wait();
                    RigthOpenFolder(RigthPathBox.Text).Wait();
                }
                Dlg.Destroy();
            };
            MenuItem BntListFiles = new MenuItem("List Files");
            BntListFiles.ButtonPressEvent += (a, e) =>
            {
                ListFilesB(RigthPathBox.Text).Wait();
            };
            var Clicked = GetTreeNodeByLocation(RightNodeList, RigthStore, args.Event.X, args.Event.Y);
            if (Clicked != null && Clicked.Name != "...")
            {
                MenuItem BntExclude = new MenuItem("Exclude Folder");
                BntExclude.ButtonPressEvent += (a, e) =>
                {
                    var Rst = Message("You really want exclude this folder from of the sync?", "Are you Sure?", MessageType.Question, ButtonsType.YesNo, true);
                    if (Rst != ResponseType.Yes)
                        return;

                    MirrorWorker.ExcludeB.Add(RigthPathBox.Text.TrimEnd('\\', '/') + '/' + Clicked.Name);
                };
                m.Add(BntExclude);
            }
            m.Add(BntCreate);
            m.Add(BntListFiles);
            m.ShowAll();
            m.Popup();
        }
    }

    private NameTreeNode GetTreeNodeByLocation(NodeView View, NodeStore Store, double X, double Y)
    {
        View.GetPathAtPos((int)X, (int)Y, out TreePath Path);
        if (Path == null)
            return null;

        return (NameTreeNode)Store.GetNode(Path);
    }

    protected async void OnCreateMirrorClicked(object sender, EventArgs e)
    {
        bntCreateMirror.Visible = false;
        LeftSpliter.Sensitive = false;
        RigthSpliter.Sensitive = false;
        await Server.CreateMirror(LeftPathBox.Text, RigthPathBox.Text, (a) => SetStatus(a, YELLOW));
        LeftSpliter.Sensitive = true;
        RigthSpliter.Sensitive = true;
        bntCreateMirror.Visible = true;
        SetStatus("Connected", GREEN);
    }

    private async Task ListFilesA(string Directory)
    {
        var Dir = await Server.ParsePathA(Directory);
        var Files = (await Server.EnumFilesA(Dir, true)).ToList();
        ListFiles(Files);
    }
    private async Task ListFilesB(string Directory)
    {
        var Dir = await Server.ParsePathB(Directory);
        var Files = (await Server.EnumFilesB(Dir, true)).ToList();
        ListFiles(Files);
    }
    private void ListFiles(List<FileInfo?> Files)
    {
        Files = (from x in Files orderby x?.Name select x).ToList();
        do
        {
            const int Amount = 20;
            string MSG = string.Empty;
            int Reaming = Files.Count - Amount;
            foreach (var File in Files.Take(Reaming >= 0 ? Amount : Files.Count))
            {
                if (File.Value.Trashed)
                    MSG += $"<span foreground=\"#{RED}\">{File?.Name}</span>\n";
                else
                    MSG += $"{File?.Name}\n";
            }

            bool Ok = false;
            ResponseType Resp = ResponseType.None;
            Application.Invoke((s, arg) =>
            {
                Resp = Message(MSG, "Files in this Folder", MessageType.Other, ButtonsType.OkCancel, true);
                Ok = true;
            });

            while (!Ok)
            {
                DoEvents();
                System.Threading.Thread.Sleep(100);
            }

            if (Reaming <= 0 || Resp == ResponseType.Cancel)
                break;

            Files = Files.Skip(Amount).ToList();
        } while (Files.Count > 0);

    }

    public static void DoEvents()
    {
        while (GLib.MainContext.Iteration()) ;
    }

    protected void ChangeCredentialsClicked(object sender, EventArgs e)
    {
        FileChooserDialog Dialog = new FileChooserDialog("Select your new credential", this, FileChooserAction.Open);
        Dialog.Filter = new FileFilter();
        Dialog.SelectMultiple = false;
#if WINDOWS
        Dialog.Filter.AddPattern("*.json");
#else
        Dialog.Filter.AddMimeType("application/json");
#endif
        Dialog.AddButton("Cancel", ResponseType.Cancel);
        Dialog.AddButton("Open", ResponseType.Ok);
        var Result = (ResponseType)Dialog.Run();
        var File = Dialog.Filename;
        Dialog.Destroy();
        if (Result != ResponseType.Ok)
            return;

        System.IO.File.Copy(File, System.IO.Path.Combine(Server.AppDataDirectory, "credentials.json"), true);
    }
    protected async void DebugClicked(object sender, EventArgs e)
    {
        if (System.Diagnostics.Debugger.IsAttached) {
            Message("Deattach the debugger first.", "Can't Enable the Debug Mode", MessageType.Error, ButtonsType.Ok);
            return;
        }

        Server.Debug = true;
        await Server.CloseServer();
    }
}
