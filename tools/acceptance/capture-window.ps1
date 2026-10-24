# Captures one client window to a PNG for an acceptance row that is judged from a frame.
#
# A visual row is judged from a frame the run captured from the CLIENT'S OWN WINDOW (never the desktop), so the
# clients never have to be in front. PrintWindow with PW_RENDERFULLCONTENT renders a D3D window without
# activating it; the DPI awareness call keeps the rectangle in physical pixels on a scaled display.
#
#   powershell -ExecutionPolicy Bypass -File tools/acceptance/capture-window.ps1 `
#       -Process CasualtiesUnknown -Out <acceptance-artifacts-dir>\<artifact-id>.png [-Attempts 3]
#
[CmdletBinding()]
param(
	[string]$Name = 'CasualtiesUnknown',
	[Parameter(Mandatory)][string]$Out,
	[int]$Id = 0,
	[int]$Attempts = 3,
	[int]$DelayMs = 400
)

$ErrorActionPreference = 'Stop'

Add-Type -ReferencedAssemblies 'System.Drawing' -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class WindowCapture
{
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    public const uint RenderFullContent = 0x00000002;

    public static bool Capture(IntPtr hwnd, string path, out string detail)
    {
        detail = "";
        RECT rect;
        if (!GetWindowRect(hwnd, out rect))
        {
            detail = "GetWindowRect failed";
            return false;
        }
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            detail = "zero-size window (" + width + "x" + height + ")";
            return false;
        }
        using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                IntPtr hdc = graphics.GetHdc();
                bool rendered;
                try { rendered = PrintWindow(hwnd, hdc, RenderFullContent); }
                finally { graphics.ReleaseHdc(hdc); }
                if (!rendered)
                {
                    detail = "PrintWindow returned false";
                    return false;
                }
            }
            bitmap.Save(path, ImageFormat.Png);
            detail = width + "x" + height;
            return true;
        }
    }
}
'@

[void][WindowCapture]::SetProcessDPIAware()

$target = if ($Id -gt 0) {
	Get-Process -Id $Id -ErrorAction SilentlyContinue
}
else {
	Get-Process -Name $Name -ErrorAction SilentlyContinue |
		Where-Object { ([int64]$_.MainWindowHandle) -ne 0 } |
		Select-Object -First 1
}
if ($null -eq $target) {
	if ($Id -gt 0) { throw "No process with id $Id." }
	throw "No window found for process '$Name'."
}
if ((([int64]$target.MainWindowHandle)) -eq 0) {
	throw "The window handle of '$($target.ProcessName)' (id $($target.Id)) is zero; it has no window to capture."
}
$hwnd = [IntPtr]([int64]$target.MainWindowHandle)
if ($hwnd -eq [IntPtr]::Zero) {
	throw "The window handle of '$Name' is zero."
}
if (-not [WindowCapture]::IsWindowVisible($hwnd)) {
	throw "The window of '$Name' is not visible; a hidden client cannot be captured."
}

$directory = Split-Path -Parent $Out
if ($directory -and -not (Test-Path -LiteralPath $directory)) {
	New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$detail = ''
for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
	if ([WindowCapture]::Capture($hwnd, $Out, [ref]$detail)) {
		$file = Get-Item -LiteralPath $Out
		Write-Output ("captured {0} ({1}, {2} bytes) from pid {3}" -f $Out, $detail, $file.Length, $target.Id)
		return
	}
	if ($attempt -lt $Attempts) {
		Start-Sleep -Milliseconds $DelayMs
	}
}

throw "PrintWindow failed $Attempts time(s) for '$Name': $detail (is the client minimised?)."
