#!/bin/sh
CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -1)
R=/usr/lib/mono/4.8-api
mkdir -p out
dotnet "$CSC" -nologo -noconfig -nostdlib -target:winexe -platform:anycpu -optimize+ -langversion:10.0 -nowarn:CS8632 \
 -out:out/RightClickMenuManager.exe -win32manifest:app.manifest -win32icon:app.ico \
 -r:$R/mscorlib.dll -r:$R/System.dll -r:$R/System.Core.dll -r:$R/System.Drawing.dll -r:$R/System.Windows.Forms.dll \
 *.cs
