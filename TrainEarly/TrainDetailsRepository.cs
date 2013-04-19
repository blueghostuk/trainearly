using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrainEarly
{
    internal sealed class TrainDetailsRepository : DbRepository
    {
        public dynamic GetTrain(string trainId)
        {
            const string sql = @"
                SELECT TOP 1
                    [LiveTrain].[OriginDepartTimestamp]
                    ,[ScheduleTrain].[TrainUid]
                    ,[LiveTrain].[Headcode]
                    ,[OriginTiploc].[CRS] AS [OriginCRS]
                    ,[OriginTiploc].[Description] AS [OriginName]
                    ,[DestinationTiploc].[CRS] AS [DestinationCRS]
                    ,[DestinationTiploc].[Description] AS [DestinationName]
                FROM [LiveTrain]
                INNER JOIN [ScheduleTrain] ON [LiveTrain].[ScheduleTrain] = [ScheduleTrain].[ScheduleId]
                INNER JOIN  [Tiploc] [OriginTiploc] ON [ScheduleTrain].[OriginStopTiplocId] = [OriginTiploc].[TiplocId]
                INNER JOIN  [Tiploc] [DestinationTiploc] ON [ScheduleTrain].[DestinationStopTiplocId] = [DestinationTiploc].[TiplocId]
                WHERE [LiveTrain].[TrainId] = @trainId
                ORDER BY [LiveTrain].[OriginDepartTimestamp] DESC"; // get latest occurance

            using (DbConnection dbConnection = CreateAndOpenConnection())
            {
                return ExecuteScalar<dynamic>(sql, new { trainId });
            }
        }
    }
}
