using System.IO;
using System.Windows;

namespace AutoUpdate
{
    public class PathNames
    {
#if DEBUG
        public static DebugPaths paths = new DebugPaths();
#else
        public static ReleasePaths paths = new ReleasePaths();
#endif

        /// <summary>
        /// 服务器地址分割符
        /// </summary>
        public const char ServerSplitChar = '@';

        /// <summary>
        /// 用户名密码分割符
        /// </summary>
        public const char UserPwdSplitChar = '#';

        /// <summary>
        /// 更新包文件名
        /// </summary>
        public const string UpdateFileName = "update";

        /// <summary>
        /// 当前登录用户的临时目录
        /// </summary>
        public static string TempPath { get => Path.GetTempPath(); }

        /// <summary>
        /// debug时路径
        /// </summary>
        public class DebugPaths
        {
            /// <summary>
            /// 项目路径
            /// </summary>
            public string ProjectPath { get => Environment.CurrentDirectory; }

            /// <summary>
            /// 配置文件路径
            /// </summary>
            public string IniFile { get => Path.Combine(Environment.CurrentDirectory, $"{UpdateFileName}.ini"); }

            /// <summary>
            /// 【新增方案 创建hash文件】
            /// 哈希文件路径
            /// </summary>
            public string HashFile { get=> Path.Combine(Environment.CurrentDirectory, "hash.json"); }

            /// <summary>
            /// 本地挂载文件路径
            /// </summary>
            public string CurrentDirectory { get => Directory.GetParent(Environment.CurrentDirectory).FullName; }

        }

        /// <summary>
        /// 从外部引用时路径
        /// </summary>
        public class ReleasePaths
        {
            /// <summary>
            /// 项目路径
            /// </summary>
            public string ProjectPath { get => Directory.GetParent(Environment.CurrentDirectory).FullName; }

            /// <summary>
            /// 配置文件路径
            /// </summary>
            public string IniFile { get => Path.Combine(Environment.CurrentDirectory, @$"update\{UpdateFileName}.ini"); }

            /// <summary>
            /// 哈希文件路径
            /// </summary>
            public string HashFile { get => Path.Combine(Environment.CurrentDirectory, @"update\hash.json"); }

            /// <summary>
            /// 本地挂载文件路径
            /// </summary>
            public string CurrentDirectory { get => Environment.CurrentDirectory; }
        }
    }

    /// <summary>
    /// 返回码约定
    /// </summary>
    public static class ErrorCode
    {
        /// <summary>
        /// 退出程序
        /// </summary>
        /// <param name="code">返回码</param>
        /// <param name="error">错误信息</param>
        public static void Exit(int code, string? error)
        {
            if (error != null)
            {
                Console.WriteLine(error);
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown(code);
            });
        }

        /// <summary>
        /// 未被定义的正常退出
        /// </summary>
        public const int Normal = 0;

        /// <summary>
        /// 已完成更新
        /// </summary>
        public const int Updated = 1;
        
        /// <summary>
        /// 已经是最新的了 不需要更新
        /// </summary>
        public const int NotUpdate = 2;

        /// <summary>
        /// 未符合预期的错误
        /// </summary>
        public const int Unknown = 3;

        /// <summary>
        /// 更新时出错
        /// </summary>
        public const int UpdateError = 10;

        /// <summary>
        /// 配置文件有误
        /// </summary>
        public const int InitError = 99;

        /// <summary>
        /// 服务器请求超时
        /// </summary>
        public const int Timeout = 9999;
    }
}
