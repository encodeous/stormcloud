using System;
using System.Collections.Generic;
using System.Text;

namespace stormutils
{
    public class StormUtils
    {
        public static string GetValidClientID()
        {
            var t = DateTime.UtcNow.Day * DateTime.UtcNow.Year * DateTime.UtcNow.Year * 19232957398534523L;
            return t.ToString("X");
        }
    }
}
