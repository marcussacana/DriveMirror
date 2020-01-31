using System.Linq;
using System.Collections.Generic;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using System.Threading.Tasks;
using Dasync.Collections;
using System;

namespace DriveMirror
{
    public static class DriveFileManager
    {
        const string GoogleFolderMimeType = "application/vnd.google-apps.folder";

        internal static async Task<File> GetFileById(this DriveService Service, string FileId) {
            var Request = Service.Files.Get(FileId);
            Request.SupportsAllDrives = true;
            Request.Fields = "*";
            while (true)
            {
                try
                {
                    return await Request.ExecuteAsync();
                }
                catch (Exception ex)
                {
                    if (ex.IsRemoteException())
                    {
                        await Task.Delay(1000 * 10);//10 sec
                        continue;
                    }
                    throw;
                }
            }
        }

        internal static bool IsDirectory(this File File) => File.MimeType == GoogleFolderMimeType;

        internal static IAsyncEnumerable<File> EnumFiles(this DriveService Service, string Parent = null, Drive SharedDrive = null, bool Metadata = false, bool ShowDeleted = false) =>
            Service.EnumFiles(new File() { Id = Parent }, SharedDrive, Metadata, ShowDeleted);

        internal static IAsyncEnumerable<File> EnumFiles(this DriveService Service, File Parent = null, Drive SharedDrive = null, bool Metadata = false, bool ShowDeleted = false)
        {
            return new AsyncEnumerable<File>(async yield =>
            {
                var Request = Service.Files.List();
                Request.PageSize = 100;

                string ParentID = Parent?.Id ?? "root";
                if (SharedDrive?.Id != null)
                {
                    if (ParentID == "root")
                        ParentID = SharedDrive.Id;

                    Request.Corpora = "drive";
                    Request.DriveId = SharedDrive.Id;
                    Request.SupportsAllDrives = true;
                    Request.IncludeItemsFromAllDrives = true;
                }
                Request.Q = $"mimeType != '{GoogleFolderMimeType}' and '{ParentID}' in parents";

                string Fields = "id,name,parents,mimeType,trashed";

                if (Metadata)
                    Fields += ",md5Checksum,modifiedTime";

                Request.Fields = $"nextPageToken,files({Fields})";

                if (SharedDrive != null)
                    Request.DriveId = SharedDrive.Id;

                while (true)
                {
                    FileList Result;

                    try
                    {
                        Result = await Request.ExecuteAsync();
                    }
                    catch (Exception ex) {
                        if (ex.IsRemoteException()) {
                            await Task.Delay(1000 * 10);//10 sec
                            continue;
                        }
                        throw;
                    }

                    foreach (var File in Result.Files)
                        if (ShowDeleted || !File.Trashed.Value)
                            await yield.ReturnAsync(File);
                    if (Result.NextPageToken == null)
                        break;
                    Request.PageToken = Result.NextPageToken;
                }
            });
        }
        internal static IAsyncEnumerable<File> EnumFolders(this DriveService Service, string Parent = null, Drive SharedDrive = null, bool Metadata = false, bool ShowDeleted = false) =>
            Service.EnumFolders(new File() { Id = Parent }, SharedDrive, Metadata, ShowDeleted);
        internal static IAsyncEnumerable<File> EnumFolders(this DriveService Service, File Parent = null, Drive SharedDrive = null, bool Metadata = false, bool ShowDeleted = false)
        {
            return new AsyncEnumerable<File>(async yield =>
            {
                var Request = Service.Files.List();
                Request.PageSize = 90;

                string ParentID = Parent?.Id ?? "root";
                if (SharedDrive?.Id != null)
                {
                    if (ParentID == "root")
                        ParentID = SharedDrive.Id;

                    Request.Corpora = "drive";
                    Request.DriveId = SharedDrive.Id;
                    Request.SupportsAllDrives = true;
                    Request.IncludeItemsFromAllDrives = true;
                }

                Request.Q = $"mimeType = '{GoogleFolderMimeType}' and '{ParentID}' in parents";

                string Fields = "id,name,parents,mimeType,trashed";

                if (Metadata)
                    Fields += ",md5Checksum,modifiedTime";

                Request.Fields = $"nextPageToken,files({Fields})";

                while (true)
                {
                    FileList Result;

                    try
                    {
                        Result = await Request.ExecuteAsync();
                    }
                    catch (Exception ex)
                    {
                        if (ex.IsRemoteException())
                        {
                            await Task.Delay(1000 * 10);//10 sec
                            continue;
                        }
                        throw;
                    }

                    foreach (var File in Result.Files)
                        if (ShowDeleted || !File.Trashed.Value)
                            await yield.ReturnAsync(File);
                    if (Result.NextPageToken == null)
                        break;
                    Request.PageToken = Result.NextPageToken;
                }
            });
        }

