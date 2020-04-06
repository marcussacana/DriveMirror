using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dasync.Collections;
using Gtk;

namespace DriveMirror
{
    internal static class MirrorWorker
    {
        /// <summary>
        /// <list type="">True  = Files from DirA will have priority</list> 
        /// <list type="">False = Files from DirB will have priority</list> 
        /// <list type="">null  = the newest file will have priority</list> 
        /// </summary>
        public static bool? Priority = null;

        private static List<SyncQuery> Queries;
        private static List<SyncQuery> FinishedQueries;

        internal static List<string> ExcludeA = new List<string>();
        internal static List<string> ExcludeB = new List<string>();

        static string CurrentDirectoyA;
        static string CurrentDirectoyB;

        static bool Saving;

        static string DRPName => string.Format("Task_{0}_{1}.drp", GetMD5(CurrentDirectoyA), GetMD5(CurrentDirectoyB));

        public static Action<string> StatusChanged;

        public static void SaveProgress() {
            string SavePath = Path.Combine(Server.AppDataDirectory, DRPName);

            Saving = true;
            using (var Stream = File.Create(SavePath))
            using (var Writer = new BinaryWriter(Stream, System.Text.Encoding.Unicode)) {
                Writer.WriteStr(CurrentDirectoyA);
                Writer.WriteStr(CurrentDirectoyB);

                Writer.WriteStr(Server.DriveA.Id);
                Writer.WriteStr(Server.DriveB.Id);

                Writer.Write(ExcludeA.Count);
                foreach (var ItemA in ExcludeA)
                    Writer.WriteStr(ItemA);

                Writer.Write(ExcludeB.Count);
                foreach (var ItemB in ExcludeB)
                    Writer.WriteStr(ItemB);

                Writer.Write(FinishedQueries.Count);
                foreach (var FinishedQuery in FinishedQueries)
                    Writer.Write(FinishedQuery);

                Writer.Flush();
                Writer.Close();
            }
            Saving = false;
        }

        public static void LoadProgress(string PathA, string PathB) {
            CurrentDirectoyA = PathA;
            CurrentDirectoyB = PathB;

            ExcludeA = new List<string>();
            ExcludeB = new List<string>();

            Queries = new List<SyncQuery>();
            FinishedQueries = new List<SyncQuery>();

            string DriveA = null;
            string DriveB = null;

            string SavePath = Path.Combine(Server.AppDataDirectory, DRPName);
            if (File.Exists(SavePath))
            {
                using (var Stream = File.Open(SavePath, FileMode.Open, FileAccess.Read))
                using (var Reader = new BinaryReader(Stream, System.Text.Encoding.Unicode))
                {
                    CurrentDirectoyA = Reader.ReadStr();
                    CurrentDirectoyB = Reader.ReadStr();

                    DriveA = Reader.ReadStr();
                    DriveB = Reader.ReadStr();

                    int Length = Reader.ReadInt32();
                    for (int i = 0; i < Length; i++)
                        ExcludeA.Add(Reader.ReadStr());

                    Length = Reader.ReadInt32();
                    for (int i = 0; i < Length; i++)
                        ExcludeB.Add(Reader.ReadStr());

                    Length = Reader.ReadInt32();
                    for (int i = 0; i < Length; i++)
                        FinishedQueries.Add(Reader.ReadQuery());

                    Reader.Close();
                }
            }
            else
                return;

            if (PathA != CurrentDirectoyA || PathB != CurrentDirectoyB || DriveA != Server.DriveA.Id || DriveB != Server.DriveB.Id) {
                ExcludeA = new List<string>();
                ExcludeB = new List<string>();
                Queries = new List<SyncQuery>();
                FinishedQueries = new List<SyncQuery>();

                CurrentDirectoyA = PathA;
                CurrentDirectoyB = PathB;
            }
        }

