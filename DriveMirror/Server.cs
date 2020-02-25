using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dasync.Collections;
using DriveMirror;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Gtk;

public static class Server {

    public enum Commands : ushort { 
        PING,
        ConnectA,
        ConnectB,
        Disconnect,
        EnumDrivesA,
        EnumDrivesB,
        GetFileByIdA,
        GetFileByIdB,
        ParsePathA,
        ParsePathB,
        EnumFilesA,
        EnumFilesB,
        EnumFoldersA,
        EnumFoldersB,
        CopyFileA,
        CopyFileB,
        DeleteFileA,
        DeleteFileB,
        CreateDirectoryA,
        CreateDirectoryB,
        SelectDriveA,
        SelectDriveB,
        InterAccounts,
        ShareFileA,
        ShareFileB,
        StopShareA,
        StopShareB,
        CloseServer
    }

    public const string ServerName = "DriveMirror.Pipe.";
    public static string ServerInstance = null;


    public const string UserA = "DriveMirror.A";
    public const string UserB = "DriveMirror.B";

    static ClientSecrets ApiCredentials;
    static UserCredential CredentialsA;
    static UserCredential CredentialsB;
    static DriveService ServiceA;
    static DriveService ServiceB;

    static User UserInfoA;
    static User UserInfoB;

    public static Drive DriveA;
    public static Drive DriveB;

    static string[] Scopes = {
        DriveService.Scope.Drive,
        DriveService.Scope.DriveAppdata,
        DriveService.Scope.DriveMetadata
    };

    public static string CurrentDirectory
    {
        get
        {
            var AppImg = Environment.GetEnvironmentVariable("APPIMAGE");
            if (!string.IsNullOrWhiteSpace(AppImg))
                return Path.GetDirectoryName(AppImg);
            return Environment.CurrentDirectory;
        }

    }

    public static string AppDirectory
    {
        get {
            var AppDir = Environment.GetEnvironmentVariable("APPDIR");
            if (!string.IsNullOrWhiteSpace(AppDir))
                return AppDir;
            return Environment.CurrentDirectory;
        }

    }

    public static string CurrentExecutable
    {
        get
        {
            var AppImg = Environment.GetEnvironmentVariable("APPIMAGE");
            if (!string.IsNullOrWhiteSpace(AppImg))
                return AppImg;
            return Assembly.GetExecutingAssembly().Location;
        }

    }

    public static string AppDataDirectory
    {
        get {
            string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            AppData = Path.Combine(AppData, "DriveMirror");
            if (!Directory.Exists(AppData))
                Directory.CreateDirectory(AppData);
            return AppData;
        }
    }

