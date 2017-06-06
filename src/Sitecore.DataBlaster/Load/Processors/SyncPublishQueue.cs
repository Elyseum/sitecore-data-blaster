﻿using System.Diagnostics;
using Sitecore.DataBlaster.Load.Sql;

namespace Sitecore.DataBlaster.Load.Processors
{
    public class SyncPublishQueue : ISyncInTransaction
    {
        public void Process(BulkLoadContext loadContext, BulkLoadSqlContext sqlContext)
        {
            if (!loadContext.UpdatePublishQueue.GetValueOrDefault()) return;
	        if (loadContext.ItemChanges.Count == 0) return;

			var stopwatch = Stopwatch.StartNew();

            var sql = sqlContext.GetEmbeddedSql(loadContext, "Sql.10.UpdatePublishQueue.sql");
            sqlContext.ExecuteSql(sql,
                commandProcessor: cmd => cmd.Parameters.AddWithValue("@UserName", Sitecore.Context.User.Name));

            loadContext.Log.Info($"Updated publish queue: {(int)stopwatch.Elapsed.TotalSeconds}s");
        }
    }
}