        public static async Task Sync(string PathA, string PathB)
        {
            CurrentDirectoyA = PathA;
            CurrentDirectoyB = PathB;

            LoadProgress(PathA, PathB);

            await DoMirror(PathA, PathB);

            if (File.Exists(Path.Combine(Server.AppDataDirectory, DRPName)))
                File.Delete(Path.Combine(Server.AppDataDirectory, DRPName));
        }

        static async Task DoMirror(string PathA, string PathB)
        {
            while (Saving)
                await Task.Delay(100);

            lock (Queries)
            {
                if ((from x in Queries where x.PathA == PathA && x.PathB == PathB select x).Any())
                    return;

                if ((from x in FinishedQueries where x.PathA == PathA && x.PathB == PathB select x).Any())
                    return;

                Queries.Add(new SyncQuery()
                {
                    PathA = PathA,
                    PathB = PathB
                });
            }

            if ((from x in ExcludeA where PathA == x select x).Any() || (from x in ExcludeB where PathB == x select x).Any())
                return;

            int GFails = 0;
            int RFails = 0;
            while (true)
            {
                try
                {

                    var DirA = await Server.ParsePathA(PathA);
                    var DirB = await Server.ParsePathB(PathB);

                    if (DirA == null)
                        DirA = await Server.CreateDirectoryA(PathA);

                    if (DirB == null)
                        DirB = await Server.CreateDirectoryB(PathB);

                    await SendStatus("Loading Directory: " + PathA);
                    var FoldersA = (await Server.EnumFoldersA(DirA, false)).ToList();
                    await SendStatus("Loading Directory: " + PathB);
                    var FoldersB = (await Server.EnumFoldersB(DirB, false)).ToList();

                    await FoldersA.ParallelForEachAsync(async (x) =>
                    {
                        string DirName = x?.Name + "/";
                        await DoMirror(CombinePath(PathA, DirName), CombinePath(PathB, DirName));
                    }, 1);

                    await FoldersB.ParallelForEachAsync(async (x) =>
                    {
                        string DirName = x?.Name + "/";
                        await DoMirror(CombinePath(PathA, DirName), CombinePath(PathB, DirName));
                    }, 1);

                    await SendStatus("Enumerating Directory: " + PathA);
                    var FileListA = (await Server.EnumFilesA(DirA, true)).ToList();
                    await SendStatus("Enumerating Directory: " + PathB);
                    var FileListB = (await Server.EnumFilesB(DirB, true)).ToList();

                    await FileListA.ParallelForEachAsync(async (x) =>
                     {
                         int Fails = 0;
                         while (true)
                         {
                             try
                             {
                                 string FileA = CombinePath(PathA, x?.Name);
                                 string FileB = CombinePath(PathB, x?.Name);

                                 await SendStatus("Verifying File: " + FileA);

                                 FileInfo? FA = x;
                                 FileInfo? FB = null;

                                 var Query = (from y in FileListB where y?.Name == x?.Name select y);
                                 if (Query.Any())
                                     FB = Query.First();

                                 if (FB != null && FA?.MD5 == FB?.MD5)
                                     return;

                                 await SendStatus("Syncrying File: " + FileA);

                                 if (FB != null && !Priority.HasValue)
                                 {
                                     if (FB?.ModifiedTime > FA?.ModifiedTime)
                                         return;
                                     await Server.DeleteFileB(FB);
                                 }
                                 else if (FB != null) {
                                     if (Priority.Value)
                                         await Server.DeleteFileB(FB);
                                     else
                                         return;
                                 }

                                 var Tmp = new FileInfo();
                                 Tmp.Name = x?.Name;
                                 FB = Tmp;

                                 if (await Server.InterAccounts())
                                 {
                                     FA = await Server.ShareFileA(FA);
                                     FB = await Server.CopyFileB(FA, x?.Name, DirB);
                                     FA = await Server.StopShareA(FA);
                                     return;
                                 }

                                 FB = await Server.CopyFileB(FA, x?.Name, DirB);

                             }
                             catch (Exception ex)
                             {
                                 if (Fails++ > 3)
                                 {
                                     throw ex;
                                 }
                                 await Task.Delay(100);
                                 continue;
                             }
                             return;
                         }
                     }, 1);

                    await FileListB.ParallelForEachAsync(async (x) =>
                    {
                        int Fails = 0;
                        while (true)
                        {
                            try
                            {
                                string FileA = CombinePath(PathA, x?.Name);
                                string FileB = CombinePath(PathB, x?.Name);

                                await SendStatus("Verifying File: " + FileA);

                                FileInfo? FA = null;

                                var Query = (from y in FileListA where y?.Name == x?.Name select y);
                                if (Query.Any())
                                    FA = Query.First();

                                FileInfo? FB = x;

                                if (FA != null && FA?.MD5 == FB?.MD5)
                                    return;

                                await SendStatus("Syncrying File: " + FileA);

                                if (FA != null && !Priority.HasValue)
                                {
                                    if (FA?.ModifiedTime > FB?.ModifiedTime)
                                        return;
                                    await Server.DeleteFileA(FA);
                                }
                                else if (FA != null)
                                {
                                    if (!Priority.Value)
                                        await Server.DeleteFileA(FA);
                                    else
                                        return;
                                }

                                var Tmp = new FileInfo();
                                Tmp.Name = x?.Name;
                                FA = Tmp;

                                if (await Server.InterAccounts())
                                {
                                    FB = await Server.ShareFileB(FB);
                                    FA = await Server.CopyFileA(FB, x?.Name, DirA);
                                    FB = await Server.StopShareB(FB);
                                    return;
                                }
                                FA = await Server.CopyFileA(FB, x?.Name, DirA);

                            }
                            catch (Exception ex)
                            {
                                if (Fails++ > 3)
                                {
                                    throw ex;
                                }
                                await Task.Delay(100);
                                continue;
                            }
                            return;
                        }
                    }, 1);
                    break;
                }
                catch (Exception ex)
                {
                    if (GFails++ > 3)
                    {
                        throw ex;
                    }
                    if (ex.IsRemoteException())
                    {
                        GFails--;
                        if (RFails++ > 15)
                            throw ex;
                        await SendStatus("API Cooldown...");
                        await Task.Delay(1000 * 10);//10 sec
                    }
                    await Task.Delay(100);
                    continue;
                }
            }

            while (Saving)
                await Task.Delay(100);

            FinishedQueries.Add(new SyncQuery()
            {
                PathA = PathA,
                PathB = PathB
            });
        }