    public async static Task OpenServer()
    {
#if DEBUG
        System.Diagnostics.Debugger.Launch();
#endif
        ApiCredentials = new ClientSecrets();
        string CustomCredentials = Path.Combine(AppDataDirectory, "credentials.json");

        if (System.IO.File.Exists(CustomCredentials))
        {
            using (var Stream = new MemoryStream(System.IO.File.ReadAllBytes(CustomCredentials)))
                ApiCredentials = GoogleClientSecrets.Load(Stream).Secrets;
        }
        else
        {
            ApiCredentials.ClientId = "1028112365169-3iss4ffrp626a31lc96pn3c3cujkbdg2.apps.googleusercontent.com";
            ApiCredentials.ClientSecret = "lDb5KUCJt6gLrOtHNScZ6QYm";
        }

        if (ServerInstance == null) {
            Message("Null server instance", "dbg", MessageType.Other, ButtonsType.Ok);
            return;
        }

        using (NamedPipeServerStream Server = new NamedPipeServerStream(ServerName + (ServerInstance ?? "GLOBAL")))
        {
            while (true) {
                bool? InCommand = null;
                try
                {
                    if (!Server.IsConnected)
                        await Server.WaitForConnectionAsync();
                    InCommand = false;
                    var Cmd = (Commands)await Server.ReadU16();
                    InCommand = true;
                    switch (Cmd)
                    {
                        case Commands.PING:
                            await Server.WriteBool(true);
                            break;
                        case Commands.InterAccounts:
                            await Server.WriteBool(UserInfoA.EmailAddress != UserInfoB.EmailAddress);
                            break;
                        case Commands.CloseServer:
                            Server.Close();
                            return;
                        case Commands.ConnectA:
                            CredentialsA = await GoogleWebAuthorizationBroker.AuthorizeAsync(ApiCredentials, Scopes, UserA, System.Threading.CancellationToken.None);
                            ServiceA = new DriveService(new BaseClientService.Initializer()
                            {
                                HttpClientInitializer = CredentialsA,
                                ApplicationName = "DriveMirror"
                            });
                            UserInfoA = await ServiceA.GetUserInfo();
                            break;
                        case Commands.ConnectB:
                            CredentialsB = await GoogleWebAuthorizationBroker.AuthorizeAsync(ApiCredentials, Scopes, UserB, System.Threading.CancellationToken.None);
                            ServiceB = new DriveService(new BaseClientService.Initializer()
                            {
                                HttpClientInitializer = CredentialsB,
                                ApplicationName = "DriveMirror"
                            });
                            UserInfoB = await ServiceB.GetUserInfo();
                            break;
                        case Commands.Disconnect:
                            await CredentialsA.RevokeTokenAsync(CancellationToken.None);
                            await CredentialsB.RevokeTokenAsync(CancellationToken.None);
                            break;
                        case Commands.EnumDrivesA:
                            var DrivesA = await ServiceA.EnumDrives().ToListAsync();
                            await Server.WriteU32((uint)DrivesA.Count);
                            foreach (var Drive in DrivesA)
                            {
                                await Server.WriteString(Drive.Name);
                                await Server.WriteString(Drive.Id);
                            }
                            break;
                        case Commands.EnumDrivesB:
                            var DrivesB = await ServiceB.EnumDrives().ToListAsync();
                            await Server.WriteU32((uint)DrivesB.Count);
                            foreach (var Drive in DrivesB)
                            {
                                await Server.WriteString(Drive.Name);
                                await Server.WriteString(Drive.Id);
                            }
                            break;
                        case Commands.GetFileByIdA:
                            var FileAID = await Server.ReadString();
                            var AInfo = await ServiceA.GetFileById(FileAID);
                            await Server.WriteFileInfo(AInfo);
                            break;
                        case Commands.GetFileByIdB:
                            var FileBID = await Server.ReadString();
                            var BInfo = await ServiceB.GetFileById(FileBID);
                            await Server.WriteFileInfo(BInfo);
                            break;
                        case Commands.ParsePathA:
                            var APath = await Server.ReadString();
                            var AFInfo = await ServiceA.TranslatePath(APath, DriveA, true);
                            await Server.WriteFileInfo(AFInfo);
                            break;
                        case Commands.ParsePathB:
                            var BPath = await Server.ReadString();
                            var BFInfo = await ServiceB.TranslatePath(BPath, DriveB, true);
                            await Server.WriteFileInfo(BFInfo);
                            break;
                        case Commands.EnumFilesA:
                            var AEFID = await Server.ReadString();
                            var ATrashed = await Server.ReadBool();
                            if (AEFID.StartsWith("/"))
                                AEFID = (await ServiceA.TranslatePath(AEFID)).Id;
                            await ServiceA.EnumFiles(AEFID, DriveA, true, ATrashed).ForEachAsync(async x =>
                            {
                                await Server.WriteBool(true);
                                await Server.WriteFileInfo(x);
                            });
                            await Server.WriteBool(false);
                            break;
                        case Commands.EnumFilesB:
                            var BEFID = await Server.ReadString();
                            var BTrashed = await Server.ReadBool();
                            if (BEFID.StartsWith("/"))
                                BEFID = (await ServiceB.TranslatePath(BEFID)).Id;
                            await ServiceB.EnumFiles(BEFID, DriveB, true, BTrashed).ForEachAsync(async x =>
                            {
                                await Server.WriteBool(true);
                                await Server.WriteFileInfo(x);
                            });
                            await Server.WriteBool(false);
                            break;
                        case Commands.EnumFoldersA:
                            var AEDID = await Server.ReadString();
                            var ADTrashed = await Server.ReadBool();
                            if (AEDID.StartsWith("/"))
                                AEDID = (await ServiceA.TranslatePath(AEDID)).Id;
                            await ServiceA.EnumFolders(AEDID, DriveA, ADTrashed).ForEachAsync(async x =>
                            {
                                await Server.WriteBool(true);
                                await Server.WriteFileInfo(x);
                            });
                            await Server.WriteBool(false);
                            break;
                        case Commands.EnumFoldersB:
                            var BEDID = await Server.ReadString();
                            var BDTrashed = await Server.ReadBool();
                            if (BEDID.StartsWith("/"))
                                BEDID = (await ServiceB.TranslatePath(BEDID)).Id;
                            await ServiceB.EnumFolders(BEDID, DriveB, BDTrashed).ForEachAsync(async x =>
                            {
                                await Server.WriteBool(true);
                                await Server.WriteFileInfo(x);
                            });
                            await Server.WriteBool(false);
                            break;
                        case Commands.CopyFileA:
                            string ACPYID = await Server.ReadString();
                            string ACPYNM = await Server.ReadString();
                            string ACPYDR = await Server.ReadString();
                            var ACPYNF = await ServiceA.CopyFile(ACPYID, ACPYNM, ACPYDR);
                            await Server.WriteFileInfo(ACPYNF);
                            break;
                        case Commands.CopyFileB:
                            string BCPYID = await Server.ReadString();
                            string BCPYNM = await Server.ReadString();
                            string BCPYDR = await Server.ReadString();
                            var BCPYNF = await ServiceB.CopyFile(BCPYID, BCPYNM, BCPYDR);
                            await Server.WriteFileInfo(BCPYNF);
                            break;
                        case Commands.DeleteFileA:
                            string ADELID = await Server.ReadString();
                            await ServiceA.Delete(await ServiceA.GetFileById(ADELID));
                            break;
                        case Commands.DeleteFileB:
                            string BDELID = await Server.ReadString();
                            await ServiceB.Delete(await ServiceB.GetFileById(BDELID));
                            break;
                        case Commands.CreateDirectoryA:
                            string NewDirPathA = await Server.ReadString();
                            var NDirA = await ServiceA.CreateDirectory(NewDirPathA, DriveA);
                            await Server.WriteFileInfo(NDirA);
                            break;
                        case Commands.CreateDirectoryB:
                            string NewDirPathB = await Server.ReadString();
                            var NDirB = await ServiceB.CreateDirectory(NewDirPathB, DriveB);
                            await Server.WriteFileInfo(NDirB);
                            break;
                        case Commands.SelectDriveA:
                            string ADriveID = await Server.ReadString();
                            DriveA = new Drive() { Id = ADriveID };
                            break;
                        case Commands.SelectDriveB:
                            string BDriveID = await Server.ReadString();
                            DriveB = new Drive() { Id = BDriveID };
                            break;
                        case Commands.ShareFileA:
                            string ASFID = await Server.ReadString();
                            var ASFInfo = await ServiceA.ShareFile(ASFID, ServiceB);
                            await Server.WriteFileInfo(ASFInfo);
                            break;
                        case Commands.ShareFileB:
                            string BSFID = await Server.ReadString();
                            var BSFInfo = await ServiceB.ShareFile(BSFID, ServiceA);
                            await Server.WriteFileInfo(BSFInfo);
                            break;
                        case Commands.StopShareA:
                            var StpFileInfA = await Server.ReadFileInfo();
                            var NWFileInfA = await ServiceA.StopSharing(StpFileInfA);
                            await Server.WriteFileInfo(NWFileInfA);
                            break;
                        case Commands.StopShareB:
                            var StpFileInfB = await Server.ReadFileInfo();
                            var NWFileInfB = await ServiceB.StopSharing(StpFileInfB);
                            await Server.WriteFileInfo(NWFileInfB);
                            break;
                    }
                    await Server.FlushAsync();
                }
                catch (Exception ex) {
                    if (InCommand == null)
                        return;
                    if (InCommand != null && !InCommand.Value)
                        continue;
                    if (!Client.IsConnected)
                        return;

                    Message(ex.ToString(), "DriveMirror Service", MessageType.Error, ButtonsType.Ok);
                }
            }
        }
    }

