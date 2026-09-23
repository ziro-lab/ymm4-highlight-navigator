param([Parameter(Mandatory=$true)][string]$Ymm4Dir,[Parameter(Mandatory=$true)][string]$OutputDir,[Parameter(Mandatory=$true)][string]$MediaPath)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $OutputDir | Out-Null
$result=Join-Path $OutputDir 'result.json'
if(Test-Path $result){throw 'Evidence directory must be fresh.'}
$env:NAV_NATIVE_OUTPUT=$OutputDir
$env:NAV_NATIVE_MEDIA=$MediaPath
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class NativeNavWindow {
 public delegate bool Callback(IntPtr h,IntPtr p);
 [DllImport("user32.dll")] public static extern bool EnumWindows(Callback c,IntPtr p);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
}
'@
$p=Start-Process (Join-Path $Ymm4Dir 'YukkuriMovieMaker.exe') -WorkingDirectory $Ymm4Dir -PassThru
try {
 $limit=[DateTime]::UtcNow.AddSeconds(120)
 while([DateTime]::UtcNow -lt $limit -and -not $p.HasExited -and -not(Test-Path $result)){
  $callback=[NativeNavWindow+Callback]{param([IntPtr]$h,[IntPtr]$unused)
   [uint32]$ownerPid=0; [void][NativeNavWindow]::GetWindowThreadProcessId($h,[ref]$ownerPid)
   if($ownerPid -eq $p.Id){
    $b=[Text.StringBuilder]::new(512); [void][NativeNavWindow]::GetWindowText($h,$b,$b.Capacity); $title=$b.ToString()
    if($title -like '*Check for updates*' -or $title -like '*About YukkuriMovieMaker*'){
     [void][NativeNavWindow]::PostMessage($h,0x0010,[IntPtr]::Zero,[IntPtr]::Zero)
    }
   }
   return $true
  }
  [void][NativeNavWindow]::EnumWindows($callback,[IntPtr]::Zero)
  Start-Sleep -Milliseconds 400
 }
 if(-not(Test-Path $result)){throw 'No fresh product native result.'}
 Get-Content $result
 $r=Get-Content -Raw $result | ConvertFrom-Json
 if($r.schema -ne 'navigator.native.v1' -or $r.passed -cne $true -or $r.checkout -ne $env:GITHUB_SHA -or $r.error){throw 'Native result identity/status rejected.'}
 $required=@(Get-Content (Join-Path $PSScriptRoot 'required-native-cases.json') -Raw | ConvertFrom-Json)
 if($required.Count -ne 111 -or @($required | Select-Object -Unique).Count -ne 111){throw 'Invalid independent native requirements.'}
 if($r.assertions.Count -ne $required.Count){throw 'Wrong native assertion count.'}
 foreach($id in $required){$found=@($r.assertions|Where-Object id -eq $id); if($found.Count -ne 1 -or $found[0].passed -cne $true){throw "Missing/failed assertion: $id"}}
 $p.Refresh()
 if($p.HasExited){throw 'YMM4 exited unexpectedly during proof.'}
 Get-Content (Join-Path $OutputDir 'summary.json')
 Get-Content (Join-Path $OutputDir 'learning-summary.json')
 $edit=Get-Content (Join-Path $OutputDir 'rebinding-summary.json') -Raw | ConvertFrom-Json
 if($edit.schema -ne 'navigator.rebinding-native.v1' -or $edit.checkout -ne $env:GITHUB_SHA -or $edit.completed -cne $true -or $edit.callsAfter -ne $edit.decodeCount -or $edit.decodeCount -ne ($edit.callsBefore + 1) -or $edit.unhandled -ne 0){throw 'Rebinding evidence rejected.'}
 Get-Content (Join-Path $OutputDir 'rebinding-summary.json')
 $ux=Get-Content (Join-Path $OutputDir 'ux-summary.json') -Raw | ConvertFrom-Json
 if($ux.schema -ne 'navigator.ux-native.v1' -or $ux.checkout -ne $env:GITHUB_SHA -or $ux.completed -cne $true -or $ux.callsAfter -ne $ux.callsBefore){throw 'UX integration evidence rejected.'}
 Get-Content (Join-Path $OutputDir 'ux-summary.json')
 Get-Content (Join-Path $OutputDir 'assertions.txt')
 Write-Output 'PASS_NAVIGATOR_PRODUCT: 111 independent assertions'
} finally {
 if(-not $p.HasExited){Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue}
 Remove-Item Env:NAV_NATIVE_OUTPUT,Env:NAV_NATIVE_MEDIA -ErrorAction SilentlyContinue
}