        internal static IAsyncEnumerable<Drive> EnumDrives(this DriveService Service) {
            return new AsyncEnumerable<Drive>(async yield =>
            {
                var Request = Service.Drives.List();
                Request.PageSize = 10;
                Request.Fields = "nextPageToken, drives";
                await yield.ReturnAsync(new Drive()
                {
                    Id = null,
                    Name = "My Drive"
                });
                while (true)
                {
                    DriveList Result;

                    try
                    {
                        Result = await Request.ExecuteAsync();
                    }
                    catch (Exception ex)
                    {
                        if (ex.IsRemoteException())
                        {
                            await Task.Delay(1000 * 10);//10 sec
                            continue;
                        }
                        throw;
                    }

                    foreach (var Drive in Result.Drives)
                        await yield.ReturnAsync(Drive);
                    if (Result.NextPageToken == null)
                        break;
                    Request.PageToken = Result.NextPageToken;
                }
            });
        }

        internal static async Task<File> CopyFile(this DriveService Service, string FromId, string OriginalName = null, string NewDirectory = null) =>
            await Service.CopyFile(FromId, OriginalName, new File() { Id = NewDirectory });

        internal static async Task<File> CopyFile(this DriveService Service, string FromId, string OriginalName = null, File NewDirectory = null) {
            File SaveAs = new File();
            SaveAs.Name = OriginalName ?? (await Service.GetFileById(FromId)).Name;

            if (NewDirectory != null)
            {
                SaveAs.Parents = new List<string>();
                SaveAs.Parents.Add(NewDirectory.Id);
            }

            var Request = Service.Files.Copy(SaveAs, FromId);
            Request.SupportsAllDrives = true;
            Request.Fields = "*";

            while (true)
            {
                try
                {
                    return await Request.ExecuteAsync();
                }
                catch (Exception ex)
                {
                    if (ex.IsRemoteException())
                    {
                        await Task.Delay(1000 * 10);//10 sec
                        continue;
                    }
                    throw;
                }
            }
        }

        internal static async Task<File> MoveFile(this DriveService Service, File File, File Directory) {
            var NFile = new File();
            NFile.Name = File.Name;
            var Request = Service.Files.Update(NFile, File.Id);
            Request.SupportsAllDrives = true;
            Request.Fields = "*";
            Request.AddParents = Directory.Id;
            Request.RemoveParents = string.Join(",", File.Parents);

            while (true)
            {
                try
                {
                    return await Request.ExecuteAsync();
                }
                catch (Exception ex)
                {
                    if (ex.IsRemoteException())
                    {
                        await Task.Delay(1000 * 10);//10 sec
                        continue;
                    }
                    throw;
                }
            }
        }

        internal static async Task<File> ShareFile(this DriveService Service, string FileID, DriveService AllowTo = null) =>
            await Service.ShareFile(new File() { Id = FileID }, AllowTo);

        internal static async Task<File> ShareFile(this DriveService Service, File File, DriveService AllowTo = null)
        {
            var Request = Service.Permissions.Create(new Permission() {
                Role = "reader",
                Type = AllowTo == null ? "anyone" : "user",
                EmailAddress = AllowTo == null ? null : (await AllowTo.GetUserInfo()).EmailAddress
            }, File.Id);

            Request.SupportsAllDrives = true;
            Permission Rst;

            while (true)
            {
                try
                {
                    Rst = await Request.ExecuteAsync();
                    File.Permissions = new List<Permission>();
                    File.Permissions.Add(Rst);
                    return File;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    if (ex.IsRemoteException())
                    {
                        await Task.Delay(1000 * 10);//10 sec
                        continue;
                    }
                    throw;
                }
            }

        }

        internal static async Task<File> StopSharing(this DriveService Service, File File) {
            var Perm = (from x in File.Permissions where x.Type == "anyone" select x).Single();
            var PRequest = Service.Permissions.Delete(File.Id, Perm.Id);
            PRequest.SupportsAllDrives = true;
            PRequest.Fields = "*";
            await PRequest.ExecuteAsync();

            while (true)
            {
                try
                {
                    await PRequest.ExecuteAsync();
                    break;
                }
                catch (Exception ex)
                {
                    if (ex.IsRemoteException())
                    {
                        await Task.Delay(1000 * 10);//10 sec
                        continue;
                    }
                    throw;
                }
            }

            return await Service.GetFileById(File.Id);
        }


        internal static async Task Delete(this DriveService Service, string FileID) =>
            await Service.Delete(new File() { Id = FileID });

        internal static async Task Delete(this DriveService Service, File File) {
            var Request = Service.Files.Delete(File.Id);
            Request.SupportsAllDrives = true;
            while (true)
            {
                try
                {
                    await Request.ExecuteAsync();
                    return;
                }
                catch (Exception ex)
                {
                    if (ex.IsRemoteException())
                    {
                        await Task.Delay(1000 * 10);//10 sec
                        continue;
                    }
                    throw;
                }
            }
        }

