using System.Configuration;
using System.Data;
using System.Windows;

namespace AutoUpdate
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            //base.OnStartup(e);

            var args = e.Args;
            try
            {
                string file = Environment.CurrentDirectory;
                if (args != null && args.Length % 2 == 0)
                {
                    List<string[,]> entrance = new List<string[,]>();
                    for (int i = 0; i < args.Length; i += 2)
                    {
                        string[,] addlist = new string[1, 2];
                        addlist[0, 0] = args[i];
                        addlist[0, 1] = args[i + 1];
                        entrance.Add(addlist);
                    }

                    //new UpdateWindow(entrance).Hide();
                    if (IsInspectVersion() || args.Contains("-i"))
                    {
                        
                        new UpdateWindow(entrance).Hide();
                    }
                    else
                    {
                        //没有配置文件
                        Err_print("无法连接至服务器");
                    }
                }
                else
                {
                    Err_print();
                }
            }
            catch (Exception ex)
            {
                Err_print();
            }

        }

        private void Err_print(string err = "参数有误")
        {
            Console.WriteLine(err);
            Application.Current.Shutdown(ErrorCode.InitError);
        }

        private bool IsInspectVersion()
        {
            if (System.IO.File.Exists(PathNames.paths.IniFile))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }

}
