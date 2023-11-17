/*
Copyright (c) 2023, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class WindowsColorOnMouseDown
    {
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("gdi32.dll")]
        public static extern uint GetPixel(IntPtr hDC, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowsHookEx(int idHook, MouseProc callback, IntPtr hInstance, uint threadId);

        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, int wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string lpFileName);

        private delegate IntPtr MouseProc(int nCode, int wParam, IntPtr lParam);

        private const int WH_MOUSE_LL = 14;
        private static MouseProc mouseProc;
        private static IntPtr hookId = IntPtr.Zero;

        public WindowsColorOnMouseDown()
        {
        }

        private static IntPtr MouseHookCallback(int nCode, int wParam, IntPtr lParam)
        {
            if (nCode >= 0 && MouseMessages.WM_LBUTTONDOWN == (MouseMessages)wParam)
            {
                return (IntPtr)1; // Indicates that we handle the message
            }

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private enum MouseMessages
        {
            WM_LBUTTONDOWN = 0x0201
        }

        public void GetColor(Action<Agg.Color> setColor)
        {
            setColor?.Invoke(WaitForClick());
        }

        public static void SetHook()
        {
            mouseProc = MouseHookCallback;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                hookId = SetWindowsHookEx(WH_MOUSE_LL, mouseProc,
                    LoadLibrary("user32"), 0);
            }
        }

        public static void ReleaseHook()
        {
            UnhookWindowsHookEx(hookId);
        }

        public Agg.Color WaitForClick()
        {
            var clickWaitHandle = new AutoResetEvent(false);
            Timer timeoutTimer = null;

            MouseProc clickProc = (nCode, wParam, lParam) =>
            {
                if (nCode >= 0 && MouseMessages.WM_LBUTTONDOWN == (MouseMessages)wParam)
                {
                    timeoutTimer?.Dispose();
                    timeoutTimer = null;
                    clickWaitHandle.Set();
                    return (IntPtr)1; // Stop further processing
                }
                return CallNextHookEx(hookId, nCode, wParam, lParam);
            };

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                hookId = SetWindowsHookEx(WH_MOUSE_LL, clickProc, LoadLibrary("user32"), 0);
            }

            // Set a timer to release the hook after 20 seconds
            timeoutTimer = new Timer(_ =>
            {
                ReleaseHook();
                clickWaitHandle.Set();
            }, null, 20000, Timeout.Infinite);
            
            clickWaitHandle.WaitOne();
            ReleaseHook();

            // Check if the timer has been disposed, indicating a click occurred
            if (timeoutTimer != null)
            {
                timeoutTimer.Dispose();
                return Agg.Color.Transparent; // or another default value to indicate timeout
            }
            
            return GetColorAtCursor();
        }

        public Agg.Color GetColorAtCursor()
        {
            GetCursorPos(out POINT cursor);
            IntPtr desktopDC = GetDC(IntPtr.Zero);
            uint color = GetPixel(desktopDC, cursor.X, cursor.Y);
            ReleaseDC(IntPtr.Zero, desktopDC);

            return new Agg.Color((int)(color & 0x000000FF),
                (int)(color & 0x0000FF00) >> 8,
                (int)(color & 0x00FF0000) >> 16);
        }
    }
}