    /// <summary>
    /// Wait if the server is busy
    /// </summary>
    public static async Task PING(bool EnsureConnection = true) {
        if (EnsureConnection)
            await ConnectServer();
        await Client.WriteU16((ushort)Commands.PING);
        await Client.FlushAsync();
        if (!await Client.ReadBool())
            throw new Exception();
    }

    /// <summary>
    /// Verify if the mirror will be created bettewen different accounts
    /// </summary>
    /// <returns>If the user have used different accounts, returns true, otherwise false.</returns>
    public static async Task<bool> InterAccounts() {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.InterAccounts);
        await Client.FlushAsync();

        return await Client.ReadBool();
    }

    public static async Task CloseServer() {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.CloseServer);
        await Client.FlushAsync();

        Client = null;
    }

    public static async Task ConnectA()
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.ConnectA);
        await Client.FlushAsync();
    }

    public static async Task ConnectB()
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.ConnectB);
        await Client.FlushAsync();
    }

    public static async Task Disconnect()
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.Disconnect);
        await Client.FlushAsync();
    }

    public static async Task<DriveInfo[]> EnumDrivesA()
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.EnumDrivesA);
        await Client.FlushAsync();

        var DrivesInfo = new DriveInfo[await Client.ReadU32()];
        for (int i = 0; i < DrivesInfo.Length; i++)
            DrivesInfo[i] = new DriveInfo()
            {
                Name = await Client.ReadString(),
                ID = await Client.ReadString()
            };

        return DrivesInfo;
    }

    public static async Task<DriveInfo[]> EnumDrivesB()
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.EnumDrivesB);
        await Client.FlushAsync();

        var DrivesInfo = new DriveInfo[await Client.ReadU32()];
        for (int i = 0; i < DrivesInfo.Length; i++)
            DrivesInfo[i] = new DriveInfo()
            {
                Name = await Client.ReadString(),
                ID = await Client.ReadString()
            };

        return DrivesInfo;
    }

    public static async Task<FileInfo?> GetFileByIdA(string ID)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.GetFileByIdA);
        await Client.WriteString(ID);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task<FileInfo?> GetFileByIdB(string ID)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.GetFileByIdB);
        await Client.WriteString(ID);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task<FileInfo?> ParsePathA(string Path)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.ParsePathA);
        await Client.WriteString(Path);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task<FileInfo?> ParsePathB(string Path)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.ParsePathB);
        await Client.WriteString(Path);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task<FileInfo?[]> EnumFilesA(string Parent, bool IncludeTrashed)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.EnumFilesA);
        await Client.WriteString(Parent);
        await Client.WriteBool(IncludeTrashed);
        await Client.FlushAsync();


        List<FileInfo?> Entries = new List<FileInfo?>();
        while (await Client.ReadBool())
            Entries.Add(await Client.ReadFileInfo());
        return (from x in Entries orderby x?.Name select x).ToArray();
    }

    public static async Task<FileInfo?[]> EnumFilesB(string Parent, bool IncludeTrashed)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.EnumFilesB);
        await Client.WriteString(Parent);
        await Client.WriteBool(IncludeTrashed);
        await Client.FlushAsync();

        List<FileInfo?> Entries = new List<FileInfo?>();
        while (await Client.ReadBool())
            Entries.Add(await Client.ReadFileInfo());
        return (from x in Entries orderby x?.Name select x).ToArray();
    }

    public static async Task<FileInfo?[]> EnumFoldersA(string Parent, bool IncludeTrashed)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.EnumFoldersA);
        await Client.WriteString(Parent);
        await Client.WriteBool(IncludeTrashed);
        await Client.FlushAsync();

        List<FileInfo?> Entries = new List<FileInfo?>();
        while (await Client.ReadBool())
            Entries.Add(await Client.ReadFileInfo());
        return (from x in Entries orderby x?.Name select x).ToArray();
    }

    public static async Task<FileInfo?[]> EnumFoldersB(string Parent, bool IncludeTrashed)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.EnumFoldersB);
        await Client.WriteString(Parent);
        await Client.WriteBool(IncludeTrashed);
        await Client.FlushAsync();

        List<FileInfo?> Entries = new List<FileInfo?>();
        while (await Client.ReadBool())
            Entries.Add(await Client.ReadFileInfo());
        return (from x in Entries orderby x?.Name select x).ToArray();
    }

    public static async Task<FileInfo?> CopyFileA(string FromID, string OriginalName, string NewDirectory)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.CopyFileA);
        await Client.WriteString(FromID);
        await Client.WriteString(OriginalName);
        await Client.WriteString(NewDirectory);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task<FileInfo?> CopyFileB(string FromID, string OriginalName, string NewDirectory)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.CopyFileB);
        await Client.WriteString(FromID);
        await Client.WriteString(OriginalName);
        await Client.WriteString(NewDirectory);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task DeleteFileA(string FileID)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.DeleteFileA);
        await Client.WriteString(FileID);
        await Client.FlushAsync();
    }

    public static async Task DeleteFileB(string FileID)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.DeleteFileB);
        await Client.WriteString(FileID);
        await Client.FlushAsync();
    }

    public static async Task<FileInfo?> CreateDirectoryA(string Path)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.CreateDirectoryA);
        await Client.WriteString(Path);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task<FileInfo?> CreateDirectoryB(string Path)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.CreateDirectoryB);
        await Client.WriteString(Path);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task SelectDriveA(string ID)
    {
        DriveA = new Drive() { Id = ID };
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.SelectDriveA);
        await Client.WriteString(ID);
        await Client.FlushAsync();
    }

    public static async Task SelectDriveB(string ID)
    {
        DriveB = new Drive() { Id = ID };
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.SelectDriveB);
        await Client.WriteString(ID);
        await Client.FlushAsync();
    }

    public static async Task<FileInfo?> ShareFileA(string FileID)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.ShareFileA);
        await Client.WriteString(FileID);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task<FileInfo?> ShareFileB(string FileID)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.ShareFileB);
        await Client.WriteString(FileID);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task<FileInfo?> StopShareA(FileInfo? File)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.StopShareA);
        await Client.WriteFileInfo(File);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task<FileInfo?> StopShareB(FileInfo? File)
    {
        await ConnectServer();
        await Client.WriteU16((ushort)Commands.StopShareB);
        await Client.WriteFileInfo(File);
        await Client.FlushAsync();

        return await Client.ReadFileInfo();
    }

    public static async Task CreateMirror(string PathA, string PathB, Action<string> StatusChanged) {
        await ConnectServer();
        MirrorWorker.StatusChanged = StatusChanged;
        await MirrorWorker.Sync(PathA, PathB);
    }

    static NamedPipeClientStream Client = null;
    public static async Task ConnectServer()
    {
        if (Client != null && Client.IsConnected)
        {
            try {
                await PING(false);
                return;
            }
            catch { }
        }

        bool FirstTime = Client == null;

        RunServer();
        while (true)
        {
            Thread.Sleep(1000);
            try
            {
                Client = new NamedPipeClientStream(ServerName + (ServerInstance ?? "GLOBAL"));
                Client.Connect();
                if (Client.IsConnected)
                    break;
            }
            catch {
                try {
                    Client?.Dispose();
                } catch { }
                continue;
            }
        }

        if (!FirstTime)
        {
            await ConnectA();
            await ConnectB();
            await SelectDriveA(DriveA.Id);
            await SelectDriveB(DriveB.Id);
        }
    }


    private static void RunServer()
    {
        ServerInstance = new Random().Next(0, int.MaxValue).ToString();
        Process.Start(CurrentExecutable, $"-instance {ServerInstance} -service");
    }

    private static async Task<FileInfo?> ReadFileInfo(this Stream Stream)
    {
        if (!await Stream.ReadBool())
            return null;

        var NewFile = new FileInfo() { 
            Name = await Stream.ReadString(),
            ID = await Stream.ReadString(),
            MD5 = await Stream.ReadString(),
            IsDirectory = await Stream.ReadBool(),
            Trashed = await Stream.ReadBool(),
            ModifiedTime = null
        };

        if (await Stream.ReadBool())
            NewFile.ModifiedTime = DateTime.FromBinary(await Stream.ReadS64());

        if (await Stream.ReadBool())
        {
            string[] Perms = new string[await Stream.ReadU32()];
            for (int i = 0; i < Perms.Length; i++)
                Perms[i] = await Stream.ReadString();
            NewFile.Permissions = Perms;
        }

        return NewFile;
    }

    private static async Task WriteFileInfo(this Stream Stream, Google.Apis.Drive.v3.Data.File FileInfo)
    {
        if (FileInfo == null)
        {
            await Stream.WriteBool(false);
            return;
        }

        await Stream.WriteBool(true);
        await Stream.WriteString(FileInfo.Name);
        await Stream.WriteString(FileInfo.Id);
        await Stream.WriteString(FileInfo.Md5Checksum);
        await Stream.WriteBool(FileInfo.IsDirectory());
        await Stream.WriteBool(FileInfo.Trashed.Value);

        if (FileInfo.ModifiedTime == null)
            await Stream.WriteBool(false);
        else
        {
            await Stream.WriteBool(true);
            await Stream.WriteS64(FileInfo.ModifiedTime.Value.ToBinary());
        }


        if (FileInfo.Permissions == null)
            await Stream.WriteBool(false);
        else
        {
            await Stream.WriteBool(true);
            await Stream.WriteU32((uint)FileInfo.Permissions.Count);
            foreach (var Perm in FileInfo.Permissions)
                await Stream.WriteString(Perm.Id);
        }
    }

    private static async Task<bool> ReadBool(this Stream Stream) => await Stream.ReadByteAsync() > 0;
    private static async Task WriteBool(this Stream Stream, bool Boolean) =>  await Stream.WriteByteAsync((byte)(Boolean ? 1 : 0));


    private static async Task<byte> ReadByteAsync(this Stream Stream)
    {
        byte[] Buffer = new byte[1];
        if (await Stream.ReadAsync(Buffer, 0, Buffer.Length) == Buffer.Length)
            return Buffer[0];
        throw new IOException("Insufficient Memory");
    }

    private static async Task WriteByteAsync(this Stream Stream, byte Byte)
    {
        await Stream.WriteAsync(new byte[] { Byte }, 0, 1);
    }

    private static async Task<ushort> ReadU16(this Stream Stream)
    {
        byte[] Buffer = new byte[2];
        if (await Stream.ReadAsync(Buffer, 0, Buffer.Length) == Buffer.Length)
            return BitConverter.ToUInt16(Buffer, 0);
        throw new IOException("Insufficient Memory");
    }

    private static async Task WriteU16(this Stream Stream, ushort Value)
    {
        byte[] Buffer = BitConverter.GetBytes(Value);
        await Stream.WriteAsync(Buffer, 0, Buffer.Length);
    }

    private static async Task<uint> ReadU32(this Stream Stream)
    {
        byte[] Buffer = new byte[4];
        if (await Stream.ReadAsync(Buffer, 0, Buffer.Length) == Buffer.Length)
            return BitConverter.ToUInt32(Buffer, 0);
        throw new IOException("Insufficient Memory");
    }

    private static async Task WriteU32(this Stream Stream, uint Value)
    {
        byte[] Buffer = BitConverter.GetBytes(Value);
        await Stream.WriteAsync(Buffer, 0, Buffer.Length);
    }

    private static async Task<long> ReadS64(this Stream Stream)
    {
        byte[] Buffer = new byte[8];
        if (await Stream.ReadAsync(Buffer, 0, Buffer.Length) == Buffer.Length)
            return BitConverter.ToInt64(Buffer, 0);
        throw new IOException("Insufficient Memory");
    }

    private static async Task WriteS64(this Stream Stream, long Value)
    {
        byte[] Buffer = BitConverter.GetBytes(Value);
        await Stream.WriteAsync(Buffer, 0, Buffer.Length);
    }

    private static async Task<string> ReadString(this Stream Stream)
    {
        uint Length = await Stream.ReadU32();
        if (Length == uint.MaxValue)
            return null;

        byte[] Buffer = new byte[Length];
        if (await Stream.ReadAsync(Buffer, 0, Buffer.Length) == Buffer.Length)
            return Encoding.Unicode.GetString(Buffer);
        throw new IOException("Insufficient Memory");
    }

    private static async Task WriteString(this Stream Stream, string Content)
    {
        if (Content == null) {
            await Stream.WriteU32(uint.MaxValue);
            return;
        }

        var Data = Encoding.Unicode.GetBytes(Content);
        await Stream.WriteU32((uint)Data.LongLength);
        await Stream.WriteAsync(Data, 0, Data.Length);
    }



    private static ResponseType Message(string text, string title, MessageType MType, ButtonsType Buttons, bool Markup = false)
    {
        var Dialog = new MessageDialog(null, DialogFlags.DestroyWithParent, MType, Buttons, Markup, text);
        Dialog.Title = title;
        var Response = (ResponseType)Dialog.Run();
        Dialog.Destroy();
        return Response;
    }
}

public struct DriveInfo
{
    public string Name;
    public string ID;

    public override string ToString()
    {
        return ID;
    }
}

public struct FileInfo
{
    public string Name;
    public string ID;
    public string MD5;
    public bool IsDirectory;
    public bool Trashed;

    public DateTime? ModifiedTime;

    public string[] Permissions;

    public static implicit operator string(FileInfo? fi) => fi?.ID;
    public static implicit operator string(FileInfo fi) => fi.ID;

    public static implicit operator Google.Apis.Drive.v3.Data.File(FileInfo fi) => (FileInfo?)fi;

    public static implicit operator Google.Apis.Drive.v3.Data.File(FileInfo? fi) {
        if (fi == null)
            return null;

        var NFile = new Google.Apis.Drive.v3.Data.File
        {
            Id = fi?.ID,
            Name = fi?.Name,
            Trashed = fi?.Trashed,
            Md5Checksum = fi?.MD5,
            ModifiedTime = fi?.ModifiedTime
        };

        if (fi?.Permissions != null)
        {
            NFile.Permissions = new List<Permission>();
            foreach (var Perm in fi?.Permissions)
                NFile.Permissions.Add(new Permission() { Id = Perm });
        }

        return NFile;
    }
}