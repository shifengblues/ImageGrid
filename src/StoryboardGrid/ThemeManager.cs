using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace StoryboardGrid;
internal static class ThemeManager
{
    static Application? owner;
    internal static int Mode {get;private set;}=2;
    internal static bool IsDark {get;private set;}
    internal static event Action? Changed;
    internal static void Initialize(Application app)
    {
        if(owner==app)return;owner=app;
        SystemEvents.UserPreferenceChanged+=SystemChanged;
        app.Exit+=(_,_)=>{SystemEvents.UserPreferenceChanged-=SystemChanged;owner=null;};Apply();
    }
    internal static void SetMode(int mode){Mode=Math.Clamp(mode,0,2);Apply();}
    internal static bool ResolveDark(int mode,bool systemDark)=>mode==1||mode==2&&systemDark;
    static bool SystemDark()
    {
        try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");return key?.GetValue("AppsUseLightTheme") is int value&&value==0;}catch{return false;}
    }
    static void SystemChanged(object sender,UserPreferenceChangedEventArgs args)
    {
        if(Mode==2&&owner is {} app&&!app.Dispatcher.HasShutdownStarted)_=app.Dispatcher.BeginInvoke(new Action(Apply));
    }
    static void Apply()
    {
        if(owner==null)return;IsDark=ResolveDark(Mode,SystemDark());
        Set("AppBackground",IsDark?"#191D22":"#F7F8FA");Set("Card",IsDark?"#23292F":"#FFFFFF");
        Set("Text",IsDark?"#E9EEF2":"#25282D");Set("MutedText",IsDark?"#A4AFBA":"#717680");Set("FaintText",IsDark?"#8C99A6":"#8C94A1");
        Set("Border",IsDark?"#3B454F":"#E4E7EC");Set("Accent","#16865D");Set("AccentText",IsDark?"#77DEAE":"#126745");
        Set("AccentSoft",IsDark?"#213F33":"#E8F5EE");Set("HoverBorder",IsDark?"#45896A":"#91CFB1");Set("DangerText",IsDark?"#FF9B8D":"#B54738");
        Set("CanvasBackground",IsDark?"#12161A":"#EFF1F5");Set("ThumbnailBackground",IsDark?"#303840":"#F2F4F8");
        foreach(Window window in owner.Windows)UpdateCaption(window);
        Changed?.Invoke();
    }
    static void Set(string key,string hex){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));brush.Freeze();owner!.Resources[key]=brush;}
    internal static Brush Brush(string key)=>Application.Current?.TryFindResource(key) as Brush??Brushes.White;
    internal static void Register(Window window)=>window.SourceInitialized+=(_,_)=>UpdateCaption(window);
    static void UpdateCaption(Window window)
    {
        var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;int dark=IsDark?1:0;
        if(DwmSetWindowAttribute(handle,20,ref dark,4)!=0)DwmSetWindowAttribute(handle,19,ref dark,4);
    }
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
}
