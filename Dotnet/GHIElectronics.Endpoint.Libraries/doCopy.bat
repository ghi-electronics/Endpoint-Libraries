@echo off
rem Copies the packages of a Release build (made in output\, see
rem Endpoint.Libraries.props) to the local NuGet folder
echo Copying packages to D:\Local Nuget...
copy /y output\*.nupkg "D:\Local Nuget"
