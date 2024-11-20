@echo off
chcp 65001

update\AutoUpdate.exe -i 0 -s 2607077992ZQ@ProjectTest@main -t Github
update\AutoUpdate.exe -r start.bat

echo 当前版本1.0.0
update\AutoUpdate.exe -v 1.0.1

echo 正在检查自动更新
update\AutoUpdate.exe -a 0

if %errorlevel%==0 (
	start project\hello.txt
)

pause