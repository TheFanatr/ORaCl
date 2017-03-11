using Gma.System.MouseKeyHook;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Management;
using System.Windows.Shell;
using System.Threading;

namespace ORaCl
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }
    internal enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }
    internal enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_INVALID_STATE = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        private IKeyboardMouseEvents globalMouseHook = Hook.AppEvents();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        public static Point GetMousePosition()
        {
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);
            return new Point(w32Mouse.X, w32Mouse.Y);
        }

        Size previousWindowSize;
        double previousWindowTop;
        double previousWindowLeft;

        bool Maximized
        {
            set
            {
                if (!Maximized)
                {
                    MaximizeButton.Content = new PackIcon { Kind = PackIconKind.ChevronDown };
                    previousWindowSize.Height = Height;
                    previousWindowSize.Width = Width;
                    previousWindowLeft = Left;
                    previousWindowTop = Top;
                    Height = SystemParameters.WorkArea.Height;
                    Width = SystemParameters.WorkArea.Width;
                    Left = 0;
                    Top = 0;
                    ResizeMode = ResizeMode.NoResize;
                }
                else
                {
                    MaximizeButton.Content = new PackIcon { Kind = PackIconKind.CropSquare };
                    Top = previousWindowTop;
                    Left = previousWindowLeft;
                    Width = previousWindowSize.Width;
                    Height = previousWindowSize.Height;
                    ResizeMode = ResizeMode.CanResizeWithGrip;
                }
            }
            get { return ActualWidth == SystemParameters.WorkArea.Width && ActualHeight == SystemParameters.WorkArea.Height && Left == 0 && Top == 0; }
        }

        bool maximizedBeforeTabletMode;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                EnableBlur();
                HandleTabletMode();
                previousWindowSize = new Size(Width, Height);
                if (WindowsIdentity.GetCurrent() != null && WindowsIdentity.GetCurrent().User != null)
                {
                    var managementEventWatcher = new ManagementEventWatcher(new EventQuery(string.Format(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive='HKEY_USERS' AND KeyPath='{0}\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\ImmersiveShell' AND ValueName='TabletMode'", WindowsIdentity.GetCurrent().User.Value)));
                    managementEventWatcher.EventArrived += (sI, eI) => Dispatcher.Invoke(() => HandleTabletMode());
                    managementEventWatcher.Start();
                }
                Style = (Style)Resources["TestStyle"];
            };
            Closed += (s, e) => globalMouseHook.Dispose();
            StateChanged += (s, e) =>
            {
                switch (WindowState)
                {
                    case WindowState.Maximized:
                        WindowState = WindowState.Normal;
                        Maximized = !Maximized;
                        break;
                    case WindowState.Minimized:
                        if (Width == SystemParameters.WorkArea.Width && Height == SystemParameters.WorkArea.Height)
                        {
                            WindowState = WindowState.Normal;
                            Maximized = !Maximized;
                        }
                        break;
                }
            };
            KeyDown += (sI, eI) =>
            {
                //if (eI.KeyboardDevice.IsKeyDown(Key.LWin) && eI.KeyboardDevice.IsKeyDown(Key.Down)) ToggleMaxmization(null, null);
            };
            globalMouseHook.MouseDragStartedExt += (s, e) =>
            {
                if (TitleBar.IsMouseOver)
                {
                    if (Maximized)
                    {
                        Maximized = !Maximized;
                        Top = 0;
                        Left = (GetMousePosition().X - Width) /2;
                    }
                    DragMove();
                }
            };
            globalMouseHook.MouseDoubleClick += (s, e) =>
            {
                Maximized = !Maximized;
            };
            CloseButton.Click += (s, e) => Close();
            MaximizeButton.Click += (s, e) => Maximized = !Maximized;
            MinimizeButton.Click += (s, e) => WindowState = WindowState.Minimized;
            void HandleTabletMode()
            {
                if ((int)Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\ImmersiveShell", "TabletMode", 0) == 1)
                {
                    if (Maximized) maximizedBeforeTabletMode = true;
                    WindowControls.Children.Remove(MinimizeButton);
                    WindowControls.Children.Remove(MaximizeButton);
                }
                else if (!(WindowControls.Children.Contains(MinimizeButton)))
                {
                    if (!maximizedBeforeTabletMode) Maximized = !Maximized;
                    WindowControls.Children.Insert(0, MinimizeButton);
                    WindowControls.Children.Insert(1, MaximizeButton);
                }
            }
        }
        internal void EnableBlur()
        {
            var windowHelper = new WindowInteropHelper(this);
            var accent = new AccentPolicy();
            var accentStructSize = Marshal.SizeOf(accent);
            accent.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WindowCompositionAttributeData()
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };
            SetWindowCompositionAttribute(windowHelper.Handle, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }
    }
}
