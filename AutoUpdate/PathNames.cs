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
        public const char ServerSplitChar = '/';


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
}
