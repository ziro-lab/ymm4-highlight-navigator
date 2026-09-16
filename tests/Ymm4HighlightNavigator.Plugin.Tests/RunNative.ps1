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
 $limit=[DateTime]::UtcNow.AddSeconds(100)
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
 $required=@('real_product_tool','capture_two_occurrences','same_source_different_rates','native_map_fractional_projection','exclusive_end_no_jump','selection_independent_targets','empty_capture_atomic','background_ui_responsive','overlap_decoded_once','source_to_occurrences','chronological_queue','next_jump_exact','all_profiles_off','toggle_without_decoder','sensitivity_without_decoder','cancelled_query_not_old_success','query_recovers_without_redecode','visited_survives_requery','item_state_unchanged','source_bytes_unchanged','stale_target_rejected_atomically','timeline_detach_invalidates','narrow_primary_controls_contained','dispose_clears_targets')
 if($r.assertions.Count -ne $required.Count){throw 'Wrong native assertion count.'}
 foreach($id in $required){$found=@($r.assertions|Where-Object id -eq $id); if($found.Count -ne 1 -or $found[0].passed -cne $true){throw "Missing/failed assertion: $id"}}
 Get-Content (Join-Path $OutputDir 'summary.json')
 Get-Content (Join-Path $OutputDir 'assertions.txt')
 Write-Output 'PASS_NAVIGATOR_PRODUCT: 24 independent assertions'
} finally {
 if(-not $p.HasExited){Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue}
 Remove-Item Env:NAV_NATIVE_OUTPUT,Env:NAV_NATIVE_MEDIA -ErrorAction SilentlyContinue
}
