using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoUpdate
{
    /// <summary>
    /// UpdateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UpdateWindow : Window
    {
        public UpdateWindow(List<string[,]> enter)
        {
            EntranceData = enter;
            InitializeComponent();

            Init();
        }

        #region
        private List<string[,]> EntranceData = new List<string[,]>();
        #endregion

        private void Init()
        {
            new Thread(() =>
            {
                ParameterType type = new ParameterType();

                var ini = EntranceData.Find(w => w[0, 0] == "-i");
                if (ini != null)
                {
                    type.Init();
                }

                object? update = null;
                foreach (var item in EntranceData)
                {
                    switch (item[0, 0])
                    {
                        case "-s": type.Server(item[0, 1]); break;
                        case "-t": type.ServerType(item[0, 1]); break;
                        case "-r": type.FilterRoute(item[0, 1]); break;
                        case "-d": type.DeleteRoute(item[0, 1]); break;
                        case "-v": type.Version(item[0, 1]); break;
                        case "-p": type.Path_Server(item[0, 1]); break;
                        case "-h":
                            update = new Thread(async () =>
                            {
                                await new FilesHelp().CreateHashFile();
                                type.Version(item[0, 1]);
                                ErrorCode.Exit(ErrorCode.Updated, $"文件结构以更新至{item[0, 1]}");
                            })
                            { IsBackground = true };
                            break;
                        case "-a": update = item; break;
                        default: break;
                    }
                }

                switch (update)
                {
                    case null: ErrorCode.Exit(ErrorCode.Normal, null); break;
                    case string[,]:
                        if (type.IsServerSetting(out string s, out string t))
                        {
                            try
                            {
                                string[,] time = (string[,])update;

                                Thread.Sleep(Convert.ToInt32(time[0, 1]));
                                AutoUpdate(s, t);
                            }
                            catch (Exception)
                            {
                                ErrorCode.Exit(ErrorCode.InitError, "定时更新时间有误 单位为: ms");
                            }
                        }
                        else
                        {
                            //没配置文件 抛出主程序
                            ErrorCode.Exit(ErrorCode.InitError, "未配置服务器地址");
                        }
                        break;
                    case Thread:
                        (update as Thread).Start();
                        break;
                    default:
                        ErrorCode.Exit(ErrorCode.Unknown, "unknown");
                        break;
                }

            })
            { IsBackground = true }.Start();
        }

        /// <summary>
        /// 执行自动更新
        /// </summary>
        /// <param name="server"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private async void AutoUpdate(string server, string type)
        {
            List<string[,]> fileHash = await new FilesHelp().FilesHash();
            string serverPath = new ParameterType().ServerPath();

            if (serverPath != string.Empty)
            {
                try
                {
                    switch (type.ToLower())
                    {
                        case "github": await GithubUpdate(fileHash, server); break;
                        case "ftp": await FtpUpdate(fileHash, server, serverPath); break;
                        default:
                            break;
                    }

                    ErrorCode.Exit(ErrorCode.Updated, "更新完成");
                }
                catch (Exception ex)
                {
                    ErrorCode.Exit(ErrorCode.UpdateError, ex.Message);
                }
            }
            else
            {
                ErrorCode.Exit(ErrorCode.InitError, "未指定服务端文件路径");
            }
        }

        /// <summary>
        /// github仓库更新
        /// </summary>
        /// <param name="hash">本地文件hash</param>
        /// <param name="server">用户仓库分支位置</param>
        /// <returns></returns>
        private async Task GithubUpdate(List<string[,]> hash, string server)
        {
            string[] warehouse = server.Split(PathNames.ServerSplitChar);
            if (warehouse.Count() == 3)
            {
                //下载github的配置文件 对比hash 如果不一致就拉仓库 更新至本地
                DownloadClass down = new DownloadClass();
                string IniFile = await down.GitHubFileAsync(warehouse);

                ParameterType parameterType = new ParameterType();
                if (FilesHelp.CalculateFileHash(IniFile) != FilesHelp.CalculateFileHash(PathNames.paths.IniFile))
                {
                    if (parameterType.VersionCheck(IniFile))
                    {
                        //触发拉取
                        UI_Show((int)down.DownloadProgress);
                        string downZip = await down.GitHubFileAsync(warehouse, false);

                        //解压-> 对比hash 差异化更新
                        string uid = Guid.NewGuid().ToString();
                        string extractPath = Path.Combine(Path.GetTempPath(), uid);
                        System.IO.Compression.ZipFile.ExtractToDirectory(downZip, extractPath);

                        string downloadPath = Directory.GetDirectories(Path.Combine(PathNames.TempPath, uid)).FirstOrDefault();
                        var downfileHash = await new FilesHelp().FileHash(downloadPath);

                        List<string[,]> CopyFile = new List<string[,]>();   //左新右老
                        List<Task> tasks = new List<Task>();
                        object lockObject = new object();

                        foreach (var item in downfileHash)
                        {
                            var NewHash = item;
                            tasks.Add(Task.Run(() =>
                            {
                                var NewFile = NewHash[0, 0].Replace(downloadPath, string.Empty);
                                var pastFile = hash.Find(w => w[0, 0].Replace(PathNames.paths.CurrentDirectory, string.Empty) == NewFile);

                                string[,] updateFile = new string[1, 2];
                                if (pastFile != null)
                                {
                                    updateFile[0, 0] = NewHash[0, 0];
                                    updateFile[0, 1] = pastFile[0, 0];

                                    lock (lockObject)
                                        CopyFile.Add(updateFile);
                                }
                                else
                                {
                                    updateFile[0, 0] = NewHash[0, 0];
                                    updateFile[0, 1] = Path.Combine(PathNames.paths.CurrentDirectory, NewFile.TrimStart('\\'));

                                    lock (lockObject)
                                        CopyFile.Add(updateFile);

                                    Directory.CreateDirectory(Path.GetDirectoryName(updateFile[0, 1]));
                                }
                            }));
                        }

                        await Task.WhenAll(tasks);
                        FileCopy(CopyFile);
                        FileClean(new List<string>() { IniFile, downZip, downloadPath });
                    }
                }
                else
                {
                    //不需要更新
                    FileClean(new List<string>() { IniFile });
                    ErrorCode.Exit(ErrorCode.NotUpdate, null);
                }
            }
            else
            {
                ErrorCode.Exit(ErrorCode.InitError, "Github服务请求有误");
            }
        }

        /// <summary>
        /// ftp更新
        /// </summary>
        /// <param name="hash">本地文件hash</param>
        /// <param name="server">服务器地址</param>
        /// <param name="Serverpath">服务器文件路径</param>
        private async Task FtpUpdate(List<string[,]> hash, string server, string ServerPath)
        {
            string[] warehouse = server.Split(PathNames.ServerSplitChar);

            /*
             * 规则:分割符切割
             * 前段为地址:端口
             * 后段为用户名:密码
             */
            if (new[] { 1, 2 }.Contains(warehouse.Count()))
            {
                DownloadClass.FtpHelp ftp;

                if(warehouse.Length == 2)
                {
                    UserSSL.USER user = new UserSSL().SpiltUserAndPwd(warehouse[1]);
                    ftp = new DownloadClass.FtpHelp(warehouse[0], ServerPath, user);
                }
                else
                {
                    ftp = new DownloadClass.FtpHelp(warehouse[0], ServerPath);
                }

                string InIFile =  ftp.GetFTPFile(DownloadClass.FtpHelp.FileTypeEnum.ini);
                if (FilesHelp.CalculateFileHash(InIFile) != FilesHelp.CalculateFileHash(PathNames.paths.IniFile))
                {
                    string ServerHash = ftp.GetFTPFile(DownloadClass.FtpHelp.FileTypeEnum.hash);
                    var Newhash = JsonConvert.DeserializeObject<List<FilesHelp.FileHashValue>>(File.ReadAllText(ServerHash));

                    List<Task> tasks = new List<Task>();
                    foreach (var item in Newhash)
                    {
                        var values = item;
                        tasks.Add(Task.Run(() =>
                        {
                            if (!hash.Any(w => w[0, 1] == values.Value))
                            {
                                string exiPath = Path.GetDirectoryName(Path.Combine(PathNames.paths.CurrentDirectory, values.Path));
                                if (!Directory.Exists(exiPath))
                                {
                                    Directory.CreateDirectory(exiPath);
                                }
                                ftp.GetFTPFile(DownloadClass.FtpHelp.FileTypeEnum.file, values.Path);
                            }
                        }));
                    }
                    await Task.WhenAll(tasks);

                    FileCopy(new List<string[,]> {
                        new string[1, 2] { { ServerHash, PathNames.paths.HashFile } },
                        new string[1, 2] { { InIFile, PathNames.paths.IniFile } },
                    });

                    FileClean(new List<string> { InIFile, ServerHash });
                }
                else
                {
                    FileClean(new List<string> { InIFile });
                }
            }
            else
            {
                ErrorCode.Exit(ErrorCode.InitError, "FTP服务器请求有误");
            }

        }

        /// <summary>
        /// 磁盘回收
        /// </summary>
        /// <param name="files"></param>
        private void FileClean(List<string> files)
        {
            foreach (var item in files)
            {
                try
                {
                    if (File.Exists(item))
                    {
                        File.Delete(item);
                    }
                    else if (Directory.Exists(item))
                    {
                        Directory.Delete(item, true);
                    }
                }
                catch (Exception)
                {

                }
            }
        }

        /// <summary>
        /// 文件复制 
        /// </summary>
        /// <param name="paths">1,2数组->左新 右老</param>
        private void FileCopy(List<string[,]> paths)
        {
            //UI_Show(paths.Count);
            int count = 0;

            string[,] End = new string[0, 1];
            object? verification = null;

            foreach (var item in paths)
            {
                if (item[0, 1] == new ParameterType().IniFile)
                {
                    End = item;
                    verification = true;
                    continue;
                }
                else
                {
                    try
                    {
                        File.Copy(item[0, 0], item[0, 1], true);
                        count++;
                        UI_Update(paths.Count, count);
                    }
                    catch (Exception ex)
                    {
                        verification = false;
                    }
                }
                
            }

            //校验文件 如果全部替换完成 则更新配置文件 否则更新失败
            //【这里应该加一个所有本地文件hash计算效验】
            if (verification is bool)
            {
                File.Copy(End[0, 0], End[0, 1], true);
                Console.WriteLine($"已更新至版本:{new ParameterType().GetVersion()}");
            }

        }

        /// <summary>
        /// 接口参数类
        /// </summary>
        private class ParameterType
        {
            public string IniFile { get => PathNames.paths.IniFile; }
            private string[] Node = new string[] { "Settings", "Route" };
            private string[] Setting = new string[] { "Server", "Type", "Path", "Version", "File" };

            private enum SettingEnum
            {
                Server,
                Type,
                Path,
                Version,
                File
            }

            #region systemDll

            [DllImport("kernel32", CharSet = CharSet.Auto)]
            private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder returnValue, int size, string filePath);

            [DllImport("kernel32", CharSet = CharSet.Auto)]
            private static extern long WritePrivateProfileString(string section, string key, string value, string filePath);

            public void Write(string section, string key, string value)
            {
                WritePrivateProfileString(section, key, value, IniFile);
            }

            public string Read(string section, string key, string defaultValue = "")
            {
                StringBuilder returnValue = new StringBuilder(255);
                GetPrivateProfileString(section, key, defaultValue, returnValue, returnValue.Capacity, IniFile);
                return returnValue.ToString();
            }

            #endregion

            /// <summary>
            /// 初始化函数 生成配置文件
            /// </summary>
            /// <param name="file"></param>
            public void Init()
            {
                string[] files;
                using (FileStream fs = File.Create(IniFile))
                {
                    var updatefiles = Directory.GetParent(IniFile).FullName;

                    files = Directory.GetFiles(updatefiles);
                    //files = Directory.GetFiles(updatefiles).Select(s => s.Replace(Directory.GetParent(updatefiles).FullName, string.Empty)).ToArray();
                }
                Path_Server("/");   //初始化为根
                if (files != null)
                {
                    List<string> fileList = files.ToList();
                    fileList.Remove(IniFile);

                    foreach (var item in fileList)
                    {
                        FilterRoute(item);
                    }
                }
            }

            /// <summary>
            /// 服务器地址
            /// </summary>
            /// <param name="str"></param>
            public void Server(string str)
            {
                Write(Node[0], Setting[(int)SettingEnum.Server], str);
            }

            /// <summary>
            /// 服务器类型
            /// </summary>
            /// <param name="str"></param>
            public void ServerType(string str)
            {
                Write(Node[0], Setting[(int)SettingEnum.Type], str);
            }

            /// <summary>
            /// 当前本地版本号
            /// </summary>
            /// <param name="str"></param>
            public void Version(string str)
            {
                Write(Node[0], Setting[(int)SettingEnum.Version], str);
            }

            /// <summary>
            /// 过滤更新文件地址
            /// </summary>
            /// <param name="str"></param>
            public void FilterRoute(string str)
            {
                string filter = Read(Node[1], Setting[(int)SettingEnum.File]);
                string path = str.ToLower().Replace(PathNames.paths.CurrentDirectory.ToLower() + "\\", string.Empty);

                if (File.Exists(str))
                {
                    if (!string.IsNullOrEmpty(filter))
                    {
                        FileJsonConfig filelist = JObject.Parse(filter).ToObject<FileJsonConfig>();
                        if (filelist.File.Find(w => w == path) == null)
                        {
                            filelist.File.Add(path);
                            Write(Node[1], Setting[(int)SettingEnum.File], JsonConvert.SerializeObject(filelist));
                        }
                    }
                    else
                    {
                        FileJsonConfig json = new FileJsonConfig();
                        json.File.Add(path);

                        Write(Node[1], Setting[(int)SettingEnum.File], JsonConvert.SerializeObject(json));
                    }
                }
            }

            /// <summary>
            /// 移除过滤更新文件地址
            /// </summary>
            /// <param name="str"></param>
            public void DeleteRoute(string str)
            {
                if (File.Exists(str))
                {
                    string filter = Read(Node[1], Setting[(int)SettingEnum.File]);
                    if (!string.IsNullOrEmpty(filter))
                    {
                        string path = str.ToLower().Replace(PathNames.paths.CurrentDirectory.ToLower() + "\\", string.Empty);
                        FileJsonConfig filelist = JObject.Parse(filter).ToObject<FileJsonConfig>();
                        if (filelist.File.Find(w => w == path) != null)
                        {
                            filelist.File.Remove(path);
                            Write(Node[1], Setting[(int)SettingEnum.File], JsonConvert.SerializeObject(filelist));
                        }
                    }
                }
            }

            /// <summary>
            /// 服务端文件位置
            /// </summary>
            /// <param name="path"></param>
            public void Path_Server(string str)
            {
                Write(Node[0], Setting[(int)SettingEnum.Path], str);
            }

            /// <summary>
            /// 获取服务端文件位置
            /// </summary>
            /// <returns></returns>
            public string ServerPath()
            {
                return Read(Node[0], Setting[(int)SettingEnum.Path]);
            }

            /// <summary>
            /// 服务器是否已经配置
            /// </summary>
            /// <returns></returns>
            public bool IsServerSetting(out string server, out string type)
            {
                server = Read(Node[0], Setting[(int)SettingEnum.Server]);
                type = Read(Node[0], Setting[(int)SettingEnum.Type]);
                if (!string.IsNullOrEmpty(server) && !string.IsNullOrEmpty(type))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            /// <summary>
            /// 取过滤的文件路径
            /// </summary>
            /// <returns></returns>
            public List<string>? GetFile()
            {
                string file = Read(Node[1], Setting[(int)SettingEnum.File]);
                if (!string.IsNullOrEmpty(file))
                {
                    FileJsonConfig filelist = JObject.Parse(file).ToObject<FileJsonConfig>();
                    if (filelist.File.Count > 0)
                    {
                        return filelist.File;
                    }
                }
                return null;
            }

            /// <summary>
            /// 获取本地版本号
            /// </summary>
            /// <returns></returns>
            public string GetVersion()
            {
                return Read(Node[0], Setting[(int)SettingEnum.Version]);
            }

            /// <summary>
            /// 版本号检查
            /// </summary>
            /// <param name="file"></param>
            /// <returns></returns>
            public bool VersionCheck(string file)
            {
                StringBuilder returnValue = new StringBuilder(255);
                GetPrivateProfileString(Node[0], Setting[(int)SettingEnum.Version], string.Empty, returnValue, returnValue.Capacity, file);
                if (GetVersion() != returnValue.ToString())
                    return true;
                else
                    return false;
            }

            private class FileJsonConfig
            {
                public List<string> File { get; set; } = new List<string>();
            }
        }

        /// <summary>
        /// 文件类处理
        /// </summary>
        private class FilesHelp
        {
            /// <summary>
            /// 本地文件Hash
            /// </summary>
            /// <param name="filePath"></param>
            /// <returns></returns>
            public static string CalculateFileHash(string filePath)
            {
                using (var sha256 = SHA256.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        byte[] hashBytes = sha256.ComputeHash(stream);
                        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    }
                }
            }

            /// <summary>
            /// 本地解决方案的所有文件hash
            /// </summary>
            /// <returns></returns>
            public async Task<List<string[,]>> FilesHash()
            {
                string projcetFile = PathNames.paths.CurrentDirectory;
                List<string[,]> hash = new List<string[,]>();
                List<Task> tasks = new List<Task>();

                var Files = GetPaths(projcetFile);
                var RouteFile = new ParameterType().GetFile();

                object lockObject = new object();
                if (RouteFile != null)
                {
                    foreach (var item in Files)
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            string flie = item;
                            if (!RouteFile.Any(w => string.Equals(Path.GetFullPath(Path.Combine(projcetFile, w)), flie, StringComparison.OrdinalIgnoreCase)))
                            {
                                string[,] fileHash = new string[1, 2];
                                fileHash[0, 0] = flie;
                                fileHash[0, 1] = CalculateFileHash(flie);

                                lock (lockObject)
                                    hash.Add(fileHash);
                            }

                        }));
                    }
                }
                else
                {
                    foreach (var item in Files)
                    {
                        string file = item;
                        tasks.Add(Task.Run(() =>
                        {
                            string[,] fileHash = new string[1, 2];
                            fileHash[0, 0] = file;
                            fileHash[0, 1] = CalculateFileHash(file);

                            lock (lockObject)
                                hash.Add(fileHash);

                        }));
                    }
                }

                await Task.WhenAll(tasks);

                return hash;

            }

            /// <summary>
            /// 新解决方案的hash
            /// </summary>
            /// <param name="path">解决方案路径</param>
            /// <returns></returns>
            public async Task<List<string[,]>> FileHash(string path)
            {
                List<string[,]> hash = new List<string[,]>();

                List<string> Files = await Task.Run(() =>
                {
                    return GetPaths(path);
                });
                var RouteFile = new ParameterType().GetFile();

                List<Task> tasks = new List<Task>();
                object lockObject = new object();
                if (RouteFile != null)
                {
                    foreach (var item in Files)
                    {
                        string file = item;
                        tasks.Add(Task.Run(() =>
                        {
                            if (!RouteFile.Any(w => string.Equals(Path.GetFullPath(Path.Combine(path, w)), file, StringComparison.OrdinalIgnoreCase)))
                            {
                                string[,] fileHash = new string[1, 2];
                                fileHash[0, 0] = item;
                                fileHash[0, 1] = CalculateFileHash(item);

                                lock (lockObject)
                                    hash.Add(fileHash);
                            }
                        }));
                    }
                }
                else
                {
                    foreach (var item in Files)
                    {
                        string file = item;
                        tasks.Add(Task.Run(() =>
                        {
                            string[,] fileHash = new string[1, 2];
                            fileHash[0, 0] = item;
                            fileHash[0, 1] = CalculateFileHash(item);

                            lock(lockObject)
                                hash.Add(fileHash);
                        }));
                    }
                }

                await Task.WhenAll(tasks);
                return hash;
            }

            /// <summary>
            /// 递归所有文件
            /// </summary>
            /// <param name="folderPath"></param>
            /// <returns></returns>
            public List<string> GetPaths(string folderPath)
            {
                var paths = new List<string>();

                string[] directories = Directory.GetDirectories(folderPath);
                string[] files = Directory.GetFiles(folderPath);

                paths.AddRange(files);

                foreach (var item in directories)
                {
                    paths.AddRange(GetPaths(item));
                }
                return paths;
            }

            /// <summary>
            /// 创建hash文件
            /// </summary>
            public async Task CreateHashFile()
            {
                string hread = PathNames.paths.CurrentDirectory;
                List<string[,]> fileHash = await new FilesHelp().FilesHash();

                List<FileHashValue> hashList = new List<FileHashValue>();
                foreach (var item in fileHash)
                {
                    var value = item;
                    hashList.Add(new FileHashValue
                    {
                        Path = value[0, 0].Replace(hread, string.Empty).TrimStart('\\'),
                        Value = value[0, 1]
                    });
                }

                using (FileStream fs = File.Create(PathNames.paths.HashFile))
                {
                    string json = JsonConvert.SerializeObject(hashList);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    fs.Write(bytes, 0, bytes.Length);
                }

            }

            /// <summary>
            /// 文件hash值
            /// </summary>
            public class FileHashValue
            {
                public string Path { get; set; }

                public string Value { get; set; }
            }
        }

        /// <summary>
        /// 下载类
        /// </summary>
        private class DownloadClass
        {
            private string UpdateFile = string.Format($"{PathNames.UpdateFileName}.");
            
            /// <summary>
            /// 下载进度 占总百分比的1/2
            /// </summary>
            public double DownloadProgress { get; set; }

            /// <summary>
            /// github文件拉取
            /// </summary>
            /// <param name="GetIni">true 配置文件 fasle仓库</param>
            /// <returns></returns>
            public async Task<string> GitHubFileAsync(string[] project, bool GetIni = true)
            {
                var handler = new HttpClientHandler
                {
                    UseProxy = true,
                    Proxy = WebRequest.DefaultWebProxy,
                    UseDefaultCredentials = true
                };

                using (HttpClient client = new HttpClient(handler))
                {
                    string url = string.Empty;
                    if (GetIni)
                        url = $"https://raw.githubusercontent.com/{project[0]}/{project[1]}/refs/heads/{project[2]}/update/{UpdateFile}ini";
                    else
                        url = $"https://github.com/{project[0]}/{project[1]}/archive/refs/heads/{project[2]}.zip";

                    try
                    {
                        HttpResponseMessage response = await client.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        long totalBytes = response.Content.Headers.ContentLength ?? 0;

                        using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                            fileStream = new FileStream(System.IO.Path.Combine(PathNames.TempPath, UpdateFile + (GetIni ? "ini" : "zip")), FileMode.Create, FileAccess.Write))
                        {
                            //await contentStream.CopyToAsync(fileStream);

                            byte[] buffer = new byte[8192]; //缓冲
                            long totalBytesRead = 0;
                            int bytesRead;
                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                totalBytesRead += bytesRead;
                                await fileStream.WriteAsync(buffer, 0, bytesRead);

                                DownloadProgress = ((double)totalBytesRead / totalBytes * 100) / 2;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorCode.Exit(ErrorCode.Timeout, ex.Message);
                    }
                }
                return System.IO.Path.Combine(PathNames.TempPath, UpdateFile + (GetIni ? "ini" : "zip"));
            }

            /// <summary>
            /// FTP
            /// </summary>
            public class FtpHelp
            {
                /// <summary>
                /// 服务器地址
                /// </summary>
                private string Url { get; }

                /// <summary>
                /// 登录凭据
                /// </summary>
                private UserSSL.USER? User { get; }

                /// <summary>
                /// 构造服务器地址
                /// </summary>
                /// <param name="server">服务器地址</param>
                /// <param name="path">服务器路径</param>
                public FtpHelp(string server, string path)
                {
                    Url = string.Format($"ftp://{server}/{(path.Trim() == "/" ? string.Empty : path.TrimStart('/').TrimEnd('/') + "/")}");
                }

                /// <summary>
                /// 构造服务器地址
                /// </summary>
                /// <param name="server">服务器地址</param>
                /// <param name="path">服务器路径</param>
                /// <param name="user">登录凭据</param>
                public FtpHelp(string server, string path, UserSSL.USER user)
                {
                    Url = string.Format($"ftp://{server}/{(path.Trim() == "/" ? string.Empty : path.TrimStart('/').TrimEnd('/') + "/")}");
                    User = user;
                }

                /// <summary>
                /// 文件类型
                /// </summary>
                public enum FileTypeEnum
                {
                    ini,
                    hash,
                    file
                }

                /// <summary>
                /// 下载单个文件
                /// </summary>
                /// <param name="fileType"></param>
                /// <param name="path">下载路径</param>
                /// <returns>下载保存路径</returns>
                public string GetFTPFile(FileTypeEnum fileType, string path = "")
                {
                    string DownloadPath = string.Empty;
                    StringBuilder url = new StringBuilder();
                    url.Append(Url);
                    switch (fileType)
                    {
                        case FileTypeEnum.ini:
                            url.Append($"update/{PathNames.UpdateFileName}.ini");
                            DownloadPath = $"{PathNames.UpdateFileName}.ini";
                            break;
                        case FileTypeEnum.hash:
                            url.Append("update/hash.json");
                            DownloadPath = $"hash.json";
                            break;
                        case FileTypeEnum.file:
                            url.Append($"{path}");
                            break;
                    }

                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(url.ToString());
                    request.Method = WebRequestMethods.Ftp.DownloadFile;
                    if (User != null)
                        request.Credentials = new NetworkCredential(User.Name, User.Password);

                    using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                    {
                        using (Stream responseStream = response.GetResponseStream())
                        {
                            /*
                             * 如果是配置文件 -> 临时文件夹+文件
                             * 如果是下载文件 -> 挂载路径+path直接覆盖
                             *
                             */

                            if (fileType == FileTypeEnum.file)
                                DownloadPath = Path.Combine(PathNames.paths.CurrentDirectory, path);
                            else
                                DownloadPath = Path.Combine(PathNames.TempPath, DownloadPath);

                            using (FileStream fs = new FileStream(DownloadPath, FileMode.Create)) 
                            {
                                responseStream.CopyTo(fs);
                            }
                        }
                    }

                    return DownloadPath;
                }
            }

        }

        /// <summary>
        /// 登录凭据
        /// </summary>
        private class UserSSL
        {
            /// <summary>
            /// 用户实体
            /// </summary>
            public class USER
            {
                public string Name { get; set; }
                public string? Password { get; set; }
            }

            /// <summary>
            /// 处理登录凭据
            /// </summary>
            /// <param name="str">用户名密码拼接的字符串</param>
            /// <param name="user">用户名</param>
            /// <param name="pwd">密码</param>
            public USER SpiltUserAndPwd(string str)
            {
                if (str != string.Empty)
                {
                    string[] ssl = str.Split(PathNames.UserPwdSplitChar);
                    switch (ssl.Length)
                    {
                        case 1: return new USER { Name = ssl[0] };
                        case 2:return new USER { Name = ssl[0], Password = ssl[1] };
                        default:
                            ErrorCode.Exit(ErrorCode.InitError, "用户名密码配置有误");
                            return null;
                    }
                }
                else
                {
                    return null;
                }
                
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }

        private void UI_Show(int sum)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                lblCount.Content += sum.ToString();
            }));
        }

        private void UI_Update(int sum, int count)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                double value = sum / count * 100;
                progress.Value = value;
                lblprogress.Content = value + "%";
            }));
        }
    }

}
