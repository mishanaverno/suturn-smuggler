namespace Utilities
{
    public static class Log
    {
        private static string dmsg = "";
        public static void Debug(string msg, bool debug)
        {
            if (debug)
            {
                dmsg += "\n";
                dmsg += msg;
                
            }
        }

        public static void EndDebug(bool debug)
        {
            if (debug)
            {
                UnityEngine.Debug.Log(dmsg);
                dmsg = "";
            }
        }
    }
}
