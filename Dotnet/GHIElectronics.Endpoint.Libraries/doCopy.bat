@echo off
echo Cleaning....


for /f "tokens=*" %%a in ('dir /b /s /a:d ".\"') do (
	if /i "%%~nxa"=="bin" (
		echo Copying folder...  %%a
		copy %%a\Release\*.nupkg "D:\Local Nuget"	
        copy %%a\Release\*.nupkg .\output\
	)	
)




