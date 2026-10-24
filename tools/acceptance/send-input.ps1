# Send one real mouse/keyboard input to a window, for the acceptance runs that need a gesture the
# in-process driver cannot perform (a drag, a click on a button, a key press).
#
# The user authorised simulated input for the acceptance tooling on 2026-10-24: the rows blocked on
# "the tooling may not drive the mouse/keyboard" (the drag cues, the end-of-layer panel, the key-bind
# capture, the CPR button) are read with this, instead of guessed at or skipped.
#
# Coordinates are CLIENT coordinates of the target window (0,0 = its top-left content pixel), so a
# caller never needs the machine's screen layout. The window is brought to the foreground first,
# because Unity reads input from the focused window only.
#
# Examples:
#   tools/acceptance/send-input.ps1 -Name CasualtiesUnknown -Action click -X 640 -Y 360
#   tools/acceptance/send-input.ps1 -ProcessId 12345 -Action key -Vk 13          # Enter
#   tools/acceptance/send-input.ps1 -ProcessId 12345 -Action drag -X1 300 -Y1 400 -X2 700 -Y2 400 -HoldMs 600
#
# With more than one client running, name the one you mean: -ProcessId takes the exact client, -Name the
# first process of that name that owns a window.
param(
	[string]$Name = 'CasualtiesUnknown',
	[int]$ProcessId = 0,
	[Parameter(Mandatory = $true)][ValidateSet('click', 'key', 'drag')][string]$Action,
	[int]$X = 0,
	[int]$Y = 0,
	[int]$X1 = 0,
	[int]$Y1 = 0,
	[int]$X2 = 0,
	[int]$Y2 = 0,
	[int]$Vk = 0,
	[int]$HoldMs = 400,
	[switch]$Down,
	[switch]$Up
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class CuoInput
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public INPUTUNION u; }

    public const uint INPUT_MOUSE = 0;
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    public const uint MOUSEEVENTF_MOVE = 0x0001;

    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr hWnd, bool altTab);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

    /// <summary>
    /// Bring a window forward for real. A background process may not call SetForegroundWindow alone (Windows refuses
    /// it and the click would land on whatever is in front), so this borrows the foreground thread's input state, sets
    /// the window, and lets go; SwitchToThisWindow is the fallback for the shapes the attach refuses.
    /// </summary>
    public static bool ForceForeground(IntPtr hWnd)
    {
        if (GetForegroundWindow() == hWnd) return true;

        var foreground = GetForegroundWindow();
        var targetThread = GetWindowThreadProcessId(hWnd, IntPtr.Zero);
        var foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, IntPtr.Zero);
        var currentThread = GetCurrentThreadId();

        var attachedForeground = false;
        var attachedTarget = false;
        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
            {
                attachedForeground = AttachThreadInput(currentThread, foregroundThread, true);
            }

            if (targetThread != 0 && targetThread != currentThread && targetThread != foregroundThread)
            {
                attachedTarget = AttachThreadInput(currentThread, targetThread, true);
            }

            BringWindowToTop(hWnd);
            ShowWindow(hWnd, 9);
            SetForegroundWindow(hWnd);
        }
        finally
        {
            if (attachedTarget) AttachThreadInput(currentThread, targetThread, false);
            if (attachedForeground) AttachThreadInput(currentThread, foregroundThread, false);
        }

        if (GetForegroundWindow() == hWnd) return true;

        SwitchToThisWindow(hWnd, true);
        System.Threading.Thread.Sleep(150);
        return GetForegroundWindow() == hWnd;
    }

    public static void Click(int screenX, int screenY)
    {
        SetCursorPos(screenX, screenY);
        System.Threading.Thread.Sleep(80);
        var inputs = new INPUT[2];
        inputs[0].type = INPUT_MOUSE; inputs[0].u.mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
        inputs[1].type = INPUT_MOUSE; inputs[1].u.mi.dwFlags = MOUSEEVENTF_LEFTUP;
        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void MouseDown(int screenX, int screenY)
    {
        SetCursorPos(screenX, screenY);
        System.Threading.Thread.Sleep(80);
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_MOUSE; inputs[0].u.mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void MouseUp(int screenX, int screenY)
    {
        SetCursorPos(screenX, screenY);
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_MOUSE; inputs[0].u.mi.dwFlags = MOUSEEVENTF_LEFTUP;
        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void Key(ushort vk)
    {
        var inputs = new INPUT[2];
        inputs[0].type = INPUT_KEYBOARD; inputs[0].u.ki.wVk = vk;
        inputs[1].type = INPUT_KEYBOARD; inputs[1].u.ki.wVk = vk; inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;
        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    // One half of a press. Unity samples Input.GetKeyDown on a frame boundary, so a tap whose down and up leave in the
    // same SendInput batch can be missed outright; a batch that must have the game SEE the press sends the halves apart.
    public static void KeyDown(ushort vk)
    {
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_KEYBOARD; inputs[0].u.ki.wVk = vk;
        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void KeyUp(ushort vk)
    {
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_KEYBOARD; inputs[0].u.ki.wVk = vk; inputs[0].u.ki.dwFlags = KEYEVENTF_KEYUP;
        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}
'@

[void][CuoInput]::SetProcessDPIAware()

$process = if ($ProcessId -gt 0) {
	Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
} else {
	Get-Process -Name $Name -ErrorAction SilentlyContinue |
		Where-Object { $_.MainWindowHandle -ne 0 } |
		Select-Object -First 1
}
if (-not $process -or $process.MainWindowHandle -eq 0) {
	throw "no window found for '$Name'$(if ($ProcessId -gt 0) { " pid $ProcessId" }) (is the client running and past the launcher?)"
}

$handle = $process.MainWindowHandle
if (-not [CuoInput]::ForceForeground($handle)) {
	throw "could not bring '$Name' to the foreground (the click/key would go to another window)"
}
Start-Sleep -Milliseconds 250

function ConvertTo-Screen([int]$clientX, [int]$clientY) {
	$point = New-Object CuoInput+POINT
	$point.X = $clientX
	$point.Y = $clientY
	if (-not [CuoInput]::ClientToScreen($handle, [ref]$point)) {
		throw "ClientToScreen failed for ($clientX,$clientY)"
	}
	return $point
}

switch ($Action) {
	'click' {
		$p = ConvertTo-Screen $X $Y
		[CuoInput]::Click($p.X, $p.Y)
	}
	'key' {
		if ($Vk -le 0) { throw '-Vk is required for -Action key' }
		$keyCode = [System.Convert]::ToUInt16($Vk)
		if ($Down -or $Up) {
			if ($Down) { [CuoInput]::KeyDown($keyCode) }
			if ($Up) { [CuoInput]::KeyUp($keyCode) }
		}
		else {
			[CuoInput]::Key($keyCode)
		}
	}
	'drag' {
		$from = ConvertTo-Screen $X1 $Y1
		$to = ConvertTo-Screen $X2 $Y2
		[CuoInput]::MouseDown($from.X, $from.Y)
		$steps = [Math]::Max(2, [int]($HoldMs / 40))
		for ($i = 1; $i -le $steps; $i++) {
			$sx = [int]($from.X + (($to.X - $from.X) * $i / $steps))
			$sy = [int]($from.Y + (($to.Y - $from.Y) * $i / $steps))
			[void][CuoInput]::SetCursorPos($sx, $sy)
			Start-Sleep -Milliseconds 40
		}
		[CuoInput]::MouseUp($to.X, $to.Y)
	}
}

$sent = [pscustomobject]@{
	ok = $true
	window = $Name
	handle = "$handle"
	action = $Action
	at = (Get-Date).ToString('HH:mm:ss')
}
$sent | ConvertTo-Json -Compress
