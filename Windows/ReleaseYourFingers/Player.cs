using System.Runtime.InteropServices;


namespace ReleaseYourFingers
{
    public static class Player
    {
        public static uint SND_ASYNC = 0x0001;
        public static uint SND_FILENAME = 0x00020000;
        [DllImport("winmm.dll")]
        public static extern uint mciSendString(string lpstrCommand,
        string lpstrReturnString, uint uReturnLength, uint hWndCallback);
 
        public static void Play(string strFileName)
        {
            mciSendString(@"close temp_alias", null, 0, 0);
            mciSendString(@"open "+ strFileName +" alias temp_alias", null, 0, 0);
            mciSendString("play temp_alias", null, 0, 0);
        }
    }
}