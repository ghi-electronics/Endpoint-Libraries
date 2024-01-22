@echo off
SET OutputAssemblyName=%1
SET BuildMode=Release
SET OutputLocation=bin\%BuildMode%\net8.0
SET CurrentDir=%CD%

IF "%DoAssemblySign%" == "true" (
	pushd "%CurrentDir%"

	DEL /Q *.nupkg

	signtool.exe sign /fd sha512 /f "%VsixSignerCertificatePath%" /p "%VsixSignerCertificatePassword%" /t "http://timestamp.digicert.com" /sha1 "%AssemblySignerCertificateSha1%" "%OutputLocation%\%OutputAssemblyName%.dll"
	nuget pack "%OutputAssemblyName%.csproj" -Properties Configuration=%BuildMode%
	xcopy /q /y %OutputAssemblyName%*.nupkg ..\output\
	xcopy /q /y %OutputAssemblyName%*.nupkg %NugetPackerOutputDirectory%

	popd
)



