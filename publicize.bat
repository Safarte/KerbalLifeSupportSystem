.\Utilities\NStrip\NStrip.exe -p -n -d %1 -cg --cg-exclude-events --unity-non-serialized .\Packages\KSP2_x64\Assembly-CSharp.dll .\Assembly-CSharp.tmp
if %ERRORLEVEL% NEQ 0 (
    echo Error: Previous command failed. Exiting.
    exit /b %ERRORLEVEL%
)
del .\Packages\KSP2_x64\Assembly-CSharp.dll
move .\Assembly-CSharp.tmp .\Packages\KSP2_x64\Assembly-CSharp.dll