        static async Task SendStatus(string Status) {
            StatusChanged?.Invoke(Status);
            await Task.Delay(1);
        }

        static void Write(this BinaryWriter Stream, SyncQuery Query)
        {
            Stream.WriteStr(Query.PathA);
            Stream.WriteStr(Query.PathB);
        }

        static SyncQuery ReadQuery(this BinaryReader Stream)
        {
            var Query = new SyncQuery();
            Query.PathA = Stream.ReadStr();
            Query.PathB = Stream.ReadStr();

            return Query;
        }



        static void WriteStr(this BinaryWriter Stream, string String)
        {
            if (String == null) {
                Stream.Write(false);
                return;
            }

            Stream.Write(true);
            Stream.Write(String);
        }

        static string ReadStr(this BinaryReader Stream)
        {
            string Rst = null;

            if (Stream.ReadBoolean()) {
                Rst = Stream.ReadString();
            }

            return Rst;
        }

        static string CombinePath(string A, string B) => A.TrimEnd('/', '\\') + '/' + B.TrimStart('\\', '/');

        static string GetMD5(this string String) { 
            using (var Crypto = new MD5CryptoServiceProvider()) {
                var Data = Crypto.ComputeHash(System.Text.Encoding.Unicode.GetBytes(String));
                string Rst = string.Empty;
                foreach (var Byte in Data)
                    Rst += $"{Byte:X2}";
                return Rst;
            }
        }

        struct SyncQuery
        {
            public string PathA;
            public string PathB;
        }
    }

    public class StatusChangedArgs : EventArgs
    {
        public StatusChangedArgs(string Status) => this.Status = Status;
        public string Status { get; set; }
    }
}
