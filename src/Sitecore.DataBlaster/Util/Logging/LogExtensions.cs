﻿using log4net;
using log4net.spi;

namespace Sitecore.DataBlaster.Util.Logging
{
    public static class LogExtensions
    {
        public static void Trace(this ILog log, string message)
        {
            if (!log.Logger.IsEnabledFor(Level.TRACE)) return;
            log.Logger.Log(null, Level.TRACE, message, null);
        }
    }
}
