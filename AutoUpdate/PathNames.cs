using System.IO;

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
            public string IniFile { get => Path.Combine(Environment.CurrentDirectory, @"update.ini"); }

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
            public string IniFile { get => Path.Combine(Environment.CurrentDirectory, @"update\update.ini"); }

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
