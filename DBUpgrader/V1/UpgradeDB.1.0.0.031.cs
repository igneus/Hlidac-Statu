﻿using Devmasters.DatabaseUpgrader;


namespace HlidacStatu.DBUpgrades
{
    public static partial class DBUpgrader
    {

        private partial class UpgradeDB
        {

            [DatabaseUpgradeMethod("1.0.0.31")]
            public static void Init_1_0_0_31(IDatabaseUpgrader du)
            {

                string sql = @"
ALTER TABLE dbo.WatchDog ADD
	FocusId int NOT NULL CONSTRAINT DF_WatchDog_FocusId DEFAULT 0

";
                du.RunDDLCommands(sql);

                //add new bank account



            }




        }

    }
}
