using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TextCopy;

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
    public class ClipboardCodeReceiver : ICodeReceiver
    {
        public string RedirectUri
        {
            get { return GoogleAuthConsts.InstalledAppRedirectUri; }
        }

        public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl url, CancellationToken taskCancellationToken)
        {
            var authorizationUrl = url.Build().AbsoluteUri;
            await Clipboard.SetTextAsync("");

            Process.Start(authorizationUrl);

            string Response = null;
            while (Response == null || Response.Length != 57)
            {
                Response = await Clipboard.GetTextAsync();
                await Task.Delay(100);
            }


            return await Task.FromResult(new AuthorizationCodeResponseUrl { Code = Response });

        }
    }
}
