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
        private string ProjectPath { get => PathNames.paths.ProjectPath; }
        #endregion

        private void Init()
        {
            new Thread(async () =>
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
                        case "-a": update = item; break;
                        default: break;
                    }
                }

                if (update != null)
                {
                    if (type.IsServerSetting(out string s, out string t))
                    {
                        try
                        {
                            string[,] time = (string[,])update;

                            Thread.Sleep(Convert.ToInt32(time[0, 1]));
                            AutoUpdate(s, t);   //更新时需要等待async 调试状态先注释掉下面的提示

                            //Console.WriteLine("更新完成");
                            //Application.Current.Dispatcher.Invoke(new Action(() =>
                            //{
                            //    Application.Current.Shutdown();
                            //}));
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("定时更新时间有误 单位为:ms");
                            Application.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                Application.Current.Shutdown();
                            }));
                        }
                    }
                    else
                    {
                        //没配置文件 抛出主程序
                        Console.WriteLine(type.IniFile);
                        Console.WriteLine("未配置服务器地址");
                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            Application.Current.Shutdown();
                        }));
                    }
                }
                else
                {
                    //不计划更新 关闭
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        Application.Current.Shutdown();
                    }));
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

            try
            {
                switch (type.ToLower())
                {
                    case "github":
                        //对比本地的ini的哈希 或对比本地的version 如果不一致就拉仓库
                        GithubUpdate(fileHash, server);
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine("程序出错 进程已结束");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            }

        }

        /// <summary>
        /// github仓库更新
        /// </summary>
        /// <param name="server"></param>
        private async void GithubUpdate(List<string[,]> hash, string server)
        {
            string[] warehouse = server.Split(PathNames.ServerSplitChar);
            if (warehouse.Count() == 3)
            {
                //下载github的配置文件 对比hash 如果不一致就拉仓库 更新至本地
                DownloadClass down = new DownloadClass();
                string tmp = await down.GitHubFileAsync(warehouse);

                ParameterType parameterType = new ParameterType();
                //if (FilesHelp.CalculateFileHash(tmp) != hash.Find(w => w[0, 0].ToLower() == PathNames.paths.IniFile.ToLower())[0, 1])
                if (FilesHelp.CalculateFileHash(tmp) != FilesHelp.CalculateFileHash(PathNames.paths.IniFile))
                {
                    if (parameterType.VersionCheck(tmp))
                    {
                        //触发拉取
                        FileClean(new List<string>() { tmp });
                        UI_Show((int)down.DownloadProgress);
                        tmp = await down.GitHubFileAsync(warehouse, false);

                        //解压-> 对比hash 差异化更新
                        string uid = Guid.NewGuid().ToString();
                        string extractPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), uid);
                        System.IO.Compression.ZipFile.ExtractToDirectory(tmp, extractPath);

                        string downloadPath = Directory.GetDirectories(System.IO.Path.Combine(down.TempPath, uid)).FirstOrDefault();
                        var downfileHash = await new FilesHelp().FileHash(downloadPath);

                        List<string[,]> CopyFile = new List<string[,]>();   //左新右老
                        List<Task> tasks = new List<Task>();
                        object lockObject = new object();

                        foreach (var item in downfileHash)
                        {
                            tasks.Add(Task.Run(() =>
                            {
                                var NewFile = item[0, 0].Replace(downloadPath, string.Empty);
                                var pastFile = hash.Find(w => w[0, 0].Replace(PathNames.paths.CurrentDirectory, string.Empty) == NewFile);

                                string[,] updateFile = new string[1, 2];
                                if (pastFile != null)
                                {
                                    updateFile[0, 0] = item[0, 0];
                                    updateFile[0, 1] = pastFile[0, 0];

                                    lock (lockObject)
                                        CopyFile.Add(updateFile);
                                }
                                else
                                {
                                    updateFile[0, 0] = item[0, 0];
                                    updateFile[0, 1] = Path.Combine(PathNames.paths.CurrentDirectory, NewFile.TrimStart('\\'));

                                    lock (lockObject)
                                        CopyFile.Add(updateFile);

                                    Directory.CreateDirectory(Path.GetDirectoryName(updateFile[0, 1]));
                                }
                            }));
                        }

                        await Task.WhenAll(tasks);
                        FileCopy(CopyFile);
                        FileClean(new List<string>() { tmp, downloadPath });
                    }
                }
                else
                {
                    //不需要更新
                    FileClean(new List<string>() { tmp });
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown();
                    });
                }
            }
            else
            {
                Console.WriteLine("github服务请求有误");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
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
        /// <param name="paths">左新 右老</param>
        private void FileCopy(List<string[,]> paths)
        {
            //UI_Show(paths.Count);
            int count = 0;

            string[,] End = new string[0, 1];
            bool verification = true;

            foreach (var item in paths)
            {
                if (item[0, 1] == new ParameterType().IniFile)
                {
                    End = item;
                    continue;
                }
                else
                {
                    try
                    {
                        if (File.Exists(item[0, 1]))
                        {
                            File.Replace(item[0, 0], item[0, 1], null);
                        }
                        else
                        {
                            File.Copy(item[0, 0], item[0, 1]);
                        }
                        count++;
                        UI_Update(paths.Count, count);
                    }
                    catch (Exception)
                    {
                        verification = false;
                    }
                }
                
            }

            //校验文件 如果全部替换完成 则更新配置文件 否则更新失败
            if (verification)
            {
                File.Replace(End[0, 0], End[0, 1], null);
            }

        }

        /// <summary>
        /// 接口参数类
        /// </summary>
        private class ParameterType
        {
            public string IniFile { get => PathNames.paths.IniFile; }
            private string[] Node = new string[] { "Settings", "Route" };
            private string[] Setting = new string[] { "Server", "Type", "Version", "File" };

            private enum SettingEnum
            {
                Server,
                Type,
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
                if (files != null)
                {
                    foreach (var item in files)
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

            /// <summary>
            /// 移除过滤更新文件地址
            /// </summary>
            /// <param name="str"></param>
            public void DeleteRoute(string str)
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
            /// 版本号检查
            /// </summary>
            /// <param name="file"></param>
            /// <returns></returns>
            public bool VersionCheck(string file)
            {
                StringBuilder returnValue = new StringBuilder(255);
                GetPrivateProfileString(Node[0], Setting[(int)SettingEnum.Version], string.Empty, returnValue, returnValue.Capacity, file);
                if (Read(Node[0], Setting[(int)SettingEnum.Version]) != returnValue.ToString())
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
                        if (RouteFile.Find(w => System.IO.Path.Combine(projcetFile, w) == item) == null)
                        {
                            tasks.Add(Task.Run(() =>
                            {
                                string[,] fileHash = new string[1, 2];
                                fileHash[0, 0] = item;
                                fileHash[0, 1] = CalculateFileHash(item);

                                lock (lockObject)
                                    hash.Add(fileHash);
                            }));
                        }
                    }
                }
                else
                {
                    foreach (var item in Files)
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            string[,] fileHash = new string[1, 2];
                            fileHash[0, 0] = item;
                            fileHash[0, 1] = CalculateFileHash(item);

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
                if (RouteFile != null)
                {
                    foreach (var item in Files)
                    {
                        if (RouteFile.Find(w => System.IO.Path.Combine(path + w) == item) == null)
                        {
                            string[,] fileHash = new string[1, 2];
                            fileHash[0, 0] = item;
                            fileHash[0, 1] = CalculateFileHash(item);

                            hash.Add(fileHash);
                        }
                    }
                }
                else
                {
                    foreach (var item in Files)
                    {
                        string[,] fileHash = new string[1, 2];
                        fileHash[0, 0] = item;
                        fileHash[0, 1] = CalculateFileHash(item);

                        hash.Add(fileHash);
                    }
                }

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
        }

        /// <summary>
        /// 下载类
        /// </summary>
        private class DownloadClass
        {
            private string UpdateFile = string.Format("update.");
            public string TempPath { get => System.IO.Path.GetTempPath(); }
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
                        url = $"https://raw.githubusercontent.com/{project[0]}/{project[1]}/{project[2]}/Update/{UpdateFile}ini";
                    else
                        url = $"https://github.com/{project[0]}/{project[1]}/archive/refs/heads/{project[2]}.zip";

                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? 0;

                    using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                        fileStream = new FileStream(System.IO.Path.Combine(TempPath, UpdateFile + (GetIni ? "ini" : "zip")), FileMode.Create, FileAccess.Write))
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
                return System.IO.Path.Combine(TempPath, UpdateFile + (GetIni ? "ini" : "zip"));
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