        static readonly char[] PathSplitChars = new char[] { '/', '\\' };
        
        internal static async Task<File> CreateDirectory(this DriveService Service, string DirectoryPath, Drive Drive = null) {
            DirectoryPath = DirectoryPath.TrimEnd(PathSplitChars);
            string DirName = DirectoryPath.Split(PathSplitChars).Last();
            DirectoryPath = DirectoryPath.Substring(0, DirectoryPath.LastIndexOfAny(PathSplitChars) + 1);

            var Parent = await Service.TranslatePath(DirectoryPath, Drive);
            if (Parent == null)
                Parent = await Service.CreateDirectory(DirectoryPath, Drive);

            File NewDir = new File();
            NewDir.Name = DirName;
            NewDir.MimeType = GoogleFolderMimeType;
            NewDir.Parents = new List<string>();
            NewDir.Parents.Add(Parent.Id);

            var Request = Service.Files.Create(NewDir);
            Request.Fields = "id";
            Request.SupportsAllDrives = true;
            File Rst;

            while (true)
            {
                try
                {
                    Rst = await Request.ExecuteAsync();
                    break;
                }
                catch (Exception ex)
                {
                    if (ex.IsRemoteException())
                    {
                        await Task.Delay(1000 * 10);//10 sec
                        continue;
                    }
                    throw;
                }
            }

            return await Service.GetFileById(Rst.Id);
        }

        internal static async Task<User> GetUserInfo(this DriveService Service) {
            var Request = Service.About.Get();
            Request.Fields = "*";

            while (true)
            {
                try
                {
                    var Rst = await Request.ExecuteAsync();
                    return Rst.User;
                }
                catch (Exception ex)
                {
                    if (ex.IsRemoteException())
                    {
                        await Task.Delay(1000 * 10);//10 sec
                        continue;
                    }
                    throw;
                }
            }
        }

        internal static async Task<File> TranslatePath(this DriveService Service, string fPath, Drive Drive = null, bool Metadata = false) {
            var Queries = (from x in QueryCache where x.Service == Service && x.Drive == Drive && x.Path == fPath select x).ToArray();
            if (Queries.Length == 1)
                return Queries.Single().Result;

            string BaseID = Drive?.Id ?? "root";
            var CurParent = await Service.GetFileById(BaseID);

            string FullPath = fPath.Trim(System.IO.Path.GetInvalidPathChars());
            string[] Path = FullPath.Trim(PathSplitChars).Split(PathSplitChars);

            bool IsDir = FullPath.EndsWith("\\") || FullPath.EndsWith("/");
            if (Path.Length == 1 && Path[0] == string.Empty)
                return CurParent;

            int Begin = 0;
            for (int i = Path.Length; i >= 0; i--) {
                string PartialPath = string.Join("/", Path.Take(i));
                var Rst = (from x in QueryCache where x.Service == Service && x.Drive == Drive && x.Path.Trim(PathSplitChars) == PartialPath select x).ToArray();
                if (Rst.Length == 1) {
                    Begin = i;
                    CurParent = Rst.Single().Result;
                    break;
                }
            }

            for (int i = Begin; i < Path.Length; i++) {
                string Name = Path[i];
                bool IsLast = i + 1 >= Path.Length;
                if (!IsLast || IsDir) {
                    var Dirs = await Service.EnumFolders(CurParent, Drive).ToListAsync();
                    CurParent = null;
                    foreach (var Dir in Dirs) {
                        if (Dir.Name == Name) {
                            CurParent = Dir;
                            break;
                        }
                    }

                    if (CurParent == null)
                        return null;
                    continue;
                }
                var Files = await Service.EnumFiles(CurParent, Drive, Metadata).ToListAsync();
                CurParent = null;
                foreach (var File in Files)
                {
                    if (File.Name == Name)
                    {
                        CurParent = File;
                        break;
                    }
                }
                if (CurParent == null)
                    return null;

                break;
            }

            QueryCache.Add(new QueryInfo()
            {
                Service = Service,
                Drive = Drive,
                Path = fPath,
                Result = CurParent
            });

            return CurParent;
        }

        internal static bool IsRemoteException(this Exception ex) {
            if (ex.Message.Contains("Internal Error [500]"))
                return true;
            if (ex.Message.Contains("User Rate"))
                return true;
            if (ex.Message.Contains("The SSL connection"))
                return true;
            if (ex.Message.Contains("Insufficient Memory"))
                return true;
            if (ex.StackTrace.Contains("http"))
                return true;
            if (ex is AggregateException) {
                var Exceptions = ((AggregateException)ex).InnerExceptions;
                foreach (var exp in Exceptions)
                    if (IsRemoteException(exp))
                        return true;
                return false;
            }
            return false;
        }

        static List<QueryInfo> QueryCache = new List<QueryInfo>();

        struct QueryInfo
        {
            public DriveService Service;
            public Drive Drive;
            public string Path;
            public File Result;
        }
    }
}
